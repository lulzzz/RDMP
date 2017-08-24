﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrightIdeasSoftware;
using CatalogueManager.ItemActivation;
using CatalogueManager.Refreshing;
using CatalogueManager.TestsAndSetup.ServicePropogation;
using DataExportLibrary.CohortDescribing;
using DataExportLibrary.Data.DataTables;
using ReusableLibraryCode;
using ReusableUIComponents;

namespace DataExportManager.CohortUI
{
    /// <summary>
    /// Shows a collection of cohorts that are ready for data extraction (typically all the cohorts associated with a project or a global list of all cohorts).  These are identifier lists
    /// and release identifier substitutions stored in the external cohort database(s).  The control provides a readonly summary of the number of unique patient identifiers in each cohort.
    /// If a project/global list includes more than one Cohort Source (e.g. you link NHS numbers to ReleaseIdentifiers but also link CHI numbers to ReleaseIdentifiers or if you have the same
    /// private identifier but different release identifier formats) then each seperate cohort source table will be listed along with the associated cohorts found by RDMP.
    /// </summary>
    public partial class ExtractableCohortCollection : RDMPUserControl, ILifetimeSubscriber
    {
        public ExtractableCohortCollection()
        {
            InitializeComponent();

            lbCohortDatabaseTable.FormatRow += lbCohortDatabaseTable_FormatRow;
            lbCohortDatabaseTable.AlwaysGroupByColumn = olvSource;
            lbCohortDatabaseTable.CellToolTipShowing += LbCohortDatabaseTableOnCellToolTipShowing;

            //always show selection in the same highlight colour
            lbCohortDatabaseTable.SelectedForeColor = Color.White;
            lbCohortDatabaseTable.SelectedBackColor = Color.FromArgb(55, 153, 255);
            lbCohortDatabaseTable.UnfocusedSelectedForeColor = Color.White;
            lbCohortDatabaseTable.UnfocusedSelectedBackColor = Color.FromArgb(55, 153, 255);

        }


        private bool haveSubscribed = false;

        public void SetupForAllCohorts(IActivateItems activator)
        {
            try
            {
                if(!haveSubscribed)
                {
                    activator.RefreshBus.EstablishLifetimeSubscription(this);
                    haveSubscribed = true;
                }

                ReFetchCohortDetailsAsync();
            }
            catch (Exception e)
            {
                ExceptionViewer.Show(this.GetType().Name + " could not load Cohorts:" + Environment.NewLine + ExceptionHelper.ExceptionToListOfInnerMessages(e), e);
            }
        }

        public void SetupFor(ExtractableCohort[] cohorts)
        {
            try
            {
                lbCohortDatabaseTable.ClearObjects();
                lbCohortDatabaseTable.AddObjects(cohorts.Select(cohort => new ExtractableCohortDescription(cohort)).ToArray());
            }
            catch (Exception e)
            {
                ExceptionViewer.Show(this.GetType().Name + " could not load Cohorts:" + Environment.NewLine + ExceptionHelper.ExceptionToListOfInnerMessages(e), e);
            }
        }


        private void ReFetchCohortDetailsAsync()
        {
            lbCohortDatabaseTable.ClearObjects();

            //gets the empty placeholder cohort objects, these have string values like "Loading..." and -1 for counts but each one comes with a Fetch object, the node will populate itself once the callback finishes
            CohortDescriptionFactory factory = new CohortDescriptionFactory(RepositoryLocator.DataExportRepository);
            var fetchDescriptionsDictionary = factory.Create();

            lbCohortDatabaseTable.AddObjects(fetchDescriptionsDictionary.SelectMany(kvp => kvp.Value).ToArray());

            //Just because the object updates itself doesn't mean ObjectListView will notice, so we must also subscribe to the fetch completion (1 per cohort source table) 
            //when the fetch completes, update the UI nodes (they also themselves subscribe to the fetch completion handler and should be registered further up the inovcation list)
            foreach (var kvp in fetchDescriptionsDictionary)
            {
                var fetch = kvp.Key;
                var nodes = kvp.Value;

                //Could be we are disposed when this happens
                fetch.Finished += () =>
                {
                    if (!lbCohortDatabaseTable.IsDisposed)
                        lbCohortDatabaseTable.RefreshObjects(nodes);
                };
            }
        }

