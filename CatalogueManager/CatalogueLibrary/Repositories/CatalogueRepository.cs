using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.Data.Cache;
using CatalogueLibrary.Data.Cohort;
using CatalogueLibrary.Data.ImportExport;
using CatalogueLibrary.Data.Referencing;
using CatalogueLibrary.Data.Serialization;
using CatalogueLibrary.Properties;
using CatalogueLibrary.Repositories.Construction;
using HIC.Logging;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Attributes;
using ReusableLibraryCode;
using ReusableLibraryCode.Comments;

namespace CatalogueLibrary.Repositories
{
    /// <summary>
    /// Pointer to the Catalogue Repository database in which all DatabaseEntities declared in CatalogueLibrary.dll are stored.  Ever DatabaseEntity class must exist in a
    /// Microsoft Sql Server Database (See DatabaseEntity) and each object is compatible only with a specific type of TableRepository (i.e. the database that contains the
    /// table matching their name).  CatalogueLibrary.dll objects in CatalogueRepository, DataExportLibrary.dll objects in DataExportRepository, DataQualityEngine.dll objects
    /// in DQERepository etc.
    /// 
    /// <para>This class allows you to fetch objects and should be passed into constructors of classes you want to construct in the Catalogue database.  </para>
    /// 
    /// <para>It also includes helper properties for setting up relationships and controling records in the non DatabaseEntity tables in the database e.g. AggregateForcedJoiner</para>
    /// </summary>
    public class CatalogueRepository : TableRepository, ICatalogueRepository
    {
        /// <inheritdoc/>
        public AggregateForcedJoin AggregateForcedJoiner { get; set; }

        /// <inheritdoc/>
        public TableInfoToCredentialsLinker TableInfoToCredentialsLinker { get; set; }

        /// <inheritdoc/>
        public PasswordEncryptionKeyLocation PasswordEncryptionKeyLocation { get; set; }

        /// <inheritdoc/>
        public JoinInfoFinder JoinInfoFinder { get; set; }

        /// <inheritdoc/>
        public MEF MEF { get; set; }
        
        readonly ObjectConstructor _constructor = new ObjectConstructor();

        /// <inheritdoc/>
        public CommentStore CommentStore { get; set; }
        
        /// <summary>
        /// By default CatalogueRepository will execute DocumentationReportMapsDirectlyToDatabase which will load all the Types and find documentation in the source code for 
        /// them obviously this affects test performance so set this to true if you want it to skip this process.  Note where this is turned on, it's in the static constructor
        /// of DatabaseTests which means if you stick a static constructor in your test you can override it if you need access to the help text somehow in your test
        /// </summary>
        public static bool SuppressHelpLoading;

        /// <summary>
        /// Sets up an <see cref="IRepository"/> which connects to the database <paramref name="catalogueConnectionString"/> to fetch/create <see cref="DatabaseEntity"/> objects.
        /// </summary>
        /// <param name="catalogueConnectionString"></param>
        public CatalogueRepository(DbConnectionStringBuilder catalogueConnectionString): base(null,catalogueConnectionString)
        {
            AggregateForcedJoiner = new AggregateForcedJoin(this);
            TableInfoToCredentialsLinker = new TableInfoToCredentialsLinker(this);
            PasswordEncryptionKeyLocation = new PasswordEncryptionKeyLocation(this);
            JoinInfoFinder = new JoinInfoFinder(this);
            MEF = new MEF();
            
            ObscureDependencyFinder = new CatalogueObscureDependencyFinder(this);
        }

        /// <summary>
        /// Initializes and loads <see cref="CommentStore"/> with all the xml doc/dll files found in the provided <paramref name="directories"/> 
        /// </summary>
        /// <param name="directories"></param>
        public void LoadHelp(params string[] directories)
        {
            if (!SuppressHelpLoading)
            {
                CommentStore = new CommentStore();
                CommentStore.ReadComments(directories);
                AddToHelp(Resources.KeywordHelp);
            }
        }

        private void AddToHelp(string keywordHelpFileContents)
        {
            //null is true for us loading help
            if (SuppressHelpLoading)
                return;

            var lines = keywordHelpFileContents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var split = line.Split(':');

                if (split.Length != 2)
                    throw new Exception("Malformed line in Resources.KeywordHelp, line is:" + Environment.NewLine + line + Environment.NewLine + "We expected it to have exactly one colon in it");

                if (!CommentStore.ContainsKey(split[0]))
                    CommentStore.Add(split[0], split[1]);
            }
        }
        
