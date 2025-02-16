﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using Rdmp.Core.DataExport.Data;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    internal class ExecuteCommandFreezeExtractionConfiguration : BasicUICommandExecution, IAtomicCommand
    {
        private ExtractionConfiguration _extractionConfiguration;

        public ExecuteCommandFreezeExtractionConfiguration(IActivateItems activator, ExtractionConfiguration extractionConfiguration) : base(activator)
        {
            _extractionConfiguration = extractionConfiguration;

            if (extractionConfiguration.IsReleased)
                SetImpossible("ExtractionConfiguration is already released)");
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return CatalogueIcons.FrozenExtractionConfiguration;
        }
        public override void Execute()
        {
            base.Execute();

            _extractionConfiguration.IsReleased = true;
            _extractionConfiguration.SaveToDatabase();
            Publish(_extractionConfiguration);
        }
    }
}