// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.FilterImporting.Construction;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.CommandExecution.AtomicCommands.CohortCreationCommands;

namespace Rdmp.UI.Menus
{
    [System.ComponentModel.DesignerCategory("")]
    class ExtractionInformationMenu : RDMPContextMenuStrip
    {
        public ExtractionInformationMenu(RDMPContextMenuStripArgs args, ExtractionInformation extractionInformation): base(args,extractionInformation)
        {
            Add(new ExecuteCommandCreateNewFilter(_activator,new ExtractionFilterFactory(extractionInformation)));
            Add(new ExecuteCommandCreateNewCohortFromCatalogue(_activator, extractionInformation));
            Add(new ExecuteCommandChangeExtractionCategory(_activator,extractionInformation));
        }
    }
}
