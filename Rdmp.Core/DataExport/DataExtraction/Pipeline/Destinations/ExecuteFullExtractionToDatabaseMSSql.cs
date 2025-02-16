// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FAnsi.Discovery;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataExport.DataExtraction.UserPicks;
using Rdmp.Core.DataExport.DataRelease.Pipeline;
using Rdmp.Core.DataExport.DataRelease.Potential;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad.Engine.Pipeline.Destinations;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories;
using ReusableLibraryCode;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Progress;
using TypeGuesser;

namespace Rdmp.Core.DataExport.DataExtraction.Pipeline.Destinations
{
    /// <summary>
    /// Alternate extraction pipeline destination in which the DataTable containing the extracted dataset is written to an Sql Server database
    /// </summary>
    public class ExecuteFullExtractionToDatabaseMSSql : ExtractionDestination
    {
        [DemandsInitialization("External server to create the extraction into, a new database will be created for the project based on the naming pattern provided",Mandatory = true)]
        public IExternalDatabaseServer TargetDatabaseServer { get; set; }

        [DemandsInitialization(@"How do you want to name datasets, use the following tokens if you need them:   
         $p - Project Name ('e.g. My Project')
         $n - Project Number (e.g. 234)
         $t - Master Ticket (e.g. 'LINK-1234')
         $r - Request Ticket (e.g. 'LINK-1234')
         $l - Release Ticket (e.g. 'LINK-1234')
         ", Mandatory = true, DefaultValue = "Proj_$n_$l")]
        public string DatabaseNamingPattern { get; set; }

        [DemandsInitialization(@"How do you want to name datasets, use the following tokens if you need them:   
         $p - Project Name ('e.g. My Project')
         $n - Project Number (e.g. 234)
         $c - Configuration Name (e.g. 'Cases')
         $d - Dataset name (e.g. 'Prescribing')
         $a - Dataset acronym (e.g. 'Presc') 

         You must have either $a or $d
         ",Mandatory=true,DefaultValue="$c_$d")]
        public string TableNamingPattern { get; set; }
        
        [DemandsInitialization(@"If the extraction fails half way through AND the destination table was created during the extraction then the table will be dropped from the destination rather than being left in a half loaded state ",defaultValue:true)]
        public bool DropTableIfLoadFails { get; set; }
        
        [DemandsInitialization(DataTableUploadDestination.AlterTimeout_Description, DefaultValue = 300)]
        public int AlterTimeout { get; set; }

        [DemandsInitialization("True to copy the column collations from the source database when creating the destination database.  Only works if both the source and destination have the same DatabaseType.  Excludes columns which feature a transform as part of extraction.",DefaultValue=false)]
        public bool CopyCollations { get; set; }

        private DiscoveredDatabase _destinationDatabase;
        private DataTableUploadDestination _destination;

        private bool _tableDidNotExistAtStartOfLoad;
        private bool _isTableAlreadyNamed;
        DataTable _toProcess;
        
        public ExecuteFullExtractionToDatabaseMSSql():base(false)
        {

        }

        public override DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener job,
            GracefulCancellationToken cancellationToken)
        {
            _destinationDatabase = GetDestinationDatabase(job);
            return base.ProcessPipelineData(toProcess, job, cancellationToken);
        }

        protected override void Open(DataTable toProcess, IDataLoadEventListener job, GracefulCancellationToken cancellationToken)
        {
            _toProcess = toProcess;
            _destination = PrepareDestination(job, toProcess);
            OutputFile = toProcess.TableName;
        }
                
        protected override void WriteRows(DataTable toProcess, IDataLoadEventListener job, GracefulCancellationToken cancellationToken, Stopwatch stopwatch)
        {
            toProcess.TableName = GetTableName();

            //give the data table the correct name
            if (toProcess.ExtendedProperties.ContainsKey("ProperlyNamed") && toProcess.ExtendedProperties["ProperlyNamed"].Equals(true))
                _isTableAlreadyNamed = true;
            
            _destination.ProcessPipelineData(toProcess, job, cancellationToken);

            LinesWritten += toProcess.Rows.Count;
        }
        
        private DataTableUploadDestination PrepareDestination(IDataLoadEventListener listener, DataTable toProcess)
        {
            //see if the user has entered an extraction server/database 
            if (TargetDatabaseServer == null)
                throw new Exception("TargetDatabaseServer (the place you want to extract the project data to) property has not been set!");

            try
            {
                if (!_destinationDatabase.Exists())
                    _destinationDatabase.Create();

                if (_request is ExtractGlobalsCommand)
                    return null;

                var tblName = _toProcess.TableName;

                //See if table already exists on the server (likely to cause problems including duplication, schema changes in configuration etc)
                if (_destinationDatabase.ExpectTable(tblName).Exists())
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning,
                                            "A table called " + tblName + " already exists on server " + TargetDatabaseServer + 
                                            ", data load might crash if it is populated and/or has an incompatible schema"));
                else
                {
                    _tableDidNotExistAtStartOfLoad = true;
                }
            }
            catch (Exception e)
            {
                //Probably the database didn't exist or the credentials were wrong or something
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "Failed to inspect destination for already existing datatables", e));
            }

