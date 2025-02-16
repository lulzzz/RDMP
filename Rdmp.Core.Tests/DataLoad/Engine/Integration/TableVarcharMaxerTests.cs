// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FAnsi;
using FAnsi.Discovery;
using Moq;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataLoad.Engine.DatabaseManagement.EntityNaming;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Modules.Mutilators;
using ReusableLibraryCode.Checks;
using Tests.Common;
using TypeGuesser;

namespace Rdmp.Core.Tests.DataLoad.Engine.Integration
{
    public class TableVarcharMaxerTests : DatabaseTests
    {
        [TestCase(DatabaseType.MySql,true)]
        [TestCase(DatabaseType.MySql, false)]
        [TestCase(DatabaseType.MicrosoftSQLServer,true)]
        [TestCase(DatabaseType.MicrosoftSQLServer,false)]
        public void TestTableVarcharMaxer(DatabaseType dbType,bool allDataTypes)
        {
            var db = GetCleanedServer(dbType);

            var tbl = db.CreateTable("Fish",new[]
            {
                new DatabaseColumnRequest("Dave",new DatabaseTypeRequest(typeof(string),100)), 
                new DatabaseColumnRequest("Frank",new DatabaseTypeRequest(typeof(int))) 
            });

            TableInfo ti;
            ColumnInfo[] cols;
            Import(tbl, out ti, out cols);

            var maxer = new TableVarcharMaxer();
            maxer.AllDataTypes = allDataTypes;
            maxer.TableRegexPattern = new Regex(".*");
            maxer.DestinationType = db.Server.GetQuerySyntaxHelper().TypeTranslater.GetSQLDBTypeForCSharpType(new DatabaseTypeRequest(typeof(string),int.MaxValue));
            
            maxer.Initialize(db,LoadStage.AdjustRaw);
            maxer.Check(new ThrowImmediatelyCheckNotifier(){ThrowOnWarning = true});

            var job = Mock.Of<IDataLoadJob>(x => 
                x.RegularTablesToLoad==new List<ITableInfo>(){ti} &&
                x.Configuration==new HICDatabaseConfiguration(db.Server,null,null,null));

            maxer.Mutilate(job);

            switch (dbType)
            {
                case DatabaseType.MicrosoftSQLServer:
                    Assert.AreEqual("varchar(max)",tbl.DiscoverColumn("Dave").DataType.SQLType);
                    Assert.AreEqual(allDataTypes ? "varchar(max)" : "int", tbl.DiscoverColumn("Frank").DataType.SQLType);
                    break;
                case DatabaseType.MySql:
                    Assert.AreEqual("text",tbl.DiscoverColumn("Dave").DataType.SQLType);
                    Assert.AreEqual(allDataTypes ? "text" : "int", tbl.DiscoverColumn("Frank").DataType.SQLType);
                    break;
                case DatabaseType.Oracle:
                    Assert.AreEqual("varchar(max)",tbl.DiscoverColumn("Dave").DataType.SQLType);
                Assert.AreEqual(allDataTypes ? "varchar(max)" : "int", tbl.DiscoverColumn("Frank").DataType.SQLType);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("dbType");
            }
        }
    }
}