// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.Cache;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.Core.Curation.Data.Cohort.Joinables;
using Rdmp.Core.Curation.Data.Dashboarding;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.Governance;
using Rdmp.Core.Curation.Data.ImportExport;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.Curation.Data.Remoting;
using Rdmp.Core.Providers.Nodes;
using Rdmp.Core.Providers.Nodes.CohortNodes;
using Rdmp.Core.Providers.Nodes.LoadMetadataNodes;
using Rdmp.Core.Providers.Nodes.PipelineNodes;
using Rdmp.Core.Providers.Nodes.SharingNodes;
using Rdmp.Core.Repositories;
using Rdmp.Core.Repositories.Managers;
using Rdmp.Core.Repositories.Managers.HighPerformance;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Comments;

namespace Rdmp.Core.Providers
{
    /// <summary>
    /// Performance optimisation class and general super class in charge of recording and discovering all objects in the Catalogue database so they can be displayed in 
    /// RDMPCollectionUIs etc.  This includes issuing a single database query per Type fetching all objects (e.g. AllProcessTasks, AllLoadMetadatas etc) and then in evaluating
    /// and documenting the hierarchy in _childDictionary.  Every object that is not a root level object also has a DescendancyList which records the path of parents to that
    /// exact object.  Therefore you can easily identify 1. what the immediate children of any object are, 2. what the full path to any given object is.
    /// 
    /// <para>The pattern is:
    /// 1. Identify a root level object 
    /// 2. Create a method overload AddChildren that takes the object
    /// 3. Create a new HashSet containing all the child objects (regardless of mixed Type)
    /// 4. Call AddToDictionaries with a new DescendancyList containing the parent object
    /// 5. For each of the objects added that has children of it's own repeat the above (Except call DescendancyList.Add instead of creating a new one)</para>
    ///  
    /// </summary>
    public class CatalogueChildProvider :ICoreChildProvider
    {
        //Load System
        public LoadMetadata[] AllLoadMetadatas { get; set; }
        public ProcessTask[] AllProcessTasks { get; set; }
        public LoadProgress[] AllLoadProgresses { get; set; }
        public CacheProgress[] AllCacheProgresses { get; set; }
        public PermissionWindow[] AllPermissionWindows { get; set; }
        
        //Catalogue side of things
        public Catalogue[] AllCatalogues { get; set; }
        public Dictionary<int, Catalogue> AllCataloguesDictionary { get; private set; }
        
        public SupportingDocument[] AllSupportingDocuments { get; set; }
        public SupportingSQLTable[] AllSupportingSQL { get; set; }

        //tells you the imediate children of a given node.  Do not add to this directly instead add using AddToDictionaries unless you want the Key to be an 'on the sly' no known descendency child
        private ConcurrentDictionary<object, HashSet<object>> _childDictionary = new ConcurrentDictionary<object, HashSet<object>>();
        
        //This is the reverse of _childDictionary in some ways.  _childDictionary tells you the immediate children while
        //this tells you for a given child object what the navigation tree down to get to it is e.g. ascendancy[child] would return [root,grandParent,parent]
        private Dictionary<object, DescendancyList> _descendancyDictionary = new Dictionary<object, DescendancyList>();


        public IEnumerable<CatalogueItem> AllCatalogueItems { get { return AllCatalogueItemsDictionary.Values; } }
        
        private From1ToM<Catalogue,CatalogueItem> _catalogueToCatalogueItems;
        public Dictionary<int,CatalogueItem> AllCatalogueItemsDictionary { get; private set; }

        private readonly Dictionary<int,ColumnInfo> _allColumnInfos;
        
        public AggregateConfiguration[] AllAggregateConfigurations { get; private set; }
        public AggregateDimension[] AllAggregateDimensions { get; private set; }
        
        public AllRDMPRemotesNode AllRDMPRemotesNode { get; private set; }
        public RemoteRDMP[] AllRemoteRDMPs { get; set; }

        public AllDashboardsNode AllDashboardsNode { get;set;}
        public DashboardLayout[] AllDashboards { get;set;}

        public AllObjectSharingNode AllObjectSharingNode { get; private set; }
        public ObjectImport[] AllImports { get; set; }
        public ObjectExport[] AllExports { get; set; }

        public AllStandardRegexesNode AllStandardRegexesNode { get; private set; }
        public AllPipelinesNode AllPipelinesNode { get; private set; }
        public OtherPipelinesNode OtherPipelinesNode { get; private set; }
        public Pipeline[] AllPipelines { get; set; }
        public PipelineComponent[] AllPipelineComponents { get; set; }

        public StandardRegex[] AllStandardRegexes { get; set; }

        //TableInfo side of things
        public AllANOTablesNode AllANOTablesNode { get; private set; }
        public ANOTable[] AllANOTables { get; set; }

        public ExternalDatabaseServer[] AllExternalServers { get; private set; }
        public TableInfoServerNode[] AllServers { get;private set; }
        public TableInfo[] AllTableInfos { get; private set; }

        public AllDataAccessCredentialsNode AllDataAccessCredentialsNode { get; set; }

        public AllExternalServersNode AllExternalServersNode { get; private set; }
        public AllServersNode AllServersNode { get; private set; }

        public DataAccessCredentials[] AllDataAccessCredentials { get; set; }
        public Dictionary<TableInfo, List<DataAccessCredentialUsageNode>> AllDataAccessCredentialUsages { get; set; }

        private From1ToM<TableInfo, ColumnInfo> _tableInfosToColumnInfos;
        public ColumnInfo[] AllColumnInfos { get; private set; }
        public PreLoadDiscardedColumn[] AllPreLoadDiscardedColumns { get; private set; }

        public Lookup[] AllLookups { get; set; }

        public JoinInfo[] AllJoinInfos { get; set; }

        public AnyTableSqlParameter[] AllAnyTableParameters;

        //Filter / extraction side of things
        public IEnumerable<ExtractionInformation> AllExtractionInformations { get
        {
            return AllExtractionInformationsDictionary.Values;
        } }

        public AllPermissionWindowsNode AllPermissionWindowsNode { get; set; }
        public AllLoadMetadatasNode AllLoadMetadatasNode { get; set; }

        public AllConnectionStringKeywordsNode AllConnectionStringKeywordsNode { get; set; }
        public ConnectionStringKeyword[] AllConnectionStringKeywords { get; set; }

        protected Dictionary<int,ExtractionInformation> AllExtractionInformationsDictionary;
        protected Dictionary<int, ExtractionInformation> _extractionInformationsByCatalogueItem;

        private readonly IFilterManager _aggregateFilterManager;

        //Filters for Aggregates (includes filter containers (AND/OR)
        public Dictionary<int, AggregateFilterContainer> AllAggregateContainersDictionary { get; private set; }
        public AggregateFilterContainer[] AllAggregateContainers { get { return AllAggregateContainersDictionary.Values.ToArray();}}

        public AggregateFilter[] AllAggregateFilters { get; private set; }
        private AggregateFilterParameter[] AllAggregateFilterParameters;