            _destination = new DataTableUploadDestination();
            
            PrimeDestinationTypesBasedOnCatalogueTypes(toProcess);

            _destination.AllowResizingColumnsAtUploadTime = true;
            _destination.AlterTimeout = AlterTimeout;

            _destination.PreInitialize(_destinationDatabase, listener);

            return _destination;
        }

        private void PrimeDestinationTypesBasedOnCatalogueTypes(DataTable toProcess)
        { 
            //if the extraction is of a Catalogue
            var datasetCommand = _request as IExtractDatasetCommand;
            
            if(datasetCommand == null)
                return;

            //for every extractable column in the Catalogue
            foreach (var extractionInformation in datasetCommand.ColumnsToExtract.OfType<ExtractableColumn>().Select(ec=>ec.CatalogueExtractionInformation))//.GetAllExtractionInformation(ExtractionCategory.Any))
            {
                if(extractionInformation == null)
                    continue;

                var catItem = extractionInformation.CatalogueItem;

                //if we do not know the data type or the ei is a transform
                if (catItem == null || catItem.ColumnInfo == null || extractionInformation.IsProperTransform())
                    continue;

                string destinationType = GetDestinationDatabaseType(extractionInformation);
                
                //Tell the destination the datatype of the ColumnInfo that underlies the ExtractionInformation (this might be changed by the ExtractionInformation e.g. as a 
                //transform but it is a good starting point.  We don't want to create a varchar(10) column in the destination if the origin dataset (Catalogue) is a varchar(100)
                //since it will just confuse the user.  Bear in mind these data types can be degraded later by the destination
                var columnName = extractionInformation.Alias ?? catItem.ColumnInfo.GetRuntimeName();
                var addedType = _destination.AddExplicitWriteType(columnName, destinationType);
                addedType.IsPrimaryKey = toProcess.PrimaryKey.Any(dc => dc.ColumnName == columnName);
                
                //if user wants to copy collation types and the destination server is the same type as the origin server
                if (CopyCollations && _destinationDatabase.Server.DatabaseType == catItem.ColumnInfo.TableInfo.DatabaseType)
                    addedType.Collation = catItem.ColumnInfo.Collation;
            }

            foreach (ReleaseIdentifierSubstitution sub in datasetCommand.QueryBuilder.SelectColumns.Where(sc => sc.IColumn is ReleaseIdentifierSubstitution).Select(sc => sc.IColumn))
            {
                var columnName = sub.GetRuntimeName();
                bool isPk = toProcess.PrimaryKey.Any(dc => dc.ColumnName == columnName);

                var addedType = _destination.AddExplicitWriteType(columnName, datasetCommand.ExtractableCohort.GetReleaseIdentifierDataType());
                addedType.IsPrimaryKey = isPk;
                addedType.AllowNulls = !isPk;
            }
        }

        private string GetDestinationDatabaseType(ConcreteColumn col)
        {
            //Make sure we know if we are going between database types
            var fromDbType = _destinationDatabase.Server.DatabaseType;
            var toDbType = col.ColumnInfo.TableInfo.DatabaseType;
            if (fromDbType != toDbType)
            {
                var fromSyntax = col.ColumnInfo.GetQuerySyntaxHelper();
                var toSyntax = _destinationDatabase.Server.GetQuerySyntaxHelper();

                DatabaseTypeRequest intermediate = fromSyntax.TypeTranslater.GetDataTypeRequestForSQLDBType(col.ColumnInfo.Data_type);
                return toSyntax.TypeTranslater.GetSQLDBTypeForCSharpType(intermediate);
            }
            
            return col.ColumnInfo.Data_type;
        }

