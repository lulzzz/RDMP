// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FAnsi.Discovery.QuerySyntax;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.DataLoad.Engine.Pipeline.Components;
using Rdmp.Core.DataLoad.Engine.Pipeline.Sources;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories;
using ReusableLibraryCode;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Progress;
using IContainer = Rdmp.Core.Curation.Data.IContainer;

namespace Rdmp.Core.DataExport.DataExtraction.Pipeline.Sources
{
    /// <summary>
    /// Executes a single Dataset extraction by linking a cohort with a dataset (either core or custom data - See IExtractCommand).  Also calculates the number
    /// of unique identifiers seen, records row validation failures etc.
    /// </summary>
    public class ExecuteDatasetExtractionSource : IPluginDataFlowSource<DataTable>, IPipelineRequirement<IExtractCommand>
    {
        //Request is either for one of these
        public ExtractDatasetCommand Request { get; protected set; }
        public ExtractGlobalsCommand GlobalsRequest { get; protected set; }

        public const string AuditTaskName = "DataExtraction";

        private readonly List<string> _extractionIdentifiersidx = new List<string>();
        
        private bool _cancel = false;
        
        ICatalogue _catalogue;

        protected const string ValidationColumnName = "RowValidationResult";

        public ExtractionTimeValidator ExtractionTimeValidator { get; protected set; }
        public Exception ValidationFailureException { get; protected set; }

        public HashSet<object> UniqueReleaseIdentifiersEncountered { get; set; }

        public ExtractionTimeTimeCoverageAggregator ExtractionTimeTimeCoverageAggregator { get; set; }

        [DemandsInitialization("Determines the systems behaviour when an extraction query returns 0 rows.  Default (false) is that an error is reported.  If set to true (ticked) then instead a DataTable with 0 rows but all the correct headers will be generated usually resulting in a headers only 0 line/empty extract file")]
        public bool AllowEmptyExtractions { get; set; }

        [DemandsInitialization("Batch size, number of records to read from source before releasing it into the extraction pipeline", DefaultValue = 10000, Mandatory = true)]
        public int BatchSize { get; set; }

        [DemandsInitialization("In seconds. Overrides the global timeout for SQL query execution. Use 0 for infinite timeout.", DefaultValue = 50000, Mandatory = true)]
        public int ExecutionTimeout { get; set; }

        [DemandsInitialization(@"Determines how the system achieves DISTINCT on extraction.  These include:
None - Do not DISTINCT the records, can result in duplication in your extract (not recommended)
SqlDistinct - Adds the DISTINCT keyword to the SELECT sql sent to the server
OrderByAndDistinctInMemory - Adds an ORDER BY statement to the query and applies the DISTINCT in memory as records are read from the server (this can help when extracting very large data sets where DISTINCT keyword blocks record streaming until all records are ready to go)"
            ,DefaultValue = Sources.DistinctStrategy.SqlDistinct)]
        public DistinctStrategy DistinctStrategy { get; set; }
        
        /// <summary>
        /// This is a dictionary containing all the CatalogueItems used in the query, the underlying datatype in the origin database and the
        /// actual datatype that was output after the transform operation e.g. a varchar(10) could be converted into a bona fide DateTime which
        /// would be an sql Date.  Finally
        /// a recommended SqlDbType is passed back.
        /// </summary>
        public Dictionary<ExtractableColumn, ExtractTimeTransformationObserved> ExtractTimeTransformationsObserved;
        private DbDataCommandDataFlowSource _hostedSource;

        protected virtual void Initialize(ExtractDatasetCommand request)
        {
            Request = request;

            if (request == ExtractDatasetCommand.EmptyCommand)
                return;

            _timeSpentValidating = new Stopwatch();
            _timeSpentCalculatingDISTINCT = new Stopwatch();
            _timeSpentBuckettingDates = new Stopwatch();

            Request.ColumnsToExtract.Sort();//ensure they are in the right order so we can record the release identifiers
        
            //if we have a cached builder already
            if(request.QueryBuilder == null)
                request.GenerateQueryBuilder();
            
            foreach (ReleaseIdentifierSubstitution substitution in Request.ReleaseIdentifierSubstitutions)
                _extractionIdentifiersidx.Add(substitution.GetRuntimeName());
            
            UniqueReleaseIdentifiersEncountered = new HashSet<object>();

            _catalogue = request.Catalogue;

            if (!string.IsNullOrWhiteSpace(_catalogue.ValidatorXML))
                ExtractionTimeValidator = new ExtractionTimeValidator(_catalogue, request.ColumnsToExtract);
          
            //if there is a time periodicity ExtractionInformation (AND! it is among the columns the user selected to be extracted)
            if (_catalogue.TimeCoverage_ExtractionInformation_ID != null && request.ColumnsToExtract.Cast<ExtractableColumn>().Any(c => c.CatalogueExtractionInformation_ID == _catalogue.TimeCoverage_ExtractionInformation_ID))
                ExtractionTimeTimeCoverageAggregator = new ExtractionTimeTimeCoverageAggregator(_catalogue, request.ExtractableCohort);
            else
                ExtractionTimeTimeCoverageAggregator = null;
        }