        //Catalogue master filters (does not include any support for filter containers (AND/OR)
        private ExtractionFilter[] AllCatalogueFilters;
        public ExtractionFilterParameter[] AllCatalogueParameters;
        public ExtractionFilterParameterSet[] AllCatalogueValueSets;
        public ExtractionFilterParameterSetValue[] AllCatalogueValueSetValues;
        
        private readonly ICohortContainerManager _cohortContainerManager;
        
        public CohortIdentificationConfiguration[] AllCohortIdentificationConfigurations { get; private set; }
        public CohortAggregateContainer[] AllCohortAggregateContainers { get; set; }
        public JoinableCohortAggregateConfiguration[] AllJoinables { get; set; }
        public JoinableCohortAggregateConfigurationUse[] AllJoinUses { get; set; }

        public ConcurrentDictionary<object,HashSet<IMasqueradeAs>> AllMasqueraders { get; private set; }

        public readonly IChildProvider[] PluginChildProviders;
        private readonly ICatalogueRepository _catalogueRepository;
        private readonly ICheckNotifier _errorsCheckNotifier;
        private readonly List<IChildProvider> _blacklistedPlugins = new List<IChildProvider>();

        public AllGovernanceNode AllGovernanceNode { get; private set; }
        public GovernancePeriod[] AllGovernancePeriods { get; private set; }
        public GovernanceDocument[] AllGovernanceDocuments { get; private set; }
        public Dictionary<int, HashSet<int>> GovernanceCoverage { get; private set; }
        
        private CommentStore _commentStore;

        public JoinableCohortAggregateConfigurationUse[] AllJoinableCohortAggregateConfigurationUse { get; private set; }
        public AllPluginsNode AllPluginsNode { get; private set;}
        public Curation.Data.Plugin[] AllPlugins { get; }
        public Curation.Data.Plugin[] AllCompatiblePlugins { get; }

        public HashSet<StandardPipelineUseCaseNode> PipelineUseCases {get;set; } = new HashSet<StandardPipelineUseCaseNode>();
            
        public AllOrphanAggregateConfigurationsNode OrphanAggregateConfigurationsNode { get;set; } = new AllOrphanAggregateConfigurationsNode();

        public HashSet<AggregateConfiguration> OrphanAggregateConfigurations;

