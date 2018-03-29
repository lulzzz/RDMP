using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using CachingEngine.Requests;
using CachingEngine.Requests.FetchRequestProvider;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Cache;
using CatalogueLibrary.DataFlowPipeline;
using CatalogueLibrary.DataFlowPipeline.Requirements;
using CatalogueLibrary.Repositories;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Progress;

namespace CachingEngine.PipelineExecution.Sources
{
    /// <summary>
    /// Abstract base class for a pipeline component which makes time based fetch requests to produce time specific packets of data which will be packaged into files
    /// and stored further down the caching pipeline.  Use the Request property to determine which dates/times you are supposed to handle within DoGetChunk.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [InheritedExport(typeof(IDataFlowSource<ICacheChunk>))]
    public abstract class CacheSource<T> : ICacheSource, IPluginDataFlowSource<T>,IPipelineRequirement<ICatalogueRepository> where T : class,ICacheChunk
    {
        public ICacheFetchRequestProvider RequestProvider { get; set; }
        public IPermissionWindow PermissionWindow { get; set; }

        protected T Chunk;
        protected ICacheFetchRequest Request;

        protected ICatalogueRepository CatalogueRepository;
        protected MEF MEF;

        /// <summary>
        /// Enforces behaviour required for logging unsuccessful cache requests and providing implementation-independent checks, so that the plugin author doesn't need to remember to call Request[Succeeded|Failed] or do general checks.
        /// Plugin author provides implementation-specific caching in the 'DoGetChunk' function.
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual T GetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken)
        {
            Request = RequestProvider.GetNext(listener);

            // If GetNext returns null, there are no further failures to process and we're done
            if (Request == null)
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "The RequestProvider has no more requests to provide (RequestProvider.GetNext returned null)"));
                return null;
            }

            if (Request.CacheProgress == null)
                throw new InvalidOperationException("The request has no CacheProgress item (in this case to determine when the lag period begins)");

            // Check if we will stray into the lag period with this fetch and if so signal we are finished
            var cacheLagPeriod = Request.CacheProgress.GetCacheLagPeriod();
            if (cacheLagPeriod != null && cacheLagPeriod.TimeIsWithinPeriod(Request.End))
            {
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Information, "The Request is for a time within the Cache Lag Period. This means we are up-to-date and can stop now."));
                return null;
            }

            DoGetChunk(listener, cancellationToken);

            if (Chunk != null && Chunk.Request == null && Request != null)
                listener.OnNotify(this,
                    new NotifyEventArgs(ProgressEventType.Error,
                        "DoGetChunk completed and set a Chunk Successfully but the Chunk.Request was null.  Try respecting the Request property in your class when creating your Chunk."));

            return Chunk;
        }

        public abstract void DoGetChunk(IDataLoadEventListener listener, GracefulCancellationToken cancellationToken);

        public void PreInitialize(ICacheFetchRequestProvider value, IDataLoadEventListener listener)
        {
            RequestProvider = value;
        }

        public void PreInitialize(IPermissionWindow value, IDataLoadEventListener listener)
        {
            PermissionWindow = value;
        }
        
        public abstract void Dispose(IDataLoadEventListener listener, Exception pipelineFailureExceptionIfAny);
        public abstract void Abort(IDataLoadEventListener listener);
        public abstract T TryGetPreview();
        public abstract void Check(ICheckNotifier notifier);
        public void PreInitialize(ICatalogueRepository value, IDataLoadEventListener listener)
        {
            CatalogueRepository = value;
            MEF = value.MEF;
        }

        
    }
}