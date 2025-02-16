// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.Providers.Nodes.PipelineNodes;
using Rdmp.UI.CommandExecution.AtomicCommands;

namespace Rdmp.UI.Menus
{
    class PipelineMenu : RDMPContextMenuStrip
    {
        public PipelineMenu(RDMPContextMenuStripArgs args, PipelineCompatibleWithUseCaseNode node): base(args,node)
        {
            Add(new ExecuteCommandCreateNewPipeline(_activator, node.UseCase));
            Add(new ExecuteCommandClonePipeline(_activator, node.Pipeline));
        }
        public PipelineMenu(RDMPContextMenuStripArgs args, StandardPipelineUseCaseNode node): base(args, node)
        {
            Add(new ExecuteCommandCreateNewPipeline(_activator, node.UseCase));
        }
        public PipelineMenu(RDMPContextMenuStripArgs args, Pipeline pipeline): base(args, pipeline)
        {
            Add(new ExecuteCommandCreateNewPipeline(_activator, null));
            Add(new ExecuteCommandClonePipeline(_activator, pipeline));
        }
    }
}
