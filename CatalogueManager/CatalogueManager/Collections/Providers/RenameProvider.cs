﻿using System;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueManager.Refreshing;
using MapsDirectlyToDatabaseTable;
using ReusableUIComponents;

namespace CatalogueManager.Collections.Providers
{
    public class RenameProvider
    {
        private readonly RefreshBus _refreshBus;
        private readonly ObjectListView _olv;
        private readonly OLVColumn _columnThatSupportsRenaming;
        private readonly Label _lblAdviceAboutRenaming;
        
        public bool AllowRenaming {  
            get
            {
                return _columnThatSupportsRenaming.IsEditable;
            }
            set
            {
                _olv.CellEditActivation = value ? ObjectListView.CellEditActivateMode.SingleClick : ObjectListView.CellEditActivateMode.None;
                _columnThatSupportsRenaming.IsEditable = value;
                _lblAdviceAboutRenaming.Visible = value;
            } }

        public RenameProvider(RefreshBus refreshBus, ObjectListView olv, OLVColumn columnThatSupportsRenaming, Label lblAdviceAboutRenaming)
        {
            _refreshBus = refreshBus;
            _olv = olv;
            _columnThatSupportsRenaming = columnThatSupportsRenaming;
            _lblAdviceAboutRenaming = lblAdviceAboutRenaming;
        }

        public void RegisterEvents()
        {
            _olv.CellEditStarting += OlvOnCellEditStarting;
            _olv.CellEditFinishing += OlvOnCellEditFinishing;

            _columnThatSupportsRenaming.CellEditUseWholeCell = true;
            _columnThatSupportsRenaming.AutoCompleteEditorMode = AutoCompleteMode.None;

            AllowRenaming = true;

        }
        private void OlvOnCellEditStarting(object sender, CellEditEventArgs e)
        {
            if (!(e.RowObject is INamed))
                e.Cancel = true;
        }

        private static bool haveComplainedAboutToStringImplementationOfINamed = false;

        void OlvOnCellEditFinishing(object sender, CellEditEventArgs e)
        {
            if(e.RowObject == null)
                return;
            
            if(e.Column != _columnThatSupportsRenaming)
                return;

            //don't let them rename things to blank names
            if (string.IsNullOrWhiteSpace((string) e.NewValue))
            {
                e.Cancel = true;
                return;
            }

            var name = e.RowObject as INamed;

            if (name != null)
            {
                name.Name = (string)e.NewValue;

                if(!haveComplainedAboutToStringImplementationOfINamed && !name.ToString().Contains(name.Name))
                {

                    WideMessageBox.Show("ToString method of INamed class '" + name.GetType().Name + "' does not return the Name property, this makes it highly unsuitable for RenameProvider.  Try adding the following code to your class:" 
                                        + Environment.NewLine +
                                        @"public override string ToString()
        {
            return Name;
        }"
                        
                        );
                    haveComplainedAboutToStringImplementationOfINamed = true;
                }

                EnsureNameIfCohortIdentificationAggregate(e.RowObject);

                name.SaveToDatabase();
                _refreshBus.Publish(this, new RefreshObjectEventArgs((DatabaseEntity)name));
            }
        }

        private void EnsureNameIfCohortIdentificationAggregate(object o)
        {
            //handle Aggregates that are part of cohort identification
            var aggregate = o as AggregateConfiguration;
            if (aggregate != null)
            {
                var cic = aggregate.GetCohortIdentificationConfigurationIfAny();

                if (cic != null)
                    cic.EnsureNamingConvention(aggregate);
            }
        }
    }
}