        public CatalogueChildProvider(ICatalogueRepository repository, IChildProvider[] pluginChildProviders, ICheckNotifier errorsCheckNotifier)
        {
            _commentStore = repository.CommentStore;
            _catalogueRepository = repository;
            _errorsCheckNotifier = errorsCheckNotifier;

            // all the objects which are 
            AllMasqueraders = new ConcurrentDictionary<object, HashSet<IMasqueradeAs>>();
            
            PluginChildProviders = pluginChildProviders;
            
            AllAnyTableParameters = GetAllObjects<AnyTableSqlParameter>(repository);

            AllANOTables = GetAllObjects<ANOTable>(repository);
            AllANOTablesNode = new AllANOTablesNode();
            AddChildren(AllANOTablesNode);

            AllCatalogues = GetAllObjects<Catalogue>(repository);
            AllCataloguesDictionary = AllCatalogues.ToDictionary(i => i.ID, o => o);

            AllLoadMetadatas = GetAllObjects<LoadMetadata>(repository);
            AllProcessTasks = GetAllObjects<ProcessTask>(repository);
            AllLoadProgresses = GetAllObjects<LoadProgress>(repository);
            AllCacheProgresses = GetAllObjects<CacheProgress>(repository);
            
            AllPermissionWindows = GetAllObjects<PermissionWindow>(repository);
            AllPermissionWindowsNode = new AllPermissionWindowsNode();
            AddChildren(AllPermissionWindowsNode);

            AllRemoteRDMPs = GetAllObjects<RemoteRDMP>(repository);

            AllExternalServers = GetAllObjects<ExternalDatabaseServer>(repository);

            AllTableInfos = GetAllObjects<TableInfo>(repository);
            AllDataAccessCredentials = GetAllObjects<DataAccessCredentials>(repository);
            AllDataAccessCredentialsNode = new AllDataAccessCredentialsNode();
            AddChildren(AllDataAccessCredentialsNode);

            AllConnectionStringKeywordsNode = new AllConnectionStringKeywordsNode();
            AllConnectionStringKeywords = GetAllObjects<ConnectionStringKeyword>(repository).ToArray();
            AddToDictionaries(new HashSet<object>(AllConnectionStringKeywords), new DescendancyList(AllConnectionStringKeywordsNode));
            
            Task.WaitAll(
                //which TableInfos use which Credentials under which DataAccessContexts
                Task.Factory.StartNew(() => { AllDataAccessCredentialUsages = repository.TableInfoCredentialsManager.GetAllCredentialUsagesBy(AllDataAccessCredentials,AllTableInfos);}),
                Task.Factory.StartNew(() => { AllColumnInfos = GetAllObjects<ColumnInfo>(repository); })
                );
            
            _tableInfosToColumnInfos = new From1ToM<TableInfo, ColumnInfo>(c=>c.TableInfo_ID,AllColumnInfos);
            AllPreLoadDiscardedColumns = GetAllObjects<PreLoadDiscardedColumn>(repository);

            AllSupportingDocuments = GetAllObjects<SupportingDocument>(repository);
            AllSupportingSQL = GetAllObjects<SupportingSQLTable>(repository);

            AllCohortIdentificationConfigurations = GetAllObjects<CohortIdentificationConfiguration>(repository);
            AllJoinableCohortAggregateConfigurationUse = GetAllObjects<JoinableCohortAggregateConfigurationUse>(repository);

            AllCatalogueItemsDictionary = GetAllObjects<CatalogueItem>(repository).ToDictionary(i => i.ID, o => o);
            _catalogueToCatalogueItems = new From1ToM<Catalogue, CatalogueItem>(ci=>ci.Catalogue_ID,AllCatalogueItems);
            _allColumnInfos = AllColumnInfos.ToDictionary(i=>i.ID,o=>o);
            
            //Inject known ColumnInfos into CatalogueItems
            Parallel.ForEach(AllCatalogueItems, (ci) =>
            {
                ColumnInfo col = null;

                if (ci.ColumnInfo_ID != null && _allColumnInfos.ContainsKey(ci.ColumnInfo_ID.Value))
                    col = _allColumnInfos[ci.ColumnInfo_ID.Value];

                ci.InjectKnown(col);
            });

            AllExtractionInformationsDictionary = GetAllObjects<ExtractionInformation>(repository).ToDictionary(i => i.ID, o => o);
            _extractionInformationsByCatalogueItem = AllExtractionInformationsDictionary.Values.ToDictionary(k=>k.CatalogueItem_ID,v=>v);

            //Inject known CatalogueItems into ExtractionInformations
            foreach (ExtractionInformation ei in AllExtractionInformationsDictionary.Values)
            {
                CatalogueItem ci = AllCatalogueItemsDictionary[ei.CatalogueItem_ID];
                ei.InjectKnown(ci.ColumnInfo);
                ei.InjectKnown(ci);
            }

            AllAggregateConfigurations = GetAllObjects<AggregateConfiguration>(repository);
            AllAggregateDimensions = GetAllObjects<AggregateDimension>(repository);

            //to start with all aggregates are orphans (we prune this as we determine descendency in AddChildren methods
            OrphanAggregateConfigurations = new HashSet<AggregateConfiguration>(AllAggregateConfigurations.Where(ac=>ac.IsCohortIdentificationAggregate));

            foreach (AggregateConfiguration configuration in AllAggregateConfigurations)
            {
                configuration.InjectKnown(AllCataloguesDictionary[configuration.Catalogue_ID]);
                configuration.InjectKnown(AllAggregateDimensions.Where(d=>d.AggregateConfiguration_ID == configuration.ID).ToArray());
            }

            foreach (AggregateDimension d in AllAggregateDimensions)
                d.InjectKnown(AllExtractionInformationsDictionary[d.ExtractionInformation_ID]);

            AllCohortAggregateContainers = GetAllObjects<CohortAggregateContainer>(repository);
            AllJoinables = GetAllObjects<JoinableCohortAggregateConfiguration>(repository);
            AllJoinUses = GetAllObjects<JoinableCohortAggregateConfigurationUse>(repository);

            AllAggregateContainersDictionary = GetAllObjects<AggregateFilterContainer>(repository).ToDictionary(o => o.ID, o2 => o2);
            AllAggregateFilters = GetAllObjects<AggregateFilter>(repository);
            AllAggregateFilterParameters = GetAllObjects<AggregateFilterParameter>(repository);

            AllCatalogueFilters = GetAllObjects<ExtractionFilter>(repository);
            AllCatalogueParameters = GetAllObjects<ExtractionFilterParameter>(repository);
            AllCatalogueValueSets = GetAllObjects<ExtractionFilterParameterSet>(repository);
            AllCatalogueValueSetValues = GetAllObjects<ExtractionFilterParameterSetValue>(repository);

            //if we have a database repository then we should get asnwers from the caching version CohortContainerManagerFromChildProvider otherwise
            //just use the one that is configured on the repository.
            var cataRepo = repository as CatalogueRepository;
            _aggregateFilterManager = cataRepo != null ? new FilterManagerFromChildProvider(cataRepo, this) : repository.FilterManager;
            
            _cohortContainerManager = cataRepo != null ? new CohortContainerManagerFromChildProvider(cataRepo, this) : repository.CohortContainerManager;

            AllLookups = GetAllObjects<Lookup>(repository);

            foreach (Lookup l in AllLookups)
                l.SetKnownColumns(_allColumnInfos[l.PrimaryKey_ID], _allColumnInfos[l.ForeignKey_ID],_allColumnInfos[l.Description_ID]);

            AllJoinInfos = repository.GetAllObjects<JoinInfo>();

            foreach (JoinInfo j in AllJoinInfos)
                j.SetKnownColumns(_allColumnInfos[j.PrimaryKey_ID], _allColumnInfos[j.ForeignKey_ID]);

            AllExternalServersNode = new AllExternalServersNode();
            AddChildren(AllExternalServersNode);

            AllRDMPRemotesNode = new AllRDMPRemotesNode();
            AddChildren(AllRDMPRemotesNode);

            AllDashboardsNode = new AllDashboardsNode();
            AllDashboards = GetAllObjects<DashboardLayout>(repository);
            AddChildren(AllDashboardsNode);

            AllObjectSharingNode = new AllObjectSharingNode();
            AllExports = GetAllObjects<ObjectExport>(repository);
            AllImports = GetAllObjects<ObjectImport>(repository);

            AddChildren(AllObjectSharingNode);

            //Pipelines setup (see also DataExportChildProvider for calls to AddPipelineUseCases)
            //Root node for all pipelines
            AllPipelinesNode = new AllPipelinesNode();

            //Pipelines not found to be part of any use case after AddPipelineUseCases
            OtherPipelinesNode = new OtherPipelinesNode();
            AllPipelines = GetAllObjects<Pipeline>(repository);
            AllPipelineComponents = GetAllObjects<PipelineComponent>(repository);

            foreach (Pipeline p in AllPipelines)
                p.InjectKnown(AllPipelineComponents.Where(pc => pc.Pipeline_ID == p.ID).ToArray());

            AllStandardRegexesNode = new AllStandardRegexesNode();
            AllStandardRegexes = GetAllObjects<StandardRegex>(repository);
            AddToDictionaries(new HashSet<object>(AllStandardRegexes),new DescendancyList(AllStandardRegexesNode));
            
            //All the things for TableInfoCollectionUI
            BuildServerNodes();

            AddChildren(CatalogueFolder.Root,new DescendancyList(CatalogueFolder.Root));
            
            AllLoadMetadatasNode = new AllLoadMetadatasNode();
            AddChildren(AllLoadMetadatasNode);

            foreach (CohortIdentificationConfiguration cic in AllCohortIdentificationConfigurations)
                AddChildren(cic);

            //add the orphans under the orphan folder
            AddToDictionaries(new HashSet<object>(OrphanAggregateConfigurations),new DescendancyList(OrphanAggregateConfigurationsNode));

            //Some AggregateConfigurations are 'Patient Index Tables', this happens when there is an existing JoinableCohortAggregateConfiguration declared where
            //the AggregateConfiguration_ID is the AggregateConfiguration.ID.  We can inject this knowledge now so to avoid database lookups later (e.g. at icon provision time)
            Dictionary<int, JoinableCohortAggregateConfiguration> joinableDictionaryByAggregateConfigurationId =  AllJoinables.ToDictionary(j => j.AggregateConfiguration_ID,v=> v);

            foreach (AggregateConfiguration ac in AllAggregateConfigurations)
            {
                var joinable = joinableDictionaryByAggregateConfigurationId.ContainsKey(ac.ID) //if theres a joinable
                    ? joinableDictionaryByAggregateConfigurationId[ac.ID] //inject that we know the joinable (and what it is)
                    : null; //otherwise inject that it is not a joinable (suppresses database checking later)

                ac.InjectKnown(joinable);
            }
                    
            AllGovernanceNode = new AllGovernanceNode();
            AllGovernancePeriods = GetAllObjects<GovernancePeriod>(repository);
            AllGovernanceDocuments = GetAllObjects<GovernanceDocument>(repository);
            GovernanceCoverage = repository.GovernanceManager.GetAllGovernedCataloguesForAllGovernancePeriods();

            AddChildren(AllGovernanceNode);

            AllPluginsNode = new AllPluginsNode();
            AllPlugins = GetAllObjects<Curation.Data.Plugin>(repository);
            AllCompatiblePlugins = _catalogueRepository.PluginManager.GetCompatiblePlugins();

            AddChildren(AllPluginsNode);

            var searchables = new Dictionary<int, HashSet<IMapsDirectlyToDatabaseTable>>();

            foreach (IMapsDirectlyToDatabaseTable o in _descendancyDictionary.Keys.OfType<IMapsDirectlyToDatabaseTable>())
            {
                if(!searchables.ContainsKey(o.ID))
                    searchables.Add(o.ID,new HashSet<IMapsDirectlyToDatabaseTable>());

                searchables[o.ID].Add(o);
            }
            
            foreach (ObjectExport e in AllExports)
            {
                if(!searchables.ContainsKey(e.ReferencedObjectID))
                    continue;
                
                var known = searchables[e.ReferencedObjectID].FirstOrDefault(s => e.ReferencedObjectType == s.GetType().FullName);

                if(known != null)
                    e.InjectKnown(known);
            }
            
        }

