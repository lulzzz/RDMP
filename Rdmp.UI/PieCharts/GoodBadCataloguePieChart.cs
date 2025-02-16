// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Curation.Data.Dashboarding;
using Rdmp.UI.CommandExecution.AtomicCommands;
using Rdmp.UI.DashboardTabs.Construction;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using Rdmp.UI.Refreshing;
using Rdmp.UI.SimpleDialogs;
using Rdmp.UI.TestsAndSetup.ServicePropogation;
using ReusableUIComponents.Dialogs;

namespace Rdmp.UI.PieCharts
{
    /// <summary>
    /// Part of OverviewScreen, shows a pie chart showing ow many extractable columns are there which do not yet have descriptions in the Data Catalogue Database (See CatalogueItemUI)
    /// 
    /// <para>Each of these can either be displayed for a single catalogue or as a combined total across all active catalogues (not deprecated / internal etc)</para>
    /// 
    /// </summary>
    public partial class GoodBadCataloguePieChart : RDMPUserControl, IDashboardableControl
    {
        private ToolStripButton btnSingleCatalogue = new ToolStripButton("Single",CatalogueIcons.Catalogue) { Name = "btnSingleCatalogue" };
        private ToolStripButton btnAllCatalogues = new ToolStripButton("All",CatalogueIcons.AllCataloguesUsedByLoadMetadataNode){Name= "btnAllCatalogues" };
        private ToolStripButton btnRefresh = new ToolStripButton("Refresh",FamFamFamIcons.text_list_bullets) { Name = "btnRefresh" };
        private ToolStripLabel toolStripLabel1 = new ToolStripLabel("Type:"){Name= "toolStripLabel1" };
        private ToolStripButton btnShowLabels = new ToolStripButton("Labels",FamFamFamIcons.text_align_left) { Name = "btnShowLabels", CheckOnClick = true };

        private List<ToolStripMenuItem> _flagOptions = new List<ToolStripMenuItem>();

        public GoodBadCataloguePieChart()
        {
            InitializeComponent();

            btnViewDataTable.Image = CatalogueIcons.TableInfo;

            this.btnAllCatalogues.Click += new System.EventHandler(this.btnAllCatalogues_Click);
            this.btnSingleCatalogue.Click += new System.EventHandler(this.btnSingleCatalogue_Click);
            this.btnShowLabels.CheckStateChanged += new System.EventHandler(this.btnShowLabels_CheckStateChanged);
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            
            //put edit mode on for the designer
            NotifyEditModeChange(false);
        }

        private void SetupFlags()
        {
            if(!firstTime)
                return;

            AddFlag("Non Extractable Catalogues", c=> c.IncludeNonExtractableCatalogues, (c,r) => c.IncludeNonExtractableCatalogues= r);
            AddFlag("Deprecated Catalogues", c => c.IncludeDeprecatedCatalogues, (c, r) => c.IncludeDeprecatedCatalogues = r);
            AddFlag("Internal Catalogues", c => c.IncludeInternalCatalogues, (c, r) => c.IncludeInternalCatalogues = r);
            AddFlag("Cold Storage Catalogues", c => c.IncludeColdStorageCatalogues, (c, r) => c.IncludeColdStorageCatalogues = r);
            AddFlag("Project Specific Catalogues", c => c.IncludeProjectSpecificCatalogues, (c, r) => c.IncludeProjectSpecificCatalogues = r);

            AddFlag("Non Extractable CatalogueItems", c => c.IncludeNonExtractableCatalogueItems, (c, r) => c.IncludeNonExtractableCatalogueItems = r);
            AddFlag("Internal Catalogue Items", c => c.IncludeInternalCatalogueItems, (c, r) => c.IncludeInternalCatalogueItems = r);
            AddFlag("Deprecated Catalogue Items", c => c.IncludeDeprecatedCatalogueItems, (c, r) => c.IncludeDeprecatedCatalogueItems = r);
            firstTime = false;
        }

        private void AddFlag(string caption, Func<GoodBadCataloguePieChartObjectCollection,bool> getProp, Action<GoodBadCataloguePieChartObjectCollection,bool> setProp)
        {
            var btn = new ToolStripMenuItem(caption);
            btn.Checked = getProp(_collection);
            btn.CheckedChanged += (sender,e) =>{setProp(_collection,((ToolStripMenuItem)sender).Checked);};
            btn.CheckedChanged += (s, e) => GenerateChart();
            btn.CheckOnClick = true;
            _flagOptions.Add(btn);
        }
        private DashboardControl _dashboardControlDatabaseRecord;
        private GoodBadCataloguePieChartObjectCollection _collection;
        
        private void GenerateChart()
        {
            chart1.Visible = false;
            lblNoIssues.Visible = false;

            if (_collection.IsSingleCatalogueMode)
                gbWhatThisIs.Text = "Column Descriptions in " + _collection.GetSingleCatalogueModeCatalogue();
            else
                gbWhatThisIs.Text = "Column Descriptions";

            PopulateAsEmptyDescriptionsChart();
        }
        
