// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Linq;
using FAnsi.Discovery;
using Oracle.ManagedDataAccess.Client;
using ReusableLibraryCode.Exceptions;

namespace Rdmp.Core.DataLoad.Triggers.Implementations
{
    internal class OracleTriggerImplementer:MySqlTriggerImplementer
    {
        public OracleTriggerImplementer(DiscoveredTable table, bool createDataLoadRunIDAlso = true) : base(table, createDataLoadRunIDAlso)
        {
        }

        protected override string GetTriggerBody()
        {
            using (var con = _server.GetConnection())
            {
                con.Open();

                var cmd = _server.GetCommand(string.Format("select trigger_body from all_triggers where trigger_name = UPPER('{0}')", GetTriggerName()), con);
                ((OracleCommand)cmd).InitialLONGFetchSize = -1;
                
                var r = cmd.ExecuteReader();

                while (r.Read())
                {
                    return (string) r["trigger_body"];
                }
            }

            return null;
        }
        protected override string CreateTriggerBody()
        {
            return string.Format(@"BEGIN
    INSERT INTO {0} ({1},hic_validTo,hic_userID,hic_status) VALUES ({2},CURRENT_DATE,USER,'U');
  END", _archiveTable.GetFullyQualifiedName(),
                string.Join(",", _columns.Select(c => c.GetRuntimeName())),
                string.Join(",", _columns.Select(c => ":old." + c.GetRuntimeName())));
        }

        protected override void AssertTriggerBodiesAreEqual(string sqlThen, string sqlNow)
        {
            sqlNow = sqlNow??"";
            sqlThen = sqlThen??"";

            if(!sqlNow.Trim(';',' ','\t').Equals(sqlThen.Trim(';',' ','\t')))
                throw new ExpectedIdenticalStringsException("Sql body for trigger doesn't match expcted sql",sqlThen,sqlNow);
        }
    }
}
