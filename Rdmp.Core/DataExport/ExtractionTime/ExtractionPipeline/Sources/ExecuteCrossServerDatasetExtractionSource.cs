// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using Rdmp.Core.CatalogueLibrary.Data;
using Rdmp.Core.CatalogueLibrary.DataFlowPipeline;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Progress;

namespace Rdmp.Core.DataExport.ExtractionTime.ExtractionPipeline.Sources
{
    /// <summary>
    /// Data Extraction Source which can fulfill the IExtractCommand even when the dataset in the command is on a different server from the cohort.  This is done
    /// by copying the Cohort from the cohort database into tempdb for the duration of the pipeline execution and doing the linkage against that instead of
    /// the original cohort table.
    /// 
    /// </summary>
    public class ExecuteCrossServerDatasetExtractionSource : ExecuteDatasetExtractionSource
    {
        private bool _haveCopiedCohortAndAdjustedSql = false;

        [DemandsInitialization("Database to upload the cohort to prior to linking", defaultValue: "tempdb",mandatory:true)]
        public string TemporaryDatabaseName { get; set; }

        [DemandsInitialization("Determines behaviour if TemporaryDatabaseName is not found on the dataset server.  True to create it as a new database (and drop it afterwards), False to crash", defaultValue: true)]
        public bool CreateAndDestroyTemporaryDatabaseIfNotExists { get; set; }

        [DemandsInitialization("Determines behaviour if TemporaryDatabaseName already contains a Cohort table.  True to drop it, False to crash", defaultValue: true)]
        public bool DropExistingCohortTableIfExists { get; set; }

        public override DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            if (!_haveCopiedCohortAndAdjustedSql)
                CopyCohortToDataServer(listener,cancellationToken);

            return base.GetChunk(listener, cancellationToken);
        }

        private bool _hadToCreate = false;

        private List<DiscoveredTable> tablesToCleanup = new List<DiscoveredTable>();