        private void AddChildren(AllPluginsNode allPluginsNode)
        {
            HashSet<object> children = new HashSet<object>();
            var descendancy = new DescendancyList(allPluginsNode);

            foreach (var p in AllCompatiblePlugins)
                children.Add(p);
        
            var expiredPluginsNode = new AllExpiredPluginsNode(); 
            children.Add(expiredPluginsNode);
            AddChildren(expiredPluginsNode,descendancy.Add(expiredPluginsNode));

            AddToDictionaries(children, descendancy);
        }

        private void AddChildren(AllExpiredPluginsNode expiredPluginsNode, DescendancyList descendancy)
        {
            HashSet<object> children = new HashSet<object>();

            foreach (var p in AllPlugins.Except(AllCompatiblePlugins))
                children.Add(p);

            AddToDictionaries(children, descendancy);
        }

        private void AddChildren(AllGovernanceNode allGovernanceNode)
        {
            HashSet<object> children = new HashSet<object>();
            var descendancy = new DescendancyList(allGovernanceNode);

            foreach (GovernancePeriod gp in AllGovernancePeriods)
            {
                children.Add(gp);
                AddChildren(gp, descendancy.Add(gp));
            }

            AddToDictionaries(children, descendancy);
        }

        private void AddChildren(GovernancePeriod governancePeriod, DescendancyList descendancy)
        {
            HashSet<object> children = new HashSet<object>();
            
            foreach (GovernanceDocument doc in AllGovernanceDocuments.Where(d=>d.GovernancePeriod_ID == governancePeriod.ID))
                children.Add(doc);
            
            AddToDictionaries(children, descendancy);
        }


        private void AddChildren(AllLoadMetadatasNode allLoadMetadatasNode)
        {
            HashSet<object> children = new HashSet<object>();
            var descendancy = new DescendancyList(allLoadMetadatasNode);

            foreach (LoadMetadata lmd in AllLoadMetadatas)
            {
                children.Add(lmd);
                AddChildren(lmd, descendancy.Add(lmd));
            }

            AddToDictionaries(children,descendancy);
        }

        private void AddChildren(AllPermissionWindowsNode allPermissionWindowsNode)
        {
            var descendancy = new DescendancyList(allPermissionWindowsNode);

            foreach (var permissionWindow in AllPermissionWindows)
                AddChildren(permissionWindow, descendancy.Add(permissionWindow));


            AddToDictionaries(new HashSet<object>(AllPermissionWindows),descendancy);
        }

        private void AddChildren(PermissionWindow permissionWindow, DescendancyList descendancy)
        {
            HashSet<object> children = new HashSet<object>();

            foreach (CacheProgress cacheProgress in AllCacheProgresses)
                if (cacheProgress.PermissionWindow_ID == permissionWindow.ID)
                    children.Add(new PermissionWindowUsedByCacheProgressNode(cacheProgress, permissionWindow, false));

            AddToDictionaries(children,descendancy);
        }

        private void AddChildren(AllExternalServersNode allExternalServersNode)
        {
            AddToDictionaries(new HashSet<object>(AllExternalServers), new DescendancyList(allExternalServersNode));
        }

        private void AddChildren(AllRDMPRemotesNode allRDMPRemotesNode)
        {
            AddToDictionaries(new HashSet<object>(AllRemoteRDMPs), new DescendancyList(allRDMPRemotesNode));
        }

        private void AddChildren(AllDashboardsNode allDashboardsNode)
        {
            AddToDictionaries(new HashSet<object>(AllDashboards), new DescendancyList(allDashboardsNode));
        }

        private void AddChildren(AllObjectSharingNode allObjectSharingNode)
        {
            var descendancy = new DescendancyList(allObjectSharingNode);

            var allExportsNode = new AllObjectExportsNode();
            var allImportsNode = new AllObjectImportsNode();

            AddToDictionaries(new HashSet<object>(AllExports), descendancy.Add(allExportsNode));
            AddToDictionaries(new HashSet<object>(AllImports), descendancy.Add(allImportsNode));

            AddToDictionaries(new HashSet<object>(new object[] { allExportsNode, allImportsNode }), descendancy);
        }


        /// <summary>
        /// Creates new <see cref="StandardPipelineUseCaseNode"/>s and fills it with all compatible Pipelines - do not call this method more than once
        /// </summary>
        protected void AddPipelineUseCases(Dictionary<string,PipelineUseCase> useCases)
        {
            var descendancy = new DescendancyList(AllPipelinesNode);
            HashSet<object> children = new HashSet<object>();

            //pipelines not found to be part of any StandardPipelineUseCase
            HashSet<object> unknownPipelines = new HashSet<object>(AllPipelines);

            foreach (var useCase in useCases)
            {
                var node = new StandardPipelineUseCaseNode(useCase.Key, useCase.Value, _commentStore);
                
                //keep track of all the use cases
                PipelineUseCases.Add(node);

                foreach (Pipeline pipeline in AddChildren(node, descendancy.Add(node)))
                    unknownPipelines.Remove(pipeline);

                children.Add(node);
            }

            children.Add(OtherPipelinesNode);

            AddToDictionaries(unknownPipelines,descendancy.Add(OtherPipelinesNode));
            
            //it is the first standard use case
            AddToDictionaries(children, descendancy);
            
        }

        private IEnumerable<Pipeline> AddChildren(StandardPipelineUseCaseNode node, DescendancyList descendancy)
        {
            HashSet<object> children = new HashSet<object>();
            
            MemoryRepository repo = new MemoryRepository();

            //find compatible pipelines useCase.Value
            foreach (Pipeline compatiblePipeline in AllPipelines.Where(node.UseCase.GetContext().IsAllowable))
            {
                children.Add(new PipelineCompatibleWithUseCaseNode(repo,compatiblePipeline, node.UseCase));
            }

            //it is the first standard use case
            AddToDictionaries(children, descendancy);

            return children.Cast<PipelineCompatibleWithUseCaseNode>().Select(u => u.Pipeline);
        }


        private void BuildServerNodes()
        {
            Dictionary<TableInfoServerNode,List<TableInfo>> allServers = new Dictionary<TableInfoServerNode,List<TableInfo>>();

            //add a root node for all the servers to be children of
            AllServersNode = new AllServersNode();

            //find the unique server names among TableInfos
            foreach (TableInfo t in AllTableInfos)
            {
                //make sure we have the in our dictionary
                if(!allServers.Keys.Any(k=>k.IsSameServer(t)))
                    allServers.Add(new TableInfoServerNode(t.Server,t.DatabaseType),new List<TableInfo>());

                var match = allServers.Single(kvp => kvp.Key.IsSameServer(t));
                match.Value.Add(t);
            }

            //create the server nodes
            AllServers = allServers.Keys.ToArray();

            //document the children
            foreach (var kvp in allServers)
            {
                var tableInfos = kvp.Value;

                //record the fact that the TableInfos are children of their specific TableInfoServerNode
                AddToDictionaries(new HashSet<object>(tableInfos), new DescendancyList(AllServersNode, kvp.Key));
                
                //record the children of the table infos (mostly column infos)
                var kvp1 = kvp;
                Parallel.ForEach(tableInfos, (t) =>
                {
                    //t descends from :
                    //the all servers node=>the TableInfoServerNode => the t
                    AddChildren(t,new DescendancyList(AllServersNode, kvp1.Key, t));
                });

            }

            //record the fact that all the servers are children of the all servers node
            AddToDictionaries(new HashSet<object>(AllServers),new DescendancyList(AllServersNode));
        }


