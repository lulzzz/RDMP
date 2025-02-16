// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FAnsi.Connections;
using FAnsi.Discovery;
using FAnsi.Discovery.TableCreation;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataFlowPipeline.Requirements;
using Rdmp.Core.Logging;
using Rdmp.Core.Logging.Listeners;
using Rdmp.Core.Repositories.Construction;
using ReusableLibraryCode;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;
using ReusableLibraryCode.Progress;
using TypeGuesser;

namespace Rdmp.Core.DataLoad.Engine.Pipeline.Destinations
{
    /// <summary>
    /// Pipeline component (destination) which commits the DataTable(s) (in batches) to the DiscoveredDatabase (PreInitialize argument).  Supports cross platform 
    /// targets (MySql , Sql Server etc).  Normally the SQL Data Types and column names will be computed from the DataTable and a table will be created with the
    /// name of the DataTable being processed.  If a matching table already exists you can choose to load it anyway in which case a basic bulk insert will take 
    /// place.
    /// </summary>
    public class DataTableUploadDestination : IPluginDataFlowComponent<DataTable>, IDataFlowDestination<DataTable>, IPipelineRequirement<DiscoveredDatabase>
    {
        public const string LoggingServer_Description = "The logging server to log the upload to (leave blank to not bother auditing)";
        public const string AllowResizingColumnsAtUploadTime_Description = "If the target table being loaded has columns that are too small the destination will attempt to resize them";
        public const string AllowLoadingPopulatedTables_Description = "Normally when DataTableUploadDestination encounters a table that already contains records it will abandon the insertion attempt.  Set this to true to instead continue with the load.";
        public const string AlterTimeout_Description = "Timeout to perform all ALTER TABLE operations (column resize and PK creation)";

        [DemandsInitialization(LoggingServer_Description)]
        public ExternalDatabaseServer LoggingServer { get; set; }

        [DemandsInitialization(AllowResizingColumnsAtUploadTime_Description, DefaultValue = true)]
        public bool AllowResizingColumnsAtUploadTime { get; set; }
        
        [DemandsInitialization(AllowLoadingPopulatedTables_Description, DefaultValue = false)]
        public bool AllowLoadingPopulatedTables { get; set; }
        
        [DemandsInitialization(AlterTimeout_Description, DefaultValue = 300)]
        public int AlterTimeout { get; set; }

        [DemandsInitialization("Optional - Change system behaviour when a new table is being created by the component", TypeOf = typeof(IDatabaseColumnRequestAdjuster))]
        public Type Adjuster { get; set; }

        private CultureInfo _culture;
        [DemandsInitialization("The culture to use for uploading (determines date format etc)")]
        public CultureInfo Culture
        {
            get => _culture ?? CultureInfo.CurrentCulture;
            set => _culture = value;
        }

        public string TargetTableName { get; private set; }
        
        private IBulkCopy _bulkcopy;
        private int _affectedRows = 0;
        
        Stopwatch swTimeSpentWritting = new Stopwatch();
        Stopwatch swMeasuringStrings = new Stopwatch();

        private DiscoveredServer _loggingDatabaseSettings;

        private DiscoveredServer _server;
        private DiscoveredDatabase _database;

        private DataLoadInfo _dataLoadInfo;

        private IManagedConnection _managedConnection;
        private ToLoggingDatabaseDataLoadEventListener _loggingDatabaseListener;

        public List<DatabaseColumnRequest> ExplicitTypes { get; set; }

        private bool _firstTime = true;
        private HashSet<string> _primaryKey = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        private DiscoveredTable discoveredTable;

        //All column values sent to server so far
        Dictionary<string, Guesser> _dataTypeDictionary;

        public DataTableUploadDestination()
        {
            ExplicitTypes = new List<DatabaseColumnRequest>();
        }

