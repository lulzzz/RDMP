// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MapsDirectlyToDatabaseTable;
using Rdmp.UI.SimpleDialogs;

namespace Rdmp.UI.SimpleControls
{
    public partial class SelectIMapsDirectlyToDatabaseTableComboBox : UserControl
    {
        private List<IMapsDirectlyToDatabaseTable> _available;
        private bool _settingUp;
        public event EventHandler<EventArgs> SelectedItemChanged;

        public IMapsDirectlyToDatabaseTable SelectedItem
        {
            get { return suggestComboBox1.SelectedItem as IMapsDirectlyToDatabaseTable; }
            set
            {
                if (value != null && !_available.Contains(value))
                {
                    _available.Add(value);
                    SetUp(_available);
                }

                //avoids circular event calls
                if(!Equals(suggestComboBox1.SelectedItem,value))
                {
                    if (value != null)
                        suggestComboBox1.SelectedItem = value;
                    else
                        suggestComboBox1.SelectedIndex = -1;

                    suggestComboBox1_SelectedIndexChanged(this,new EventArgs());
                }
            }
        }

        public SelectIMapsDirectlyToDatabaseTableComboBox()
        {
            InitializeComponent();

            suggestComboBox1.PropertySelector = (s) => s.Cast<object>().Select(o => o == null ? "<None>>": o.ToString());
            suggestComboBox1.SelectedIndexChanged += suggestComboBox1_SelectedIndexChanged;
        }

        void suggestComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(_settingUp)
                return;

            if(SelectedItemChanged != null)
                SelectedItemChanged(this,new EventArgs());
        }

        public void SetUp(IEnumerable<IMapsDirectlyToDatabaseTable> available)
        {
            _settingUp = true;
            _available = available.ToList();

            try
            {
                int before = suggestComboBox1.SelectedIndex;
                suggestComboBox1.DataSource = available;

                //if it was clear before don't take item 0
                if(before == -1)
                    suggestComboBox1.SelectedIndex = -1;
            }
            finally 
            {
                _settingUp = false;
            }
        }

        private void lPick_Click(object sender, System.EventArgs e)
        {
            var dialog = new SelectIMapsDirectlyToDatabaseTableDialog(_available.Cast<IMapsDirectlyToDatabaseTable>(), false, false);
            if (dialog.ShowDialog() == DialogResult.OK)
                suggestComboBox1.SelectedItem = dialog.Selected;
        }

        private void suggestComboBox1_TextUpdate(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(suggestComboBox1.Text))
                suggestComboBox1.SelectedIndex = -1;

        }

        
    }
}