        private string GetTableName(string suffix = null)
        {
            string tblName;
            if (_isTableAlreadyNamed)
            {
                tblName = SanitizeNameForDatabase(_toProcess.TableName);

                if (!String.IsNullOrWhiteSpace(suffix))
                    tblName += "_" + suffix;

                return tblName;
            }

            tblName = TableNamingPattern;
            var project = _request.Configuration.Project;
            
            tblName = tblName.Replace("$p", project.Name);
            tblName = tblName.Replace("$n", project.ProjectNumber.ToString());
            tblName = tblName.Replace("$c", _request.Configuration.Name);

            if (_request is ExtractDatasetCommand)
            {
                tblName = tblName.Replace("$d", ((ExtractDatasetCommand)_request).DatasetBundle.DataSet.Catalogue.Name);
                tblName = tblName.Replace("$a", ((ExtractDatasetCommand)_request).DatasetBundle.DataSet.Catalogue.Acronym);
            }

            if (_request is ExtractGlobalsCommand)
            {
                tblName = tblName.Replace("$d", ExtractionDirectory.GLOBALS_DATA_NAME);
                tblName = tblName.Replace("$a", "G");
            }

            var cachedGetTableNameAnswer = SanitizeNameForDatabase(tblName);
            if (!String.IsNullOrWhiteSpace(suffix))
                cachedGetTableNameAnswer += "_" + suffix;

            return cachedGetTableNameAnswer;
        }

        private string SanitizeNameForDatabase(string tblName)
        {
            if (_destinationDatabase == null)
                throw new Exception("Cannot pick a TableName until we know what type of server it is going to, _server is null");

            //get rid of brackets and dots
            tblName = Regex.Replace(tblName,"[.()]", "_");

            var syntax = _destinationDatabase.Server.GetQuerySyntaxHelper();
            syntax.ValidateTableName(tblName);

            //otherwise, fetch and cache answer
            string cachedGetTableNameAnswer = syntax.GetSensibleEntityNameFromString(tblName);

            if (String.IsNullOrWhiteSpace(cachedGetTableNameAnswer))
                throw new Exception("TableNamingPattern '" + TableNamingPattern + "' resulted in an empty string for request '" + _request + "'");

            return cachedGetTableNameAnswer;
        }

