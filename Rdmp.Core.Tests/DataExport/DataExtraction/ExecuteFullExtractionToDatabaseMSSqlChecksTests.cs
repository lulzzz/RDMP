// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using FAnsi;
using FAnsi.Discovery;
using Moq;
using NUnit.Framework;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.DataExport.DataExtraction.Pipeline.Destinations;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;
using Tests.Common;
using Tests.Common.Scenarios;

namespace Rdmp.Core.Tests.DataExport.DataExtraction
{
    public class ExecuteFullExtractionToDatabaseMSSqlChecksTests:DatabaseTests
    {
        private IProject _projectStub;
        private IExtractCommand _commandStub;
        
        public DiscoveredDatabase Database { get; set; }

        [SetUp]
        protected override void SetUp()
        {
            base.SetUp();

            _projectStub = Mock.Of<IProject>();
            _projectStub.ProjectNumber = -123;

            var cfg = Mock.Of<IExtractionConfiguration>();
            
            _commandStub = Mock.Of<IExtractCommand>(cmd => cmd.Configuration==cfg);

            Database = GetCleanedServer(DatabaseType.MicrosoftSQLServer);
        }

        


        [Test]
        public void NoServer()
        {
            var destination = new ExecuteFullExtractionToDatabaseMSSql();

            var tomemory = new ToMemoryCheckNotifier();
            destination.Check(tomemory);

            Assert.AreEqual(CheckResult.Fail,tomemory.Messages[0].Result);
            Assert.IsTrue(tomemory.Messages[0].Message.StartsWith("Target database server property has not been set"));
        }
        [Test]
        public void ServerMissingServerName()
        {
            var server = new ExternalDatabaseServer(CatalogueRepository, "Fiction",null);
            try
            {
                var destination = new ExecuteFullExtractionToDatabaseMSSql();
                destination.TargetDatabaseServer = server;

                var tomemory = new ToMemoryCheckNotifier();
                destination.Check(tomemory);

                Assert.AreEqual(CheckResult.Fail, tomemory.Messages[0].Result);
                Assert.IsTrue(tomemory.Messages[0].Message.StartsWith("TargetDatabaseServer does not have a .Server specified"));
            }
            finally
            {
                server.DeleteInDatabase();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ServerDatabaseIsPresentAndCorrect(bool alreadyExists)
        {
            var server = new ExternalDatabaseServer(CatalogueRepository, "Fiction",null);
            server.Server = DiscoveredServerICanCreateRandomDatabasesAndTablesOn.Name;
            //server.Database = "FictionalDatabase"; Ignored by the extractor!
            
            try
            {

                var destination = new ExecuteFullExtractionToDatabaseMSSql();
                destination.PreInitialize(_projectStub, new ThrowImmediatelyDataLoadEventListener());
                destination.PreInitialize(_commandStub, new ThrowImmediatelyDataLoadEventListener());

                destination.TargetDatabaseServer = server;
                destination.TableNamingPattern = "$d";

                if (alreadyExists)
                    destination.DatabaseNamingPattern = Database.GetRuntimeName(); //database that exists
                else
                    destination.DatabaseNamingPattern = "Fictional$nDatabase";  //database does not exist (but server does)

                var tomemory = new ToMemoryCheckNotifier(new ThrowImmediatelyCheckNotifier());
                destination.Check(tomemory);

                Assert.AreEqual(alreadyExists? CheckResult.Warning: CheckResult.Success, tomemory.GetWorst());

            }
            finally
            {
                server.DeleteInDatabase();
            }
        }

        [Test]
        public void ServerDatabaseIsPresentAndCorrectButHasTablesInIt()
        {
            var server = new ExternalDatabaseServer(CatalogueRepository, "Fiction",null);
            server.Server = DiscoveredServerICanCreateRandomDatabasesAndTablesOn.Name;
            //server.Database = "FictionalDatabase"; Ignored by the extractor!
            
            using (var con = Database.Server.GetConnection())
            {
                con.Open();

                Database.Server.GetCommand("CREATE TABLE Bob(name varchar(10))", con).ExecuteNonQuery();
            }
            
            try
            {
                var destination = new ExecuteFullExtractionToDatabaseMSSql();
                destination.PreInitialize(_projectStub, new ThrowImmediatelyDataLoadEventListener());
                destination.PreInitialize(_commandStub, new ThrowImmediatelyDataLoadEventListener());
                destination.TargetDatabaseServer = server;
                destination.TableNamingPattern = "$d";
                destination.DatabaseNamingPattern = "FictionalDatabase";

                var tomemory = new ToMemoryCheckNotifier(new ThrowImmediatelyCheckNotifier());
                destination.Check(tomemory);

                Assert.AreEqual(CheckResult.Warning, tomemory.GetWorst());

                Database.ExpectTable("Bob").Drop();
            }
            finally
            {
                server.DeleteInDatabase();
            }
        }
    }
}