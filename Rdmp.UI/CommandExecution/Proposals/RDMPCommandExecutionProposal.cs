// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Curation.Data;
using Rdmp.UI.Copying.Commands;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution;
using ReusableUIComponents.CommandExecution;
using ReusableUIComponents.CommandExecution.Proposals;

namespace Rdmp.UI.CommandExecution.Proposals
{
    public abstract class RDMPCommandExecutionProposal<T>:ICommandExecutionProposal where T : class
    {
        protected readonly IActivateItems ItemActivator;

        protected RDMPCommandExecutionProposal(IActivateItems itemActivator)
        {
            ItemActivator = itemActivator;
        }

        public abstract bool CanActivate(T target);
        public abstract void Activate(T target);

        /// <summary>
        /// Decides which (if any) command should be advertised/run when combining the dragged object (cmd) with the drop target
        /// </summary>
        /// <param name="cmd">Self contained class describing both the object(s) being dragged and salient facts about it e.g. if  it is a
        /// <see cref="CatalogueCommand"/> then it will know whether the dragged <see cref="Catalogue"/>
        /// has at least one patient identifier column.</param>
        /// 
        /// <param name="target"> The object the cursor is currently hovering over </param>
        /// <param name="insertOption">Whether the cursor is above or below or ontop of your object (if the collection the object is in supports it)</param>
        /// <returns></returns>
        public abstract ICommandExecution ProposeExecution(ICommand cmd, T target, InsertOption insertOption = InsertOption.Default);

        public bool IsCompatibleTarget(object target)
        {
            return target is T && IsCompatibleTargetImpl((T)target);
        }

        protected virtual bool IsCompatibleTargetImpl(T target)
        {
            return true;
        }

        public ICommandExecution ProposeExecution(ICommand cmd, object target,InsertOption insertOption = InsertOption.Default)
        {
            if(IsCompatibleTarget(target))
                return ProposeExecution(cmd,(T)target, insertOption);

            return null;
        }

        public void Activate(object target)
        {
            if (IsCompatibleTarget(target))
                Activate((T) target);
        }

        public bool CanActivate(object target)
        {
            return IsCompatibleTarget(target) && CanActivate((T)target);
        }
    }
}
