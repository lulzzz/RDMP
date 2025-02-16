// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.Providers;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.SimpleDialogs;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    internal class ExecuteCommandChooseCohort : BasicUICommandExecution,IAtomicCommand
    {
        private readonly ExtractionConfiguration _extractionConfiguration;
        private DataExportChildProvider _childProvider;
        List<ExtractableCohort> _compatibleCohorts = new List<ExtractableCohort>();

        public ExecuteCommandChooseCohort(IActivateItems activator, ExtractionConfiguration extractionConfiguration):base(activator)
        {
            _extractionConfiguration = extractionConfiguration;

            var project = _extractionConfiguration.Project;

            if (extractionConfiguration.IsReleased)
            {
                SetImpossible("ExtractionConfiguration has already been released");
                return;
            }

            if (!project.ProjectNumber.HasValue)
            {
                SetImpossible("Project does not have a ProjectNumber, this determines which cohorts are eligible");
                return;
            }

            _childProvider = Activator.CoreChildProvider as DataExportChildProvider;

            if (_childProvider == null)
            {
                SetImpossible("Activator.CoreChildProvider is not an DataExportChildProvider");
                return;
            }

            //find cohorts that match the project number
            if (_childProvider.ProjectNumberToCohortsDictionary.ContainsKey(project.ProjectNumber.Value))
                _compatibleCohorts = (_childProvider.ProjectNumberToCohortsDictionary[project.ProjectNumber.Value]).ToList();

            //if theres only one compatible cohort and that one is already selected
            if (_compatibleCohorts.Count == 1 && _compatibleCohorts.Single().ID == _extractionConfiguration.Cohort_ID)
                SetImpossible("The currently select cohort is the only one available");

            if(!_compatibleCohorts.Any())
                SetImpossible("There are no cohorts currently configured with ProjectNumber " + project.ProjectNumber.Value);
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.ExtractableCohort, OverlayKind.Link);
        }

        public override void Execute()
        {
            base.Execute();
            
            var dialog = new SelectIMapsDirectlyToDatabaseTableDialog(_compatibleCohorts.Where(c => c.ID != _extractionConfiguration.Cohort_ID), false, false);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                //clear current one
                _extractionConfiguration.Cohort_ID = ((ExtractableCohort)dialog.Selected).ID;
                _extractionConfiguration.SaveToDatabase();
                Publish(_extractionConfiguration);
            }
        
        }
    }
}