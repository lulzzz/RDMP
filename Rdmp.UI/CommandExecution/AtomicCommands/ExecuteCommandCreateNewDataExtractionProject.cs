// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using System.Windows.Forms;
using Rdmp.Core.DataExport.Data;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.ItemActivation.Emphasis;
using Rdmp.UI.Wizard;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandCreateNewDataExtractionProject : BasicUICommandExecution, IAtomicCommand
    {
        public ExecuteCommandCreateNewDataExtractionProject(IActivateItems activator) : base(activator)
        {
            UseTripleDotSuffix = true;
        }

        public override void Execute()
        {
            base.Execute();
            var wizard = new CreateNewDataExtractionProjectUI(Activator);
            if(wizard.ShowDialog() == DialogResult.OK && wizard.ExtractionConfigurationCreatedIfAny != null)
            {
                var p = (Project) wizard.ExtractionConfigurationCreatedIfAny.Project;
                Publish(p);
                Activator.RequestItemEmphasis(this, new EmphasiseRequest(p, int.MaxValue));
                
                //now execute it
                var executeCommand = new ExecuteCommandExecuteExtractionConfiguration(Activator).SetTarget(wizard.ExtractionConfigurationCreatedIfAny);
                if(!executeCommand.IsImpossible)
                    executeCommand.Execute(); 

            }
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Project, OverlayKind.Add);
        }

        public override string GetCommandHelp()
        {
            return
                "This will open a window which will guide you in the steps for creating a Data Extraction Project.\r\n" +
                "You will be asked to choose a Cohort, the Catalogues to extract and the destination folder.";
        }
    }
}