using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueManager.Collections;
using CatalogueManager.CommandExecution.AtomicCommands.UIFactory;
using CatalogueManager.ItemActivation;
using CatalogueManager.SimpleControls;
using CatalogueManager.Refreshing;
using CatalogueManager.SimpleDialogs.Reports;
using CatalogueManager.Theme;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableUIComponents;

namespace CatalogueManager.TestsAndSetup.ServicePropogation
{
    /// <summary>
    /// TECHNICAL: base abstract class for all Controls which are concerned with a single root DatabaseEntity e.g. AggregateGraph is concerned only with an AggregateConfiguration
    /// and it's children.  The reason this class exists is to streamline lifetime publish subscriptions (ensuring multiple tabs editting one anothers database objects happens 
    /// in a seamless a way as possible). 
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [TechnicalUI]
    public abstract class RDMPSingleDatabaseObjectControl<T> : RDMPUserControl, IRDMPSingleDatabaseObjectControl where T : DatabaseEntity
    {
        private Control _colorIndicator;
        private ToolStrip _toolStrip;

        protected IActivateItems _activator;
        private AtomicCommandUIFactory atomicCommandUIFactory;
        
        public DatabaseEntity DatabaseObject { get; private set; }
        protected RDMPCollection AssociatedCollection = RDMPCollection.None;

        public virtual void SetDatabaseObject(IActivateItems activator, T databaseObject)
        {
            _activator = activator;
            _activator.RefreshBus.EstablishSelfDestructProtocol(this,activator,databaseObject);
            DatabaseObject = databaseObject;
            
            if(_colorIndicator == null && AssociatedCollection != RDMPCollection.None)
            {
                var colorProvider = new BackColorProvider();
                _colorIndicator = new Control();
                _colorIndicator.Dock = DockStyle.Top;
                _colorIndicator.Location = new Point(0, 0);
                _colorIndicator.Size = new Size(150, BackColorProvider.IndiciatorBarSuggestedHeight);
                _colorIndicator.TabIndex = 0;
                _colorIndicator.BackColor = colorProvider.GetColor(AssociatedCollection);
                this.Controls.Add(this._colorIndicator);
            }

            //Clear the tool strip 
            if(_toolStrip != null)
                _toolStrip.Items.Clear();
        }

        public void SetDatabaseObject(IActivateItems activator, DatabaseEntity databaseObject)
        {
            SetDatabaseObject(activator,(T)databaseObject);
        }

        public Type GetTypeOfT()
        {
            return typeof (T);
        }

        public virtual string GetTabName()
        {
            var named = DatabaseObject as INamed;

            if (named != null)
                return named.Name;


            if (DatabaseObject != null)
                return DatabaseObject.ToString();

            return "Unamed Tab";
        }
        
        public void Publish(DatabaseEntity e)
        {
            _activator.RefreshBus.Publish(this,new RefreshObjectEventArgs(e));
        }

        public virtual void ConsultAboutClosing(object sender, FormClosingEventArgs e) {}

        /// <summary>
        /// Adds the given <paramref name="cmd"/> to the menu bar at the top of the control
        /// </summary>
        /// <param name="cmd"></param>
        protected void Add(IAtomicCommand cmd)
        {
            if (atomicCommandUIFactory == null)
                atomicCommandUIFactory = new AtomicCommandUIFactory(_activator);

            if (_toolStrip == null)
            {
                _toolStrip = new ToolStrip();
                _toolStrip.Location = new Point(0, 0);
                _toolStrip.TabIndex = 1;
                this.Controls.Add(this._toolStrip);
            }

            _toolStrip.Items.Add(atomicCommandUIFactory.CreateToolStripItem(cmd));
        }

        /// <summary>
        /// Adds the all <see cref="IAtomicCommand"/> specified by <see cref="IActivateItems.PluginUserInterfaces"/> for the current control.  Commands
        /// will appear in the menu bar at the top of the control
        /// </summary>
        protected void AddPluginCommands(IRDMPSingleDatabaseObjectControl control, DatabaseEntity o)
        {
            foreach (var p in _activator.PluginUserInterfaces)
            {
                var toAdd = p.GetAdditionalCommandsForControl(control, o);

                if(toAdd != null)
                    foreach (IAtomicCommand cmd in toAdd)
                        Add(cmd);
            }
        }

    }
}