        public DataTable ProcessPipelineData(DataTable toProcess, IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            if (toProcess == null)
                return null;

            IDatabaseColumnRequestAdjuster adjuster = null;
            if (Adjuster != null)
            {
                var constructor = new ObjectConstructor();
                adjuster = (IDatabaseColumnRequestAdjuster) constructor.Construct(Adjuster);
            }

            //work out the table name for the table we are going to create
            if (TargetTableName == null)
            {
                if (string.IsNullOrWhiteSpace(toProcess.TableName))
                    throw new Exception("Chunk did not have a TableName, did not know what to call the newly created table");
                
                TargetTableName = QuerySyntaxHelper.MakeHeaderNameSensible(toProcess.TableName);
            }

            ClearPrimaryKeyFromDataTableAndExplicitWriteTypes(toProcess);
            
            StartAuditIfExists(TargetTableName);

            if (_loggingDatabaseListener != null)
                listener = new ForkDataLoadEventListener(listener, _loggingDatabaseListener);

            EnsureTableHasDataInIt(toProcess);

            bool createdTable = false;
            
            if (_firstTime)
            {
                bool tableAlreadyExistsButEmpty = false;

                if (!_database.Exists())
                    throw new Exception("Database " + _database + " does not exist");

                discoveredTable = _database.ExpectTable(TargetTableName);

                //table already exists
                if (discoveredTable.Exists())
                {
                    tableAlreadyExistsButEmpty = true;
                    
                    if(!AllowLoadingPopulatedTables)
                        if (discoveredTable.IsEmpty())
                            listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Found table " + TargetTableName + " already, normally this would forbid you from loading it (data duplication / no primary key etc) but it is empty so we are happy to load it, it will not be created"));
                        else
                            throw new Exception("There is already a table called " + TargetTableName + " at the destination " + _database);
                    
                    if (AllowResizingColumnsAtUploadTime)
                        _dataTypeDictionary = discoveredTable.DiscoverColumns().ToDictionary(k => k.GetRuntimeName(), v => v.GetGuesser(),StringComparer.CurrentCultureIgnoreCase);
                }
                else
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Determined that the table name " + TargetTableName + " is unique at destination " + _database));
                
                //create connection to destination
               if (!tableAlreadyExistsButEmpty)
               {
                   createdTable = true;

                   if (AllowResizingColumnsAtUploadTime)
                       _database.CreateTable(out _dataTypeDictionary, TargetTableName, toProcess, ExplicitTypes.ToArray(), true, adjuster);
                   else
                       _database.CreateTable(TargetTableName, toProcess, ExplicitTypes.ToArray(), true, adjuster);

                   listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Created table " + TargetTableName + " successfully."));
                }

                _managedConnection = _server.BeginNewTransactedConnection();
                _bulkcopy = discoveredTable.BeginBulkInsert(Culture,_managedConnection.ManagedTransaction);
                
                _firstTime = false;
            }

            try
            {
                if (AllowResizingColumnsAtUploadTime && !createdTable)
                    ResizeColumnsIfRequired(toProcess, listener);

                //push the data
                swTimeSpentWritting.Start();

                _affectedRows += _bulkcopy.Upload(toProcess);
                    
                swTimeSpentWritting.Stop();
                listener.OnProgress(this, new ProgressEventArgs("Uploading to " + TargetTableName, new ProgressMeasurement(_affectedRows, ProgressType.Records), swTimeSpentWritting.Elapsed));
            }
            catch (Exception e)
            {
                _managedConnection.ManagedTransaction.AbandonAndCloseConnection();

                if (LoggingServer != null)
                    _dataLoadInfo.LogFatalError(GetType().Name, ExceptionHelper.ExceptionToListOfInnerMessages(e, true));

                throw new Exception("Failed to write rows (in transaction) to table " + TargetTableName, e);
            }

            return null;
        }

