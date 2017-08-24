﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.SqlServer.Management.Smo;
using ReusableLibraryCode.DatabaseHelpers;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableLibraryCode.Exceptions;

namespace ReusableLibraryCode.DataAccess
{
    public class DataAccessPortal
    {
        private static readonly object oLockInstance = new object();
        private static DataAccessPortal _instance;

        public static DataAccessPortal GetInstance()
        {
            lock (oLockInstance)
            {
                if (_instance == null)
                    _instance = new DataAccessPortal();
            }
            return _instance;
        }

        private DataAccessPortal()
        {
            
        }

        public DiscoveredServer ExpectServer(IDataAccessPoint dataAccessPoint, DataAccessContext context, bool setInitialDatabase=true)
        {
            var builder = GetConnectionStringBuilder(dataAccessPoint, context,setInitialDatabase);
            return new DiscoveredServer(builder);
        }
        public DiscoveredDatabase ExpectDatabase(IDataAccessPoint dataAccessPoint, DataAccessContext context)
        {
            return ExpectServer(dataAccessPoint, context).ExpectDatabase(SqlSyntaxHelper.GetRuntimeName(dataAccessPoint.Database));
        }
        public DiscoveredServer ExpectDistinctServer(IDataAccessPoint[] collection, DataAccessContext context, bool setInitialDatabase)
        {
            var builder = GetDistinctConnectionStringBuilder(collection, context, setInitialDatabase);
            return new DiscoveredServer(builder);
        }

        private DbConnectionStringBuilder GetConnectionStringBuilder(IDataAccessPoint dataAccessPoint, DataAccessContext context, bool setInitialDatabase=true)
        {
            IDataAccessCredentials credentials = dataAccessPoint.GetCredentialsIfExists(context);

            return new DatabaseHelperFactory(dataAccessPoint.DatabaseType).CreateInstance().GetConnectionStringBuilder(
                dataAccessPoint.Server,
                setInitialDatabase?SqlSyntaxHelper.GetRuntimeName(dataAccessPoint.Database):"",
                credentials != null?credentials.Username:null,
                credentials != null ? credentials.GetDecryptedPassword() : null);
        }

        private DbConnectionStringBuilder GetDistinctConnectionStringBuilder(IDataAccessPoint[] collection, DataAccessContext context, bool setInitialDatabase)
        {
            ///////////////////////Exception handling///////////////////////////////////////////////
            if(!collection.Any())
                throw new Exception("No IDataAccessPoints were passed, so no connection string builder can be created");

            IDataAccessPoint first = collection.First();

            //There can be only one - server
            foreach (IDataAccessPoint accessPoint in collection)
            {
                if (!first.Server.Equals(accessPoint.Server))
                    throw new ExpectedIdenticalStringsException("There was a mismatch in server names for data access points " + first + " and " + accessPoint + " server names must match exactly", first.Server, accessPoint.Server);
                
                if(setInitialDatabase)
                {
                    if(string.IsNullOrWhiteSpace(first.Database))
                        throw new Exception("DataAccessPoint '" + first +"' does not have a Database specified on it");

                    var firstDbName = SqlSyntaxHelper.GetRuntimeName(first.Database);
                    var currentDbName = SqlSyntaxHelper.GetRuntimeName(accessPoint.Database);

                    if (!firstDbName.Equals(currentDbName))
                        throw new ExpectedIdenticalStringsException("All data access points must be into the same database, access points '" + first + "' and '" + accessPoint + "' are into different databases", firstDbName, currentDbName);    
                }
            }
            
            //There can be only one - credentials (but there might not be any)
            var credentials = collection.Select(t => t.GetCredentialsIfExists(context)).ToArray();

            //if there are credentials
            if(credentials.Any(c => c != null)) 
                if (credentials.Any(c=>c == null))//all objects in collection must have a credentials if any of them do
                    throw new Exception("IDataAccessPoint collection could not agree whether to use Credentials or not "+Environment.NewLine
                        +"Objects wanting to use Credentials" + string.Join(",",collection.Where(c=>c.GetCredentialsIfExists(context)!=null).Select(s=>s.ToString())) + Environment.NewLine
                        + "Objects not wanting to use Credentials" + string.Join(",", collection.Where(c => c.GetCredentialsIfExists(context) == null).Select(s => s.ToString())) + Environment.NewLine
                        );
                else
                //There can be only one - Username
                if(credentials.Select(c=>c.Username).Distinct().Count() != 1)
                    throw new Exception("IDataAccessPoint collection could not agree on a single Username to use to access the data under context " + context + " (Servers were " + string.Join("," + Environment.NewLine, collection.Select(c => c + " = " + c.Database + " - " + c.DatabaseType)) + ")");
                else
                //There can be only one - Password
                if(credentials.Select(c=>c.GetDecryptedPassword()).Distinct().Count() != 1)
                    throw new Exception("IDataAccessPoint collection could not agree on a single Password to use to access the data under context " + context + " (Servers were " + string.Join("," + Environment.NewLine, collection.Select(c => c + " = " + c.Database + " - " + c.DatabaseType)) + ")");
                
                
                
            ///////////////////////////////////////////////////////////////////////////////

            //the bit that actually matters:
            return GetConnectionStringBuilder(first, context, setInitialDatabase);
            
        }

    }
}
