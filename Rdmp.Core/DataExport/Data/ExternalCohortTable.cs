// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using FAnsi;
using FAnsi.Connections;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Attributes;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Repositories;
using ReusableLibraryCode;
using ReusableLibraryCode.Annotations;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Core.DataExport.Data
{
    /// <inheritdoc cref="IExternalCohortTable"/>
    public class ExternalCohortTable : DatabaseEntity, IDataAccessCredentials, IExternalCohortTable,INamed
    {
        #region Database Properties
        private string _name;

        /// <summary>
        /// Human readable name for the type of cohort which is stored in the database referenced by this object (e.g."CHI to Guid Cohorts")
        /// </summary>
        [NotNull]
        [Unique]
        public string Name
        {
            get { return _name; }
            set { SetField(ref _name, value); }
        }

        #endregion

        [NoMappingToDatabase]
        private SelfCertifyingDataAccessPoint SelfCertifyingDataAccessPoint { get; set; }

        /// <inheritdoc/>
        public string DefinitionTableForeignKeyField
        {
            get { return _definitionTableForeignKeyField; }
            set { SetField(ref _definitionTableForeignKeyField, GetQuerySyntaxHelper().EnsureFullyQualified(Database, null,TableName, value)); }
        }

        /// <inheritdoc/>
        public string TableName
        {
            get { return _tableName; }
            set { SetField(ref _tableName, GetQuerySyntaxHelper().EnsureFullyQualified(Database,null, value)); }
        }

        /// <inheritdoc/>
        public string DefinitionTableName
        {
            get { return _definitionTableName; }
            set { SetField(ref _definitionTableName, GetQuerySyntaxHelper().EnsureFullyQualified(Database, null, value)); }
        }

        /// <inheritdoc/>
        public string PrivateIdentifierField
        {
            get { return _privateIdentifierField; }
            set { SetField(ref _privateIdentifierField, GetQuerySyntaxHelper().EnsureFullyQualified(Database,null, TableName, value)); }
        }

        /// <inheritdoc/>
        /// <summary>
        /// When reading this, use GetReleaseIdentifier(ExtractableCohort cohort) where possible to respect cohort.OverrideReleaseIdentifierSQL
        /// </summary>
        public string ReleaseIdentifierField
        {
            get { return _releaseIdentifierField; }
            set { SetField(ref _releaseIdentifierField, GetQuerySyntaxHelper().EnsureFullyQualified(Database,null, TableName, value)); }
        }

        /// <summary>
        /// Fields expected to be part of any table referenced by the <see cref="DefinitionTableName"/> property
        /// </summary>
        public static readonly string[] CohortDefinitionTable_RequiredFields = new[]
        {
            "id",
            // joins to CohortToDefinitionTableJoinColumn and is used as ID in all ExtractableCohort entities throughout DataExportManager
            "projectNumber",
            "description",
            "version",
            "dtCreated"
        };

        private string _privateIdentifierField;
        private string _releaseIdentifierField;
        private string _tableName;
        private string _definitionTableName;
        private string _definitionTableForeignKeyField;

        /// <summary>
        /// Returns <see cref="Name"/>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Creates a new blank pointer to a cohort database.
        /// </summary>
        /// <param name="repository">Metadata repository in which to create the object</param>
        /// <param name="name"></param>
        /// <param name="databaseType"></param>
        public ExternalCohortTable(IDataExportRepository repository, string name, DatabaseType databaseType)
        {
            Repository = repository;
            SelfCertifyingDataAccessPoint = new SelfCertifyingDataAccessPoint(repository.CatalogueRepository, databaseType);
            Repository.InsertAndHydrate(this, new Dictionary<string, object>
            {
                {"Name", name ?? "NewExternalSource" + Guid.NewGuid()},
                {"DatabaseType",databaseType.ToString()}
            });
        }

        /// <summary>
        /// Reads an existing cohort database reference out of the metadata repository database
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="r"></param>
        internal ExternalCohortTable(IDataExportRepository repository, DbDataReader r)
            : base(repository, r)
        {
            Name = r["Name"] as string;
            var databaseType = (DatabaseType)Enum.Parse(typeof(DatabaseType), r["DatabaseType"].ToString());

            SelfCertifyingDataAccessPoint = new SelfCertifyingDataAccessPoint(((DataExportRepository)repository).CatalogueRepository,databaseType);

            Server = r["Server"] as string;
            Username = r["Username"] as string;
            Password = r["Password"] as string;
            Database = r["Database"] as string;

            var syntaxHelper = GetQuerySyntaxHelper();

            
            TableName = syntaxHelper.EnsureFullyQualified(Database, null, r["TableName"] as string);
            DefinitionTableForeignKeyField = syntaxHelper.EnsureFullyQualified(Database, null, TableName, r["DefinitionTableForeignKeyField"] as string);

            DefinitionTableName = syntaxHelper.EnsureFullyQualified(Database, null, r["DefinitionTableName"] as string);
            

            PrivateIdentifierField = syntaxHelper.EnsureFullyQualified(Database,null, TableName, r["PrivateIdentifierField"] as string);
            ReleaseIdentifierField = syntaxHelper.EnsureFullyQualified(Database,null, TableName, r["ReleaseIdentifierField"] as string);
        }

        /// <inheritdoc/>
        public IQuerySyntaxHelper GetQuerySyntaxHelper()
        {
            return new QuerySyntaxHelperFactory().Create(SelfCertifyingDataAccessPoint.DatabaseType);
        }
        
        /// <inheritdoc/>
        public DiscoveredDatabase Discover()
        {
            return SelfCertifyingDataAccessPoint.Discover(DataAccessContext.DataExport);
        }

        /// <inheritdoc/>
        public DiscoveredTable DiscoverCohortTable()
        {
            var db = Discover();
            return db.ExpectTable(db.Server.GetQuerySyntaxHelper().GetRuntimeName(TableName));
        }

        public DiscoveredTable DiscoverDefinitionTable()
        {
            var db = Discover();
            return db.ExpectTable(db.Server.GetQuerySyntaxHelper().GetRuntimeName(DefinitionTableName));
        }

        /// <inheritdoc/>
        public DiscoveredColumn DiscoverPrivateIdentifier()
        {
            return Discover(DiscoverCohortTable(), PrivateIdentifierField);
        }
        /// <inheritdoc/>
        public DiscoveredColumn DiscoverReleaseIdentifier()
        {
            return Discover(DiscoverCohortTable(), ReleaseIdentifierField);
        }

        public DiscoveredColumn DiscoverDefinitionTableForeignKey()
        {
            return Discover(DiscoverCohortTable(), DefinitionTableForeignKeyField);
        }

        private DiscoveredColumn Discover(DiscoveredTable tbl, string column)
        {
            return tbl.DiscoverColumn(tbl.Database.Server.GetQuerySyntaxHelper().GetRuntimeName(column));
        }
        

        /// <summary>
        /// Checks that the remote cohort storage database described by this class exists and contains a compatible schema.
        /// </summary>
        /// <param name="notifier"></param>
        public void Check(ICheckNotifier notifier)
        {
            //make sure we can get to server
            CheckCohortDatabaseAccessible( notifier);

            CheckCohortDatabaseHasCorrectTables( notifier);

            if (string.Equals(PrivateIdentifierField, ReleaseIdentifierField))
                notifier.OnCheckPerformed(
                    new CheckEventArgs(
                        "PrivateIdentifierField and ReleaseIdentifierField are the same, this means your cohort will extract IDENTIFIABLE data (no cohort identifier substitution takes place)",
                        CheckResult.Fail));
        }

        #region Stuff for checking the remote (not data export manager) table where the cohort is allegedly stored
        
        /// <inheritdoc/>
        public bool IDExistsInCohortTable(int originID)
        {
            var server = DataAccessPortal.GetInstance().ExpectServer(this, DataAccessContext.DataExport);
            
            using (DbConnection con = server.GetConnection())
            {
                con.Open();

                string sql = @"select count(*) from " + DefinitionTableName + " where id = " + originID;

                var cmdGetDescriptionOfCohortFromConsus = server.GetCommand(sql, con);
                try
                {
                    return int.Parse(cmdGetDescriptionOfCohortFromConsus.ExecuteScalar().ToString()) >= 1;
                }
                catch (Exception e)
                {
                    throw new Exception(
                        "Could not connect to server " + Server + " (Database '" + Database +
                        "') which is the data source of ExternalCohortTable (source) called '" + Name + "' (ID=" + ID +
                        ")", e);
                }
            }

        }
        private void CheckCohortDatabaseHasCorrectTables(ICheckNotifier notifier)
        {
            try
            {
                var database = Discover();

                DiscoveredTable cohortTable = DiscoverCohortTable();
                if (cohortTable.Exists())
                {
                    notifier.OnCheckPerformed(new CheckEventArgs("Found table " + cohortTable + " in database " + Database, CheckResult.Success, null));
                    
                    DiscoverPrivateIdentifier();
                    DiscoverReleaseIdentifier();
                    DiscoverDefinitionTableForeignKey();
                }
                else
                    notifier.OnCheckPerformed(new CheckEventArgs("Could not find table " + TableName + " in database " + Database, CheckResult.Fail, null));

                DiscoveredTable foundCohortDefinitionTable = DiscoverDefinitionTable();

                if (foundCohortDefinitionTable.Exists())
                {
                    notifier.OnCheckPerformed(new CheckEventArgs("Found table " + DefinitionTableName + " in database " + Database, CheckResult.Success, null));

                    var cols = foundCohortDefinitionTable.DiscoverColumns();
                    
                    foreach (string requiredField in ExternalCohortTable.CohortDefinitionTable_RequiredFields)
                        ComplainIfColumnMissing(DefinitionTableName, cols, requiredField, notifier);
                }
                else
                    notifier.OnCheckPerformed(new CheckEventArgs("Could not find table " + DefinitionTableName + " in database " + Database, CheckResult.Fail, null));
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Could not check table intactness for ExternalCohortTable '" + Name + "'", CheckResult.Fail, e));
            }
        }
        
        private void CheckCohortDatabaseAccessible(ICheckNotifier notifier)
        {
            try
            {
                DataAccessPortal.GetInstance().ExpectServer(this, DataAccessContext.DataExport).TestConnection();
              
                notifier.OnCheckPerformed(new CheckEventArgs("Connected to Cohort database '" + Name + "'", CheckResult.Success, null));
            }
            catch (Exception e)
            {
                notifier.OnCheckPerformed(new CheckEventArgs("Could not connect to Cohort database called '" + Name + "'", CheckResult.Fail, e));
            }
        }
        
        /// <inheritdoc/>
        public void PushToServer(ICohortDefinition newCohortDefinition,IManagedConnection connection)
        {
            newCohortDefinition.ID = DiscoverDefinitionTable().Insert(new Dictionary<string, object>()
            {
                {"projectNumber",newCohortDefinition.ProjectNumber},
                {"version",newCohortDefinition.Version},
                {"description",newCohortDefinition.Description},
            },connection.ManagedTransaction);
        }
        #endregion

        private void ComplainIfColumnMissing(string tableNameFullyQualified, DiscoveredColumn[] columns, string colToFindCanBeFullyQualifiedIfYouLike, ICheckNotifier notifier)
        {
            string tofind = GetQuerySyntaxHelper().GetRuntimeName(colToFindCanBeFullyQualifiedIfYouLike);

            if (columns.Any(col=>col.GetRuntimeName().Equals(tofind,StringComparison.CurrentCultureIgnoreCase)))
                notifier.OnCheckPerformed(new CheckEventArgs("Found required field " + tofind + " in table " + tableNameFullyQualified,
                    CheckResult.Success, null));
            else
                notifier.OnCheckPerformed(new CheckEventArgs(
                    "Could not find required field " + tofind + " in table " + tableNameFullyQualified +
                    "(It had the following columns:" + columns.Aggregate("", (s, n) => s + n + ",") + ")",
                    CheckResult.Fail, null));
        }
        

        #region IDataAccessCredentials and IDataAccessPoint delegation
        
        /// <inheritdoc/>
        public string Password
        {
            get { return SelfCertifyingDataAccessPoint.Password; }
            set
            {
                if (Equals(SelfCertifyingDataAccessPoint.Password, value))
                    return;

                var old = SelfCertifyingDataAccessPoint.Password;
                SelfCertifyingDataAccessPoint.Password = value;
                OnPropertyChanged(old, value);
            }
        }

        /// <inheritdoc/>
        public string GetDecryptedPassword()
        {
            return SelfCertifyingDataAccessPoint.GetDecryptedPassword();
        }

        /// <inheritdoc/>
        public string Username
        {
            get { return SelfCertifyingDataAccessPoint.Username; }
            set
            {
                if (Equals(SelfCertifyingDataAccessPoint.Username, value))
                    return;

                var old = SelfCertifyingDataAccessPoint.Username;
                SelfCertifyingDataAccessPoint.Username = value;
                OnPropertyChanged(old, value);
            }
        }

        /// <inheritdoc/>
        public string Server
        {
            get { return SelfCertifyingDataAccessPoint.Server; }
            set
            {
                if (Equals(SelfCertifyingDataAccessPoint.Server, value))
                    return;

                var old = SelfCertifyingDataAccessPoint.Server;
                SelfCertifyingDataAccessPoint.Server = value;
                OnPropertyChanged(old, value);
            }
        }

        /// <inheritdoc/>
        public string Database
        {
            get { return SelfCertifyingDataAccessPoint.Database; }
            set
            {
                if (Equals(SelfCertifyingDataAccessPoint.Database, value))
                    return;

                var old = SelfCertifyingDataAccessPoint.Database;
                SelfCertifyingDataAccessPoint.Database = value;
                OnPropertyChanged(old, value);
            }
        }

        /// <inheritdoc/>
        public DatabaseType DatabaseType
        {
            get { return SelfCertifyingDataAccessPoint.DatabaseType; }
            set
            {
                if (Equals(SelfCertifyingDataAccessPoint.DatabaseType, value))
                    return;

                var old = SelfCertifyingDataAccessPoint.DatabaseType;
                SelfCertifyingDataAccessPoint.DatabaseType = value;
                OnPropertyChanged(old, value);
            }
        }

        /// <inheritdoc/>
        public IDataAccessCredentials GetCredentialsIfExists(DataAccessContext context)
        {
            return SelfCertifyingDataAccessPoint.GetCredentialsIfExists(context);
        }
        #endregion


        /// <summary>
        /// Returns SQL query for counting the number of unique patients in each cohort defined in the database
        /// referenced by this <see cref="ExternalCohortTable"/>
        /// </summary>
        /// <returns></returns>
        public string GetCountsDataTableSql()
        {
            
            string sql = @"SELECT 
id as OriginID,
count(*) as Count,
count(distinct {0}) as CountDistinct,
projectNumber as ProjectNumber,
version as Version,
description as Description,
dtCreated as dtCreated
  FROM
   {1}
   join 
   {2} on {3} = id
   group by 
   id,
   projectNumber,
   version,
   description,
   dtCreated";

            return string.Format(sql, ReleaseIdentifierField, TableName, DefinitionTableName, DefinitionTableForeignKeyField);
        }
        
        /// <summary>
        /// Returns SQL query for listing all cohorts stored in the database referenced by this <see cref="ExternalCohortTable"/>.
        /// This includes only the ids, project numbers, version, description etc not the actual patient identifiers themselves.
        /// </summary>
        /// <returns></returns>
        public string GetExternalDataSql()
        {
            string sql = @"SELECT 
id as OriginID,
projectNumber as ProjectNumber,
version as Version,
description as Description,
dtCreated as dtCreated
  FROM
   {0}";

                return string.Format(sql, DefinitionTableName);

        }
        
        /// <summary>
        /// Returns nothing
        /// </summary>
        /// <returns></returns>
        public IHasDependencies[] GetObjectsThisDependsOn()
        {
            return new IHasDependencies[0];
        }

        /// <summary>
        /// Returns all cohorts in the source
        /// </summary>
        /// <returns></returns>
        public IHasDependencies[] GetObjectsDependingOnThis()
        {
            return (IHasDependencies[])Repository.GetAllObjects<ExtractableCohort>().Where(c => c.ExternalCohortTable_ID == ID); 
        }

        /// <summary>
        /// returns true if all the relevant fields are populated (table names, column names etc)
        /// </summary>
        /// <returns></returns>
        public bool IsFullyPopulated()
        {
            return !
                (string.IsNullOrWhiteSpace(TableName) ||
                 string.IsNullOrWhiteSpace(PrivateIdentifierField) ||
                 string.IsNullOrWhiteSpace(ReleaseIdentifierField) ||
                 string.IsNullOrWhiteSpace(DefinitionTableForeignKeyField) ||
                 string.IsNullOrWhiteSpace(DefinitionTableName));
        }

        public bool DiscoverExistence(DataAccessContext context, out string reason)
        {
            return SelfCertifyingDataAccessPoint.DiscoverExistence(context,out reason);
        }
    }
}