        private void PopulateAsEmptyDescriptionsChart()
        {
            try
            {
                CatalogueItem[] catalogueItems = GetCatalogueItems();

                if (!catalogueItems.Any())
                {
                    chart1.DataSource = null;
                    chart1.Visible = false;
                    lblNoIssues.Visible = true;
                    
                    return;
                }

                int countPopulated = 0;
                int countNotPopulated = 0;
                    
                foreach (CatalogueItem ci in catalogueItems)
                    if (string.IsNullOrWhiteSpace(ci.Description))
                        countNotPopulated++;
                    else
                        countPopulated++;

                DataTable dt = new DataTable();
                dt.Columns.Add("Count");
                dt.Columns.Add("State");

                dt.Rows.Add(new object[] { countNotPopulated, "Missing (" + countNotPopulated + ")" });
                dt.Rows.Add(new object[] { countPopulated, "Populated (" + countPopulated + ")" });
                
                chart1.Series[0].XValueMember = dt.Columns[1].ColumnName;
                chart1.Series[0].YValueMembers = dt.Columns[0].ColumnName;

                chart1.DataSource = dt;
                chart1.DataBind();
                chart1.Visible = true;
                lblNoIssues.Visible = false;
            }
            catch (Exception e)
            {
                ExceptionViewer.Show(this.GetType().Name + " failed to load data", e);
            }
        }

        private CatalogueItem[] GetCatalogueItems()
        {
            if (!_collection.IsSingleCatalogueMode)
            {
                var catalogues = Activator.CoreChildProvider.AllCatalogues.Where(c => _collection.Include(c, Activator.RepositoryLocator.DataExportRepository)).ToArray();

                //if there are some
                if (catalogues.Any())
                    return catalogues.SelectMany(c => c.CatalogueItems).Where(ci => _collection.Include(ci)).ToArray();//get the extractable columns
                else
                    return new CatalogueItem[0];//there weren't any so Catalogues so wont be any ExtractionInformationsEither
            }
            else
                return  _collection.GetSingleCatalogueModeCatalogue().CatalogueItems;
        }

        public void RefreshBus_RefreshObject(object sender, RefreshObjectEventArgs e)
        {
            
        }

        bool _bLoading;
        private bool firstTime = true;

        public void SetCollection(IActivateItems activator, IPersistableObjectCollection collection)
        {
            _bLoading = true;
            SetItemActivator(activator);

            _collection = (GoodBadCataloguePieChartObjectCollection)collection;

            if(firstTime)
                SetupFlags();

            btnAllCatalogues.Checked = !_collection.IsSingleCatalogueMode;
            btnSingleCatalogue.Checked = _collection.IsSingleCatalogueMode;
            btnShowLabels.Checked = _collection.ShowLabels;

            CommonFunctionality.Add(btnAllCatalogues);
            CommonFunctionality.Add(toolStripLabel1);
            CommonFunctionality.Add(btnAllCatalogues);
            CommonFunctionality.Add(btnSingleCatalogue);
            CommonFunctionality.Add(btnShowLabels);
            CommonFunctionality.Add(btnRefresh);

            foreach(var mi in _flagOptions)
                CommonFunctionality.AddToMenu(mi);

            GenerateChart();
            _bLoading = false;
        }

        public IPersistableObjectCollection GetCollection()
        {
            return _collection;
        }

        public string GetTabName()
        {
            return Text;
        }


        public IPersistableObjectCollection ConstructEmptyCollection(DashboardControl databaseRecord)
        {
            _dashboardControlDatabaseRecord = databaseRecord;

            return new GoodBadCataloguePieChartObjectCollection();
        }

        public void NotifyEditModeChange(bool isEditModeOn)
        {
            var l = new Point(Margin.Left,Margin.Right);
            var s = new Size(Width - Margin.Horizontal, Height - Margin.Vertical);

            CommonFunctionality.ToolStrip.Visible = isEditModeOn;

            if (isEditModeOn)
            {
                gbWhatThisIs.Location = new Point(l.X, l.Y + 25);//move it down 25 to allow space for tool bar
                gbWhatThisIs.Size = new Size(s.Width, s.Height - 25);//and adjust height accordingly
            }
            else
            {
                gbWhatThisIs.Location = l;
                gbWhatThisIs.Size = s;
            }
        }

        private void btnAllCatalogues_Click(object sender, EventArgs e)
        {
            btnAllCatalogues.Checked = true;
            btnSingleCatalogue.Checked = false;
            _collection.SetAllCataloguesMode();
            GenerateChart();
            SaveCollectionChanges();
        }

        private void btnSingleCatalogue_Click(object sender, EventArgs e)
        {
            var dialog = new SelectIMapsDirectlyToDatabaseTableDialog(Activator.RepositoryLocator.CatalogueRepository.GetAllObjects<Catalogue>(), false,false);

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var c = (Catalogue) dialog.Selected;
                _collection.SetSingleCatalogueMode(c);
                
                btnAllCatalogues.Checked = false;
                btnSingleCatalogue.Checked = true;

                SaveCollectionChanges();
                GenerateChart();
            }
        }
        
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            GenerateChart();
        }

        private void SaveCollectionChanges()
        {
            if (_bLoading)
                return;

            _dashboardControlDatabaseRecord.SaveCollectionState(_collection);
        }
        
        private void btnShowLabels_CheckStateChanged(object sender, EventArgs e)
        {
            _collection.ShowLabels = btnShowLabels.Checked;

            chart1.Series[0]["PieLabelStyle"] = _collection.ShowLabels ? "Inside" : "Disabled";
            SaveCollectionChanges();
        }

        private void btnViewDataTable_Click(object sender, EventArgs e)
        {
            var cmd = new ExecuteCommandShow(Activator, GetCatalogueItems(),1);
            cmd.Execute();
        }
    }
}
