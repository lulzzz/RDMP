// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using System.Windows.Forms;
using BrightIdeasSoftware;
using ReusableLibraryCode.CommandExecution;

namespace ReusableUIComponents.CommandExecution
{
    /// <summary>
    /// Handles the commencement of drag operations.  This involves deciding whether a given object can be dragged and parceling up the object
    /// into an <see cref="ICommand"/> (which will gather relevant facts about the object).  Dropping is handled by <see cref="ICommandExecutionFactory"/>
    /// </summary>
    public interface ICommandFactory
    {
        ICommand Create(OLVDataObject o);
        ICommand Create(ModelDropEventArgs e);
        ICommand Create(DragEventArgs e);
        ICommand Create(FileInfo[] files);
        ICommand Create(object modelObject);
        
    }
}
