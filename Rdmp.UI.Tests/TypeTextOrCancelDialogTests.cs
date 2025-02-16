﻿// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using NUnit.Framework;
using ReusableUIComponents;
using ReusableUIComponents.Dialogs;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Rdmp.UI.Tests
{
    class TypeTextOrCancelDialogTests : UITests
    {
        [Test]
        public void Test_TypeTextOrCancelDialog_TinyStrings()
        {
            var dlg = new TypeTextOrCancelDialog("f", "m", 5000);

            //pretend like we launched it
            LastUserInterfaceLaunched = dlg;

            //the title and body should be a reasonable length
            Assert.AreEqual(1, dlg.Text.Length);
            Assert.AreEqual(1, GetControl<Label>().Single().Text.Length);

            //dialog shouldn't go thinner than 460 pixels
            Assert.AreEqual(460, dlg.Width);
        }

        [Test]
        public void Test_TypeTextOrCancelDialog_LargeStrings()
        {
            StringBuilder sb = new StringBuilder();

            //send TypeTextOrCancelDialog a million characters
            for (int i = 0; i < 1_000_000; i++)
                sb.Append("f");

            var s = sb.ToString();

            var dlg = new TypeTextOrCancelDialog(s,s,5000);
            
            //pretend like we launched it
            LastUserInterfaceLaunched = dlg;

            //the title and body should be a reasonable length
            Assert.AreEqual(WideMessageBox.MAX_LENGTH_TITLE, dlg.Text.Length);
            Assert.AreEqual(WideMessageBox.MAX_LENGTH_BODY, GetControl<Label>().Single().Text.Length);
            
            //dialog shouldn't go wider than 720 pixels
            Assert.AreEqual(720,dlg.Width);

        }
        
    }
}
