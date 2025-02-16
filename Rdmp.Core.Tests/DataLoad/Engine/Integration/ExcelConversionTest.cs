// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rdmp.Core.Curation;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Core.DataLoad.Engine.Job;
using Rdmp.Core.DataLoad.Modules.DataProvider.FlatFileManipulation;
using ReusableLibraryCode.Progress;

namespace Rdmp.Core.Tests.DataLoad.Engine.Integration
{
    [Category("Unit")]
    public class ExcelConversionTest
    {
        private readonly Stack<DirectoryInfo> _dirsToCleanUp = new Stack<DirectoryInfo>();
        private DirectoryInfo _parentDir;
        
        [OneTimeSetUp]
        protected virtual void OneTimeSetUp()
        {
            var testDir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            _parentDir = testDir.CreateSubdirectory("ExcelConversionTest");
            _dirsToCleanUp.Push(_parentDir);
        }
        
        private LoadDirectory CreateLoadDirectoryForTest(string directoryName)
        {
            var loadDirectory = LoadDirectory.CreateDirectoryStructure(_parentDir, directoryName,true);
            _dirsToCleanUp.Push(loadDirectory.RootPath);
            return loadDirectory;
        }

        [Test]
        public void TestExcelFunctionality_OnSimpleXlsx()
        {
            var LoadDirectory = CreateLoadDirectoryForTest("TestExcelFunctionality_OnSimpleXlsx");

            //clean SetUp anything in the test project folders forloading directory
            foreach (FileInfo fileInfo in LoadDirectory.ForLoading.GetFiles())
                fileInfo.Delete();

            string targetFile = Path.Combine(LoadDirectory.ForLoading.FullName, "Test.xlsx");
            File.WriteAllBytes(targetFile, Resources.Resource1.TestExcelFile1);

            TestConversionFor(targetFile, "*.xlsx", 5, LoadDirectory);
        }

        [Test]
        public void TestExcelFunctionality_DodgyFileExtension()
        {
            var LoadDirectory = CreateLoadDirectoryForTest("TestExcelFunctionality_DodgyFileExtension");

            //clean SetUp anything in the test project folders forloading directory
            foreach (FileInfo fileInfo in LoadDirectory.ForLoading.GetFiles())
                fileInfo.Delete();

            string targetFile = Path.Combine(LoadDirectory.ForLoading.FullName, "Test.xml");
            File.WriteAllText(targetFile, Resources.Resource1.TestExcelFile2);

            var ex = Assert.Throws<Exception>(()=>TestConversionFor(targetFile, "*.fish", 1, LoadDirectory));

            Assert.IsTrue(ex.Message.StartsWith("Did not find any files matching Pattern '*.fish' in directory"));
        }
        
        private void TestConversionFor(string targetFile, string fileExtensionToConvert, int expectedNumberOfSheets, LoadDirectory directory)
        {
            FileInfo f = new FileInfo(targetFile);

            try
            {
                Assert.IsTrue(f.Exists);
                Assert.IsTrue(f.Length > 100);

                ExcelToCSVFilesConverter converter = new ExcelToCSVFilesConverter();

                var job = new ThrowImmediatelyDataLoadJob(new ThrowImmediatelyDataLoadEventListener(){ThrowOnWarning =  true, WriteToConsole =  true});
                job.LoadDirectory = directory;

                converter.ExcelFilePattern = fileExtensionToConvert;
                converter.Fetch(job, new GracefulCancellationToken());

                FileInfo[] filesCreated = directory.ForLoading.GetFiles("*.csv");

                Assert.AreEqual(expectedNumberOfSheets,filesCreated.Length);

                foreach (FileInfo fileCreated in filesCreated)
                {
                    Assert.IsTrue(Regex.IsMatch(fileCreated.Name, "Sheet[0-9].csv"));
                    Assert.GreaterOrEqual(fileCreated.Length, 100);
                    fileCreated.Delete();
                }
            }
            finally
            {
                f.Delete();
            }
        }
    }
}
