// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Linq;
using System.Windows.Forms;
using Rdmp.Core.Curation.Data.DataLoad;
using Rdmp.Core.DataLoad.Engine.Attachers;
using Rdmp.Core.DataLoad.Engine.DataProvider;
using Rdmp.Core.DataLoad.Engine.DataProvider.FromCache;
using Rdmp.Core.DataLoad.Engine.Mutilators;
using Rdmp.Core.Providers.Nodes.LoadMetadataNodes;
using Rdmp.Core.Repositories;
using Rdmp.UI.CommandExecution.AtomicCommands;

namespace Rdmp.UI.Menus
{
    internal class LoadStageNodeMenu : RDMPContextMenuStrip
    {
        private readonly LoadStageNode _loadStageNode;
        private MEF _mef;

        public LoadStageNodeMenu(RDMPContextMenuStripArgs args, LoadStageNode loadStageNode) : base(args, loadStageNode)
        {
            _loadStageNode = loadStageNode;
            _mef = _activator.RepositoryLocator.CatalogueRepository.MEF;
            
           AddMenu<IDataProvider>("Add Cached Data Provider",t=>typeof(ICachedDataProvider).IsAssignableFrom(t));
           AddMenu<IDataProvider>("Add Data Provider", t=> !typeof(ICachedDataProvider).IsAssignableFrom(t));

           AddMenu<IAttacher>("Add Attacher");
           AddMenu<IMutilateDataTables>("Add Mutilator");

           Add(new ExecuteCommandCreateNewProcessTask(_activator, ProcessTaskType.SQLFile,loadStageNode.LoadMetadata, loadStageNode.LoadStage));
           Add(new ExecuteCommandCreateNewProcessTask(_activator, ProcessTaskType.Executable, loadStageNode.LoadMetadata, loadStageNode.LoadStage));
        }

        private void AddMenu<T>(string menuName, Func<Type, bool> filterTypes)
        {
            var types = _mef.GetTypes<T>().Where(filterTypes).ToArray();
            var menu = new ToolStripMenuItem(menuName);

            ProcessTaskType taskType;

            if (typeof(T) == typeof(IDataProvider))
                taskType = ProcessTaskType.DataProvider;
            else
                if (typeof(T) == typeof(IAttacher))
                    taskType = ProcessTaskType.Attacher;
                else if (typeof(T) == typeof(IMutilateDataTables))
                    taskType = ProcessTaskType.MutilateDataTable;
                else
                    throw new ArgumentException("Type '" + typeof(T) + "' was not expected", "T");

            foreach (Type type in types)
            {
                Type toAdd = type;
                menu.DropDownItems.Add(type.Name, null, (s, e) => AddTypeIntoStage(toAdd, taskType));
            }

            menu.Enabled = ProcessTask.IsCompatibleStage(taskType, _loadStageNode.LoadStage) && types.Any();

            Items.Add(menu);
        }

        private void AddMenu<T>(string menuName)
        {
            AddMenu<T>(menuName, t => true);
        }



        private void AddTypeIntoStage(Type type, ProcessTaskType taskType)
        {
            var lmd = _loadStageNode.LoadMetadata;
            var stage = _loadStageNode.LoadStage;

            ProcessTask newTask = new ProcessTask((ICatalogueRepository)lmd.Repository, lmd, stage);
            newTask.Path = type.FullName;
            newTask.ProcessTaskType = taskType;
            newTask.Name = type.Name;

            newTask.SaveToDatabase();
            Publish(lmd);
            Activate(newTask);
        }
    }
}