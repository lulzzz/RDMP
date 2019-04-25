// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Providers.Nodes.PipelineNodes;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.Copying.Commands;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution;
using ReusableUIComponents.CommandExecution;

namespace Rdmp.UI.CommandExecution.Proposals
{
    class ProposeExecutionWhenTargetIsStandardPipelineUseCaseNode :RDMPCommandExecutionProposal<StandardPipelineUseCaseNode>
    {
        public ProposeExecutionWhenTargetIsStandardPipelineUseCaseNode(IActivateItems itemActivator) : base(itemActivator)
        {
        }

        public override bool CanActivate(StandardPipelineUseCaseNode target)
        {
            return false;
        }

        public override void Activate(StandardPipelineUseCaseNode target)
        {
            
        }

        public override ICommandExecution ProposeExecution(ICommand cmd, StandardPipelineUseCaseNode target, InsertOption insertOption = InsertOption.Default)
        {
            var sourcePipelineCommand = cmd as PipelineCommand;
            if(sourcePipelineCommand != null)
                return new ExecuteCommandEditPipelineWithUseCase(ItemActivator,sourcePipelineCommand.Pipeline, target.UseCase);

            return null;
        }
    }
}