        private void AddChildren(AllDataAccessCredentialsNode allDataAccessCredentialsNode)
        {
            HashSet<object> children = new HashSet<object>();

            bool isKeyMissing = false;
            var keyLocation = _catalogueRepository.EncryptionManager as PasswordEncryptionKeyLocation;
            if (keyLocation != null)
                isKeyMissing = string.IsNullOrWhiteSpace(keyLocation.GetKeyFileLocation());

            children.Add(new DecryptionPrivateKeyNode(isKeyMissing));

            foreach (var creds in AllDataAccessCredentials)
                children.Add(creds);


            AddToDictionaries(children, new DescendancyList(allDataAccessCredentialsNode));
        }

        private void AddChildren(AllANOTablesNode anoTablesNode)
        {
            AddToDictionaries(new HashSet<object>(AllANOTables), new DescendancyList(anoTablesNode));
        }

        private void AddChildren(CatalogueFolder folder, DescendancyList descendancy)
        {
            ConcurrentBag<object> childObjects = new ConcurrentBag<object>();

            Parallel.ForEach(folder.GetImmediateSubFoldersUsing(AllCatalogues), (f) =>
            {
                //add subfolders
                childObjects.Add(f);
                AddChildren(f, descendancy.Add(f));
            });

            //add catalogues in folder
            Parallel.ForEach(AllCatalogues.Where(c => c.Folder.Equals(folder)), c => 
            {
                AddChildren(c,descendancy.Add(c));
                childObjects.Add(c);
            });
            
            //finalise
            AddToDictionaries(new HashSet<object>(childObjects),descendancy );
        }

        #region Load Metadata
        private void AddChildren(LoadMetadata lmd, DescendancyList descendancy)
        {
            List<object> childObjects = new List<object>();

            if (lmd.OverrideRAWServer_ID.HasValue)
            {
                var server = AllExternalServers.Single(s => s.ID == lmd.OverrideRAWServer_ID.Value);
                var usage = new OverrideRawServerNode(lmd, server);
                childObjects.Add(usage);
            }

            var allSchedulesNode = new LoadMetadataScheduleNode(lmd);
            AddChildren(allSchedulesNode,descendancy.Add(allSchedulesNode));
            childObjects.Add(allSchedulesNode);

            var allCataloguesNode = new AllCataloguesUsedByLoadMetadataNode(lmd);
            AddChildren(allCataloguesNode, descendancy.Add(allCataloguesNode));
            childObjects.Add(allCataloguesNode);

            var processTasksNode = new AllProcessTasksUsedByLoadMetadataNode(lmd);
            AddChildren(processTasksNode, descendancy.Add(processTasksNode));
            childObjects.Add(processTasksNode);

            childObjects.Add(new LoadDirectoryNode(lmd));

            AddToDictionaries(new HashSet<object>(childObjects), descendancy);
        }

        private void AddChildren(LoadMetadataScheduleNode allSchedulesNode, DescendancyList descendancy)
        {
            HashSet<object> childObjects = new HashSet<object>();

            var lmd = allSchedulesNode.LoadMetadata;
            
            foreach (var lp in AllLoadProgresses.Where(p => p.LoadMetadata_ID == lmd.ID))
            {
                AddChildren(lp,descendancy.Add(lp));
                childObjects.Add(lp);
            }

            if(childObjects.Any())
                AddToDictionaries(childObjects,descendancy);
        }

        private void AddChildren(LoadProgress loadProgress, DescendancyList descendancy)
        {
            var cacheProgresses = AllCacheProgresses.Where(cp => cp.LoadProgress_ID == loadProgress.ID).ToArray();

            foreach (CacheProgress cacheProgress in cacheProgresses)
                AddChildren(cacheProgress, descendancy.Add(cacheProgress));

            if (cacheProgresses.Any())
                AddToDictionaries(new HashSet<object>(cacheProgresses),descendancy);
        }

        private void AddChildren(CacheProgress cacheProgress, DescendancyList descendancy)
        {
            var children = new HashSet<object>();

            if(cacheProgress.PermissionWindow_ID != null)
            {
                var window = AllPermissionWindows.Single(w => w.ID == cacheProgress.PermissionWindow_ID);
                var windowNode = new PermissionWindowUsedByCacheProgressNode(cacheProgress, window,true);

                children.Add(windowNode);
            }

            if(children.Any())
                AddToDictionaries(children, descendancy);
        }

        private void AddChildren(AllProcessTasksUsedByLoadMetadataNode allProcessTasksUsedByLoadMetadataNode, DescendancyList descendancy)
        {
            HashSet<object> childObjects = new HashSet<object>();

            var lmd = allProcessTasksUsedByLoadMetadataNode.LoadMetadata;
            childObjects.Add(new LoadStageNode(lmd,LoadStage.GetFiles));
            childObjects.Add(new LoadStageNode(lmd, LoadStage.Mounting));
            childObjects.Add(new LoadStageNode(lmd, LoadStage.AdjustRaw));
            childObjects.Add(new LoadStageNode(lmd, LoadStage.AdjustStaging));
            childObjects.Add(new LoadStageNode(lmd, LoadStage.PostLoad));

            foreach (LoadStageNode node in childObjects)
                AddChildren(node,descendancy.Add(node));

            AddToDictionaries(childObjects,descendancy);
        }

        private void AddChildren(LoadStageNode loadStageNode, DescendancyList descendancy)
        {
            var tasks = AllProcessTasks.Where(
                p => p.LoadMetadata_ID == loadStageNode.LoadMetadata.ID && p.LoadStage == loadStageNode.LoadStage)
                .OrderBy(o=>o.Order).ToArray();

            if(tasks.Any())
                AddToDictionaries(new HashSet<object>(tasks),descendancy);
        }

        private void AddChildren(AllCataloguesUsedByLoadMetadataNode allCataloguesUsedByLoadMetadataNode, DescendancyList descendancy)
        {
            HashSet<object> chilObjects = new HashSet<object>();

            var usedCatalogues = AllCatalogues.Where(c => c.LoadMetadata_ID == allCataloguesUsedByLoadMetadataNode.LoadMetadata.ID).ToList();


            foreach (Catalogue catalogue in usedCatalogues)
                chilObjects.Add(new CatalogueUsedByLoadMetadataNode(allCataloguesUsedByLoadMetadataNode.LoadMetadata,catalogue));

            allCataloguesUsedByLoadMetadataNode.UsedCatalogues = usedCatalogues;

            AddToDictionaries(chilObjects,descendancy);
        }

        #endregion

