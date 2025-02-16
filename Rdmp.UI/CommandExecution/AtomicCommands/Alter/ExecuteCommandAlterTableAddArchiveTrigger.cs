﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using Rdmp.Core;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.DataLoad.Triggers;
using Rdmp.Core.DataLoad.Triggers.Implementations;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.Checks;
using System.Linq;

namespace Rdmp.UI.CommandExecution.AtomicCommands.Alter
{
    internal class ExecuteCommandAlterTableAddArchiveTrigger : AlterTableCommandExecution
    {
        private readonly ITriggerImplementer _triggerImplementer;

        public ExecuteCommandAlterTableAddArchiveTrigger(IActivateItems activator, TableInfo tableInfo) : base(activator,tableInfo)
        {
            if(IsImpossible)
                return;
                
            if (!Table.DiscoverColumns().Any(c => c.IsPrimaryKey))
            {
                SetImpossible(GlobalStrings.TableHasNoPrimaryKey);
                return;
            }

            var factory = new TriggerImplementerFactory(TableInfo.DatabaseType);
            _triggerImplementer = factory.Create(Table);
            var currentStatus = _triggerImplementer.GetTriggerStatus();

            if (currentStatus != TriggerStatus.Missing)
                SetImpossible(GlobalStrings.TriggerStatusIsCurrently , currentStatus.S());
        }

        public override void Execute()
        {
            base.Execute();
            
            if (!Synchronize())
                return;
           
            if (YesNo(GlobalStrings.CreateArchiveTableYesNo, GlobalStrings.CreateArchiveTableCaption))
            {
                _triggerImplementer.CreateTrigger(new ThrowImmediatelyCheckNotifier());
                Show(GlobalStrings.CreateArchiveTableSuccess , TableInfo.GetRuntimeName() + "_Archive ");
            }

            Synchronize();

            Publish(TableInfo);
        }

    }
}