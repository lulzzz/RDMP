// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Diagnostics;
using FAnsi;
using MapsDirectlyToDatabaseTable;
using Rdmp.Core.CommandLine.DatabaseCreation;
using Rdmp.Core.Repositories;
using Rdmp.Core.Startup;
using ReusableLibraryCode.Checks;
using ReusableLibraryCode.Settings;
using ReusableUIComponents;
using ReusableUIComponents.ChecksUI;

namespace Rdmp.UI.LocationsMenu
{
    /// <summary>
    /// All metadata in RDMP is stored in one of two main databases.  The Catalogue database records all the technical, descriptive, governance, data load, filtering logic etc about 
    /// your datasets (including where they are stored etc).  The Data Export Manager database stores all the extraction configurations you have created for releasing to researchers.
    /// 
    /// <para>This window lets you tell the software where your Catalogue / Data Export Manager databases are or create new ones.  These connection strings are recorded in each users settings file.
    /// It is strongly advised that you use Integrated Security (Windows Security) for connecting rather than a username/password as this is the only case where Passwords are not encrypted
    /// (Since the encryption certificate location is stored in the Catalogue! - see PasswordEncryptionKeyLocationUI).</para>
    /// 
    /// <para>Only the Catalogue database is required, if you do not intend to do data extraction at this time then you can skip creating one.  </para>
    /// 
    /// <para>It is a good idea to run Check after configuring your connection string to ensure that the database is accessible and that the tables/columns in the database match the softwares
    /// expectations.  </para>
    /// 
    /// <para>IMPORTANT: if you configure your connection string wrongly it might take up to 30s for windows to timeout the network connection (e.g. if you specify the wrong server name). This is
    /// similar to if you type in a dodgy server name in Microsoft Windows Explorer.</para>
    /// </summary>
    public partial class ChoosePlatformDatabasesUI : Form
    {
        private readonly IRDMPPlatformRepositoryServiceLocator _repositoryLocator;

        public ChoosePlatformDatabasesUI(IRDMPPlatformRepositoryServiceLocator repositoryLocator)
        {
            _repositoryLocator = repositoryLocator;

            InitializeComponent();

            new RecentHistoryOfControls(tbCatalogueConnectionString, new Guid("75e6b0a3-03f2-49fc-9446-ebc1dae9f123"));
            new RecentHistoryOfControls(tbDataExportManagerConnectionString, new Guid("9ce952d8-d629-454a-ab9b-a1af97548be6"));

            SetState(State.PickNewOrExisting);

            //are we dealing with a database object repository?
            var cataDb = _repositoryLocator.CatalogueRepository as TableRepository;
            var dataExportDb = _repositoryLocator.DataExportRepository as TableRepository;

            //only enable connection string setting if it is a user settings repo
            tbDataExportManagerConnectionString.Enabled = 
            tbCatalogueConnectionString.Enabled =
            btnBrowseForCatalogue.Enabled =
            btnBrowseForDataExport.Enabled =
            btnSaveAndClose.Enabled =
            _repositoryLocator is UserSettingsRepositoryFinder;

            //yes
            tbCatalogueConnectionString.Text = cataDb == null ? null : cataDb.ConnectionString;
            tbDataExportManagerConnectionString.Text = dataExportDb == null ? null : dataExportDb.ConnectionString;
        }

        private void SetState(State newState)
        {
            switch (newState)
            {
                case State.PickNewOrExisting:
                    pChooseOption.Dock = DockStyle.Top;
                    
                    pResults.Visible = false;
                    gbCreateNew.Visible = false;
                    gbUseExisting.Visible = false;

                    pChooseOption.Visible = true;
                    pChooseOption.BringToFront();
                    break;
                case State.CreateNew:

                    pResults.Dock = DockStyle.Fill;
                    gbCreateNew.Dock = DockStyle.Top;
                    
                    
                    pResults.Visible = true;
                    pChooseOption.Visible = false;
                    gbUseExisting.Visible = false;

                    gbCreateNew.Visible = true;
                    pResults.BringToFront();

                    
                    break;
                case State.ConnectToExisting:
                    pResults.Dock = DockStyle.Fill;
                    gbUseExisting.Dock = DockStyle.Top;
                    
                    pChooseOption.Visible = false;
                    gbCreateNew.Visible = false;

                    pResults.Visible = true;
                    gbUseExisting.Visible = true;
                    pResults.BringToFront();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("newState");
            }
        }

        private enum State
        {
            PickNewOrExisting,
            CreateNew,
            ConnectToExisting
        }

        private bool SaveConnectionStrings()
        {
            try
            {
                // save all the settings
                UserSettings.CatalogueConnectionString = tbCatalogueConnectionString.Text;
                UserSettings.DataExportConnectionString = tbDataExportManagerConnectionString.Text;

                ((UserSettingsRepositoryFinder)_repositoryLocator).RefreshRepositoriesFromUserSettings();
                return true;
            }
            catch (Exception exception)
            {
                checksUI1.OnCheckPerformed(new CheckEventArgs("Failed to save connection settings",CheckResult.Fail,exception));
                return false;
            }
            
        }

        private void ChooseDatabase_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
                btnSaveAndClose_Click(null,null);

