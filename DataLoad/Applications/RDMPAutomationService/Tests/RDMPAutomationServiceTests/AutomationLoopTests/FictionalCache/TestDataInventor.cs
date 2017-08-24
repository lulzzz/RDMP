﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CachingEngine.PipelineExecution.Sources;
using CachingEngine.Requests;
using CatalogueLibrary.Data.Cache;
using CatalogueLibrary.DataFlowPipeline;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;

namespace RDMPAutomationServiceTests.AutomationLoopTests.FictionalCache
{
    public class TestDataInventor : CacheSource<TestDataWritterChunk>
    {
        Random r = new Random();

        public override void DoGetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            // don't load anything for today onwards
            var today = DateTime.Now.Subtract(DateTime.Now.TimeOfDay);
            if (Request.Start > today)
            {
                Chunk = null;
                return;
            }

            DateTime currentDay = Request.Start;
            
            List<FileInfo> toReturn = new List<FileInfo>();

            while(currentDay <= Request.End)
            {
                toReturn.Add(GetFileForDay(currentDay));
                currentDay = currentDay.AddDays(1);
            }

            Chunk = new TestDataWritterChunk(Request,toReturn.ToArray());
        }

        private FileInfo GetFileForDay(DateTime currentDay)
        {
            string filename = currentDay.ToString("yyyyMMdd") + ".csv";

            string contents = "MyRand,DateOfRandom" + Environment.NewLine;
            for (int i = 0; i < 100; i++)
                contents += r.Next(10000) + "," + currentDay.ToString("yyyy-MM-dd") + Environment.NewLine;

            File.WriteAllText(filename, contents);
            return new FileInfo(filename);
        }

        public override void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny)
        {
            
        }

        public override void Abort(IDataLoadEventListener listener)
        {
            
        }

        public override TestDataWritterChunk TryGetPreview()
        {
            var dt = DateTime.Now.AddYears(-200);

            return new TestDataWritterChunk(new CacheFetchRequest(null, dt){ChunkPeriod = new TimeSpan(1,0,0)}, new []{GetFileForDay(dt)});
        }

        public override void Check(ICheckNotifier notifier)
        {
        }
    }
}