        /// <summary>
        /// Clears the primary key status of the DataTable / <see cref="ExplicitTypes"/>.  These are recorded in <see cref="_primaryKey"/> and applied at Dispose time
        /// in order that primary key in the destination database table does not interfere with ALTER statements (see <see cref="ResizeColumnsIfRequired"/>)
        /// </summary>
        /// <param name="toProcess"></param>
        private void ClearPrimaryKeyFromDataTableAndExplicitWriteTypes(DataTable toProcess)
        {
            //handle primary keyness by removing it until Dispose step
            foreach (var pkCol in toProcess.PrimaryKey.Select(dc => dc.ColumnName))
                _primaryKey.Add(pkCol);

            toProcess.PrimaryKey = new DataColumn[0];

            //also get rid of any ExplicitTypes primary keys
            foreach (var dcr in ExplicitTypes.Where(dcr => dcr.IsPrimaryKey))
            {
                dcr.IsPrimaryKey = false;
                _primaryKey.Add(dcr.ColumnName);
            }
        }

        private void EnsureTableHasDataInIt(DataTable toProcess)
        {
            if(toProcess.Columns.Count == 0)
                throw new Exception("DataTable '" + toProcess + "' had no Columns!");

            if (toProcess.Rows.Count == 0)
                throw new Exception("DataTable '" + toProcess + "' had no Rows!");
        }

        private void ResizeColumnsIfRequired(DataTable toProcess, IDataLoadEventListener listener)
        {
            swMeasuringStrings.Start();

            var tbl = _database.ExpectTable(TargetTableName);
            var typeTranslater = tbl.GetQuerySyntaxHelper().TypeTranslater;
            
            //Get the current estimates from the datatype computer
            Dictionary<string, string> oldTypes = _dataTypeDictionary.ToDictionary(k => k.Key, v => typeTranslater.GetSQLDBTypeForCSharpType(v.Value.Guess),StringComparer.CurrentCultureIgnoreCase);

            //columns in 
            List<string> sharedColumns = new List<string>();

            //for each destination column
            foreach (string col in _dataTypeDictionary.Keys)
                //if it appears in the toProcess DataTable
                if (toProcess.Columns.Contains(col))
                    sharedColumns.Add(col); //it is a shared column

            
            
            //adjust the computer to 
            //for each shared column adjust the corresponding computer for all rows
            Parallel.ForEach(sharedColumns, col =>
            {
                var guesser = _dataTypeDictionary[col];
                foreach (DataRow row in toProcess.Rows)
                    guesser.AdjustToCompensateForValue(row[col]);
            });            

            //see if any have changed
            foreach (DataColumn column in toProcess.Columns)
            {
                //get what is required for the current batch and the current type that is configured in the live table
                string oldSqlType = oldTypes[column.ColumnName];
                string newSqlType = typeTranslater.GetSQLDBTypeForCSharpType(_dataTypeDictionary[column.ColumnName].Guess);

                bool changesMade = false;

                //if the SQL data type has degraded e.g. varchar(10) to varchar(50) or datetime to varchar(20)
                if(oldSqlType != newSqlType)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Resizing column '" + column + "' from '" + oldSqlType + "' to '" + newSqlType + "'"));

                    var col = tbl.DiscoverColumn(column.ColumnName,_managedConnection.ManagedTransaction);

                    //try changing the Type to the legit type
                    col.DataType.AlterTypeTo(newSqlType, _managedConnection.ManagedTransaction, AlterTimeout);
                    
                    changesMade = true;
                }

                if(changesMade)
                    _bulkcopy.InvalidateTableSchema();
            }
            
            swMeasuringStrings.Stop();
            listener.OnProgress(this,new ProgressEventArgs("Measuring DataType Sizes",new ProgressMeasurement(_affectedRows + toProcess.Rows.Count,ProgressType.Records),swMeasuringStrings.Elapsed));
        }

        public void Abort(IDataLoadEventListener listener)
        {
            _managedConnection.ManagedTransaction.AbandonAndCloseConnection();
        }

