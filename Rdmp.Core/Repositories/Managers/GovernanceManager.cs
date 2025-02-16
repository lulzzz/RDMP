// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Governance;

namespace Rdmp.Core.Repositories.Managers
{
    class GovernanceManager : IGovernanceManager
    {
        private readonly CatalogueRepository _catalogueRepository;

        public GovernanceManager(CatalogueRepository catalogueRepository)
        {
            _catalogueRepository = catalogueRepository;
        }

        public void Unlink(GovernancePeriod governancePeriod, ICatalogue catalogue)
        {
            _catalogueRepository.Delete(string.Format(@"DELETE FROM GovernancePeriod_Catalogue WHERE Catalogue_ID={0} AND GovernancePeriod_ID={1}", catalogue.ID, governancePeriod.ID));
        }

        public void Link(GovernancePeriod governancePeriod, ICatalogue catalogue)
        {
            _catalogueRepository.Insert(string.Format(
                @"INSERT INTO GovernancePeriod_Catalogue (Catalogue_ID,GovernancePeriod_ID) VALUES ({0},{1})",catalogue.ID, governancePeriod.ID), null);
        }

        /// <inheritdoc/>
        public Dictionary<int, HashSet<int>> GetAllGovernedCataloguesForAllGovernancePeriods()
        {
            var toReturn = new Dictionary<int, HashSet<int>>();

            var server = _catalogueRepository.DiscoveredServer;
            using (var con = server.GetConnection())
            {
                con.Open();
                var cmd = server.GetCommand(@"SELECT GovernancePeriod_ID,Catalogue_ID FROM GovernancePeriod_Catalogue", con);
                var r = cmd.ExecuteReader();

                while (r.Read())
                {
                    int gp = (int)r["GovernancePeriod_ID"];
                    int cata = (int)r["Catalogue_ID"];

                    if (!toReturn.ContainsKey(gp))
                        toReturn.Add(gp, new HashSet<int>());

                    toReturn[gp].Add(cata);
                }
            }

            return toReturn;
        }

        public IEnumerable<ICatalogue> GetAllGovernedCatalogues(GovernancePeriod governancePeriod)
        {
            return _catalogueRepository.SelectAll<Catalogue>(@"SELECT Catalogue_ID FROM GovernancePeriod_Catalogue where GovernancePeriod_ID=" + governancePeriod.ID,"Catalogue_ID");
        }
    }
}