        protected void AddChildren(Catalogue c, DescendancyList descendancy)
        {
            List<object> childObjects = new List<object>();

            var catalogueAggregates = AllAggregateConfigurations.Where(a => a.Catalogue_ID == c.ID).ToArray();
            var cohortAggregates = catalogueAggregates.Where(a => a.IsCohortIdentificationAggregate).ToArray();
            var regularAggregates = catalogueAggregates.Except(cohortAggregates).ToArray();

            //get all the CatalogueItems for this Catalogue
            var cis = _catalogueToCatalogueItems[c].ToArray();

            //tell the CatalogueItems that we are are their parent
            foreach (CatalogueItem ci in cis)
                ci.InjectKnown(c);
            
            //add a new CatalogueItemNode (can be empty)
            var catalogueItemsNode = new CatalogueItemsNode(c, cis);

            //if there are at least 1 catalogue items inject them into Catalogue and add a recording that the CatalogueItemsNode has these children (otherwise node has no children)
            if (cis.Any())
            {
                c.InjectKnown(cis);

                var ciNodeDescendancy = descendancy.Add(catalogueItemsNode);
                AddToDictionaries(new HashSet<object>(cis), ciNodeDescendancy);

                foreach (CatalogueItem ci in cis)
                    AddChildren(ci,ciNodeDescendancy.Add(ci));
                
            }

            //do we have any foreign key fields into this lookup table
            var lookups = AllLookups.Where(l => c.CatalogueItems.Any(ci => ci.ColumnInfo_ID == l.ForeignKey_ID)).ToArray();
            
            var docs = AllSupportingDocuments.Where(d => d.Catalogue_ID == c.ID).ToArray();
            var sql = AllSupportingSQL.Where(d => d.Catalogue_ID == c.ID).ToArray();

            //if there are supporting documents or supporting sql files then add  documentation node
            if (docs.Any() || sql.Any())
            {
                var documentationNode = new DocumentationNode(c, docs, sql);

                //add the documentations node
                childObjects.Add(documentationNode);

                //record the children
                AddToDictionaries(new HashSet<object>(docs.Cast<object>().Union(sql)),descendancy.Add(documentationNode));
            }

            if (lookups.Any())
            {
                var lookupsNode = new CatalogueLookupsNode(c, lookups);
                //add the documentations node
                childObjects.Add(lookupsNode);


                //record the children
                AddToDictionaries(new HashSet<object>(lookups), descendancy.Add(lookupsNode));
            }

            if (regularAggregates.Any())
            {
                var aggregatesNode = new AggregatesNode(c, regularAggregates);
                childObjects.Add(aggregatesNode);

                var nodeDescendancy = descendancy.Add(aggregatesNode);
                AddToDictionaries(new HashSet<object>(regularAggregates),nodeDescendancy);

                foreach (AggregateConfiguration regularAggregate in regularAggregates)
                    AddChildren(regularAggregate, nodeDescendancy.Add(regularAggregate));
            }
            
            childObjects.Add(catalogueItemsNode);

            
            //finalise
            AddToDictionaries(new HashSet<object>(childObjects),descendancy);
        }

        private void AddChildren(AggregateConfiguration aggregateConfiguration, DescendancyList descendancy)
        {
            var childrenObjects = new HashSet<object>();

            var parameters = AllAnyTableParameters.Where(p => p.IsReferenceTo(aggregateConfiguration)).Cast<ISqlParameter>().ToArray();
            var node = new ParametersNode(aggregateConfiguration, parameters);
            childrenObjects.Add(node);

            //we can step into this twice, once via Catalogue children and once via CohortIdentificationConfiguration children
            //if we get in via Catalogue children then descendancy will be Ignore=true we don't end up emphasising into CatalogueCollectionUI when
            //really user wants to see it in CohortIdentificationCollectionUI
            if(aggregateConfiguration.RootFilterContainer_ID != null)
            {
                var container = AllAggregateContainersDictionary[(int) aggregateConfiguration.RootFilterContainer_ID];
                
                AddChildren(container,descendancy.Add(container));
                childrenObjects.Add(container);
            }

            AddToDictionaries(childrenObjects, descendancy);
        }

        private void AddChildren(AggregateFilterContainer container, DescendancyList descendancy)
        {
            List<object> childrenObjects = new List<object>();

            var subcontainers = _aggregateFilterManager.GetSubContainers(container);
            var filters = _aggregateFilterManager.GetFilters(container);

            foreach (AggregateFilterContainer subcontainer in subcontainers)
            {
                //one of our children is this subcontainer
                childrenObjects.Add(subcontainer);

                //but also document its children
                AddChildren(subcontainer,descendancy.Add(subcontainer));
            }

            //also add the filters for the container
            childrenObjects.AddRange(filters);
            
            //add our children to the dictionary
            AddToDictionaries(new HashSet<object>(childrenObjects),descendancy);
        }

        private void AddChildren(CatalogueItem ci, DescendancyList descendancy)
        {
            List<object> childObjects = new List<object>();

            if(_extractionInformationsByCatalogueItem.ContainsKey(ci.ID))
            {
                var ei = _extractionInformationsByCatalogueItem[ci.ID];
                ci.InjectKnown(ei);
                childObjects.Add(ei);
                AddChildren(ei, descendancy.Add(ei));
            }

            if (ci.ColumnInfo_ID.HasValue)
                childObjects.Add(new LinkedColumnInfoNode(ci, _allColumnInfos[ci.ColumnInfo_ID.Value]));
            
            AddToDictionaries(new HashSet<object>(childObjects),descendancy);
        }

        private void AddChildren(ExtractionInformation extractionInformation, DescendancyList descendancy)
        {
            var children = new HashSet<object>();
            
            foreach (var filter in AllCatalogueFilters.Where(f => f.ExtractionInformation_ID == extractionInformation.ID))
            {
                //add the filter as a child of the 
                children.Add(filter);
                AddChildren(filter,descendancy.Add(filter));
            }

            AddToDictionaries(children,descendancy);
        }

        /*
        public IEnumerable<AggregateFilterParameter> GetParameters(AggregateFilter filter)
        {
            return AllAggregateFilterParameters.Where(p => p.AggregateFilter_ID == filter.ID);
        }
        public IEnumerable<ExtractionFilterParameterSetValue> GetValueSetValues(ExtractionFilterParameterSet parameterSet)
        {
            return AllCatalogueValueSetValues.Where(v => v.ExtractionFilterParameterSet_ID == parameterSet.ID);
        }*/

        private void AddChildren(ExtractionFilter filter, DescendancyList descendancy)
        {
            var children = new HashSet<object>();
            var parameters = AllCatalogueParameters.Where(p => p.ExtractionFilter_ID == filter.ID).ToArray();
            var parameterSets = AllCatalogueValueSets.Where(vs => vs.ExtractionFilter_ID == filter.ID);

            if (parameters.Any())
                children.Add(new ParametersNode(filter, parameters));

            foreach (ExtractionFilterParameterSet set in parameterSets)
                children.Add(set);

            if(children.Any())
                AddToDictionaries(children,descendancy);
        }

        public virtual object[] GetChildren(object model)
        {
           
            //if we don't have a record of any children in the child dictionary for the parent model object
            if(!_childDictionary.ContainsKey(model))
                return new object[0];//return none
            
            return _childDictionary[model].OrderBy(o=>o.ToString()).ToArray();
        }

