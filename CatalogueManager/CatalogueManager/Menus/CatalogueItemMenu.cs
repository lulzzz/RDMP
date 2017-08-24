﻿using System;
using System.Linq;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.PerformanceImprovement;
using CatalogueLibrary.Repositories;
using CatalogueManager.Collections.Providers;
using CatalogueManager.CommandExecution;
using CatalogueManager.Icons.IconOverlays;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.Refreshing;
using CatalogueManager.SimpleDialogs;
using MapsDirectlyToDatabaseTableUI;
using RDMPObjectVisualisation.Copying.Commands;
using RDMPStartup;

namespace CatalogueManager.Menus
{
    [System.ComponentModel.DesignerCategory("")]
    public class CatalogueItemMenu : RDMPContextMenuStrip
    {
        private readonly CatalogueItem _catalogueItem;
        private CatalogueItemClassification _classification;

        public CatalogueItemMenu(IActivateItems activator, CatalogueItem catalogueItem, CatalogueItemClassification classification) :base(activator,catalogueItem)
        {
            _catalogueItem = catalogueItem;
            _classification = classification;

            Items.Add("Add Issue", activator.CoreIconProvider.GetImage(RDMPConcept.CatalogueItemIssue,OverlayKind.Add),(s,e)=> AddIssue());

            if (catalogueItem.ColumnInfo_ID == null)
                Items.Add("Set Column Info (Currently MISSING)", activator.CoreIconProvider.GetImage(RDMPConcept.ColumnInfo,OverlayKind.Problem),(s,e)=>SetColumnInfo(catalogueItem));
            else if (classification.ExtractionInformation_ID == null)
                //it does not yet have extractability
                Items.Add("Add Extract Logic", activator.CoreIconProvider.GetImage(RDMPConcept.ExtractionInformation,OverlayKind.Add),(s, e) => AddExtractionInformation());

            var importDescription = new ToolStripMenuItem("Import Description From Another CatalogueItem");
            
            importDescription.DropDownItems.Add("Any (Ctrl + Shift + I)",null,ImportAnyDescription);
            importDescription.DropDownItems.Add("With same name (Ctrl + I)",null, ImportWithSameName);

            Items.Add(importDescription);

            AddCommonMenuItems();
        }

        private void AddIssue()
        {
            var newIssue = new CatalogueItemIssue(RepositoryLocator.CatalogueRepository, _catalogueItem);
            _activator.ActivateCatalogueItemIssue(this,newIssue);
            _activator.RefreshBus.Publish(this,new RefreshObjectEventArgs(_catalogueItem));
        }

        private void AddExtractionInformation()
        {
            var newExtractionInformation = new ExtractionInformation(RepositoryLocator.CatalogueRepository, _catalogueItem, _catalogueItem.ColumnInfo, _catalogueItem.ColumnInfo.Name);

            _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(newExtractionInformation));
            _activator.ActivateExtractionInformation(this, newExtractionInformation);
        }
        
        private void SetColumnInfo(CatalogueItem ci)
        {
            var cols = ci.Repository.GetAllObjects<ColumnInfo>().ToArray();
            var chooser = new SelectIMapsDirectlyToDatabaseTableDialog(cols, false, false);

            if (chooser.ShowDialog() == DialogResult.OK)
                new ExecuteCommandLinkCatalogueItemToColumnInfo(_activator,new ColumnInfoCommand((ColumnInfo) chooser.Selected), ci).Execute();
        }

        private void ImportWithSameName(object sender, EventArgs e)
        {
            var dialog = new ImportCloneOfCatalogueItem(_catalogueItem.Catalogue, _catalogueItem, true);
            dialog.ShowDialog();
            _catalogueItem.SaveToDatabase();

            _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(_catalogueItem));
        }

        private void ImportAnyDescription(object sender, EventArgs e)
        {
            var dialog = new ImportCloneOfCatalogueItem(_catalogueItem.Catalogue, _catalogueItem);
            dialog.ShowDialog();
            _catalogueItem.SaveToDatabase();

            _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(_catalogueItem));
        }

        public void HandleKeyPress(KeyEventArgs k)
        {
            if(k.KeyCode == Keys.I && k.Shift && k.Control)
            {
                ImportAnyDescription(null,null);
                return;
            }
            
            if(k.KeyCode == Keys.I && k.Control)
            {
                ImportWithSameName(null,null);
                return;
            }
        }
    }
}
