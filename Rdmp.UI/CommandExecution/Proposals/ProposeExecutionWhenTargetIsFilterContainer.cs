// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Curation.Data;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.Copying.Commands;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution;
using ReusableUIComponents.CommandExecution;

namespace Rdmp.UI.CommandExecution.Proposals
{
    class ProposeExecutionWhenTargetIsFilterContainer:RDMPCommandExecutionProposal<IContainer>
    {
        public ProposeExecutionWhenTargetIsFilterContainer(IActivateItems itemActivator) : base(itemActivator)
        {

        }

        public override bool CanActivate(IContainer target)
        {
            return false;
        }

        public override void Activate(IContainer target)
        {
            
        }

        public override ICommandExecution ProposeExecution(ICommand cmd, IContainer targetContainer, InsertOption insertOption = InsertOption.Default)
        {
            var sourceFilterCommand = cmd as FilterCommand;

            //drag a filter into a container
            if (sourceFilterCommand != null)
            {
                //if filter is already in the target container
                if (sourceFilterCommand.ImmediateContainerIfAny.Equals(targetContainer))
                    return null;

                //if the target container is one that is part of the filters tree then it's a move
                if (sourceFilterCommand.AllContainersInEntireTreeFromRootDown.Contains(targetContainer))
                    return new ExecuteCommandMoveFilterIntoContainer(ItemActivator, sourceFilterCommand, targetContainer);
                
                //otherwise it's an import    

                //so instead lets let them create a new copy (possibly including changing the type e.g. importing a master
                //filter into a data export AND/OR container
                return new ExecuteCommandImportNewCopyOfFilterIntoContainer(ItemActivator, sourceFilterCommand, targetContainer);
                
            }

            var sourceContainerCommand = cmd as ContainerCommand;
            
            //drag a container into another container
            if (sourceContainerCommand != null)
            {
                //if the source and target are the same container
                if (sourceContainerCommand.Container.Equals(targetContainer))
                    return null;

                //is it a movement within the current container tree
                if (sourceContainerCommand.AllContainersInEntireTreeFromRootDown.Contains(targetContainer))
                    return new ExecuteCommandMoveContainerIntoContainer(ItemActivator, sourceContainerCommand, targetContainer);
            }
            
            return null;
        

        }
    }
}
