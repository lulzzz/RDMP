// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Drawing;
using Rdmp.Core.Curation.Data;
using Rdmp.UI.Icons.IconOverlays;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.Icons.IconProvision.StateBasedIconProviders
{
    public class ColumnInfoStateBasedIconProvider : IObjectStateBasedIconProvider
    {
        private readonly IconOverlayProvider _overlayProvider;
        private Bitmap _columnInfo;
        private Bitmap _columnInfoWithANO;

        public ColumnInfoStateBasedIconProvider(IconOverlayProvider overlayProvider)
        {
            _overlayProvider = overlayProvider;
            _columnInfo = CatalogueIcons.ColumnInfo;
            _columnInfoWithANO = CatalogueIcons.ANOColumnInfo;
        }
        public Bitmap GetImageIfSupportedObject(object o)
        {
            var columnInfo = o as ColumnInfo;

            if (columnInfo == null)
                return null;

            var basicIcon = columnInfo.ANOTable_ID != null ? _columnInfoWithANO : _columnInfo;

            if (columnInfo.IsPrimaryKey)
                return _overlayProvider.GetOverlay(basicIcon, OverlayKind.Key);
            
            return basicIcon;
        }
    }
}