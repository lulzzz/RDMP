// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.DataExport.Data;
using Rdmp.UI.CohortUI.CohortSourceManagement;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution;
using ReusableUIComponents.CommandExecution;

namespace Rdmp.UI.CommandExecution.Proposals
{
    class ProposeExecutionWhenTargetIsExternalCohortTable:RDMPCommandExecutionProposal<ExternalCohortTable>
    {
        public ProposeExecutionWhenTargetIsExternalCohortTable(IActivateItems itemActivator) : base(itemActivator)
        {
        }

        public override bool CanActivate(ExternalCohortTable target)
        {
            return true;
        }

        public override void Activate(ExternalCohortTable target)
        {
            ItemActivator.Activate<ExternalCohortTableUI, ExternalCohortTable>(target);
        }

        public override ICommandExecution ProposeExecution(ICommand cmd, ExternalCohortTable target, InsertOption insertOption = InsertOption.Default)
        {
            return null;
        }
    }
}
