// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Data.Common;
using System.Text.RegularExpressions;
using FAnsi.Discovery;
using FAnsi.Extensions;
using FAnsi.Naming;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.QueryCaching.Aggregation.Arguments;
using ReusableLibraryCode;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Core.QueryCaching.Aggregation
{
    /// <summary>
    /// Handles the caching and versioning of AggregateConfigurations in a QueryCaching database (QueryCaching.Database.csproj).  Query caching is the process
    /// of storing the SQL query and resulting DataTable from running an Aggregate Configuration SQL query (Usually built by an AggregateBuilder).  
    /// 
    /// <para>Caching is vital for large CohortIdentificationConfigurations which feature many complicated subqueries with WHERE conditions and even Patient Index 
    /// Tables (See JoinableCohortAggregateConfiguration).  The only way some of these queries can finish in a sensible time frame (i.e. minutes instead of days) 
    /// is to execute each subquery (AggregateConfiguration) and cache the resulting identifier lists with primary key indexes.  The 
    /// CohortIdentificationConfiguration can then be built into a query that uses the cached results (See CohortQueryBuilder).</para>
    /// 
    /// <para>In order to ensure the cache is never stale the exact SQL query is stored in a table (CachedAggregateConfigurationResults) so that if the user changes
    /// the AggregateConfiguration the cached DataTable is discarded (until the user executes and caches the new version).</para>
    /// 
    /// <para> CachedAggregateConfigurationResultsManager can cache any CacheCommitArguments (includes not just patient identifier lists but also aggregate graphs and
    /// patient index tables).</para>
    /// </summary>
    public class CachedAggregateConfigurationResultsManager
    {
        private readonly DiscoveredServer _server;
        private DiscoveredDatabase _database;

        public CachedAggregateConfigurationResultsManager(IExternalDatabaseServer server)
        {
            _server = DataAccessPortal.GetInstance().ExpectServer(server, DataAccessContext.InternalDataProcessing);
            _database = DataAccessPortal.GetInstance().ExpectDatabase(server, DataAccessContext.InternalDataProcessing);
        }

        public const string CachingPrefix = "/*Cached:";

        public IHasFullyQualifiedNameToo GetLatestResultsTableUnsafe(AggregateConfiguration configuration,AggregateOperation operation)
        {
            using (var con = _server.GetConnection())
            {
                con.Open();

                var r = DatabaseCommandHelper.GetCommand("Select TableName from CachedAggregateConfigurationResults WHERE AggregateConfiguration_ID = " + configuration.ID + " AND Operation = '" +operation + "'" , con).ExecuteReader();
                
                if (r.Read())
                {
                    string tableName =  r["TableName"].ToString();
                    return _database.ExpectTable(tableName);
                }
            }

            return null;
        }

        public IHasFullyQualifiedNameToo GetLatestResultsTable(AggregateConfiguration configuration, AggregateOperation operation, string currentSql)
        {
            using (var con = _server.GetConnection())
            {
                con.Open();

                var cmd = DatabaseCommandHelper.GetCommand("Select TableName,SqlExecuted from CachedAggregateConfigurationResults WHERE AggregateConfiguration_ID = " + configuration.ID + " AND Operation = '" + operation + "'", con);
                var r = cmd.ExecuteReader();

                if (r.Read())
                {
                    if (IsMatchOnSqlExecuted(r, currentSql))
                    {
                        string tableName = r["TableName"].ToString();
                        return _database.ExpectTable(tableName);
                    }
                    else
                        return null; //this means that there was outdated SQL, we could show this to user at some point
                }
            }

            return null;
        }

        private bool IsMatchOnSqlExecuted(DbDataReader r, string currentSql)
        {
            //replace all whitespace with single whitespace 
            string standardisedDatabaseSql = Regex.Replace(r["SqlExecuted"].ToString(), @"\s+", " ");
            string standardisedUsersSql = Regex.Replace(currentSql,@"\s+"," ");

            return standardisedDatabaseSql.ToLower().Trim().Equals(standardisedUsersSql.ToLower().Trim());
        }

        public void CommitResults(CacheCommitArguments arguments)
        {
            var configuration = arguments.Configuration;
            var operation = arguments.Operation;

            DeleteCacheEntryIfAny(configuration, operation);

            //Do not change Types of source columns unless there is an explicit override
            arguments.Results.SetDoNotReType(true);

            using (var con = _server.GetConnection())
            {
                con.Open();
                
                string nameWeWillGiveTableInCache = operation + "_AggregateConfiguration" + configuration.ID;

                //either it has no name or it already has name we want so its ok
                arguments.Results.TableName = nameWeWillGiveTableInCache;

                //add explicit types
                var tbl = _database.ExpectTable(nameWeWillGiveTableInCache);
                if(tbl.Exists())
                    tbl.Drop();

                tbl = _database.CreateTable(nameWeWillGiveTableInCache, arguments.Results, arguments.ExplicitColumns);

                if (!tbl.Exists())
                    throw new Exception("Cache table did not exist even after CreateTable completed without error!");

                var cmdCreateNew =
                    DatabaseCommandHelper.GetCommand(
                        "INSERT INTO CachedAggregateConfigurationResults (Committer,AggregateConfiguration_ID,SqlExecuted,Operation,TableName) Values (@Committer,@AggregateConfiguration_ID,@SqlExecuted,@Operation,@TableName)",con);

                cmdCreateNew.Parameters.Add(DatabaseCommandHelper.GetParameter("@Committer", cmdCreateNew));
                cmdCreateNew.Parameters["@Committer"].Value = Environment.UserName;

                cmdCreateNew.Parameters.Add(DatabaseCommandHelper.GetParameter("@AggregateConfiguration_ID", cmdCreateNew));
                cmdCreateNew.Parameters["@AggregateConfiguration_ID"].Value = configuration.ID;

                cmdCreateNew.Parameters.Add(DatabaseCommandHelper.GetParameter("@SqlExecuted", cmdCreateNew));
                cmdCreateNew.Parameters["@SqlExecuted"].Value = arguments.SQL.Trim();

                cmdCreateNew.Parameters.Add(DatabaseCommandHelper.GetParameter("@Operation", cmdCreateNew));
                cmdCreateNew.Parameters["@Operation"].Value = operation.ToString();

                cmdCreateNew.Parameters.Add(DatabaseCommandHelper.GetParameter("@TableName", cmdCreateNew));
                cmdCreateNew.Parameters["@TableName"].Value = tbl.GetRuntimeName();

                cmdCreateNew.ExecuteNonQuery();

                arguments.CommitTableDataCompleted(tbl);
            }
        }

        public void DeleteCacheEntryIfAny(AggregateConfiguration configuration, AggregateOperation operation)
        {
            var table = GetLatestResultsTableUnsafe(configuration, operation);

            if(table != null)

            using (var con = _server.GetConnection())
            {
                con.Open();

                //drop the data
                _database.ExpectTable(table.GetRuntimeName()).Drop();
                
                //delete the record!
                int deletedRows = DatabaseCommandHelper.GetCommand("DELETE FROM CachedAggregateConfigurationResults WHERE AggregateConfiguration_ID = "+configuration.ID+" AND Operation = '"+operation+"'", con).ExecuteNonQuery();

                if(deletedRows != 1)
                    throw new Exception("Expected exactly 1 record in CachedAggregateConfigurationResults to be deleted when erasing its record of operation " + operation + " but there were " + deletedRows +" affected records");
            }
        }
    }
}
