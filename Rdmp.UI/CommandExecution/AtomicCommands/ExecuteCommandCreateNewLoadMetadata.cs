// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using System.Linq;
using Rdmp.Core.CommandExecution.AtomicCommands;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandCreateNewLoadMetadata : BasicUICommandExecution, IAtomicCommandWithTarget
    {
        private Catalogue[] _availableCatalogues;
        private Catalogue _catalogue;

        public ExecuteCommandCreateNewLoadMetadata(IActivateItems activator):base(activator)
        {
            _availableCatalogues = activator.CoreChildProvider.AllCatalogues.Where(c => c.LoadMetadata_ID == null).ToArray();
            
            if(!_availableCatalogues.Any())
                SetImpossible("There are no Catalogues that are not associated with another Load already");

            UseTripleDotSuffix = true;
        }

        public override string GetCommandHelp()
        {
            return "Create a new data load configuration for loading data into a given set of datasets through RAW=>STAGING=>LIVE migration / adjustment";
        }

        public override void Execute()
        {
            base.Execute();

            var catalogueBefore = _catalogue;
            try
            {
                //if we don't have an explicit one picked yet
                if (_catalogue == null)
                    if(!SelectOne(_availableCatalogues, out _catalogue)) //get user to pick one
                        return; //user cancelled
            
                //create the load
                var lmd = new LoadMetadata(_catalogue.CatalogueRepository, "Loading " + _catalogue.Name);

                lmd.EnsureLoggingWorksFor(_catalogue);

                _catalogue.LoadMetadata_ID = lmd.ID;
                _catalogue.SaveToDatabase();

                Publish(lmd);

                Activator.WindowArranger.SetupEditAnything(this,lmd);
                
            }
            finally
            {
                _catalogue = catalogueBefore;
            }
        }


        public override string GetCommandName()
        {
            return "Create New Data Load Configuration...";
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.LoadMetadata, OverlayKind.Add);
        }

        public IAtomicCommandWithTarget SetTarget(DatabaseEntity target)
        {
            _catalogue = (Catalogue) target;
            return this;
        }
    }
}