        private void Initialize(ExtractGlobalsCommand request)
        {
            GlobalsRequest = request;
        }

        public bool WasCancelled
        {
            get { return _cancel; }
        }
        
        private Stopwatch _timeSpentValidating;
        private int _rowsValidated = 0;

        private Stopwatch _timeSpentCalculatingDISTINCT;
        private Stopwatch _timeSpentBuckettingDates;
        private int _rowsBucketted = 0;

        private bool firstChunk = true;
        private bool firstGlobalChunk = true;
        private int _rowsRead;

        private RowPeeker _peeker = new RowPeeker();

        public virtual DataTable GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            // we are in the Global Commands case, let's return an empty DataTable (not null) 
            // so we can trigger the destination to extract the globals docs and sql
            if (GlobalsRequest != null)
            {
                GlobalsRequest.ElevateState(ExtractCommandState.WaitingForSQLServer);
                if (firstGlobalChunk)
                {
                    //unless we are checking, start auditing
                    StartAuditGlobals();
                    
                    firstGlobalChunk = false;
                    return new DataTable(ExtractionDirectory.GLOBALS_DATA_NAME);
                }

                return null;
            }

            if (Request == null)
                throw new Exception("Component has not been initialized before being asked to GetChunk(s)");

            Request.ElevateState(ExtractCommandState.WaitingForSQLServer);
            
            if(_cancel)
                throw new Exception("User cancelled data extraction");
            
           if (_hostedSource == null)
            {
               StartAudit(Request.QueryBuilder.SQL);
               
               if(Request.DatasetBundle.DataSet.DisableExtraction)
                   throw new Exception("Cannot extract " + Request.DatasetBundle.DataSet + " because DisableExtraction is set to true");

                _hostedSource = new DbDataCommandDataFlowSource(GetCommandSQL(listener),
                                                                "ExecuteDatasetExtraction " + Request.DatasetBundle.DataSet,
                                                                _catalogue.GetDistinctLiveDatabaseServer(DataAccessContext.DataExport, false).Builder, 
                                                                ExecutionTimeout);

                _hostedSource.AllowEmptyResultSets = AllowEmptyExtractions;
                _hostedSource.BatchSize = BatchSize;
            }

            DataTable chunk = null;

