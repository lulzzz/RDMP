// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Linq;
using System.Windows.Forms;
using Rdmp.Core.Curation.Data.Cohort;
using Rdmp.UI.Copying.Commands;
using Rdmp.UI.ItemActivation;
using ReusableUIComponents;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandMakePatientIndexTableIntoRegularCohortIdentificationSetAgain : BasicUICommandExecution
    {
        private readonly AggregateConfigurationCommand _sourceAggregateCommand;
        private readonly CohortAggregateContainer _targetCohortAggregateContainer;

        public ExecuteCommandMakePatientIndexTableIntoRegularCohortIdentificationSetAgain(IActivateItems activator, AggregateConfigurationCommand sourceAggregateCommand, CohortAggregateContainer targetCohortAggregateContainer) : base(activator)
        {
            _sourceAggregateCommand = sourceAggregateCommand;
            _targetCohortAggregateContainer = targetCohortAggregateContainer;

            if (!_sourceAggregateCommand.CohortIdentificationConfigurationIfAny.Equals(_targetCohortAggregateContainer.GetCohortIdentificationConfiguration()))
                SetImpossible("Aggregate belongs to a different CohortIdentificationConfiguration");
            
            if(_sourceAggregateCommand.JoinableUsersIfAny.Any())
                SetImpossible("The following Cohort Set(s) use this PatientIndex table:" + string.Join(",",_sourceAggregateCommand.JoinableUsersIfAny.Select(j=>j.ToString())));
        }

        public override void Execute()
        {
            base.Execute();

            //remove it from it's old container (really shouldn't be in any!) 
            if(_sourceAggregateCommand.ContainerIfAny != null)
                _sourceAggregateCommand.ContainerIfAny.RemoveChild(_sourceAggregateCommand.Aggregate);

            var dialog = new YesNoYesToAllDialog();

            //remove any non IsExtractionIdentifier columns
            foreach (var dimension in _sourceAggregateCommand.Aggregate.AggregateDimensions)
                if (!dimension.IsExtractionIdentifier)
                    if (
                        dialog.ShowDialog(
                            "Changing to a CohortSet means deleting AggregateDimension '" + dimension + "'.  Ok?",
                            "Delete Aggregate Dimension") ==
                        DialogResult.Yes)
                        dimension.DeleteInDatabase();
                    else
                        return;
            
            //make it is no longer a joinable
            _sourceAggregateCommand.JoinableDeclarationIfAny.DeleteInDatabase();

            //add  it to the new container
            _targetCohortAggregateContainer.AddChild(_sourceAggregateCommand.Aggregate, 0);

            //refresh the entire configuration
            Publish(_sourceAggregateCommand.CohortIdentificationConfigurationIfAny);
        }
    }
}