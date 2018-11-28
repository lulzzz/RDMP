﻿using System;
using System.Collections.Generic;
using System.Linq;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Dashboarding;
using CatalogueLibrary.Repositories;
using CatalogueManager.Collections;
using CatalogueManager.ItemActivation;
using CatalogueManager.ItemActivation.Emphasis;
using CatalogueManager.Refreshing;
using CatalogueManager.SimpleDialogs.Reports;
using CommandLine;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode.Checks;
using ReusableUIComponents;
using WeifenLuo.WinFormsUI.Docking;

namespace ResearchDataManagementPlatform.WindowManagement.ContentWindowTracking.Persistence
{
    /// <summary>
    /// A Document Tab that hosts an RDMPCollection, the control knows how to save itself to the persistence settings file for the user ensuring that when they next open the
    /// software the Tab can be reloaded and displayed.  Persistance involves storing this Tab type, the Collection Control type being hosted by the Tab (an RDMPCollection).
    /// Since there can only ever be one RDMPCollection of any Type active at a time this is all that must be stored to persist the control
    /// </summary>
    [TechnicalUI]
    [System.ComponentModel.DesignerCategory("")]
    public class PersistableToolboxDockContent:DockContent
    {
        public const string Prefix = "Toolbox";

        public readonly RDMPCollection CollectionType;

        PersistStringHelper persistStringHelper = new PersistStringHelper();

        public PersistableToolboxDockContent(RDMPCollection collectionType)
        {
            CollectionType = collectionType;
        }

        protected override string GetPersistString()
        {
            var ui = Controls.OfType<RDMPCollectionUI>().Single();

            var pin = ui.CommonFunctionality.CurrentlyPinned as IMapsDirectlyToDatabaseTable;

            var args = new Dictionary<string, string>();
            args.Add("Toolbox",CollectionType.ToString());
            
            if(pin != null)
                args.Add("Pin",persistStringHelper.GetObjectCollectionPersistString(pin));

            return Prefix + PersistStringHelper.Separator + persistStringHelper.SaveDictionaryToString(args);
        }

        public void LoadPersistString(IActivateItems activator, string persistString)
        {
            try
            {
                var s = persistString.Substring(Prefix.Length + 1);
                var pinValue = persistStringHelper.GetValueIfExistsFromPersistString("Pin", s);

                if (pinValue != null)
                {
                    var toPin = persistStringHelper.GetObjectCollectionFromPersistString(pinValue, activator.RepositoryLocator).SingleOrDefault();

                    if(toPin != null)
                        activator.RequestItemEmphasis(this,new EmphasiseRequest(toPin){Pin = true,ExpansionDepth = 2});
                }
            }
            catch (Exception e)
            {
                activator.GlobalErrorCheckNotifier.OnCheckPerformed(new CheckEventArgs("Failed to LoadPersistString '" + persistString + "' for collection " + CollectionType, CheckResult.Fail, e));
            }
        }

        public RDMPCollectionUI GetCollection()
        {
            return Controls.OfType<RDMPCollectionUI>().SingleOrDefault();
        }

        public static RDMPCollection? GetToolboxFromPersistString(string persistString)
        {
            var helper = new PersistStringHelper();
            var s = persistString.Substring(PersistableToolboxDockContent.Prefix.Length + 1);

            var args = helper.LoadDictionaryFromString(s);

            RDMPCollection collection;

            if (args.ContainsKey("Toolbox"))
            {
                Enum.TryParse(args["Toolbox"], true, out collection);
                return collection;
            }

            return null;
        }
    }
}
