// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    internal class ExecuteCommandCreateNewPipeline : BasicUICommandExecution,IAtomicCommand
    {
        private readonly PipelineUseCase _useCase;

        public ExecuteCommandCreateNewPipeline(IActivateItems activator, PipelineUseCase useCase) : base(activator)
        {
            _useCase = useCase;

            if(_useCase == null)
                SetImpossible("Pipelines can only be created under an established use case");
        }

        public override void Execute()
        {
            base.Execute();

            var newPipe = new Pipeline(Activator.RepositoryLocator.CatalogueRepository);
            var edit = new ExecuteCommandEditPipelineWithUseCase(Activator, newPipe, _useCase);
            edit.Execute();
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Pipeline, OverlayKind.Add);
        }
    }
}