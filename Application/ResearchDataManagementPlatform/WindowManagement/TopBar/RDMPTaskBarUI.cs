// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Windows.Forms;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.Curation.Data;
using Rdmp.UI.Collections;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.Theme;
using ResearchDataManagementPlatform.WindowManagement.HomePane;
using ReusableLibraryCode.Checks;
using ReusableUIComponents;
using WeifenLuo.WinFormsUI.Docking;

namespace ResearchDataManagementPlatform.WindowManagement.TopBar
{
    /// <summary>
    /// Allows you to access the main object collections that make up the RDMP.  These include 
    /// </summary>
    public partial class RDMPTaskBarUI : UserControl
    {
        private WindowManager _manager;
        
        private const string CreateNewLayout = "<<New Layout>>";

        public RDMPTaskBarUI()
        {
            InitializeComponent();
            BackColorProvider provider = new BackColorProvider();

            btnHome.Image = FamFamFamIcons.application_home;
            btnCatalogues.Image = CatalogueIcons.Catalogue;
            btnCatalogues.BackgroundImage = provider.GetBackgroundImage(btnCatalogues.Size, RDMPCollection.Catalogue);

            btnCohorts.Image = CatalogueIcons.CohortIdentificationConfiguration;
            btnCohorts.BackgroundImage = provider.GetBackgroundImage(btnCohorts.Size, RDMPCollection.Cohort);

            btnSavedCohorts.Image = CatalogueIcons.AllCohortsNode;
            btnSavedCohorts.BackgroundImage = provider.GetBackgroundImage(btnSavedCohorts.Size, RDMPCollection.SavedCohorts);

            btnDataExport.Image = CatalogueIcons.Project;
            btnDataExport.BackgroundImage = provider.GetBackgroundImage(btnDataExport.Size, RDMPCollection.DataExport);

            btnTables.Image = CatalogueIcons.TableInfo;
            btnTables.BackgroundImage = provider.GetBackgroundImage(btnTables.Size, RDMPCollection.Tables);

            btnLoad.Image = CatalogueIcons.LoadMetadata;
            btnLoad.BackgroundImage = provider.GetBackgroundImage(btnLoad.Size, RDMPCollection.DataLoad);
            
            btnFavourites.Image = CatalogueIcons.Favourite;
            btnDeleteLayout.Image = FamFamFamIcons.delete;
        }

        public void SetWindowManager(WindowManager manager)
        {
            _manager = manager;
            _manager.TabChanged += _manager_TabChanged;
            btnDataExport.Enabled = manager.RepositoryLocator.DataExportRepository != null;
            
            ReCreateDropDowns();
            
            SetupToolTipText();

            _manager.ActivateItems.Theme.ApplyTo(toolStrip1);
        }

        void _manager_TabChanged(object sender, IDockContent newTab)
        {
            btnBack.Enabled = _manager.Navigation.CanBack();
            btnForward.Enabled = _manager.Navigation.CanForward();
        }


