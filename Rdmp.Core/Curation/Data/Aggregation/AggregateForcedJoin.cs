// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Linq;
using Rdmp.Core.Repositories;
using ReusableLibraryCode;

namespace Rdmp.Core.Curation.Data.Aggregation
{
    class AggregateForcedJoin : IAggregateForcedJoinManager
    {
        private readonly CatalogueRepository _repository;

        /// <summary>
        /// Creates a new instance targetting the catalogue database referenced by the repository.  The instance can be used to populate / edit the AggregateForcedJoin in 
        /// the database.  Access via <see cref="CatalogueRepository.AggregateForcedJoinManager"/>
        /// </summary>
        /// <param name="repository"></param>
        internal AggregateForcedJoin(CatalogueRepository repository)
        {
            _repository = repository;
        }

        /// <inheritdoc/>
        public TableInfo[] GetAllForcedJoinsFor(AggregateConfiguration configuration)
        {
            return
                _repository.SelectAllWhere<TableInfo>(
                    "Select TableInfo_ID from AggregateForcedJoin where AggregateConfiguration_ID = " + configuration.ID,
                    "TableInfo_ID").ToArray();
        }

        /// <inheritdoc/>
        public void BreakLinkBetween(AggregateConfiguration configuration, TableInfo tableInfo)
        {
            _repository.Delete(string.Format("DELETE FROM AggregateForcedJoin WHERE AggregateConfiguration_ID = {0} AND TableInfo_ID = {1}", configuration.ID, tableInfo.ID));
        }

        /// <inheritdoc/>
        public void CreateLinkBetween(AggregateConfiguration configuration, TableInfo tableInfo)
        {
            using (var con = _repository.GetConnection())
                DatabaseCommandHelper.GetCommand(
                    string.Format(
                        "INSERT INTO AggregateForcedJoin (AggregateConfiguration_ID,TableInfo_ID) VALUES ({0},{1})",
                        configuration.ID, tableInfo.ID), con.Connection,con.Transaction).ExecuteNonQuery();
        }
    }
}
