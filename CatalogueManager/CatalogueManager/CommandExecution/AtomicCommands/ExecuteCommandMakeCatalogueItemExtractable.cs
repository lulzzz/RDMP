using System.Drawing;
using CatalogueLibrary.Data;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;

namespace CatalogueManager.CommandExecution.AtomicCommands
{
    internal class ExecuteCommandMakeCatalogueItemExtractable : BasicUICommandExecution,IAtomicCommand
    {
        private readonly CatalogueItem _catalogueItem;

        public ExecuteCommandMakeCatalogueItemExtractable(IActivateItems activator, CatalogueItem catalogueItem) : base(activator)
        {
            _catalogueItem = catalogueItem;

            if(_catalogueItem.ColumnInfo_ID == null)
                SetImpossible("There is no underlying ColumnInfo");

            if(_catalogueItem.ExtractionInformation != null)
                SetImpossible("CatalougeItem is already extractable");
        }

        public override string GetCommandHelp()
        {
            return "Make the column/transform available for extraction to researchers";
        }

        public override void Execute()
        {
            base.Execute();

            //Create a new ExtractionInformation (contains the transform sql / column name)
            var newExtractionInformation = new ExtractionInformation(Activator.RepositoryLocator.CatalogueRepository, _catalogueItem, _catalogueItem.ColumnInfo, _catalogueItem.ColumnInfo.Name);

            //it will be Core but if the Catalogue is ProjectSpecific then instead we should make our new ExtractionInformation ExtractionCategory.ProjectSpecific
            if(_catalogueItem.Catalogue.IsProjectSpecific(Activator.RepositoryLocator.DataExportRepository))
            {
                newExtractionInformation.ExtractionCategory = ExtractionCategory.ProjectSpecific;
                newExtractionInformation.SaveToDatabase();
            }

            Publish(_catalogueItem);
            Activate(newExtractionInformation);
        }

        public override string GetCommandName()
        {
            return "Make Extractable";
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.ExtractionInformation, OverlayKind.Add);
        }
    }
}