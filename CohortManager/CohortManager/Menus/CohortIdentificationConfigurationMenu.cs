﻿using System.Windows.Forms;
using BrightIdeasSoftware;
using CatalogueLibrary.Data.Cohort;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.Menus;
using CohortManager.Collections.Providers;
using CohortManager.CommandExecution.AtomicCommands;
using DataExportLibrary.Data;
using ReusableUIComponents.ChecksUI;

namespace CohortManager.Menus
{

    [System.ComponentModel.DesignerCategory("")]
    public class CohortIdentificationConfigurationMenu :RDMPContextMenuStrip
    {
        private CohortIdentificationConfiguration _cic;

        public CohortIdentificationConfigurationMenu(IActivateItems activator, CohortIdentificationConfiguration cic) : base( activator,cic)
        {
            _cic = cic;

            Add(new ExecuteCommandCreateNewCohortIdentificationConfiguration(activator));

            if(cic == null)
                return;
            
            Items.Add("View SQL", _activator.CoreIconProvider.GetImage(RDMPConcept.SQL),(s, e) => _activator.ActivateViewCohortIdentificationConfigurationSql(this, cic));


            Items.Add("Clone Configuration", CohortIdentificationIcons.cloneCohortIdentificationConfiguration,
                (s, e) => CloneCohortIdentificationConfiguration());

            
            var freeze = new ToolStripMenuItem("Freeze Configuration",
                CatalogueIcons.FrozenCohortIdentificationConfiguration, (s, e) => FreezeConfiguration());
            freeze.Enabled = !cic.Frozen;
            Items.Add(freeze);
                
            
            AddCommonMenuItems();
        }

        public CohortIdentificationConfigurationMenu(IActivateItems activator,ProjectCohortIdentificationConfigurationAssociation association) : this(activator,association.CohortIdentificationConfiguration)
        {
            
        }

        private void CloneCohortIdentificationConfiguration()
        {
            if (
                MessageBox.Show(
                    "This will create a 100% copy of the entire CohortIdentificationConfiguration including all datasets, filters, parameters and set operations, are you sure this is what you want?",
                    "Confirm Cloning", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {

                var checks = new PopupChecksUI("Cloning " + _cic, false);
                var clone = _cic.CreateClone(checks);

                //Load the clone up
                Publish(clone);
            }
        }

        private void FreezeConfiguration()
        {
            _cic.Freeze();
            Publish(_cic);
        }
    }

    
}