        public void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            try
            {
                if (_managedConnection != null)
                {
                    //if there was an error
                    if (pipelineFailureExceptionIfAny != null)
                    {
                        _managedConnection.ManagedTransaction.AbandonAndCloseConnection();
                        
                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Transaction rolled back sucessfully"));

                        if (_bulkcopy != null)
                            _bulkcopy.Dispose();
                    }
                    else
                    {
                        _managedConnection.ManagedTransaction.CommitAndCloseConnection();

                        if (_bulkcopy != null)
                            _bulkcopy.Dispose();

                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "Transaction committed sucessfully"));
                    }
                }
            }
            catch (Exception e)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Error, "Commit failed on transaction (probably there was a previous error?)", e));
            }

            //if we have a primary key to create
            if (pipelineFailureExceptionIfAny == null && _primaryKey != null && _primaryKey.Any() && discoveredTable != null && discoveredTable.Exists())
            {
                //Find the columns in the destination
                var allColumns = discoveredTable.DiscoverColumns();
                
                //if there are not yet any primary keys
                if(allColumns.All(c=>!c.IsPrimaryKey))
                {
                    //find the columns the user decorated in his DataTable
                    DiscoveredColumn[] pkColumnsToCreate = allColumns.Where(c => _primaryKey.Any(pk => pk.Equals(c.GetRuntimeName(), StringComparison.CurrentCultureIgnoreCase))).ToArray();

                    //make sure we found all of them
                    if(pkColumnsToCreate.Length != _primaryKey.Count)
                        throw new Exception("Could not find primary key column(s) " + string.Join(",",_primaryKey) + " in table " + discoveredTable);

                    //create the primary key to match user provided columns
                    discoveredTable.CreatePrimaryKey(AlterTimeout, pkColumnsToCreate);
                }
            }

            EndAuditIfExists();
        }

        private void EndAuditIfExists()
        {
            //user is auditing
            if (_loggingDatabaseListener != null)
                _loggingDatabaseListener.FinalizeTableLoadInfos();
        }

        public void Check(ICheckNotifier notifier)
        {
            if (LoggingServer != null)
            {
                new LoggingDatabaseChecker(LoggingServer).Check(notifier);
            }
            else
            {
                notifier.OnCheckPerformed(
                    new CheckEventArgs(
                        "There is no logging server so there will be no audit of this destinations activities",
                        CheckResult.Success));
            }
        }

        private void StartAuditIfExists(string tableName)
        {
            if (LoggingServer != null && _dataLoadInfo == null)
            {
                _loggingDatabaseSettings = DataAccessPortal.GetInstance().ExpectServer(LoggingServer, DataAccessContext.Logging);
                var logManager = new LogManager(_loggingDatabaseSettings);
                logManager.CreateNewLoggingTaskIfNotExists("Internal");

                _dataLoadInfo = (DataLoadInfo) logManager.CreateDataLoadInfo("Internal", GetType().Name, "Loading table " + tableName, "", false);
                _loggingDatabaseListener = new ToLoggingDatabaseDataLoadEventListener(logManager, _dataLoadInfo);
            }
        }

        public void PreInitialize(DiscoveredDatabase value, IDataLoadEventListener listener)
        {
            _database = value;
            _server = value.Server;
        }
        
        /// <summary>
        /// Declare that the column of name columnName (which might or might not appear in DataTables being uploaded) should always have the associated database type (e.g. varchar(59))
        /// The columnName is Case insensitive.  Note that if AllowResizingColumnsAtUploadTime is true then these datatypes are only the starting types and might get changed later to
        /// accomodate new data.
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="explicitType"></param>
        /// <param name="columnFlags"></param>
        /// <returns>The Column Request that has been added to the array</returns>
        public DatabaseColumnRequest AddExplicitWriteType(string columnName, string explicitType, ISupplementalColumnInformation columnFlags = null)
        {
            DatabaseColumnRequest columnRequest;
            
            if (columnFlags == null)
            {
                columnRequest = new DatabaseColumnRequest(columnName, explicitType, true);
                ExplicitTypes.Add(columnRequest);
                return columnRequest;
            }
            else
            {
                columnRequest = new DatabaseColumnRequest(columnName, explicitType, !columnFlags.IsPrimaryKey && !columnFlags.IsAutoIncrement)
                {
                    IsPrimaryKey = columnFlags.IsPrimaryKey,
                    IsAutoIncrement = columnFlags.IsAutoIncrement,
                    Collation = columnFlags.Collation
                };

                ExplicitTypes.Add(columnRequest);
                return columnRequest;
            }
        }
    }
}
