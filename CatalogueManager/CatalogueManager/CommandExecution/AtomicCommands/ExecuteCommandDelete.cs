using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Text.RegularExpressions;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueManager.ItemActivation;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode;
using ReusableLibraryCode.CommandExecution;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;
using ReusableUIComponents;
using ReusableUIComponents.CommandExecution;
using ReusableUIComponents.CommandExecution.AtomicCommands;
using ScintillaNET;

namespace CatalogueManager.CommandExecution.AtomicCommands
{
    public class ExecuteCommandDelete : BasicUICommandExecution, IAtomicCommand
    {
        private readonly IDeleteable _deletable;

        public ExecuteCommandDelete(IActivateItems activator, IDeleteable deletable) : base(activator)
        {
            _deletable = deletable;
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return null;
        }

        public override void Execute()
        {
            base.Execute();
            
            Activator.DeleteWithConfirmation(this, _deletable);
        }
    }
}