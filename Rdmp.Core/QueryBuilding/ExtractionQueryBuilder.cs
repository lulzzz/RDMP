// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using FAnsi.Discovery.QuerySyntax;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.DataExport;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.DataExtraction.Commands;
using Rdmp.Core.Repositories;
using Rdmp.Core.Repositories.Managers;
using TypeGuesser;

namespace Rdmp.Core.QueryBuilding
{
    /// <summary>
    /// Calculates the Extraction SQL for extracting a given ExtractDatasetCommand.  This is done by creating a normal QueryBuilder and then adding adjustment 
    /// components to it to link against the cohort, drop the private identifier column, add the release identifier column etc.
    /// </summary>
    public class ExtractionQueryBuilder
    {
        private readonly IDataExportRepository _repository;

        public ExtractionQueryBuilder(IDataExportRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// This produces the SQL that would retrieve the specified dataset columns including any JOINS 
        /// 
        /// <para>It uses:
        /// QueryBuilder and then it adds some custom lines for linking to the cohort</para>
        /// </summary>
        /// <returns></returns>
        public QueryBuilder GetSQLCommandForFullExtractionSet(ExtractDatasetCommand request, out List<ReleaseIdentifierSubstitution> substitutions)
        {
            if(request.QueryBuilder != null)
                throw new Exception("Creation of a QueryBuilder from a request can only happen once, to access the results of the creation use the cached answer in the request.QueryBuilder property");
            
            if (!request.ColumnsToExtract.Any())
                throw new Exception("No columns are marked for extraction in this configuration");

            if(request.ExtractableCohort == null)
                throw new NullReferenceException("No Cohort selected");
            
            var databaseType = request.Catalogue.GetDistinctLiveDatabaseServerType();

            if(databaseType == null)
                throw new NotSupportedException("Catalogue " + request.Catalogue + " did not know what DatabaseType it hosted, how can we extract from it! does it have no TableInfos?");

            var syntaxHelper = new QuerySyntaxHelperFactory().Create(databaseType.Value);

            substitutions = new List<ReleaseIdentifierSubstitution>();

            var memoryRepository = new MemoryRepository();
            
            switch (request.ColumnsToExtract.Count(c => c.IsExtractionIdentifier))
            { 
                //no extraction identifiers
                case 0: throw new Exception("There are no Columns in this dataset ("+request+") marked as IsExtractionIdentifier"); 
                    
                //a single extraction identifier e.g. CHI X died on date Y with conditions a,b and c
                case 1: substitutions.Add(new ReleaseIdentifierSubstitution(memoryRepository,request.ColumnsToExtract.FirstOrDefault(c => c.IsExtractionIdentifier), request.ExtractableCohort, false,syntaxHelper));
                    break;

                //multiple extraction identifiers e.g. Mother X had Babies A, B, C where A,B and C are all CHIs that must be subbed for ProCHIs
                default:
                    foreach (IColumn columnToSubstituteForReleaseIdentifier in request.ColumnsToExtract.Where(c=>c.IsExtractionIdentifier))
                        substitutions.Add(new ReleaseIdentifierSubstitution(memoryRepository, columnToSubstituteForReleaseIdentifier, request.ExtractableCohort, true,syntaxHelper));
                    break;
            }
            
            string hashingAlgorithm = _repository.DataExportPropertyManager.GetValue(DataExportProperty.HashingAlgorithmPattern);
            if (string.IsNullOrWhiteSpace(hashingAlgorithm))
                hashingAlgorithm = null;

            //identify any tables we are supposed to force join to
            var forcedJoins = request.SelectedDataSets.SelectedDataSetsForcedJoins;

            QueryBuilder queryBuilder = new QueryBuilder("DISTINCT ", hashingAlgorithm, forcedJoins.Select(s => s.TableInfo).ToArray());
            queryBuilder.TopX = request.TopX;
            
            queryBuilder.SetSalt(request.Salt.GetSalt());

            //add the constant parameters
            foreach (ConstantParameter parameter in GetConstantParameters(syntaxHelper,request.Configuration, request.ExtractableCohort))
                queryBuilder.ParameterManager.AddGlobalParameter(parameter);

            //add the global parameters
            foreach (var globalExtractionFilterParameter in request.Configuration.GlobalExtractionFilterParameters)
                queryBuilder.ParameterManager.AddGlobalParameter(globalExtractionFilterParameter);

            //remove the identification column from the query
            request.ColumnsToExtract.RemoveAll(c=>c.IsExtractionIdentifier);

            //add in the ReleaseIdentifier in place of the identification column
            queryBuilder.AddColumnRange(substitutions.ToArray());

            //add the rest of the columns to the query
            queryBuilder.AddColumnRange(request.ColumnsToExtract.Cast<IColumn>().ToArray());

            //add the users selected filters
            queryBuilder.RootFilterContainer = request.Configuration.GetFilterContainerFor(request.DatasetBundle.DataSet);
            
            ExternalCohortTable externalCohortTable = _repository.GetObjectByID<ExternalCohortTable>(request.ExtractableCohort.ExternalCohortTable_ID);

            if (request.ExtractableCohort != null)
            {
                //the JOIN with the cohort table:
                string cohortJoin;

                if (substitutions.Count == 1)
                    cohortJoin = " INNER JOIN " + externalCohortTable.TableName + " ON " + substitutions.Single().JoinSQL;
                else
                    cohortJoin = " INNER JOIN " + externalCohortTable.TableName + " ON " + string.Join(" OR ", substitutions.Select(s => s.JoinSQL));

                //add the JOIN in after any other joins
                queryBuilder.AddCustomLine(cohortJoin, QueryComponent.JoinInfoJoin);
                
                //add the filter cohortID because our new Cohort system uses ID number and a giant combo table with all the cohorts in it we need to say Select XX from XX join Cohort Where Cohort number = Y
                queryBuilder.AddCustomLine(request.ExtractableCohort.WhereSQL(), QueryComponent.WHERE);
            }


            
            request.QueryBuilder = queryBuilder;
            return queryBuilder;
        }
        
        public static List<ConstantParameter> GetConstantParameters(IQuerySyntaxHelper syntaxHelper, IExtractionConfiguration configuration, IExtractableCohort extractableCohort)
        {
            List<ConstantParameter> toReturn = new List<ConstantParameter>();

            if(syntaxHelper.DatabaseType == FAnsi.DatabaseType.Oracle)
                return toReturn;

            IProject project = configuration.Project;

            if (project.ProjectNumber == null)
                throw new ProjectNumberException("Project number has not been entered, cannot create constant paramaters");
            
            if(extractableCohort == null)
                throw new Exception("Cohort has not been selected, cannot create constant parameters");

            IExternalCohortTable externalCohortTable = extractableCohort.ExternalCohortTable;

            var declarationSqlCohortId = syntaxHelper.GetParameterDeclaration("@CohortDefinitionID", new DatabaseTypeRequest(typeof (int)));
            var declarationSqlProjectNumber = syntaxHelper.GetParameterDeclaration("@ProjectNumber", new DatabaseTypeRequest(typeof(int)));

            toReturn.Add(new ConstantParameter(declarationSqlCohortId, extractableCohort.OriginID.ToString(), "The ID of the cohort in " + externalCohortTable.TableName, syntaxHelper));
            toReturn.Add(new ConstantParameter(declarationSqlProjectNumber, project.ProjectNumber.ToString(), "The project number of project " + project.Name, syntaxHelper));

            return toReturn;
        }
    }
}