        public override void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            if(_destination != null)
            {
                _destination.Dispose(listener, pipelineFailureExceptionIfAny);

                //if the extraction failed, the table didn't exist in the destination (i.e. the table was created during the extraction) and we are to DropTableIfLoadFails
                if (pipelineFailureExceptionIfAny != null && _tableDidNotExistAtStartOfLoad && DropTableIfLoadFails)
                {
                    if(_destinationDatabase != null)
                    {
                        var tbl = _destinationDatabase.ExpectTable(_toProcess.TableName);
                        
                        if(tbl.Exists())
                        {
                            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "DropTableIfLoadFails is true so about to drop table " + tbl));
                            tbl.Drop();
                            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Dropped table " + tbl));
                        }
                    }
                }
            }

            TableLoadInfo?.CloseAndArchive();

            // also close off the cumulative extraction result
            if (_request is ExtractDatasetCommand)
            {
                var result = ((IExtractDatasetCommand)_request).CumulativeExtractionResults;
                if (result != null && _toProcess != null)
                    result.CompleteAudit(this.GetType(), GetDestinationDescription(), TableLoadInfo.Inserts);
            }
        }

        public override void Abort(IDataLoadEventListener listener)
        {
            if(_destination != null)
                _destination.Abort(listener);
        }
        protected override void PreInitializeImpl(IExtractCommand value, IDataLoadEventListener listener)
        {
            
        }


        public override string GetDestinationDescription()
        {
            return GetDestinationDescription(suffix: "");
        }

        private string GetDestinationDescription(string suffix = "")
        {
            if (_toProcess == null)
                if (_request is ExtractGlobalsCommand)
                    return "Globals";
                else
                    throw new Exception("Could not describe destination because _toProcess was null");

            var tblName = _toProcess.TableName;
            var dbName = GetDatabaseName();
            return TargetDatabaseServer.ID + "|" + dbName + "|" + tblName;
        }

        public DestinationType GetDestinationType()
        {
            return DestinationType.Database;
        }
        
        public override ReleasePotential GetReleasePotential(IRDMPPlatformRepositoryServiceLocator repositoryLocator, ISelectedDataSets selectedDataSet)
        {
            return new MsSqlExtractionReleasePotential(repositoryLocator, selectedDataSet);
        }
        public override FixedReleaseSource<ReleaseAudit> GetReleaseSource(ICatalogueRepository catalogueRepository)
        {
            return new MsSqlReleaseSource<ReleaseAudit>(catalogueRepository);
        }
        public override GlobalReleasePotential GetGlobalReleasabilityEvaluator(IRDMPPlatformRepositoryServiceLocator repositoryLocator, ISupplementalExtractionResults globalResult, IMapsDirectlyToDatabaseTable globalToCheck)
        {
            return new MsSqlGlobalsReleasePotential(repositoryLocator, globalResult, globalToCheck);
        }
        
        protected override void TryExtractSupportingSQLTableImpl(SupportingSQLTable sqlTable, DirectoryInfo directory, IExtractionConfiguration configuration,IDataLoadEventListener listener, out int linesWritten,
            out string destinationDescription)
        {
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "About to download SQL for global SupportingSQL " + sqlTable.SQL));
            using (var con = sqlTable.GetServer().GetConnection())
            {
                con.Open();
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Connection opened successfully, about to send SQL command " + sqlTable.SQL));
                var cmd = DatabaseCommandHelper.GetCommand(sqlTable.SQL, con);
                var da = DatabaseCommandHelper.GetDataAdapter(cmd);

                var sw = new Stopwatch();

                sw.Start();
                DataTable dt = new DataTable();
                da.Fill(dt);

                dt.TableName = GetTableName(_destinationDatabase.Server.GetQuerySyntaxHelper().GetSensibleEntityNameFromString(sqlTable.Name));
                linesWritten = dt.Rows.Count;

                var destinationDb = GetDestinationDatabase(listener);
                var tbl = destinationDb.ExpectTable(dt.TableName);
                
                if(tbl.Exists())
                    tbl.Drop();

                destinationDb.CreateTable(dt.TableName,dt);
                destinationDescription = TargetDatabaseServer.ID + "|" + GetDatabaseName() + "|" + dt.TableName;
            }
        }

        
        protected override void TryExtractLookupTableImpl(BundledLookupTable lookup, DirectoryInfo lookupDir,
            IExtractionConfiguration requestConfiguration,IDataLoadEventListener listener, out int linesWritten, out string destinationDescription)
        {
            var tbl = lookup.TableInfo.Discover(DataAccessContext.DataExport);
            var dt = tbl.GetDataTable();
                
            dt.TableName = GetTableName(_destinationDatabase.Server.GetQuerySyntaxHelper().GetSensibleEntityNameFromString(lookup.TableInfo.Name));

            //describe the destination for the abstract base
            destinationDescription = TargetDatabaseServer.ID + "|" + GetDatabaseName() + "|" + dt.TableName;
            linesWritten = dt.Rows.Count;

            var destinationDb = GetDestinationDatabase(listener);
            var existing = destinationDb.ExpectTable(dt.TableName);
                
            if(existing.Exists())
            {
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Warning,"Dropping existing Lookup table '" + existing.GetFullyQualifiedName() +"'"));
                existing.Drop();
            }

            destinationDb.CreateTable(dt.TableName, dt);
        }

        private DiscoveredDatabase GetDestinationDatabase(IDataLoadEventListener listener)
        {
            //tell user we are about to inspect it
            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "About to open connection to " + TargetDatabaseServer));

            var databaseName = GetDatabaseName();

            var discoveredServer = DataAccessPortal.GetInstance().ExpectServer(TargetDatabaseServer, DataAccessContext.DataExport, setInitialDatabase: false);

            var db = discoveredServer.ExpectDatabase(databaseName);
            if(!db.Exists())
                db.Create();

            return db;
        }

        private string GetDatabaseName()
        {
            string dbName = DatabaseNamingPattern;

            if(_project.ProjectNumber == null)
                throw new ProjectNumberException("Project '"+_project+"' must have a ProjectNumber");

            if (_request == null)
                throw new Exception("No IExtractCommand Request was passed to this component");

            if (_request.Configuration == null)
                throw new Exception("Request did not specify any Configuration for Project '" + _project + "'");

            dbName = dbName.Replace("$p", _project.Name)
                           .Replace("$n", _project.ProjectNumber.ToString())
                           .Replace("$t", _project.MasterTicket)
                           .Replace("$r", _request.Configuration.RequestTicket)
                           .Replace("$l", _request.Configuration.ReleaseTicket);

            return dbName;
        }

        public override void Check(ICheckNotifier notifier)
        {
            if (TargetDatabaseServer == null)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Target database server property has not been set (This component does not know where to extract data to!), " +
                                                             "to fix this you must edit the pipeline and choose an ExternalDatabaseServer to extract to)", 
                                                             CheckResult.Fail));
                return;
            }

            if (string.IsNullOrWhiteSpace(TargetDatabaseServer.Server))
            {
                notifier.OnCheckPerformed(new CheckEventArgs("TargetDatabaseServer does not have a .Server specified", CheckResult.Fail));
                return;
            }

            if(!string.IsNullOrWhiteSpace(TargetDatabaseServer.Database))
            {
                notifier.OnCheckPerformed(new CheckEventArgs("TargetDatabaseServer has .Database specified but this will be ignored!", CheckResult.Warning));
            }

            if(string.IsNullOrWhiteSpace(TableNamingPattern))
            {
                notifier.OnCheckPerformed(new CheckEventArgs("You must specify TableNamingPattern, this will tell the component how to name tables it generates in the remote destination",CheckResult.Fail));
                return;
            }

            if (string.IsNullOrWhiteSpace(DatabaseNamingPattern))
            {
                notifier.OnCheckPerformed(new CheckEventArgs("You must specify DatabaseNamingPattern, this will tell the component what database to create or use in the remote destination", CheckResult.Fail));
                return;
            }

            if (!DatabaseNamingPattern.Contains("$p") && !DatabaseNamingPattern.Contains("$n") && !DatabaseNamingPattern.Contains("$t") && !DatabaseNamingPattern.Contains("$r") && !DatabaseNamingPattern.Contains("$l"))
                notifier.OnCheckPerformed(new CheckEventArgs("DatabaseNamingPattern does not contain any token. The tables may be created alongside existing tables and Release would be impossible.", CheckResult.Warning));

            if (!TableNamingPattern.Contains("$d") && !TableNamingPattern.Contains("$a"))
                notifier.OnCheckPerformed(new CheckEventArgs("TableNamingPattern must contain either $d or $a, the name/acronym of the dataset being extracted otherwise you will get collisions when you extract multiple tables at once", CheckResult.Warning));

            if (_request == ExtractDatasetCommand.EmptyCommand)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Request is ExtractDatasetCommand.EmptyCommand, will not try to connect to Database",CheckResult.Warning));
                return;
            }

            if (TableNamingPattern != null && TableNamingPattern.Contains("$a"))
            {
                var dsRequest = _request as ExtractDatasetCommand;

                if (dsRequest != null && string.IsNullOrWhiteSpace(dsRequest.Catalogue.Acronym))
                    notifier.OnCheckPerformed(new CheckEventArgs("Catalogue '" + dsRequest.Catalogue + "' does not have an Acronym but TableNamingPattern contains $a", CheckResult.Fail));
            }

            try
            {
                var server = DataAccessPortal.GetInstance().ExpectServer(TargetDatabaseServer, DataAccessContext.DataExport, setInitialDatabase: false);
                var database = server.ExpectDatabase(GetDatabaseName());

                if (database.Exists())
                    notifier.OnCheckPerformed(
                        new CheckEventArgs(
                            "Database " + database + " already exists! if an extraction has already been run " +
                            "you may have errors if you are re-extracting the same tables", CheckResult.Warning));
                else
                {
                    notifier.OnCheckPerformed(
                        new CheckEventArgs(
                            "Database " + database + " does not exist on server... it will be created at runtime",
                            CheckResult.Success));
                    return;
                }

                var tables = database.DiscoverTables(false);

                if (tables.Any())
                    notifier.OnCheckPerformed(new CheckEventArgs("The following preexisting tables were found in the database " + string.Join(",",tables.Select(t=>t.ToString())),CheckResult.Warning));
                else
                    notifier.OnCheckPerformed(new CheckEventArgs("Confirmed that database " + database + " is empty of tables", CheckResult.Success));
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs(
                    "Could not connect to TargetDatabaseServer '" + TargetDatabaseServer  +"'", CheckResult.Fail, e));
            }
        }
    }
}
