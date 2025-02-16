// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using FAnsi;
using NUnit.Framework;
using Rdmp.Core.CohortCommitting;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.Repositories;
using ReusableLibraryCode.Checks;
using Tests.Common;

namespace Rdmp.Core.Tests.DataExport.Data
{
    class ExternalCohortTableTests:UnitTests
    {
        /// <summary>
        /// Demonstrates the minimum properties required to create a <see cref="ExternalCohortTable"/>.  See <see cref="CreateNewCohortDatabaseWizard"/>
        /// for how to create one of these based on the datasets currently held in rdmp.
        /// </summary>
        [Test]
        public void Create_ExternalCohortTable_Manually()
        {
            MemoryDataExportRepository repository = new MemoryDataExportRepository();
            var table = new ExternalCohortTable(repository, "My Cohort Database", DatabaseType.MicrosoftSQLServer);
            table.Database = "mydb";
            table.PrivateIdentifierField = "chi";
            table.ReleaseIdentifierField = "release";
            table.DefinitionTableForeignKeyField = "c_id";
            table.TableName = "Cohorts";
            table.DefinitionTableName = "InventoryTable";
            table.Server = "superfastdatabaseserver\\sqlexpress";
            table.SaveToDatabase();

            var ex = Assert.Throws<Exception>(()=>table.Check(new ThrowImmediatelyCheckNotifier()));
            Assert.AreEqual("Could not connect to Cohort database called 'My Cohort Database'",ex.Message);
        }

        /// <summary>
        /// Demonstrates how to get a hydrated instance during unit tests.  This will not map to an actually existing database
        /// </summary>
        [Test]
        public void Create_ExternalCohortTable_InTests()
        {
            var tbl = WhenIHaveA<ExternalCohortTable>();
            
            Assert.IsNotNull(tbl);
            Assert.IsNotNull(tbl.PrivateIdentifierField);
            Assert.IsNotNull(tbl.ReleaseIdentifierField);
        }
    }
}
