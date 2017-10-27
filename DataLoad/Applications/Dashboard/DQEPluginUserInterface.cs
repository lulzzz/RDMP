﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueManager.CommandExecution;
using CatalogueManager.CommandExecution.AtomicCommands.UIFactory;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.LocationsMenu;
using CatalogueManager.PluginChildProvision;
using Dashboard.CommandExecution.AtomicCommands;
using Dashboard.Menus.MenuItems;
using DataQualityEngine.Data;
using MapsDirectlyToDatabaseTableUI;
using ReusableLibraryCode.DataAccess;
using ReusableUIComponents;
using ReusableUIComponents.Icons.IconProvision;

namespace Dashboard
{
    internal class DQEPluginUserInterface : PluginUserInterface
    {

        public DQEPluginUserInterface(IActivateItems itemActivator) : base(itemActivator)
        {

        }

        public override object[] GetChildren(object model)
        {
            return null;
        }

        public override ToolStripMenuItem[] GetAdditionalRightClickMenuItems(DatabaseEntity databaseEntity)
        {
            var c = databaseEntity as Catalogue;

            if (c != null)
                return new[] {new DQEMenuItem(ItemActivator, c)};

            return null;
        }


        public override Bitmap GetImage(object concept, OverlayKind kind = OverlayKind.None)
        {
            return null;
        }
    }
}