            if(e.KeyCode == Keys.Escape)
               this.Close();

        }
        private void tbCatalogueConnectionString_KeyUp(object sender, KeyEventArgs e)
        {
            //if user is doing a paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                //check to see what he is pasting
                string toPaste = Clipboard.GetText();

                //he is pasting something with newlines
                if (toPaste.Contains(Environment.NewLine))
                {
                    //see if he is trying to paste two lines at once, in whichcase surpress Windows and paste it across the two text boxes
                    string[] toPasteArray = toPaste.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (toPasteArray.Length == 2)
                    {
                        tbCatalogueConnectionString.Text = toPasteArray[0];
                        tbDataExportManagerConnectionString.Text = toPasteArray[1];
                        e.SuppressKeyPress = true;
                    }
                }
            }
        }
        
        private MissingFieldsChecker CreateMissingFieldsChecker(MissingFieldsChecker.ThingToCheck thingToCheck)
        {
            return new MissingFieldsChecker(thingToCheck, (CatalogueRepository)_repositoryLocator.CatalogueRepository, (DataExportRepository) _repositoryLocator.DataExportRepository);
        }

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            //if save is successful
            if (SaveConnectionStrings())
                //integrity checks passed
                RestartApplication();
        }

        private void btnCheckDataExportManager_Click(object sender, EventArgs e)
        {
            CheckRepository(MissingFieldsChecker.ThingToCheck.DataExportManager);
        }

        private void btnCheckCatalogue_Click(object sender, EventArgs e)
        {
            CheckRepository(MissingFieldsChecker.ThingToCheck.Catalogue);
        }

        private void CheckRepository(MissingFieldsChecker.ThingToCheck repositoryToCheck)
        {
            try
            {
                //save the settings
                SaveConnectionStrings();

                checksUI1.StartChecking(CreateMissingFieldsChecker(repositoryToCheck));
                checksUI1.AllChecksComplete += ShowNextStageOnChecksComplete;
            }
            catch (Exception exception)
            {
                checksUI1.OnCheckPerformed(new CheckEventArgs("Checking of " + repositoryToCheck + " Database failed", CheckResult.Fail,exception));
            }
        }

        private void ShowNextStageOnChecksComplete(object sender, AllChecksCompleteHandlerArgs args)
        {
            ((ChecksUI) sender).AllChecksComplete -= ShowNextStageOnChecksComplete;
        }

        private void btnCreateSuite_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            try
            {
                Cursor = Cursors.WaitCursor;

                Console.SetOut(new StringWriter(sb));

                var opts = new PlatformDatabaseCreationOptions();
                opts.ServerName = tbSuiteServer.Text;
                opts.Prefix = tbDatabasePrefix.Text;
                opts.Username = tbUsername.Text;
                opts.Password = tbPassword.Text;

                var task = new Task(() =>
                {
                    try
                    {
                        var creator = new PlatformDatabaseCreation();
                        creator.CreatePlatformDatabases(opts);
                    }
                    catch (Exception ex)
                    {
                        checksUI1.OnCheckPerformed(
                            new CheckEventArgs("Database creation failed, check exception for details", CheckResult.Fail,
                                ex));
                    }
                });
                task.Start();

                while (!task.IsCompleted)
                {
                    task.Wait(100);
                    Application.DoEvents();

                    var result = sb.ToString();

                    if (string.IsNullOrEmpty(result))
                        continue;

                    sb.Clear();

                    if (result.Contains("Exception"))
                        throw new Exception(result);

                    checksUI1.OnCheckPerformed(new CheckEventArgs(result, CheckResult.Success));
                }

                checksUI1.OnCheckPerformed(new CheckEventArgs("Finished Creating Platform Databases", CheckResult.Success));

                var cata = opts.GetBuilder(PlatformDatabaseCreation.DefaultCatalogueDatabaseName);
                var export = opts.GetBuilder(PlatformDatabaseCreation.DefaultDataExportDatabaseName);
                
                UserSettings.CatalogueConnectionString = cata.ConnectionString;
                UserSettings.DataExportConnectionString = export.ConnectionString;
                RestartApplication();

            }
            catch (Exception exception)
            {
                checksUI1.OnCheckPerformed(new CheckEventArgs("Database creation failed, check exception for details",CheckResult.Fail, exception));
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RestartApplication()
        {
            MessageBox.Show("Connection Strings Changed, the application will now restart");
            Application.Restart();
        }

        private void btnCreateNew_Click(object sender, EventArgs e)
        {
            SetState(State.CreateNew);
        }

        private void btnUseExisting_Click(object sender, EventArgs e)
        {
            SetState(State.ConnectToExisting);
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            SetState(State.PickNewOrExisting);
        }

        private void btnBrowseForCatalogue_Click(object sender, EventArgs e)
        {
            var dialog = new ServerDatabaseTableSelectorDialog("Catalogue Database",false,false);
            dialog.LockDatabaseType(DatabaseType.MicrosoftSQLServer);
            if (dialog.ShowDialog() == DialogResult.OK)
                tbCatalogueConnectionString.Text = dialog.SelectedDatabase.Server.Builder.ConnectionString;
        }

        private void btnBrowseForDataExport_Click(object sender, EventArgs e)
        {
            var dialog = new ServerDatabaseTableSelectorDialog("Data Export Database", false, false);
            dialog.LockDatabaseType(DatabaseType.MicrosoftSQLServer);
            if (dialog.ShowDialog() == DialogResult.OK)
                tbDataExportManagerConnectionString.Text = dialog.SelectedDatabase.Server.Builder.ConnectionString;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