        private void lbCohortDatabaseTable_SelectedIndexChanged(object sender, EventArgs e)
        {
            
        }

        public event SelectedCohortChangedHandler SelectedCohortChanged;
        
        private void lbCohortDatabaseTable_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && lbCohortDatabaseTable.SelectedObject != null)
            {
                var node = (ExtractableCohortDescription)lbCohortDatabaseTable.SelectedObject;

                ExtractableCohort toDelete = node.Cohort;

                if (MessageBox.Show("Are you sure you want to delete " + toDelete + " (ID=" + toDelete.ID + ")",
                                    "Confirm Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    try
                    {
                        toDelete.DeleteInDatabase();

                        lbCohortDatabaseTable.RemoveObject(node);
                    }
                    catch (Exception exception)
                    {
                        ExceptionViewer.Show(exception);
                    }
                }
                else
                    return;
            }
        }
        
        private void tbFilter_TextChanged(object sender, EventArgs e)
        {
            lbCohortDatabaseTable.UseFiltering = true;
            lbCohortDatabaseTable.ModelFilter = new TextMatchFilter(lbCohortDatabaseTable, tbFilter.Text, StringComparison.CurrentCultureIgnoreCase);
        }

        private void lbCohortDatabaseTable_FormatRow(object sender, FormatRowEventArgs e)
        {
            var model = e.Model as ExtractableCohortDescription;

            if (model == null)
                return;

            if (model.Exception != null)
            {
                e.Item.BackColor = Color.Red;
                return;
            }

            if (string.IsNullOrWhiteSpace(model.Cohort.AuditLog))
            {
                e.Item.BackColor = Color.FromArgb(255,235,200); 
                return;
            }
        }
        private void lbCohortDatabaseTable_ItemActivate(object sender, EventArgs e)
        {
            var model = lbCohortDatabaseTable.SelectedObject as ExtractableCohortDescription;

            if(model == null)
                return;

            if(model.Exception != null)
                ExceptionViewer.Show(model.Exception);
        }

        public void SetSelectedCohort(ExtractableCohort toSelect)
        {

            if (toSelect == null)
            {
                lbCohortDatabaseTable.SelectedObject = null;
                return;
            }

            var matchingNode = lbCohortDatabaseTable.Objects.Cast<ExtractableCohortDescription>().SingleOrDefault(c => c.Cohort.ID == toSelect.ID);
            
            lbCohortDatabaseTable.SelectedObject = matchingNode;
        }

        private void lbCohortDatabaseTable_SelectionChanged(object sender, EventArgs e)
        {
            var node = lbCohortDatabaseTable.SelectedObject as ExtractableCohortDescription;
            var selected = node == null ? null : node.Cohort;

            if (SelectedCohortChanged != null)
                SelectedCohortChanged(this, selected);
        }
        private void LbCohortDatabaseTableOnCellToolTipShowing(object sender, ToolTipShowingEventArgs e)
        {

            ExtractableCohortDescription node = (ExtractableCohortDescription)e.Model;

            e.IsBalloon = true;
            e.AutoPopDelay = 32767;
            e.StandardIcon = ToolTipControl.StandardIcons.Info;

            e.Text = string.IsNullOrWhiteSpace(node.Cohort.AuditLog) ? "No Audit Log" : node.Cohort.AuditLog;


        }

        public void RefreshBus_RefreshObject(object sender, RefreshObjectEventArgs e)
        {
            if (e.Object is ExtractableCohort || e.Object is ExternalCohortTable)
                ReFetchCohortDetailsAsync();
        }
    }

    public delegate void SelectedCohortChangedHandler(object sender, ExtractableCohort selected);
}
