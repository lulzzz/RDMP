// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ReusableUIComponents
{
    /// <summary>
    /// Factory for generating a consistent representation in a <see cref="ToolStrip"/> of a user configurable timeout period.
    /// </summary>
    public class ToolStripTimeout
    {
        ToolStripLabel timeoutLabel = new ToolStripLabel("Timeout:");
        ToolStripTextBox tbTimeout = new ToolStripTextBox(){Text = "300"};
        private int _timeout;

        public int Timeout
        {
            get { return _timeout; }
            set
            {
                _timeout = value;
                tbTimeout.Text = value.ToString();
            }
        }

        public ToolStripTimeout()
        {
            tbTimeout.TextChanged += tbTimeout_TextChanged;
        }
        public IEnumerable<ToolStripItem> GetControls()
        {
            yield return timeoutLabel;
            yield return tbTimeout;
        }

        private void tbTimeout_TextChanged(object sender, EventArgs e)
        {
            try
            {
                _timeout = int.Parse(tbTimeout.Text);
                tbTimeout.ForeColor = Color.Black;
            }
            catch (Exception)
            {
                tbTimeout.ForeColor = Color.Red;
            }
        }
    }
}