        public static Semaphore OneCrossServerExtractionAtATime = new Semaphore(1, 1);
        private DiscoveredServer _server;
        private DiscoveredDatabase _tempDb;
        public override string HackExtractionSQL(string sql, IDataLoadEventListener listener)
        {
            //call base hacks
            sql = base.HackExtractionSQL(sql, listener);

            SetServer();

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Original (unhacked) SQL was " + sql, null));
            
            //now replace database with tempdb
            var extractableCohort = Request.ExtractableCohort;
            var extractableCohortSource = extractableCohort.ExternalCohortTable;
            
            var syntaxHelperFactory = new QuerySyntaxHelperFactory();
            var sourceSyntax = syntaxHelperFactory.Create(extractableCohortSource.DatabaseType);
            var destinationSyntax = syntaxHelperFactory.Create(_server.DatabaseType);

            //To replace (in this order)
            //Cohort database.table.privateId
            //Cohort database.table.releaseId
            //Cohort database.table.cohortdefinitionId
            //Cohort database.table name
            Dictionary<string,string> replacementStrings = new Dictionary<string, string>();

            var sourceDb = sourceSyntax.GetRuntimeName(extractableCohortSource.Database);
            var sourceTable = sourceSyntax.GetRuntimeName(extractableCohortSource.TableName);
            var sourcePrivateId = sourceSyntax.GetRuntimeName(extractableCohort.GetPrivateIdentifier());
            var sourceReleaseId = sourceSyntax.GetRuntimeName(extractableCohort.GetReleaseIdentifier());
            var sourceCohortDefinitionId = sourceSyntax.GetRuntimeName(extractableCohortSource.DefinitionTableForeignKeyField);

            //Swaps the given entity for the same entity but in _tempDb
            AddReplacement(replacementStrings, sourceDb, sourceTable, sourcePrivateId, sourceSyntax, destinationSyntax);
            AddReplacement(replacementStrings, sourceDb, sourceTable, sourceReleaseId, sourceSyntax, destinationSyntax);
            AddReplacement(replacementStrings, sourceDb, sourceTable, sourceCohortDefinitionId, sourceSyntax, destinationSyntax);
            AddReplacement(replacementStrings, sourceDb, sourceTable, sourceSyntax, destinationSyntax);
            
            foreach (KeyValuePair<string, string> r in replacementStrings)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Replacing '" + r.Key + "' with '" + r.Value + "'", null));
                
                if(!sql.Contains(r.Key))
                    listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "SQL extraction query string did not contain the text '" + r.Key +"' (which we expected to replace with '" + r.Value+""));

                sql = sql.Replace(r.Key, r.Value);
            }
            
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Adjusted (hacked) SQL was " + sql, null));

            //replace [MyCohortDatabase].. with [tempdb].. (while dealing with Cohort..Cohort replacement correctly as well as 'Cohort.dbo.Cohort.Fish' correctly)
            return sql;
        }

        private void AddReplacement(Dictionary<string, string> replacementStrings, string sourceDb, string sourceTable, string col, IQuerySyntaxHelper sourceSyntax, IQuerySyntaxHelper destinationSyntax)
        {
            replacementStrings.Add(
         sourceSyntax.EnsureFullyQualified(sourceDb, null, sourceTable, col),
         destinationSyntax.EnsureFullyQualified(_tempDb.GetRuntimeName(), null, sourceTable, col)
         );
        }
        private void AddReplacement(Dictionary<string, string> replacementStrings, string sourceDb, string sourceTable, IQuerySyntaxHelper sourceSyntax, IQuerySyntaxHelper destinationSyntax)
        {
            replacementStrings.Add(
         sourceSyntax.EnsureFullyQualified(sourceDb, null, sourceTable),
         destinationSyntax.EnsureFullyQualified(_tempDb.GetRuntimeName(), null, sourceTable)
         );
        }

        private void SetServer()
        {
            if(_server == null)
            {
                //it's a legit dataset being extracted?
                _server = Request.Catalogue.GetDistinctLiveDatabaseServer(DataAccessContext.DataExport, false);
                
                //expect a database called called tempdb
                _tempDb = _server.ExpectDatabase(TemporaryDatabaseName);
            }
        }


        private void CopyCohortToDataServer(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            DataTable cohortDataTable = null;
            SetServer();

            listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information,"About to wait for Semaphore OneCrossServerExtractionAtATime to become available"));
            OneCrossServerExtractionAtATime.WaitOne(-1);
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Captured Semaphore OneCrossServerExtractionAtATime"));

            try
            {
                IExtractableCohort cohort = Request.ExtractableCohort;
                cohortDataTable = cohort.FetchEntireCohort();
            }
            catch (Exception e)
            {
                throw new Exception("An error occurred while trying to download the cohort from the Cohort server (in preparation for transfering it to the data server for linkage and extraction)",e);
            }
            
            //make sure tempdb exists (this covers you for servers where it doesn't exist e.g. mysql or when user has specified a different database name)
            if (!_tempDb.Exists())
                if (CreateAndDestroyTemporaryDatabaseIfNotExists)
                {
                    _tempDb.Create();
                    _hadToCreate = true;
                }
                else
                    throw new Exception("Database '" + _tempDb + "' did not exist on server '" + _server + "' and CreateAndDestroyTemporaryDatabaseIfNotExists was false");
            else
                _hadToCreate = false;

            var tbl = _tempDb.ExpectTable(cohortDataTable.TableName);
            
            if(tbl.Exists())
            {
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning, "Found existing table called '" + tbl + "' in '" + _tempDb +"'"));

                if(DropExistingCohortTableIfExists)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "About to drop existing table '" + tbl + "'"));
                    tbl.Drop();
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Dropped existing table '" + tbl + "'"));
                }
                else
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "'" + _tempDb + "' contains a table called '" + tbl + "' and DropExistingCohortTableIfExists is false"));
                }
            }

            var destination = new DataTableUploadDestination();
            destination.PreInitialize(_tempDb, listener);
            destination.ProcessPipelineData(cohortDataTable, listener, cancellationToken);
            destination.Dispose(listener,null);

            

            if(!tbl.Exists())
                throw new Exception("Table '" + tbl + "' did not exist despite DataTableUploadDestination completing Successfully!");

            tablesToCleanup.Add(tbl);

            //table will now be in tempdb
            _haveCopiedCohortAndAdjustedSql = true;
        }


        public override void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "About to release Semaphore OneCrossServerExtractionAtATime"));
            OneCrossServerExtractionAtATime.Release(1);
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Released Semaphore OneCrossServerExtractionAtATime"));

            if(_hadToCreate)
            {
                //we created the db in the first place
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "About to drop database '" + _tempDb + "'"));
                _tempDb.Drop();
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Dropped database '" + _tempDb +"' Successfully"));
            }
            else
            {
                //we did not create the database but we did create the table
                foreach (DiscoveredTable table in tablesToCleanup)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "About to drop table '" + table+"'"));
                    table.Drop();
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Dropped table '" + table + "'"));
                }
            }
           
          
            base.Dispose(listener, pipelineFailureExceptionIfAny);
        }

        public override void Check(ICheckNotifier notifier)
        {
            

            notifier.OnCheckPerformed(new CheckEventArgs("Checking not supported for Cross Server extraction since it involves shipping off the cohort into tempdb.",CheckResult.Warning));
        }

        public override DataTable TryGetPreview()
        {
            throw new NotSupportedException("Previews are not supported for Cross Server extraction since it involves shipping off the cohort into tempdb.");
        }
    }
}
