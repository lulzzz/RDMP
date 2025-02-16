// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using FAnsi;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Attributes;
using MapsDirectlyToDatabaseTable.Injection;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Curation.Data.Defaults;
using Rdmp.Core.Curation.Data.ImportExport;
using Rdmp.Core.Curation.Data.Serialization;
using Rdmp.Core.Logging;
using Rdmp.Core.QueryBuilding;
using Rdmp.Core.Repositories;
using Rdmp.Core.Ticketing;
using ReusableLibraryCode;
using ReusableLibraryCode.Annotations;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Core.Curation.Data
{
    /// <inheritdoc cref="ICatalogue"/>
    public class Catalogue : DatabaseEntity, IComparable, ICatalogue, ICheckable, IInjectKnown<CatalogueItem[]>,IInjectKnown<CatalogueExtractabilityStatus>
    {
        #region Database Properties
        
        private string _acronym;
        private string _name;
        private CatalogueFolder _folder;
        private string _description;
        private Uri _detailPageUrl;
        private CatalogueType _type;
        private CataloguePeriodicity _periodicity;
        private CatalogueGranularity _granularity;
        private string _geographicalCoverage;
        private string _backgroundSummary;
        private string _searchKeywords;
        private string _updateFreq;
        private string _updateSched;
        private string _timeCoverage;
        private DateTime? _lastRevisionDate;
        private string _contactDetails;
        private string _resourceOwner;
        private string _attributionCitation;
        private string _accessOptions;
        private string _subjectNumbers;
        private Uri _apiAccessUrl;
        private Uri _browseUrl;
        private Uri _bulkDownloadUrl;
        private Uri _queryToolUrl;
        private Uri _sourceUrl;
        private string _countryOfOrigin;
        private string _dataStandards;
        private string _administrativeContactName;
        private string _administrativeContactEmail;
        private string _administrativeContactTelephone;
        private string _administrativeContactAddress;
        private bool? _explicitConsent;
        private string _ethicsApprover;
        private string _sourceOfDataCollection;
        private string _ticket;
        private DateTime? _datasetStartDate;
        private string _loggingDataTask;
        private string _validatorXml;
        private int? _timeCoverageExtractionInformationID;
        private int? _pivotCategoryExtractionInformationID;
        private bool _isDeprecated;
        private bool _isInternalDataset;
        private bool _isColdStorageDataset;
        private int? _liveLoggingServerID;
        
        private Lazy<CatalogueItem[]> _knownCatalogueItems;
        
        
        /// <inheritdoc/>
        [Unique]
        [DoNotImportDescriptions(AllowOverwriteIfBlank = true)]
        public string Acronym
        {
            get { return _acronym; }
            set { SetField(ref  _acronym, value); }
        }

        /// <summary>
        /// The full human readable name of the dataset.  This should usually match the name of the underlying <see cref="TableInfo"/> but might differ
        /// if there are multiple tables powering the Catalogue or they don't have user accessible names.
        /// </summary>
        [Unique]
        [NotNull]
        [DoNotImportDescriptions]
        public string Name
        {
            get { return _name; }
            set { SetField(ref  _name, value); }
        }

        /// <summary>
        /// A user defined hierarchical category which designates the role of the dataset e.g. '\datasets\extractable\labdata\'
        /// <para>Should always start and end with a '\' even if it is the root (i.e. '\')</para>
        /// </summary>
        [DoNotImportDescriptions]
        public CatalogueFolder Folder
        {
            get { return _folder; }
            set { SetField(ref  _folder, value); }
        }
         
        /// <summary>
        /// Human readable description provided by the RDMP user that describes what the dataset contains.  
        /// <para>This can be multiple paragraphs.</para>
        /// </summary>
        public string Description
        {
            get { return _description; }
            set { SetField(ref  _description, value); }
        }

        /// <summary>
        /// User defined Uri for a website page which describes the dataset (probably null)
        /// </summary>
        public Uri Detail_Page_URL
        {
            get { return _detailPageUrl; }
            set { SetField(ref  _detailPageUrl, value); }
        }

        /// <summary>
        /// User defined classification of the Type of dataset the Catalogue is e.g. Cohort, ResearchStudy etc
        /// </summary>
        public CatalogueType Type
        {
            get { return _type; }
            set { SetField(ref  _type, value); }
        }

        /// <summary>
        /// User specified period on how regularly the dataset is updated.  This does not have any technical bearing on how often it is loaded
        /// and might be an outright lie.
        /// </summary>
        public CataloguePeriodicity Periodicity
        {
            get { return _periodicity; }
            set { SetField(ref  _periodicity, value); }
        }

        /// <summary>
        /// User specified field describing how the dataset is subdivided/bounded e.g. relates to a multiple 'HealthBoards' / 'Clinics' / 'Hosptials' etc.
        /// </summary>
        public CatalogueGranularity Granularity
        {
            get { return _granularity; }
            set { SetField(ref  _granularity, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Geographical_coverage
        {
            get { return _geographicalCoverage; }
            set { SetField(ref  _geographicalCoverage, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Background_summary
        {
            get { return _backgroundSummary; }
            set { SetField(ref  _backgroundSummary, value); }
        }

        /// <summary>
        /// User specified list of keywords that are intended to help in finding the Catalogue
        /// </summary>
        public string Search_keywords
        {
            get { return _searchKeywords; }
            set { SetField(ref  _searchKeywords, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// <seealso cref="Periodicity"/>
        /// </summary>
        public string Update_freq
        {
            get { return _updateFreq; }
            set { SetField(ref  _updateFreq, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// <seealso cref="Periodicity"/>
        /// </summary>
        public string Update_sched
        {
            get { return _updateSched; }
            set { SetField(ref  _updateSched, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// <seealso cref="Periodicity"/>
        /// </summary>
        public string Time_coverage
        {
            get { return _timeCoverage; }
            set { SetField(ref  _timeCoverage, value); }
        }

        /// <summary>
        /// User specified date that user alledgedly reviewed the contents of the Catalogue / Metadata
        /// </summary>
        public DateTime? Last_revision_date
        {
            get { return _lastRevisionDate; }
            set { SetField(ref  _lastRevisionDate, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Contact_details
        {
            get { return _contactDetails; }
            set { SetField(ref  _contactDetails, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Resource_owner
        {
            get { return _resourceOwner; }
            set { SetField(ref  _resourceOwner, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Attribution_citation
        {
            get { return _attributionCitation; }
            set { SetField(ref  _attributionCitation, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Access_options
        {
            get { return _accessOptions; }
            set { SetField(ref  _accessOptions, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string SubjectNumbers
        {
            get { return _subjectNumbers; }
            set { SetField(ref  _subjectNumbers, value); }
        }

        /// <summary>
        /// User specified field.  Supposedly a URL for a webservice for accessing the dataset? Not used for anything by RDMP.
        /// </summary>
        public Uri API_access_URL
        {
            get { return _apiAccessUrl; }
            set { SetField(ref  _apiAccessUrl, value); }
        }

        /// <summary>
        /// User specified field.  Supposedly a URL for a webservice for browsing the dataset? Not used for anything by RDMP.
        /// </summary>
        public Uri Browse_URL
        {
            get { return _browseUrl; }
            set { SetField(ref  _browseUrl, value); }
        }

        /// <summary>
        /// User specified field.  Supposedly a URL for a webservice for bulk downloading the dataset? Not used for anything by RDMP.
        /// </summary>
        public Uri Bulk_Download_URL
        {
            get { return _bulkDownloadUrl; }
            set { SetField(ref  _bulkDownloadUrl, value); }
        }

        /// <summary>
        /// User specified field.  Supposedly a URL for a webservice for querying the dataset? Not used for anything by RDMP.
        /// </summary>
        public Uri Query_tool_URL
        {
            get { return _queryToolUrl; }
            set { SetField(ref  _queryToolUrl, value); }
        }

        /// <summary>
        /// User specified field.  Supposedly a URL for a website describing where you procured the data from? Not used for anything by RDMP.
        /// </summary>
        public Uri Source_URL
        {
            get { return _sourceUrl; }
            set { SetField(ref  _sourceUrl, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Country_of_origin
        {
            get { return _countryOfOrigin; }
            set { SetField(ref  _countryOfOrigin, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Data_standards
        {
            get { return _dataStandards; }
            set { SetField(ref  _dataStandards, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Administrative_contact_name
        {
            get { return _administrativeContactName; }
            set { SetField(ref  _administrativeContactName, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Administrative_contact_email
        {
            get { return _administrativeContactEmail; }
            set { SetField(ref  _administrativeContactEmail, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Administrative_contact_telephone
        {
            get { return _administrativeContactTelephone; }
            set { SetField(ref  _administrativeContactTelephone, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Administrative_contact_address
        {
            get { return _administrativeContactAddress; }
            set { SetField(ref  _administrativeContactAddress, value); }
        }

        /// <summary>
        /// User specified field.  Not used for anything by RDMP.
        /// </summary>
        public bool? Explicit_consent
        {
            get { return _explicitConsent; }
            set { SetField(ref  _explicitConsent, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Ethics_approver
        {
            get { return _ethicsApprover; }
            set { SetField(ref  _ethicsApprover, value); }
        }

        /// <summary>
        /// User specified free text field.  Not used for anything by RDMP.
        /// </summary>
        public string Source_of_data_collection
        {
            get { return _sourceOfDataCollection; }
            set { SetField(ref  _sourceOfDataCollection, value); }
        }

        /// <summary>
        /// Identifier for a ticket in your <see cref="ITicketingSystem"/> for documenting / auditing work on the Catalogue and for 
        /// recording issues (if you are not using the RDMP issue system (see CatalogueItemIssue))
        /// </summary>
        public string Ticket
        {
            get { return _ticket; }
            set { SetField(ref _ticket, value); }
        }
        
        /// <inheritdoc/>
        [DoNotExtractProperty]
        public string LoggingDataTask
        {
            get { return _loggingDataTask; }
            set { SetField(ref  _loggingDataTask, value); }
        }

        /// <inheritdoc/>
        [DoNotExtractProperty]
        public string ValidatorXML
        {
            get { return _validatorXml; }
            set { SetField(ref  _validatorXml, value); }
        }

        /// <inheritdoc/>
        [Relationship(typeof(ExtractionInformation), RelationshipType.IgnoreableLocalReference)] //todo do we want to share this?
        [DoNotExtractProperty]
        public int? TimeCoverage_ExtractionInformation_ID
        {
            get { return _timeCoverageExtractionInformationID; }
            set { SetField(ref  _timeCoverageExtractionInformationID, value); }
        }

        /// <inheritdoc/>
        [DoNotExtractProperty]
        [Relationship(typeof(ExtractionInformation), RelationshipType.IgnoreableLocalReference)] 
        public int? PivotCategory_ExtractionInformation_ID
        {
            get { return _pivotCategoryExtractionInformationID; }
            set { SetField(ref  _pivotCategoryExtractionInformationID, value); }
        }

        /// <inheritdoc/>
        [DoNotExtractProperty]
        [DoNotImportDescriptions]
        public bool IsDeprecated
        {
            get { return _isDeprecated; }
            set { SetField(ref  _isDeprecated, value); }
        }

        /// <inheritdoc/>
        [DoNotExtractProperty]
        [DoNotImportDescriptions]
        public bool IsInternalDataset
        {
            get { return _isInternalDataset; }
            set { SetField(ref  _isInternalDataset, value); }
        }

        /// <inheritdoc/>
        [DoNotExtractProperty]
        [DoNotImportDescriptions]
        public bool IsColdStorageDataset
        {
            get { return _isColdStorageDataset; }
            set { SetField(ref  _isColdStorageDataset, value); }
        }

        /// <inheritdoc/>
        [Relationship(typeof(ExternalDatabaseServer), RelationshipType.LocalReference)]
        [DoNotExtractProperty]
        public int? LiveLoggingServer_ID
        {
            get { return _liveLoggingServerID; }
            set { SetField(ref  _liveLoggingServerID, value); }
        }
        
        /// <inheritdoc/>
        public DateTime? DatasetStartDate
        {
            get { return _datasetStartDate; }
            set { SetField(ref  _datasetStartDate, value); }
        }

        private int? _loadMetadataId;

        /// <inheritdoc/>
        [DoNotExtractProperty]
        [Relationship(typeof(LoadMetadata), RelationshipType.OptionalSharedObject)]
        public int? LoadMetadata_ID
        {
            get { return _loadMetadataId; }
            set { SetField(ref _loadMetadataId , value);}
        }
        #endregion

        #region Relationships
        /// <inheritdoc/>
        [NoMappingToDatabase]
        public CatalogueItem[] CatalogueItems
        {
            get
            {
                return _knownCatalogueItems.Value;
            }
        }

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public LoadMetadata LoadMetadata
        {
            get
            {
                if (LoadMetadata_ID == null)
                    return null;

                return Repository.GetObjectByID<LoadMetadata>((int) LoadMetadata_ID);
            }
        }

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public AggregateConfiguration[] AggregateConfigurations
        {
            get { return Repository.GetAllObjectsWithParent<AggregateConfiguration>(this); }
        }

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public ExternalDatabaseServer LiveLoggingServer
        {
            get
            {
                return LiveLoggingServer_ID == null
                    ? null
                    : Repository.GetObjectByID<ExternalDatabaseServer>((int)LiveLoggingServer_ID);
            }
        }
        
        /// <inheritdoc/>
        [NoMappingToDatabase]
        public ExtractionInformation TimeCoverage_ExtractionInformation {
            get
            {
                return TimeCoverage_ExtractionInformation_ID == null
                    ? null
                    : Repository.GetObjectByID<ExtractionInformation>(TimeCoverage_ExtractionInformation_ID.Value);
            }
        }

        /// <inheritdoc/>
        [NoMappingToDatabase]
        public ExtractionInformation PivotCategory_ExtractionInformation
        {
            get
            {
                return PivotCategory_ExtractionInformation_ID == null
                    ? null
                    : Repository.GetObjectByID<ExtractionInformation>(PivotCategory_ExtractionInformation_ID.Value);
            }
        }

        #endregion

        #region Enums
        /// <summary>
        /// Somewhat arbitrary concepts for defining the limitations of a Catalogues data
        /// </summary>
        public enum CatalogueType
        {
            /// <summary>
            /// No CatalogueType has been specified
            /// </summary>
            Unknown,
            
            /// <summary>
            /// Catalogue data relates to a research study
            /// </summary>
            ResearchStudy,

            /// <summary>
            /// Catalogue data relates to or defines a Cohort
            /// </summary>
            Cohort,

            /// <summary>
            /// Catalogue data is collected by a national registry
            /// </summary>
            NationalRegistry, 

            /// <summary>
            /// Catalogue data is collected by a healthcare provider
            /// </summary>
            HealthcareProviderRegistry,

            /// <summary>
            /// Catalogue data can be classified as Electronic Health Records (prescriptions, hospital records etc.)
            /// </summary>
            EHRExtract
        }

        /// <summary>
        /// Notional user declared period on which the data in the Catalogue is refreshed.  This may not have any bearing
        /// on reality.  Not used by RDMP for any technical processes.
        /// </summary>
        public enum CataloguePeriodicity
        {
            /// <summary>
            /// No period for the dataset has been specified
            /// </summary>
            Unknown,

            /// <summary>
            /// Data is updated on a daily basis
            /// </summary>
            Daily,
            /// <summary>
            /// Data is updated on a weekly basis
            /// </summary>
            Weekly,
            /// <summary>
            /// Data is updated every 2 weeks
            /// </summary>
            Fortnightly,
            /// <summary>
            /// Data is updated every month
            /// </summary>
            Monthly,

            /// <summary>
            /// Data is updated every 2 months
            /// </summary>
            BiMonthly,

            /// <summary>
            /// Data is updated every 4 months
            /// </summary>
            Quarterly,

            /// <summary>
            /// Data is updated on a yearly basis
            /// </summary>
            Yearly
        }

        /// <summary>
        /// Notional user declared boundary for the dataset defined by the Catalogue.  The data should be isolated to this Granularity
        /// </summary>
        public enum CatalogueGranularity
        {
            /// <summary>
            /// No granularity has been specified
            /// </summary>
            Unknown,
            
            /// <summary>
            /// Contains data relating to multiple nations
            /// </summary>
            National,

            /// <summary>
            /// Contains data relating to multiple regions (e.g. Scotland / England)
            /// </summary>
            Regional,

            /// <summary>
            /// Contains data relating to multiple healthboards (e.g. Tayside / Fife)
            /// </summary>
            HealthBoard,

            /// <summary>
            /// Contains data relating to multiple hospitals (e.g. Ninewells)
            /// </summary>
            Hospital,

            /// <summary>
            /// Contains data relating to multiple clinics (e.g. Radiology)
            /// </summary>
            Clinic
        }
        #endregion

        /// <summary>
        /// Declares a new empty virtual dataset with the given Name.  This will not have any virtual columns and will not be tied to any underlying tables.  
        /// 
        /// <para>The preferred method of getting a Catalogue is to use <see cref="TableInfoImporter"/> and <see cref="ForwardEngineerCatalogue"/></para>
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="name"></param>
        public Catalogue(ICatalogueRepository repository, string name)
        {
            var loggingServer = repository.GetServerDefaults().GetDefaultFor(PermissableDefaults.LiveLoggingServer_ID);

            repository.InsertAndHydrate(this,new Dictionary<string, object>()
            {
                {"Name",name},
                {"LiveLoggingServer_ID",loggingServer == null ? (object) DBNull.Value:loggingServer.ID}
            });

            if (ID == 0 || string.IsNullOrWhiteSpace(Name) || Repository != repository)
                throw new ArgumentException("Repository failed to properly hydrate this class");

            //default values
            if(Folder == null)
                Folder = new CatalogueFolder(this, "\\");
            
            //if there is a default logging server
            if (LiveLoggingServer_ID == null)
            {
                var liveLoggingServer = repository.GetServerDefaults().GetDefaultFor(PermissableDefaults.LiveLoggingServer_ID);
                
                if(liveLoggingServer != null)
                    LiveLoggingServer_ID = liveLoggingServer.ID;
            }
            

            ClearAllInjections();
        }

        /// <summary>
        /// Creates a single runtime instance of the Catalogue based on the current state of the row read from the DbDataReader (does not advance the reader)
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="r"></param>
        internal Catalogue(ICatalogueRepository repository, DbDataReader r)
            : base(repository, r)
        {
            if(r["LoadMetadata_ID"] != DBNull.Value)
                LoadMetadata_ID = int.Parse(r["LoadMetadata_ID"].ToString()); 
          
            Acronym = r["Acronym"].ToString();
            Name = r["Name"].ToString();
            Description = r["Description"].ToString();

            //detailed info url with support for invalid urls
            Detail_Page_URL = ParseUrl(r, "Detail_Page_URL");

            LoggingDataTask = r["LoggingDataTask"] as string;

            if (r["LiveLoggingServer_ID"] == DBNull.Value)
                LiveLoggingServer_ID = null;
            else
                LiveLoggingServer_ID = (int) r["LiveLoggingServer_ID"];
            
            ////Type - with handling for invalid enum values listed in database
            object type = r["Type"];
            if (type == null || type == DBNull.Value)
                Type = CatalogueType.Unknown;
            else
            {
                CatalogueType typeAsEnum;

                if (CatalogueType.TryParse(type.ToString(), true, out typeAsEnum))
                    Type = typeAsEnum;
                else
                    throw new Exception(" r[\"Type\"] had value " + type + " which is not contained in Enum CatalogueType");
                    
            }

            //Periodicity - with handling for invalid enum values listed in database
            object periodicity = r["Periodicity"];
            if (periodicity == null || periodicity == DBNull.Value)
                Periodicity = CataloguePeriodicity.Unknown;
            else
            {
                CataloguePeriodicity periodicityAsEnum;

                if (CataloguePeriodicity.TryParse(periodicity.ToString(), true, out periodicityAsEnum))
                    Periodicity = periodicityAsEnum;
                else
                {
                    throw new Exception(" r[\"Periodicity\"] had value " + periodicity + " which is not contained in Enum CataloguePeriodicity");
                }
            }

            object granularity = r["Granularity"];
            if (granularity == null || granularity == DBNull.Value)
                Granularity = CatalogueGranularity.Unknown;
            else
            {
                CatalogueGranularity granularityAsEnum;

                if (CatalogueGranularity.TryParse(granularity.ToString(), true, out granularityAsEnum))
                    Granularity = granularityAsEnum;
                else
                    throw new Exception(" r[\"granularity\"] had value " + granularity + " which is not contained in Enum CatalogueGranularity");
              
            }

            Geographical_coverage = r["Geographical_coverage"].ToString();
            Background_summary = r["Background_summary"].ToString();
            Search_keywords = r["Search_keywords"].ToString();
            Update_freq = r["Update_freq"].ToString();
            Update_sched = r["Update_sched"].ToString();
            Time_coverage = r["Time_coverage"].ToString();
            SubjectNumbers = r["SubjectNumbers"].ToString();

            object dt = r["Last_revision_date"];
            if (dt == null || dt == DBNull.Value)
                Last_revision_date = null;
            else
                Last_revision_date = (DateTime)dt;

            Contact_details = r["Contact_details"].ToString();
            Resource_owner = r["Resource_owner"].ToString();
            Attribution_citation = r["Attribution_citation"].ToString();
            Access_options = r["Access_options"].ToString();
            
            Country_of_origin = r["Country_of_origin"].ToString();
            Data_standards = r["Data_standards"].ToString();
            Administrative_contact_name = r["Administrative_contact_name"].ToString();
            Administrative_contact_email = r["Administrative_contact_email"].ToString();
            Administrative_contact_telephone = r["Administrative_contact_telephone"].ToString();
            Administrative_contact_address = r["Administrative_contact_address"].ToString();
            Ethics_approver = r["Ethics_approver"].ToString();
            Source_of_data_collection = r["Source_of_data_collection"].ToString();

            if (r["Explicit_consent"] != null && r["Explicit_consent"]!= DBNull.Value)
                Explicit_consent = (bool)r["Explicit_consent"];

            TimeCoverage_ExtractionInformation_ID = ObjectToNullableInt(r["TimeCoverage_ExtractionInformation_ID"]);
            PivotCategory_ExtractionInformation_ID = ObjectToNullableInt(r["PivotCategory_ExtractionInformation_ID"]);

            object oDatasetStartDate = r["DatasetStartDate"];
            if (oDatasetStartDate == null || oDatasetStartDate == DBNull.Value)
                DatasetStartDate = null;
            else
                DatasetStartDate = (DateTime) oDatasetStartDate;

            
            ValidatorXML = r["ValidatorXML"] as string;

            Ticket = r["Ticket"] as string;

            //detailed info url with support for invalid urls
            API_access_URL = ParseUrl(r, "API_access_URL");
            Browse_URL = ParseUrl(r, "Browse_URL" );
            Bulk_Download_URL = ParseUrl(r, "Bulk_Download_URL");
            Query_tool_URL = ParseUrl(r, "Query_tool_URL");
            Source_URL = ParseUrl(r, "Source_URL");
            IsDeprecated = (bool) r["IsDeprecated"];
            IsInternalDataset = (bool)r["IsInternalDataset"];
            IsColdStorageDataset = (bool) r["IsColdStorageDataset"];

            Folder = new CatalogueFolder(this,r["Folder"].ToString());

            ClearAllInjections();
        }
        
        internal Catalogue(ShareManager shareManager, ShareDefinition shareDefinition)
        {
            shareManager.UpsertAndHydrate(this,shareDefinition);
            ClearAllInjections();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Sorts alphabetically based on <see cref="Name"/>
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            if (obj is Catalogue)
            {
                return -(obj.ToString().CompareTo(this.ToString())); //sort alphabetically (reverse)
            }

            throw new Exception("Cannot compare " + this.GetType().Name + " to " + obj.GetType().Name);
        }

        /// <summary>
        /// Checks that the Catalogue has a sensible Name (See <see cref="IsAcceptableName(string)"/>).  Then checks that there are no missing ColumnInfos 
        /// </summary>
        /// <param name="notifier"></param>
        public void Check(ICheckNotifier notifier)
        {
            string reason;

            if (!IsAcceptableName(Name, out reason))
                notifier.OnCheckPerformed(
                    new CheckEventArgs(
                        "Catalogue name " + Name + " (ID=" + ID + ") does not follow naming conventions reason:" + reason,
                        CheckResult.Fail));
            else
                notifier.OnCheckPerformed(new CheckEventArgs("Catalogue name " + Name + " follows naming conventions ",CheckResult.Success));
            
            ITableInfo[] tables = GetTableInfoList(true);
            foreach (TableInfo t in tables)
                t.Check(notifier);

            ExtractionInformation[] extractionInformations = this.GetAllExtractionInformation(ExtractionCategory.Core);
            
            if (extractionInformations.Any())
            {
                bool missingColumnInfos = false;

                foreach (ExtractionInformation missingColumnInfo in extractionInformations.Where(e=>e.ColumnInfo == null))
                {
                    notifier.OnCheckPerformed(
                        new CheckEventArgs(
                            "ColumnInfo behind ExtractionInformation/CatalogueItem " +
                            missingColumnInfo.GetRuntimeName() + " is MISSING, it must have been deleted",
                            CheckResult.Fail));
                    missingColumnInfos = true;
                }

                if (missingColumnInfos)
                    return;

                notifier.OnCheckPerformed(
                    new CheckEventArgs(
                        "Found " + extractionInformations.Length +
                        " ExtractionInformation(s), preparing to validate SQL with QueryBuilder", CheckResult.Success));

                var accessContext = DataAccessContext.InternalDataProcessing;

                try
                {
                    var server = DataAccessPortal.GetInstance().ExpectDistinctServer(tables, accessContext, false);
                
                    using (var con = server.GetConnection())
                    {
                        con.Open();
                        
                        string sql;
                        try
                        {
                            QueryBuilder qb = new QueryBuilder(null, null);
                            qb.TopX = 1;
                            qb.AddColumnRange(extractionInformations);
                    
                            sql = qb.SQL;
                            notifier.OnCheckPerformed(new CheckEventArgs("Query Builder assembled the following SQL:" + Environment.NewLine + sql, CheckResult.Success));
                        }
                        catch (Exception e)
                        {
                            notifier.OnCheckPerformed(
                                new CheckEventArgs("Could not generate extraction SQL for Catalogue " + this,
                                    CheckResult.Fail, e));
                            return;
                        }
                
                        var cmd = DatabaseCommandHelper.GetCommand(sql, con);
                        cmd.CommandTimeout = 10;
                        DbDataReader r = cmd.ExecuteReader();

                        if (r.Read())
                            notifier.OnCheckPerformed(new CheckEventArgs("successfully read a row of data from the extraction SQL of Catalogue " + this,CheckResult.Success));
                        else
                            notifier.OnCheckPerformed(new CheckEventArgs("The query produced an empty result set for Catalogue" + this, CheckResult.Warning));
                    
                        con.Close();
                    }
                }
                catch (Exception e)
                {
                    notifier.OnCheckPerformed(
                        new CheckEventArgs(
                            "Extraction SQL Checking failed for Catalogue " + this +
                            " make sure that you can access the underlying server under DataAccessContext." +
                            accessContext +
                            " and that the SQL generated runs correctly (see internal exception for details)",
                            CheckResult.Fail, e));
                }
            }

            //supporting documents
            var f = new SupportingDocumentsFetcher(this);
            f.Check(notifier);
        }

        /// <inheritdoc/>
        public ITableInfo[] GetTableInfoList(bool includeLookupTables)
        {
            List<ITableInfo> normalTables, lookupTables;
            GetTableInfos(out normalTables, out lookupTables);

            if (includeLookupTables)
                return normalTables.Union(lookupTables).ToArray();

            return normalTables.ToArray();
        }

        /// <inheritdoc/>
        public ITableInfo[] GetLookupTableInfoList()
        {
            List<ITableInfo> normalTables, lookupTables;
            GetTableInfos(out normalTables, out lookupTables);

            return lookupTables.ToArray();
        }
        
        /// <inheritdoc/>
        public void GetTableInfos(out List<ITableInfo> normalTables, out List<ITableInfo> lookupTables)
        {
            var tables = GetColumnInfos().GroupBy(c=>c.TableInfo_ID).Select(c => c.First().TableInfo).ToArray();

            normalTables = new List<ITableInfo>(tables.Where(t=>!t.IsLookupTable()));
            lookupTables = new List<ITableInfo>(tables.Where(t=>t.IsLookupTable()));
        }

        private IEnumerable<ColumnInfo> GetColumnInfos()
        {
            if (CatalogueItems.All(ci => ci.IsColumnInfoCached()))
                return CatalogueItems.Select(ci => ci.ColumnInfo).Where(col => col != null);

            return Repository.GetAllObjectsInIDList<ColumnInfo>(CatalogueItems.Where(ci => ci.ColumnInfo_ID.HasValue).Select(ci => ci.ColumnInfo_ID.Value).Distinct().ToList());
        }

        /// <inheritdoc/>
        public ExtractionFilter[] GetAllMandatoryFilters()
        {
             return GetAllExtractionInformation(ExtractionCategory.Any).SelectMany(f=>f.ExtractionFilters).Where(f=>f.IsMandatory).ToArray();
        }

        /// <inheritdoc/>
        public ExtractionFilter[] GetAllFilters()
        {
            return GetAllExtractionInformation(ExtractionCategory.Any).SelectMany(f => f.ExtractionFilters).ToArray();
        }
        
        /// <inheritdoc/>
        public DiscoveredServer GetDistinctLiveDatabaseServer(DataAccessContext context, bool setInitialDatabase, out IDataAccessPoint distinctAccessPoint)
        {
            var tables = GetTableInfosIdeallyJustFromMainTables();

            distinctAccessPoint = tables.FirstOrDefault();

            return DataAccessPortal.GetInstance().ExpectDistinctServer(tables, context, setInitialDatabase);
        }

        /// <inheritdoc/>
        public DiscoveredServer GetDistinctLiveDatabaseServer(DataAccessContext context, bool setInitialDatabase)
        {
            return DataAccessPortal.GetInstance().ExpectDistinctServer(GetTableInfosIdeallyJustFromMainTables(), context, setInitialDatabase);
        }

        private ITableInfo[] GetTableInfosIdeallyJustFromMainTables()
        {

            //try with only the normal tables
            var tables = GetTableInfoList(false);

            //there are no normal tables!
            if (!tables.Any())
                tables = GetTableInfoList(true);

            return tables;
        }

        /// <inheritdoc/>
        public DatabaseType? GetDistinctLiveDatabaseServerType()
        {
            var tables = GetTableInfosIdeallyJustFromMainTables();

            var type = tables.Select(t => t.DatabaseType).Distinct().ToArray();

            if (type.Length == 0)
                return null;

            if (type.Length == 1)
                return type[0];

            throw new AmbiguousDatabaseTypeException("The Catalogue '" + this + "' has TableInfos belonging to multiple DatabaseTypes (" + string.Join(",",tables.Select(t=>t.GetRuntimeName()  +"(ID=" +t.ID + " is " + t.DatabaseType +")")));
        }

        /// <summary>
        /// Use to set LoadMetadata to null without first performing Disassociation checks.  This should only be used for in-memory operations such as cloning
        /// This (if saved to the original database it was read from) could create orphans - load stages that relate to the disassociated catalogue.  But if 
        /// you are cloning a catalogue and dropping the LoadMetadata then you wont be saving the dropped state to the original database ( you will be saving it
        /// to the clone database so it won't be a problem).
        /// </summary>
        public void HardDisassociateLoadMetadata()
        {
            _loadMetadataId = null;
        }

        /// <summary>
        /// Gets the <see cref="LogManager"/> for logging load events related to this Catalogue / it's LoadMetadata (if it has one).  This will throw if no
        /// logging server has been configured.
        /// </summary>
        /// <returns></returns>
        public LogManager GetLogManager()
        {
            if(LiveLoggingServer_ID == null) 
                throw new Exception("No live logging server set for Catalogue " + this.Name);
                
            var server = DataAccessPortal.GetInstance().ExpectServer(LiveLoggingServer, DataAccessContext.Logging);

            return new LogManager(server);
        }
        
        /// <inheritdoc/>
        public IHasDependencies[] GetObjectsThisDependsOn()
        {
            List<IHasDependencies> iDependOn = new List<IHasDependencies>();

            iDependOn.AddRange(CatalogueItems);
            
            if(LoadMetadata != null)
                iDependOn.Add(LoadMetadata);

            return iDependOn.ToArray();
        }

        /// <inheritdoc/>
        public IHasDependencies[] GetObjectsDependingOnThis()
        {
            return AggregateConfigurations;
        }

        /// <inheritdoc/>
        public SupportingDocument[] GetAllSupportingDocuments(FetchOptions fetch)
        {
            return Repository.GetAllObjects<SupportingDocument>().Where(o => Fetch(o, fetch)).ToArray();
        }
        
        /// <inheritdoc/>
        public SupportingSQLTable[] GetAllSupportingSQLTablesForCatalogue(FetchOptions fetch)
        {
            return Repository.GetAllObjects<SupportingSQLTable>().Where(o=>Fetch(o,fetch)).ToArray();
        }

        private bool Fetch(ISupportingObject o, FetchOptions fetch)
        {
            switch (fetch)
            {
                case FetchOptions.AllGlobals:
                    return o.IsGlobal;
                case FetchOptions.ExtractableGlobalsAndLocals:
                    return (o.Catalogue_ID == ID || o.IsGlobal) && o.Extractable;
                case FetchOptions.ExtractableGlobals:
                    return o.IsGlobal && o.Extractable;
                case FetchOptions.AllLocals:
                    return o.Catalogue_ID == ID && !o.IsGlobal;
               case FetchOptions.ExtractableLocals:
                    return o.Catalogue_ID == ID && o.Extractable && !o.IsGlobal;
                case FetchOptions.AllGlobalsAndAllLocals:
                    return o.Catalogue_ID == ID || o.IsGlobal;
                default:
                    throw new ArgumentOutOfRangeException("fetch");
            }
        }


        private string GetFetchSQL<T>(FetchOptions fetch) where T:IMapsDirectlyToDatabaseTable
        {
            switch (fetch)
            {
                case FetchOptions.AllGlobals:
                    return "WHERE IsGlobal=1";
                case FetchOptions.ExtractableGlobalsAndLocals:
                    return  "WHERE (Catalogue_ID=" + ID + " OR IsGlobal=1) AND Extractable=1";
                  case FetchOptions.ExtractableGlobals:
                    return  "WHERE IsGlobal=1 AND Extractable=1";
                    
                case FetchOptions.AllLocals:
                    return  "WHERE Catalogue_ID=" + ID + "  AND IsGlobal=0";//globals still retain their Catalogue_ID incase the configurer removes the global attribute in which case they revert to belonging to that Catalogue as a local
                    
                case FetchOptions.ExtractableLocals:
                    return  "WHERE Catalogue_ID=" + ID + " AND Extractable=1 AND IsGlobal=0";
                    
                case FetchOptions.AllGlobalsAndAllLocals:
                    return  "WHERE Catalogue_ID=" + ID + " OR IsGlobal=1";
                    
                default:
                    throw new ArgumentOutOfRangeException("fetch");
            }
        }

        /// <inheritdoc/>
        public ExtractionInformation[] GetAllExtractionInformation(ExtractionCategory category)
        {
            return
                CatalogueItems.Select(ci => ci.ExtractionInformation)
                    .Where(e => e != null &&
                        (e.ExtractionCategory == category || category == ExtractionCategory.Any))
                    .ToArray();
        }

        private CatalogueExtractabilityStatus _extractabilityStatus;

        /// <summary>
        /// Records the known extractability status (as a cached answer for <see cref="GetExtractabilityStatus"/>)
        /// </summary>
        /// <param name="instance"></param>
        public void InjectKnown(CatalogueExtractabilityStatus instance)
        {
            _extractabilityStatus = instance;
        }

        /// <inheritdoc/>
        public void InjectKnown(CatalogueItem[] instance)
        {
            _knownCatalogueItems = new Lazy<CatalogueItem[]>(() => instance);
        }

        /// <summary>
        /// Cleares the cached answer of <see cref="GetExtractabilityStatus"/>
        /// </summary>
        public void ClearAllInjections()
        {
            _extractabilityStatus = null;
            _knownCatalogueItems = new Lazy<CatalogueItem[]>(() => Repository.GetAllObjectsWithParent<CatalogueItem,Catalogue>(this));
        }

        /// <inheritdoc/>
        public CatalogueExtractabilityStatus GetExtractabilityStatus(IDataExportRepository dataExportRepository)
        {
            if (_extractabilityStatus != null)
                return _extractabilityStatus;

            if (dataExportRepository == null)
                return null;

            _extractabilityStatus = dataExportRepository.GetExtractabilityStatus(this);
            return _extractabilityStatus;
        }

        /// <summary>
        /// Returns true if the Catalogue is extractable but only with a specific Project.  You can pass null if you are addressing a Catalouge for whom you know 
        /// IInjectKnown&lt;CatalogueExtractabilityStatus> has been called already.
        /// </summary>
        /// <param name="dataExportRepository"></param>
        /// <returns></returns>
        public bool IsProjectSpecific(IDataExportRepository dataExportRepository)
        {
            var e = GetExtractabilityStatus(dataExportRepository);
            return e != null && e.IsProjectSpecific;
        }

        /// <summary>
        /// Gets an IQuerySyntaxHelper for the <see cref="GetDistinctLiveDatabaseServerType"/> amongst all underlying <see cref="TableInfo"/>.  This can be used to assist query building.
        /// </summary>
        /// <returns></returns>
        public IQuerySyntaxHelper GetQuerySyntaxHelper()
        {
            var f = new QuerySyntaxHelperFactory();
            var type = GetDistinctLiveDatabaseServerType();

            if(type == null)
                throw new AmbiguousDatabaseTypeException("Catalogue '" + this +"' has no extractable columns so no Database Type could be determined");
            
            return f.Create(type.Value);
        }

        #region Static Methods
        /// <summary>
        /// Returns true if the given name would be sensible for a Catalogue.  This means no slashes, hashes @ symbols etc and other things which make XML serialization hard
        /// or prevent naming a database table after a Catalogue (all things we might want to do with the <see cref="Catalogue.Name"/>).
        /// </summary>
        /// <param name="name"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public static bool IsAcceptableName(string name, out string reason)
        {
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                reason = "Name cannot be blank";
                return false;
            }

            var invalidCharacters = name.Where(c => Path.GetInvalidPathChars().Contains(c) || c == '\\' || c == '/' || c == '.' || c == '#' || c == '@' || c == '$').ToArray();
            if (invalidCharacters.Any())
            {
                reason = "The following invalid characters were found:" + string.Join(",", invalidCharacters.Select(c => "'" + c + "'"));
                return false;
            }

            reason = null;
            return true;
        }

        /// <inheritdoc cref="Catalogue.IsAcceptableName(string,out string)"/>
        public static bool IsAcceptableName(string name)
        {
            string whoCares;
            return IsAcceptableName(name, out whoCares);
        }
        #endregion

        /// <summary>
        /// Provides a new instance of the object (in the database).  Properties will be copied from this object (child objects will not be created).
        /// </summary>
        /// <returns></returns>
        public Catalogue ShallowClone()
        {
            var clone = new Catalogue(CatalogueRepository, Name + " Clone");
            CopyShallowValuesTo(clone);
            return clone;
        }
    }
}