        private void SetupToolTipText()
        {
            int maxCharactersForButtonTooltips = 200;

            try
            {
                var store = _manager.ActivateItems.RepositoryLocator.CatalogueRepository.CommentStore;

                btnHome.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(HomeUI));
                btnCatalogues.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(CatalogueCollectionUI));
                btnCohorts.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(CohortIdentificationCollectionUI));
                btnSavedCohorts.ToolTipText =  store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(SavedCohortsCollectionUI));
                btnDataExport.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(DataExportCollectionUI));
                btnTables.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(TableInfoCollectionUI));
                btnLoad.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(LoadMetadataCollectionUI));
                btnFavourites.ToolTipText = store.GetTypeDocumentationIfExists(maxCharactersForButtonTooltips, typeof(FavouritesCollectionUI)); 

            }
            catch (Exception e)
            {
                _manager.ActivateItems.GlobalErrorCheckNotifier.OnCheckPerformed(new CheckEventArgs("Failed to setup tool tips", CheckResult.Fail, e));
            }

        }

        private void ReCreateDropDowns()
        {
            CreateDropDown<WindowLayout>(cbxLayouts, CreateNewLayout);
        }

        private void CreateDropDown<T>(ToolStripComboBox cbx, string createNewDashboard) where T:IMapsDirectlyToDatabaseTable, INamed
        {
            const int xPaddingForComboText = 10;

            if (cbx.ComboBox == null)
                throw new Exception("Expected combo box!");
            
            cbx.ComboBox.Items.Clear();

            var objects = _manager.RepositoryLocator.CatalogueRepository.GetAllObjects<T>();

            cbx.ComboBox.Items.Add("");

            //minimum size that it will be (same width as the combo box)
            int proposedComboBoxWidth = cbx.Width - xPaddingForComboText;

            foreach (T o in objects)
            {
                //add dropdown item
                cbx.ComboBox.Items.Add(o);

                //will that label be too big to fit in text box? if so expand the max width
                proposedComboBoxWidth = Math.Max(proposedComboBoxWidth, TextRenderer.MeasureText(o.Name, cbx.Font).Width);
            }

            cbx.DropDownWidth = Math.Min(400, proposedComboBoxWidth + xPaddingForComboText);
            cbx.ComboBox.SelectedItem = "";

            cbx.Items.Add(createNewDashboard);
        }

        private void btnHome_Click(object sender, EventArgs e)
        {
            _manager.CloseAllToolboxes();
            _manager.CloseAllWindows();
            _manager.PopHome();
        }

        private void ToolboxButtonClicked(object sender, EventArgs e)
        {
            RDMPCollection collection = ButtonToEnum(sender);

            if (_manager.IsVisible(collection))
                _manager.Pop(collection);
            else
                _manager.Create(collection);
        }

        private RDMPCollection ButtonToEnum(object button)
        {
            RDMPCollection collectionToToggle;

            if (button == btnCatalogues)
                collectionToToggle = RDMPCollection.Catalogue;
            else
            if (button == btnCohorts)
                collectionToToggle = RDMPCollection.Cohort;
            else
            if (button == btnDataExport)
                collectionToToggle = RDMPCollection.DataExport;
            else
            if (button == btnTables)
                collectionToToggle = RDMPCollection.Tables;
            else
            if (button == btnLoad)
                collectionToToggle = RDMPCollection.DataLoad;
            else if (button == btnSavedCohorts)
                collectionToToggle = RDMPCollection.SavedCohorts;
            else if (button == btnFavourites)
                collectionToToggle = RDMPCollection.Favourites;
            else
                throw new ArgumentOutOfRangeException();

            return collectionToToggle;
        }

        
        private void cbx_DropDownClosed(object sender, EventArgs e)
        {
            var cbx = (ToolStripComboBox)sender;
            var toOpen = cbx.SelectedItem as INamed;

            if (ReferenceEquals(cbx.SelectedItem, CreateNewLayout))
                AddNewLayout();

            if (toOpen != null)
            {
                var cmd = new ExecuteCommandActivate(_manager.ActivateItems, toOpen);
                cmd.Execute();
            }

            UpdateButtonEnabledness();
        }



        private void cbx_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonEnabledness();
        }

        private void UpdateButtonEnabledness()
        {
            btnSaveWindowLayout.Enabled = cbxLayouts.SelectedItem is WindowLayout;
            btnDeleteLayout.Enabled = cbxLayouts.SelectedItem is WindowLayout;
        }

        private void AddNewLayout()
        {
            string xml = _manager.MainForm.GetCurrentLayoutXml();

            var dialog = new TypeTextOrCancelDialog("Layout Name", "Name", 100, null, false);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var layout = new WindowLayout(_manager.RepositoryLocator.CatalogueRepository, dialog.ResultText,xml);

                var cmd = new ExecuteCommandActivate(_manager.ActivateItems, layout);
                cmd.Execute();

                ReCreateDropDowns();
            }
        }


        public void InjectButton(ToolStripButton button)
        {
            toolStrip1.Items.Add(button);
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            ToolStripComboBox cbx;
            if (sender == btnDeleteLayout)
                cbx = cbxLayouts;
            else
                throw new Exception("Unexpected sender");

            var d = cbx.SelectedItem as IDeleteable;
            if (d != null)
            {
                _manager.ActivateItems.DeleteWithConfirmation(this, d);
                ReCreateDropDowns();
            }
        }

        private void btnSaveWindowLayout_Click(object sender, EventArgs e)
        {
            var layout = cbxLayouts.SelectedItem as WindowLayout;
            if(layout != null)
            {
                string xml = _manager.MainForm.GetCurrentLayoutXml();

                layout.LayoutData = xml;
                layout.SaveToDatabase();
            }
        }

        private void btnBack_ButtonClick(object sender, EventArgs e)
        {
            _manager.Navigation.Back(true);
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            _manager.Navigation.Forward(true);
        }

        private void btnBack_DropDownOpening(object sender, EventArgs e)
        {
            btnBack.DropDownItems.Clear();

            int backIndex = 1;

            foreach (DockContent history in _manager.Navigation.GetHistory(16))
            {
                var i = backIndex++;
                btnBack.DropDownItems.Add(history.TabText,null,(a,b)=>_manager.Navigation.Back(i,true));
            }
        }
    }
}
