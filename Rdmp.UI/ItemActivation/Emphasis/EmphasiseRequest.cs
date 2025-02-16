// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using MapsDirectlyToDatabaseTable;
using Rdmp.UI.CommandExecution.AtomicCommands;

namespace Rdmp.UI.ItemActivation.Emphasis
{
    /// <summary>
    /// Models a request to make a given object (<see cref="ObjectToEmphasise"/>) in an RDMP tree view visible to the user.
    /// See Also <see cref="ExecuteCommandShow"/> and <see cref="ExecuteCommandPin"/>.
    /// </summary>
    public class EmphasiseRequest
    {
        public IMapsDirectlyToDatabaseTable ObjectToEmphasise { get; set; }
        public int ExpansionDepth { get; set; }
        public bool Pin { get; set; }

        public EmphasiseRequest(IMapsDirectlyToDatabaseTable objectToEmphasise, int expansionDepth = 0)
        {
            ObjectToEmphasise = objectToEmphasise;
            ExpansionDepth = expansionDepth;
            Pin = false;
        }
    }
}
