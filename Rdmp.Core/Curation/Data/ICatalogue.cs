// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using FAnsi;
using FAnsi.Discovery;
using FAnsi.Discovery.QuerySyntax;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data.Aggregation;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.Logging;
using Rdmp.Core.Repositories;
using ReusableLibraryCode;
using ReusableLibraryCode.DataAccess;

namespace Rdmp.Core.Curation.Data
{
    /// <summary>
    /// The central class for the RDMP, a Catalogue is a virtual dataset e.g. 'Hospital Admissions'.  A Catalogue can be a merging of multiple underlying tables and exists 
    /// independent of where the data is actually stored (look at other classes like TableInfo to see the actual locations of data).
    /// 
    /// <para>As well as storing human readable names/descriptions of what is in the dataset it is the hanging off point for Attachments (SupportingDocument), validation logic, 
    /// extractable columns (CatalogueItem->ExtractionInformation->ColumnInfo) ways of filtering the data, aggregations to help understand the dataset etc.</para>
    /// 
    /// <para>Catalogues are always flat views although they can be built from multiple relational data tables underneath.</para>
    /// 
    /// </summary>
    public interface ICatalogue : IHasDependencies, IHasQuerySyntaxHelper,INamed, IMightBeDeprecated
    {
        /// <summary>
        /// The load configuration (if any) which is used to load data into the Catalogue tables.  A single <see cref="LoadMetadata"/> can load multiple Catalogues.
        /// </summary>
        int? LoadMetadata_ID { get; }

        /// <summary>
        /// Name of a task in the logging database which should be used for documenting the loading of this Catalogue. 
        /// <seealso cref="LogManager"/>
        /// </summary>
        string LoggingDataTask { get; set; }

        /// <summary>
        /// The ID of the logging server that is to be used to log data loads of the dataset <see cref="LogManager"/>
        /// </summary>
        int? LiveLoggingServer_ID { get; set; }

        /// <summary>
        /// Currently configured validation rules for columns in a Catalogue, this can be deserialized into a HIC.Common.Validation.Validator
        /// </summary>
        string ValidatorXML { get; set; }

        /// <summary>
        /// The <see cref="ExtractionInformation"/> which indicates the time field (in dataset time) of the dataset.  This should be a column in your table
        /// that indicates for every row when it became active e.g. 'PrescribedDate' for prescribing.  Try to avoid using columns that have lots of nulls or 
        /// where the date is arbitrary (e.g. 'RecordLoadedDate')
        /// </summary>
        int? TimeCoverage_ExtractionInformation_ID { get; set; }

        /// <summary>
        /// The <see cref="ExtractionInformation"/> which can provide a useful subdivision of the dataset e.g. 'Healthboard'.  This should be a logical subdivision
        /// that helps in the assesment of data quality e.g. you might imagine that if you have 10% errors in data quality and 10 healthboards knowing that all the errors
        /// are from a single healthboard would be handy.
        /// 
        /// <para>This chosen column should not have hundreds/thousands of unique values</para>
        /// </summary>
        int? PivotCategory_ExtractionInformation_ID { get; set; }

        /// <summary>
        /// Bit flag indicating whether the dataset should NEVER be extracted and ONLY EVER used internally by data analysts.
        /// </summary>
        bool IsInternalDataset { get; set; }

        /// <summary>
        /// Bit flag indicating whether the Catalogue is a seldom used dataset that should be hidden by default.  Use this if you are importing lots of researcher
        /// datasets for cohort generation / extraction but don't want them to clog up your user interface.
        /// </summary>
        bool IsColdStorageDataset { get; set; }

        /// <summary>
        /// The alledged user specified date at which data began being collected.  For a more accurate answer you should run the DQE (See also DatasetTimespanCalculator)
        /// <para>This field is optional</para>
        /// </summary>
        DateTime? DatasetStartDate { get; set; }
        
        /// <inheritdoc cref="TimeCoverage_ExtractionInformation_ID"/>
        ExtractionInformation TimeCoverage_ExtractionInformation { get; }

        /// <inheritdoc cref="PivotCategory_ExtractionInformation_ID"/>
        ExtractionInformation PivotCategory_ExtractionInformation { get; }

        /// <inheritdoc cref="LoadMetadata_ID"/>
        LoadMetadata LoadMetadata { get; }

        /// <inheritdoc cref="CatalogueItem"/>
        CatalogueItem[] CatalogueItems { get; }

        /// <summary>
        /// Returns all <see cref="AggregateConfiguration"/> that are associated with the Catalogue.  This includes both summary graphs, patient index tables and all
        /// cohort aggregates that are built to query this dataset.
        /// </summary>
        /// <seealso cref="AggregateConfiguration"/>
        AggregateConfiguration[] AggregateConfigurations { get; }

        /// <inheritdoc cref="LiveLoggingServer_ID"/>
        ExternalDatabaseServer LiveLoggingServer { get; }

        /// <summary>
        /// Shorthand (recommended 3 characters or less) for referring to this dataset (e.g. 'DEM' for the dataset 'Demography')
        /// </summary>
        string Acronym { get; set; }