        private void AddChildren(CohortIdentificationConfiguration cic)
        {
            HashSet<object> children = new HashSet<object>();

            //it has an associated query cache
            if (cic.QueryCachingServer_ID != null)
                children.Add(new QueryCacheUsedByCohortIdentificationNode(cic, AllExternalServers.Single(s => s.ID == cic.QueryCachingServer_ID)));
            
            var parameters = AllAnyTableParameters.Where(p => p.IsReferenceTo(cic)).Cast<ISqlParameter>().ToArray();
            var node = new ParametersNode(cic, parameters);

            children.Add(node);

            //if it has a root container
            if (cic.RootCohortAggregateContainer_ID != null)
            {
                var container = AllCohortAggregateContainers.Single(c => c.ID == cic.RootCohortAggregateContainer_ID);
                AddChildren(container, new DescendancyList(cic, container).SetBetterRouteExists());
                children.Add(container);
            }


            //get the patient index tables
            var joinableNode = new JoinableCollectionNode(cic, AllJoinables.Where(j => j.CohortIdentificationConfiguration_ID == cic.ID).ToArray());
            AddChildren(joinableNode, new DescendancyList(cic, joinableNode).SetBetterRouteExists());
            children.Add(joinableNode);

            AddToDictionaries(children, new DescendancyList(cic).SetBetterRouteExists());
        }

        private void AddChildren(JoinableCollectionNode joinablesNode, DescendancyList descendancy)
        {
            HashSet<object> children = new HashSet<object>();

            foreach (var joinable in joinablesNode.Joinables)
            {
                try
                {
                    var agg = AllAggregateConfigurations.Single(ac => ac.ID == joinable.AggregateConfiguration_ID);
                    ForceAggregateNaming(agg,descendancy);
                    children.Add(agg);

                    //it's no longer an orphan because it's in a known cic (as a patient index table)
                    OrphanAggregateConfigurations.Remove(agg);

                    AddChildren(agg,descendancy.Add(agg));
                }
                catch (Exception e)
                {
                    throw new Exception("JoinableCohortAggregateConfiguration (patient index table) object (ID="+joinable.ID+") references AggregateConfiguration_ID " + joinable.AggregateConfiguration_ID + " but that AggregateConfiguration was not found",e);
                }
            }

            AddToDictionaries(children, descendancy);
        }

        private void AddChildren(CohortAggregateContainer container, DescendancyList descendancy)
        {
            //all our children (containers and aggregates)
            List<IOrderable> children = new List<IOrderable>();

            //get subcontainers
            var subcontainers = _cohortContainerManager.GetChildren(container).OfType<CohortAggregateContainer>().ToList();

            //if there are subcontainers
            foreach (CohortAggregateContainer subcontainer in subcontainers)
                AddChildren(subcontainer,descendancy.Add(subcontainer));

            //get our configurations
            var configurations = _cohortContainerManager.GetChildren(container).OfType<AggregateConfiguration>().ToList();

            //record the configurations children including full descendancy
            foreach (AggregateConfiguration configuration in configurations)
            {
                ForceAggregateNaming(configuration, descendancy);
                AddChildren(configuration, descendancy.Add(configuration));

                //it's no longer an orphan because it's in a known cic
                OrphanAggregateConfigurations.Remove(configuration);
            }

            //children are all aggregates and containers at the current hierarchy level in order
            children = subcontainers.Union(configurations.Cast<IOrderable>()).OrderBy(o => o.Order).ToList();

            AddToDictionaries(new HashSet<object>(children),descendancy);
        }

        private void ForceAggregateNaming(AggregateConfiguration configuration, DescendancyList descendancy)
        {
            //configuration has the wrong name
            if (!configuration.IsCohortIdentificationAggregate)
            {
                _errorsCheckNotifier.OnCheckPerformed(new CheckEventArgs("Had to fix naming of configuration '" + configuration + "' because it didn't start with correct cic prefix",CheckResult.Warning));
                descendancy.Parents.OfType<CohortIdentificationConfiguration>().Single().EnsureNamingConvention(configuration);
                configuration.SaveToDatabase();
            }
        }

        private void AddChildren(TableInfo tableInfo,DescendancyList descendancy)
        {
            //add empty hashset
            var children =  new HashSet<object>();
            
            //if the table has an identifier dump listed
            if (tableInfo.IdentifierDumpServer_ID != null)
            {
                //if there is a dump (e.g. for dillution and dumping - not appearing in the live table)
                ExternalDatabaseServer server = AllExternalServers.Single(s => s.ID == tableInfo.IdentifierDumpServer_ID.Value);

                children.Add(new IdentifierDumpServerUsageNode(tableInfo, server));
            }
            
            //get the discarded columns in this table
            var discardedCols = new HashSet<object>(AllPreLoadDiscardedColumns.Where(c => c.TableInfo_ID == tableInfo.ID));

            //tell the column who thier parent is so they don't need to look up the database
            foreach (PreLoadDiscardedColumn discardedCol in discardedCols)
                discardedCol.InjectKnown(tableInfo);

            //if there are discarded columns
            if (discardedCols.Any())
            {
                var identifierDumpNode = new PreLoadDiscardedColumnsNode(tableInfo);
                
                //record that the usage is a child of TableInfo
                children.Add(identifierDumpNode);

                //record that the discarded columns are children of identifier dump usage node
                AddToDictionaries(discardedCols, descendancy.Add(identifierDumpNode));
            }

            //if it is a table valued function
            if (tableInfo.IsTableValuedFunction)
            {
                //that has parameters
                var parameters = tableInfo.GetAllParameters();

                //then add those as a node
                if (parameters.Any())
                    children.Add(new ParametersNode(tableInfo, parameters));
            }

            //next add the column infos
            foreach (ColumnInfo c in _tableInfosToColumnInfos[tableInfo])
            {
                children.Add(c);
                c.InjectKnown(tableInfo);
                AddChildren(c,descendancy.Add(c).SetBetterRouteExists());
            }

            //finally add any credentials objects
            if (AllDataAccessCredentialUsages.ContainsKey(tableInfo))
                foreach (DataAccessCredentialUsageNode node in AllDataAccessCredentialUsages[tableInfo])
                    children.Add(node);

            //now we have recorded all the children add them with descendancy via the TableInfo descendancy
            AddToDictionaries(children,descendancy);
        }

        private void AddChildren(ColumnInfo columnInfo, DescendancyList descendancy)
        {
            var lookups = AllLookups.Where(l => l.Description_ID == columnInfo.ID).ToArray();
            var joinInfos = AllJoinInfos.Where(j => j.PrimaryKey_ID == columnInfo.ID);

            var children = new HashSet<object>();

            foreach (var l in lookups)
                children.Add(l);

            foreach (var j in joinInfos)
                children.Add(j);

            if (children.Any())
                AddToDictionaries(children,descendancy);
        }

