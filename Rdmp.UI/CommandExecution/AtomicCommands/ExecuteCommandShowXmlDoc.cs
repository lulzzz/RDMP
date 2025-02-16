// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using Rdmp.UI.Icons.IconProvision;
using Rdmp.UI.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;
using ReusableUIComponents;
using ReusableUIComponents.Dialogs;

namespace Rdmp.UI.CommandExecution.AtomicCommands
{
    public class ExecuteCommandShowXmlDoc : BasicUICommandExecution,IAtomicCommand
    {
        private readonly string _title;
        private string _help;

        /// <summary>
        /// sets up the command to show xmldoc for the supplied <paramref name="classOrProperty"/>
        /// </summary>
        /// <param name="activator"></param>
        /// <param name="classOrProperty">Name of a documented class/interface/property (e.g. "CohortIdentificationConfiguration.QueryCachingServer_ID")</param>
        /// <param name="title"></param>
        public ExecuteCommandShowXmlDoc(IActivateItems activator,string classOrProperty, string title):base(activator)
        {
            _title = title;
            _help = activator.RepositoryLocator.CatalogueRepository.CommentStore.GetDocumentationIfExists(classOrProperty, true,true);

            if(string.IsNullOrWhiteSpace(_help))
                SetImpossible("No help available for keyword '" + classOrProperty+"'");
        }
        public override Image GetImage(IIconProvider iconProvider)
        {
            return iconProvider.GetImage(RDMPConcept.Help);
        }

        public override void Execute()
        {
            base.Execute();
            WideMessageBox.Show(_title, _help, WideMessageBoxTheme.Help);
        }
    }
}