        /// <summary>
        /// Retrieves all the TableInfo objects associated with a particular catalogue
        /// </summary>
        /// <param name="includeLookupTables"></param>
        /// <returns></returns>
        ITableInfo[] GetTableInfoList(bool includeLookupTables);

        /// <summary>
        /// Retrieves all the TableInfo objects associated with a particular catalogue
        /// </summary>
        /// <returns></returns>
        ITableInfo[] GetLookupTableInfoList();

        /// <summary>
        /// Gets all distinct underlying <see cref="TableInfo"/> that are referenced by the <see cref="CatalogueItem"/>s of the Catalogue.  The tables are divided into
        /// 'normalTables' and 'lookupTables' depending on whether there are any <see cref="Lookup"/> declarations of <see cref="LookupType.Description"/> on any of the
        /// Catalogue referenced ColumnInfos.
        /// <para>The sets are exclusive, a TableInfo is either a normal data contributor or it is a linked lookup table</para>
        /// </summary>
        /// <param name="normalTables">Unique TableInfos amongst all CatalogueItems in the Catalogue</param>
        /// <param name="lookupTables">Unique TableInfos amongst all CatalogueItems in the Catalogue where there is at least
        ///  one <see cref="Lookup"/> declarations of <see cref="LookupType.Description"/> on the referencing ColumnInfo.</param>
        void GetTableInfos(out List<ITableInfo> normalTables, out List<ITableInfo> lookupTables);

        /// <summary>
        /// Returns the unique <see cref="DiscoveredServer"/> from which to access connect to in order to run queries generated from the <see cref="Catalogue"/>.  This is 
        /// determined by comparing all the underlying <see cref="TableInfo"/> that power the <see cref="ExtractionInformation"/> of the Catalogue and looking for a shared
        /// servername.  This will handle when the tables are in different databases but only if you set <paramref name="setInitialDatabase"/> to false
        /// </summary>
        /// <param name="context"></param>
        /// <param name="setInitialDatabase">True to require all tables be in the same database.  False will just connect to master / unspecified database</param>
        /// <param name="distinctAccessPoint"></param>
        /// <returns></returns>
        DiscoveredServer GetDistinctLiveDatabaseServer(DataAccessContext context, bool setInitialDatabase, out IDataAccessPoint distinctAccessPoint);

        /// <inheritdoc cref="GetDistinctLiveDatabaseServer(DataAccessContext,bool,out IDataAccessPoint)"/>
        DiscoveredServer GetDistinctLiveDatabaseServer(DataAccessContext context, bool setInitialDatabase);
        
        /// <inheritdoc cref="SupportingSQLTable"/>
        SupportingSQLTable[] GetAllSupportingSQLTablesForCatalogue(FetchOptions fetch);

        /// <summary>
        /// Returns all <see cref="ExtractionInformation"/> declared under this <see cref="Catalogue"/> <see cref="CatalogueItem"/>s.  This can be restricted by 
        /// <see cref="ExtractionCategory"/> 
        /// 
        /// <para>pass <see cref="ExtractionCategory.Any"/> to fetch all <see cref="ExtractionInformation"/> regardless of category</para>
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        ExtractionInformation[] GetAllExtractionInformation(ExtractionCategory category);

        /// <inheritdoc cref="SupportingDocument"/>
        SupportingDocument[] GetAllSupportingDocuments(FetchOptions fetch);

        /// <summary>
        /// Gets all <see cref="ExtractionFilter"/> declared under any <see cref="ExtractionInformation"/> in the Catalogue where the  <see cref="IFilter.IsMandatory"/> flag is set.
        /// </summary>
        /// <returns></returns>
        ExtractionFilter[] GetAllMandatoryFilters();

        /// <summary>
        /// Gets all <see cref="ExtractionFilter"/> declared under any <see cref="ExtractionInformation"/> in the Catalogue.
        /// </summary>
        /// <returns></returns>
        ExtractionFilter[] GetAllFilters();

        /// <summary>
        /// Returns the unique <see cref="DatabaseType"/> shared by all <see cref="TableInfo"/> which underlie the Catalogue.  This is similar to GetDistinctLiveDatabaseServer 
        /// but is faster and more tolerant of failure i.e. if there are no underlying <see cref="TableInfo"/> at all or they are on different servers this will still return
        /// the shared / null <see cref="DatabaseType"/>
        /// </summary>
        /// <returns></returns>
        DatabaseType? GetDistinctLiveDatabaseServerType();

        /// <summary>
        /// Returns the extractability of the Catalogue if it is known.  If it is not known then the repository will be used to find out (and the result will be cached)
        /// <para>If a null dataExportRepository is passed then you will get the cached answer or null</para>
        /// </summary>
        /// <param name="dataExportRepository">Pass null to fetch only the cached value (or null if that is not known)</param>
        /// <returns></returns>
        CatalogueExtractabilityStatus GetExtractabilityStatus(IDataExportRepository dataExportRepository);
    }
}