        private object oAddToDictionariesLock = new object();
        protected void AddToDictionaries(HashSet<object> children, DescendancyList list)
        {
            if(list.IsEmpty)
                throw new ArgumentException("DescendancyList cannot be empty","list");
         
            //document that the last parent has these as children
            var parent = list.Last();
            lock (oAddToDictionariesLock)
            {

                //we have already seen it before
                if(_childDictionary.ContainsKey(parent))
                {
                    if (!_childDictionary[parent].SetEquals(children))
                        throw new Exception("Ambiguous children collections for object '" + parent  +"'");
                }
                else
                    _childDictionary.AddOrUpdate(parent,children,(o, set) => set);

                //now document the entire parent order to reach each child object i.e. 'Root=>Grandparent=>Parent'  is how you get to 'Child'
                foreach (object o in children)
                {
                    //if there is a collision for the object then it means we already know of another way to get to it (that's a problem, there can be only one)
                    if(_descendancyDictionary.ContainsKey(o))
                    {

                        var collision =_descendancyDictionary[o];
                        //the old way of getting to it was marked with BetterRouteExists so we can discard it
                        if (collision.BetterRouteExists)
                            _descendancyDictionary.Remove(o);
                        //the new one is marked BetterRouteExists so just throw away the new one
                        else if (list.BetterRouteExists)
                            continue;
                        //the new one is marked as the NewBestRoute so we can get rid of the old one and replace it
                        else if (list.NewBestRoute && !collision.NewBestRoute)
                            _descendancyDictionary.Remove(o);
                        else
                        {
                            //there was a horrible problem with 
                            _errorsCheckNotifier.OnCheckPerformed(new CheckEventArgs("Could not add '" + o + "' to Ascendancy Tree with parents " + list + " because it is already listed under hierarchy " + collision,CheckResult.Fail));
                            return;
                        }
                    
                    }

                    _descendancyDictionary.Add(o, list);
                }

                foreach (IMasqueradeAs masquerader in children.OfType<IMasqueradeAs>())
                {
                    var key = masquerader.MasqueradingAs();

                    if(!AllMasqueraders.ContainsKey(key))
                        AllMasqueraders.AddOrUpdate(key,new HashSet<IMasqueradeAs>(),(o, set) => set);

                    AllMasqueraders[key].Add(masquerader);
                }
            }
        }

        public DescendancyList GetDescendancyListIfAnyFor(object model)
        {
            if (_descendancyDictionary.ContainsKey(model))
                return _descendancyDictionary[model];

            return null;
        }
        
        
        public object GetRootObjectOrSelf(IMapsDirectlyToDatabaseTable objectToEmphasise)
        {
            var descendancy = GetDescendancyListIfAnyFor(objectToEmphasise);

            if (descendancy != null && descendancy.Parents.Any())
                return descendancy.Parents[0];

            return objectToEmphasise;
        }


        public virtual Dictionary<IMapsDirectlyToDatabaseTable, DescendancyList> GetAllSearchables()
        {
            var toReturn = new Dictionary<IMapsDirectlyToDatabaseTable, DescendancyList>();

            foreach (var kvp in _descendancyDictionary.Where(kvp => kvp.Key is IMapsDirectlyToDatabaseTable))
                toReturn.Add((IMapsDirectlyToDatabaseTable) kvp.Key, kvp.Value);

            return toReturn;
        }

        public IEnumerable<object> GetAllChildrenRecursively(object o)
        {
            List<object> toReturn = new List<object>();

            foreach (var child in GetChildren(o))
            {
                toReturn.Add(child);
                toReturn.AddRange(GetAllChildrenRecursively(child));
            }

            return toReturn;
        }

        /// <summary>
        /// Asks all plugins to provide the child objects for every object we have found so far.  This method is recursive, call it with null the first time to use all objects.  It will then
        /// call itself with all the new objects that were sent back by the plugin (so that new objects found can still have children).
        /// </summary>
        /// <param name="objectsToAskAbout"></param>
        public void GetPluginChildren(HashSet<object> objectsToAskAbout = null)
        {
            HashSet<object> newObjectsFound = new HashSet<object>();
            
            Stopwatch sw = new Stopwatch();

            //for every object found so far
            foreach (var o in objectsToAskAbout?? GetAllObjects())
            {
                //for every plugin loaded (that is not blacklisted)
                foreach (var plugin in PluginChildProviders.Except(_blacklistedPlugins))
                {
                    //ask about the children
                    try
                    {
                        sw.Restart();
                        //otherwise ask plugin what it's children are
                        var pluginChildren = plugin.GetChildren(o);

                        //if the plugin takes too long to respond we need to stop
                        if (sw.ElapsedMilliseconds > 1000)
                        {
                            _blacklistedPlugins.Add(plugin);
                            throw new Exception("Plugin '" + plugin + "' was blacklisted for taking too long to respond to GetChildren(o) where o was a '" + o.GetType().Name + "' ('" + o + "')");
                        }

                        //it has children
                        if (pluginChildren != null && pluginChildren.Any())
                        {
                            //get the descendancy of the parent
                            var parentDescendancy = GetDescendancyListIfAnyFor(o);

                            DescendancyList newDescendancy;
                            if(parentDescendancy == null)
                                newDescendancy = new DescendancyList(new[] {o}); //if the parent is a root level object start a new descendancy list from it
                            else
                                newDescendancy = parentDescendancy.Add(o);//otherwise keep going down, returns a new DescendancyList so doesn't corrupt the dictionary one

                            //record that 

                            foreach (object pluginChild in pluginChildren)
                            {
                                //if the parent didn't have any children before
                                if (!_childDictionary.ContainsKey(o))
                                    _childDictionary.AddOrUpdate(o,new HashSet<object>(),(o1, set) => set);//it does now


                                //add us to the parent objects child collection
                                _childDictionary[o].Add(pluginChild);
                                
                                //add to the child collection of the parent object kvp.Key
                                _descendancyDictionary.Add(pluginChild, newDescendancy);

                                //we have found a new object so we must ask other plugins about it (chances are a plugin will have a whole tree of sub objects)
                                newObjectsFound.Add(pluginChild);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _errorsCheckNotifier.OnCheckPerformed(new CheckEventArgs(e.Message, CheckResult.Fail, e));
                    }
                }
            }

            if(newObjectsFound.Any())
                GetPluginChildren(newObjectsFound);
        }

        public IEnumerable<IMasqueradeAs> GetMasqueradersOf(object o)
        {
            if (AllMasqueraders.ContainsKey(o))
                return AllMasqueraders[o];

            return new IMasqueradeAs[0];
        }

        public DatabaseEntity GetLatestCopyOf(DatabaseEntity e)
        {
            return _descendancyDictionary.Keys.OfType<DatabaseEntity>().SingleOrDefault(k => k.Equals(e));
        }

        private HashSet<object> GetAllObjects()
        {
            //anything which has children or is a child of someone else (distinct because HashSet)
            return new HashSet<object>(_childDictionary.SelectMany(kvp => kvp.Value).Union(_childDictionary.Keys));
        }

        protected T[] GetAllObjects<T>(IRepository repository) where T : IMapsDirectlyToDatabaseTable
        {
            return repository.GetAllObjects<T>();
        }


        protected void AddToReturnSearchablesWithNoDecendancy(Dictionary<IMapsDirectlyToDatabaseTable, DescendancyList> toReturn, IEnumerable<IMapsDirectlyToDatabaseTable> toAdd)
        {
            foreach (IMapsDirectlyToDatabaseTable m in toAdd)
                toReturn.Add(m, null);
        }
    }
}