            try
            {
                chunk = _hostedSource.GetChunk(listener, cancellationToken);

                chunk = _peeker.AddPeekedRowsIfAny(chunk);
                
                //if we are trying to distinct the records in memory based on release id
                if (DistinctStrategy == DistinctStrategy.OrderByAndDistinctInMemory)
                {
                    var releaseIdentifierColumn =  Request.ReleaseIdentifierSubstitutions.First().GetRuntimeName();

                    if(chunk != null)
                    {
                        //last release id in the current chunk
                        var lastReleaseId = chunk.Rows[chunk.Rows.Count-1][releaseIdentifierColumn];

                        _peeker.AddWhile(_hostedSource,r=>Equals(r[releaseIdentifierColumn], lastReleaseId),chunk);
                        chunk = MakeDistinct(chunk,listener,cancellationToken);
                    }
                }
            }
            catch (AggregateException a)
            {
                if (a.GetExceptionIfExists<TaskCanceledException>() != null)
                    _cancel = true;

                throw;
            }
            catch (Exception e)
            {
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Error, "Read from source failed",e));
            }
            
            if(cancellationToken.IsCancellationRequested)
                throw new Exception("Data read cancelled because our cancellationToken was set, aborting data reading");
            
            //if the first chunk is null
            if (firstChunk && chunk == null)
                throw new Exception("There is no data to load, query returned no rows, query was:" + Environment.NewLine + Request.QueryBuilder.SQL);
            
            //not the first chunk anymore
            firstChunk = false;

            //data exhausted
            if (chunk == null)
            {
                listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Information, "Data exhausted after reading " + _rowsRead + " rows of data ("+UniqueReleaseIdentifiersEncountered.Count + " unique release identifiers seen)"));
                if (Request != null)
                    Request.CumulativeExtractionResults.DistinctReleaseIdentifiersEncountered = UniqueReleaseIdentifiersEncountered.Count;
                return null;
            }

            _rowsRead += chunk.Rows.Count;
            //chunk will have datatypes for all the things in the buffer so we can populate our dictionary of facts about what columns/catalogue items have spontaneously changed name/type etc
            if(ExtractTimeTransformationsObserved == null)
                GenerateExtractionTransformObservations(chunk);


            //see if the SqlDataReader has a column with the same name as the ReleaseIdentifierSQL (if so then we can use it to count the number of distinct subjects written out to the csv)
            bool includesReleaseIdentifier = _extractionIdentifiersidx.Count > 0;


            //first line - lets see what columns we wrote out
            //looks at the buffer and computes any transforms performed on the column
                    

            _timeSpentValidating.Start();
            //build up the validation report (Missing/Wrong/Etc) - this has no mechanical effect on the extracted data just some metadata that goes into a flat file
            if (ExtractionTimeValidator != null && Request.IncludeValidation)
                try
                {
                    chunk.Columns.Add(ValidationColumnName);

                    ExtractionTimeValidator.Validate(chunk, ValidationColumnName);

                    _rowsValidated += chunk.Rows.Count;
                    listener.OnProgress(this,new ProgressEventArgs("Validation",new ProgressMeasurement(_rowsValidated,ProgressType.Records), _timeSpentValidating.Elapsed));
                }
                catch (Exception ex)
                {
                    listener.OnNotify(this,new NotifyEventArgs(ProgressEventType.Error, "Could not validate data chunk",ex));
                    ValidationFailureException = ex;
                    ExtractionTimeValidator = null;
                }
            _timeSpentValidating.Stop();
            
            _timeSpentBuckettingDates.Start();
            if (ExtractionTimeTimeCoverageAggregator != null)
            {
                _rowsBucketted += chunk.Rows.Count;

                foreach (DataRow row in chunk.Rows)
                    ExtractionTimeTimeCoverageAggregator.ProcessRow(row);
                
                listener.OnProgress(this, new ProgressEventArgs("Bucketting Dates",new ProgressMeasurement(_rowsBucketted,ProgressType.Records),_timeSpentCalculatingDISTINCT.Elapsed ));
            }
            _timeSpentBuckettingDates.Stop();

            _timeSpentCalculatingDISTINCT.Start();
            //record unique release identifiers found
            if (includesReleaseIdentifier)
                foreach (string idx in _extractionIdentifiersidx)
                {
                    foreach (DataRow r in chunk.Rows)
                    {
                        if (r[idx] == DBNull.Value)
                            if (_extractionIdentifiersidx.Count == 1)
                                throw new Exception("Null release identifier found in extract of dataset " + Request.DatasetBundle.DataSet);
                            else
                                continue; //there are multiple extraction identifiers thats fine if one or two are null

                        if (!UniqueReleaseIdentifiersEncountered.Contains(r[idx]))
                            UniqueReleaseIdentifiersEncountered.Add(r[idx]);
                    }

                     listener.OnProgress(this,new ProgressEventArgs("Calculating Distinct Release Identifiers",new ProgressMeasurement(UniqueReleaseIdentifiersEncountered.Count, ProgressType.Records),_timeSpentCalculatingDISTINCT.Elapsed ));
                }
            _timeSpentCalculatingDISTINCT.Stop();

            return chunk;
        }

        /// <summary>
        /// Makes the current batch ONLY distinct.  This only works if you have a bounded batch (see OrderByAndDistinctInMemory)
        /// </summary>
        /// <param name="chunk"></param>
        /// <param name="listener"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private DataTable MakeDistinct(DataTable chunk, IDataLoadEventListener listener,GracefulCancellationToken cancellationToken)
        {
            var removeDuplicates = new RemoveDuplicates(){NoLogging=true};
            return removeDuplicates.ProcessPipelineData(chunk, listener, cancellationToken);
        }

        private void GenerateExtractionTransformObservations(DataTable chunk)
        {
            ExtractTimeTransformationsObserved = new Dictionary<ExtractableColumn, ExtractTimeTransformationObserved>();

            //create the Types dictionary
            foreach (ExtractableColumn column in Request.ColumnsToExtract)
            {
                ExtractTimeTransformationsObserved.Add(column, new ExtractTimeTransformationObserved());

                //record catalogue information about what it is supposed to be.
                if (!column.HasOriginalExtractionInformationVanished())
                {
                    var extractionInformation = column.CatalogueExtractionInformation;

                    //what the catalogue says it is
                    ExtractTimeTransformationsObserved[column].DataTypeInCatalogue =
                        extractionInformation.ColumnInfo.Data_type;
                    ExtractTimeTransformationsObserved[column].CatalogueItem = extractionInformation.CatalogueItem;

                    //what it actually is
                    if (chunk.Columns.Contains(column.GetRuntimeName()))
                    {
                        ExtractTimeTransformationsObserved[column].FoundAtExtractTime = true;
                        ExtractTimeTransformationsObserved[column].DataTypeObservedInRuntimeBuffer =
                            chunk.Columns[column.GetRuntimeName()].DataType;
                    }
                    else
                        ExtractTimeTransformationsObserved[column].FoundAtExtractTime = false;
                }
            }
        }

        private string GetCommandSQL(IDataLoadEventListener listener)
        {
            //if the user wants some custom logic for removing identical duplicates
            switch (DistinctStrategy)
            {
                //user doesn't care about identical duplicates
                case DistinctStrategy.None:
                    ((QueryBuilder)Request.QueryBuilder).SetLimitationSQL("");
                    break;

                //system default behaviour
                case DistinctStrategy.SqlDistinct:
                    break;

                //user wants to run order by the release ID and resolve duplicates in batches as they are read
                case DistinctStrategy.OrderByAndDistinctInMemory:
                    
                    //remove the DISTINCT keyword from the query
                    ((QueryBuilder)Request.QueryBuilder).SetLimitationSQL("");

                    //find the release identifier substitution (e.g. chi for PROCHI)
                    var substitution =  Request.ReleaseIdentifierSubstitutions.First();

                    //add a line at the end of the query to ORDER BY the ReleaseId column (e.g. PROCHI)
                    Request.QueryBuilder.AddCustomLine("ORDER BY " + substitution.SelectSQL, QueryComponent.Postfix);

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            string sql = Request.QueryBuilder.SQL;

            sql = HackExtractionSQL(sql,listener);

            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "/*Decided on extraction SQL:*/"+Environment.NewLine + sql));
            
            return sql;
        }

        public virtual string HackExtractionSQL(string sql, IDataLoadEventListener listener)
        {
            return sql;

        }
        
        private void StartAudit(string sql)
        {
            var dataExportRepo = ((DataExportRepository) Request.DataExportRepository);

            var previousAudit = dataExportRepo.GetAllCumulativeExtractionResultsFor(Request.Configuration, Request.DatasetBundle.DataSet).ToArray();

            //delete old audit records
            foreach (var audit in previousAudit)
                audit.DeleteInDatabase();

            var extractionResults = new CumulativeExtractionResults(dataExportRepo, Request.Configuration, Request.DatasetBundle.DataSet, sql);

            string filterDescriptions = RecursivelyListAllFilterNames(Request.Configuration.GetFilterContainerFor(Request.DatasetBundle.DataSet));

            extractionResults.FiltersUsed = filterDescriptions.TrimEnd(',');
            extractionResults.SaveToDatabase();

            Request.CumulativeExtractionResults = extractionResults;
        }

        private void StartAuditGlobals()
        {
            var dataExportRepo = ((DataExportRepository)GlobalsRequest.RepositoryLocator.DataExportRepository);

            var previousAudit = dataExportRepo.GetAllGlobalExtractionResultsFor(GlobalsRequest.Configuration);

            //delete old audit records
            foreach (var audit in previousAudit)
                audit.DeleteInDatabase();
        }

        private string RecursivelyListAllFilterNames(IContainer filterContainer)
        {
            if (filterContainer == null)
                return "";

            string toReturn = "";

            if (filterContainer.GetSubContainers() != null)
                foreach (IContainer subContainer in filterContainer.GetSubContainers())
                    toReturn += RecursivelyListAllFilterNames(subContainer);

            if(filterContainer.GetFilters() != null)
                foreach (IFilter f in filterContainer.GetFilters())
                    toReturn += f.Name +',';

            return toReturn;
        }
        
        public virtual void Dispose(IDataLoadEventListener job, Exception pipelineFailureExceptionIfAny)
        {
            
        }

        public void Abort(IDataLoadEventListener listener)
        {
            
        }

        public virtual DataTable TryGetPreview()
        {
            if(Request == ExtractDatasetCommand.EmptyCommand)
                return new DataTable();

            DataTable toReturn = new DataTable();
            var server = _catalogue.GetDistinctLiveDatabaseServer(DataAccessContext.DataExport,false);

            using (var con = server.GetConnection())
            {
                con.Open();

                var da = server.GetDataAdapter(Request.QueryBuilder.SQL, con);

                //get up to 1000 records
                da.Fill(0, 1000, toReturn);
                
                con.Close();
            }

            return toReturn;
        }

        public void PreInitialize(IExtractCommand value, IDataLoadEventListener listener)
        {
            if (value is ExtractDatasetCommand)
                Initialize(value as ExtractDatasetCommand);
            if (value is ExtractGlobalsCommand)
                Initialize(value as ExtractGlobalsCommand);
        }

        public virtual void Check(ICheckNotifier notifier)
        {
            if (Request == ExtractDatasetCommand.EmptyCommand)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Request is ExtractDatasetCommand.EmptyCommand, checking will not be carried out",CheckResult.Warning));
                return;
            }

            if (GlobalsRequest != null)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Request is for Globals, checking will not be carried out at source", CheckResult.Success));
                return;
            }
            
            if (Request == null)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("ExtractionRequest has not been set", CheckResult.Fail));
                return;
            }
        }
    }
}