        /// <summary>
        /// If the configuration is part of any aggregate container anywhere this method will return the order within that container
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public int? GetOrderIfExistsFor(AggregateConfiguration configuration)
        {
            if (configuration.Repository != this)
                if (((CatalogueRepository)configuration.Repository).ConnectionString != ConnectionString)
                    throw new NotSupportedException("AggregateConfiguration is from a different repository than this with a different connection string");

            using (var con = GetConnection())
            {
                DbCommand cmd = DatabaseCommandHelper.GetCommand("SELECT [Order] FROM CohortAggregateContainer_AggregateConfiguration WHERE AggregateConfiguration_ID = @AggregateConfiguration_ID", con.Connection, con.Transaction);

                cmd.Parameters.Add(DatabaseCommandHelper.GetParameter("@AggregateConfiguration_ID", cmd));
                cmd.Parameters["@AggregateConfiguration_ID"].Value = configuration.ID;

                return ObjectToNullableInt(cmd.ExecuteScalar());
            }
        }
        
        
        /// <inheritdoc/>
        public LogManager GetDefaultLogManager()
        {
            ServerDefaults defaults = new ServerDefaults(this);
            return new LogManager(defaults.GetDefaultFor(ServerDefaults.PermissableDefaults.LiveLoggingServer_ID));
        }

        /// <inheritdoc/>
        public Catalogue[] GetAllCatalogues(bool includeDeprecatedCatalogues = false)
        {
            return GetAllObjects<Catalogue>().Where(cata => (!cata.IsDeprecated) || includeDeprecatedCatalogues).ToArray();
        }

        /// <inheritdoc/>
        public Catalogue[] GetAllCataloguesWithAtLeastOneExtractableItem()
        {
            return
                GetAllObjects<Catalogue>(
                    @"WHERE exists (select 1 from CatalogueItem ci where Catalogue_ID = Catalogue.ID AND exists (select 1 from ExtractionInformation where CatalogueItem_ID = ci.ID)) ")
                    .ToArray();
        }

        /// <inheritdoc/>
        public IEnumerable<AnyTableSqlParameter> GetAllParametersForParentTable(IMapsDirectlyToDatabaseTable parent)
        {
            var type = parent.GetType();

            if (!AnyTableSqlParameter.IsSupportedType(type))
                throw new NotSupportedException("This table does not support parents of type " + type.Name);

            return GetReferencesTo<AnyTableSqlParameter>(parent);
        }

        /// <inheritdoc/>
        public TicketingSystemConfiguration GetTicketingSystem()
        {
            var configuration = GetAllObjects<TicketingSystemConfiguration>().Where(t => t.IsActive).ToArray();

            if (configuration.Length == 0)
                return null;

            if (configuration.Length == 1)
                return configuration[0];

            throw new NotSupportedException("There should only ever be one active ticketing system, something has gone very wrong, there are currently " + configuration.Length);
        }
        
        protected override IMapsDirectlyToDatabaseTable ConstructEntity(Type t, DbDataReader reader)
        {
            return _constructor.ConstructIMapsDirectlyToDatabaseObject<ICatalogueRepository>(t, this, reader);
        }

        protected override bool IsCompatibleType(Type type)
        {
            return typeof (DatabaseEntity).IsAssignableFrom(type);
        }

        public ExternalDatabaseServer[] GetAllTier2Databases(Tier2DatabaseType type)
        {
            var servers = GetAllObjects<ExternalDatabaseServer>();
            string assembly;

            switch (type)
            {
                case Tier2DatabaseType.Logging:
                    assembly = "HIC.Logging.Database";
                    break;
                case Tier2DatabaseType.DataQuality:
                    assembly = "DataQualityEngine.Database";
                    break;
                case Tier2DatabaseType.QueryCaching:
                    assembly = "QueryCaching.Database";
                    break;
                case Tier2DatabaseType.ANOStore:
                    assembly = "ANOStore.Database";
                    break;
                case Tier2DatabaseType.IdentifierDump:
                    assembly = "IdentifierDump.Database";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }

            return servers.Where(s => s.CreatedByAssembly == assembly).ToArray();
        }

