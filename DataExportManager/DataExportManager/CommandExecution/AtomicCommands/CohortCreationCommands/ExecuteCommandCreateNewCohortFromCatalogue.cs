﻿using System.Drawing;
using System.Linq;
using CatalogueLibrary.Data;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using DataExportLibrary.Data.DataTables;
using RDMPObjectVisualisation.Copying.Commands;
using ReusableLibraryCode.Icons.IconProvision;

namespace DataExportManager.CommandExecution.AtomicCommands.CohortCreationCommands
{
    public class ExecuteCommandCreateNewCohortFromCatalogue : CohortCreationCommandExecution
    {
        private ExtractionInformation _extractionIdentifierColumn;

        public ExecuteCommandCreateNewCohortFromCatalogue(IActivateItems activator, Catalogue catalogue): base(activator)
        {
            var eis = catalogue.GetAllExtractionInformation(ExtractionCategory.Any);

            if (eis.Count(ei => ei.IsExtractionIdentifier) != 1)
            {
                SetImpossible("Catalogue must have a single IsExtractionIdentifier column");
                return;
            }

            _extractionIdentifierColumn = eis.Single(e => e.IsExtractionIdentifier);
        }
        
        public override void Execute()
        {
            base.Execute();
            
            var request = GetCohortCreationRequest();

            //user choose to cancel the cohort creation request dialogue
            if (request == null)
                return;

            var configureAndExecute = GetConfigureAndExecuteControl(request, "Import column " + _extractionIdentifierColumn + " as cohort and commmit results");

            configureAndExecute.AddInitializationObject(_extractionIdentifierColumn);
            configureAndExecute.TaskDescription = "You have selected a patient identifier column in a dataset, the unique identifier list in this column will be commmented to the named project/cohort ready for data export.  This dialog requires you to select/create an appropriate pipeline. " + TaskDescriptionGenerallyHelpfulText;

            Activator.ShowWindow(configureAndExecute);
        }

        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.ExtractableCohort, OverlayKind.Add);
        }
    }
}