        public void UpsertAndHydrate<T>(T toCreate, ShareManager shareManager, ShareDefinition shareDefinition) where T : class,IMapsDirectlyToDatabaseTable
        {
            //Make a dictionary of the normal properties we are supposed to be importing
            Dictionary<string,object> propertiesDictionary = shareDefinition.GetDictionaryForImport();

            //for finding properties decorated with [Relationship]
            var finder = new AttributePropertyFinder<RelationshipAttribute>(toCreate);
            
            //If we have already got a local copy of this shared object?
            //either as an import or as an export
            T actual = (T)shareManager.GetExistingImportObject(shareDefinition.SharingGuid) ?? (T)shareManager.GetExistingExportObject(shareDefinition.SharingGuid);
            
            //we already have a copy imported of the shared object
            if (actual != null)
            {
                //It's an UPDATE i.e. take the new shared properties and apply them to the database copy / memory copy

                //copy all the values out of the share definition / database copy
                foreach (PropertyInfo prop in GetPropertyInfos(typeof(T)))
                {
                    //don't update any ID columns or any with relationships on UPDATE
                    if (propertiesDictionary.ContainsKey(prop.Name) && finder.GetAttribute(prop) == null)
                    {
                        SetValue(prop, propertiesDictionary[prop.Name], toCreate);
                    }
                    else
                        prop.SetValue(toCreate, prop.GetValue(actual)); //or use the database one if it isn't shared (e.g. ID, MyParent_ID etc)

                }

                toCreate.Repository = actual.Repository;
                
                //commit the updated values to the database
                SaveToDatabase(toCreate);
            }
            else
            {
                //It's an INSERT i.e. create a new database copy with the correct foreign key values and update the memory copy
                
                //for each relationship property on the class we are trying to hydrate
                foreach (PropertyInfo property in GetPropertyInfos(typeof(T)))
                {
                    RelationshipAttribute relationshipAttribute = finder.GetAttribute(property);

                    //if it has a relationship attribute then we would expect the ShareDefinition to include a dependency relationship with the sharing UID of the parent
                    //and also that we had already imported it since dependencies must be imported in order
                    if(relationshipAttribute != null)
                    {
                        int? newValue;

                        switch (relationshipAttribute.Type)
                        {
                            case RelationshipType.SharedObject:
                                //Confirm that the share definition includes the knowledge that theres a parent class to this object
                                if (!shareDefinition.RelationshipProperties.ContainsKey(relationshipAttribute))
                                    throw new Exception("Share Definition for object of Type " + typeof(T) + " is missing an expected RelationshipProperty called " + property.Name);

                                //Get the SharingUID of the parent for this property
                                Guid importGuidOfParent = shareDefinition.RelationshipProperties[relationshipAttribute];

                                //Confirm that we have a local import of the parent
                                var parentImport = shareManager.GetExistingImport(importGuidOfParent);

                                if (parentImport == null)
                                    throw new Exception("Cannot import an object of type " + typeof(T) + " because the ShareDefinition specifies a relationship to an object that has not yet been imported (A " + relationshipAttribute.Cref + " with a SharingUID of " + importGuidOfParent);

                                newValue = parentImport.ReferencedObjectID;
                                break;
                            case RelationshipType.LocalReference:
                                newValue = shareManager.GetLocalReference(property, relationshipAttribute, shareDefinition);
                                break;
                            case RelationshipType.IgnoreableLocalReference:
                                newValue = null;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        
                        //get the ID of the local import of the parent
                        if (propertiesDictionary.ContainsKey(property.Name))
                            propertiesDictionary[property.Name] = newValue;
                        else
                            propertiesDictionary.Add(property.Name,newValue);
                    }
                }

                //insert the full dictionary into the database under the Type
                InsertAndHydrate(toCreate,propertiesDictionary);

                //document that a local import of the share now exists and should be updated/reused from now on when that same GUID comes in / gets used by child objects
                shareManager.GetImportAs(shareDefinition.SharingGuid.ToString(), toCreate);
            }
        }

        public void SetValue(PropertyInfo prop, object value, IMapsDirectlyToDatabaseTable onObject)
        {
            //sometimes json decided to swap types on you e.g. int64 for int32
            var propertyType = prop.PropertyType;

            //if it is a nullable int etc
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof (Nullable<>))
                propertyType = propertyType.GetGenericArguments()[0]; //lets pretend it's just int / whatever

            if (value != null && value != DBNull.Value && !propertyType.IsInstanceOfType(value))
                if (propertyType == typeof(CatalogueFolder))
                {
                    //will be passed as a string
                    value = value is string ? new CatalogueFolder((Catalogue)onObject, (string)value):(CatalogueFolder) value;
                }
                else
                    if (typeof(Enum).IsAssignableFrom(propertyType))
                        value = Enum.ToObject(propertyType, value);//if the property is an enum
                    else
                        value = Convert.ChangeType(value, propertyType); //the property is not an enum

            prop.SetValue(onObject, value); //if it's a shared property (most properties) use the new shared value being imported
        }


        /// <summary>
        /// Returns all objects of Type T which reference the supplied object <paramref name="o"/>
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public T[] GetReferencesTo<T>(IMapsDirectlyToDatabaseTable o) where T : ReferenceOtherObjectDatabaseEntity
        {
            return GetAllObjects<T>("WHERE ReferencedObjectID = " + o.ID + " AND ReferencedObjectType = '" + o.GetType().Name + "' AND ReferencedObjectRepositoryType = '" + o.Repository.GetType().Name + "'");
        }
    }

    public enum Tier2DatabaseType
    {
        Logging,
        DataQuality,
        QueryCaching,
        ANOStore,
        IdentifierDump
    }
}
