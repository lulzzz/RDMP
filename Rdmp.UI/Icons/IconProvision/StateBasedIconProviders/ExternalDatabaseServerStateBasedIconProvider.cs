// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Rdmp.Core.Curation.Data;
using Rdmp.Core.Databases;
using Rdmp.Core.Providers.Nodes;
using Rdmp.UI.Icons.IconOverlays;
using ReusableLibraryCode.Icons.IconProvision;

namespace Rdmp.UI.Icons.IconProvision.StateBasedIconProviders
{
    public class ExternalDatabaseServerStateBasedIconProvider : IObjectStateBasedIconProvider
    {
        private readonly IconOverlayProvider _overlayProvider;
        private Bitmap _default;

        Dictionary<string,Bitmap> _assemblyToIconDictionary = new Dictionary<string, Bitmap>();
        private DatabaseTypeIconProvider _typeSpecificIconsProvider;
        
        public ExternalDatabaseServerStateBasedIconProvider(IconOverlayProvider overlayProvider)
        {
            _overlayProvider = overlayProvider;
            _default = CatalogueIcons.ExternalDatabaseServer;
            
            _assemblyToIconDictionary.Add(new DataQualityEnginePatcher().Name,CatalogueIcons.ExternalDatabaseServer_DQE);
            _assemblyToIconDictionary.Add(new ANOStorePatcher().Name, CatalogueIcons.ExternalDatabaseServer_ANO);
            _assemblyToIconDictionary.Add(new IdentifierDumpDatabasePatcher().Name, CatalogueIcons.ExternalDatabaseServer_IdentifierDump);
            _assemblyToIconDictionary.Add(new QueryCachingPatcher().Name, CatalogueIcons.ExternalDatabaseServer_Cache);
            _assemblyToIconDictionary.Add(new LoggingDatabasePatcher().Name, CatalogueIcons.ExternalDatabaseServer_Logging);

            _typeSpecificIconsProvider = new DatabaseTypeIconProvider();
        }

        public Bitmap GetIconForAssembly(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;
            if (_assemblyToIconDictionary.ContainsKey(assemblyName))
                return _assemblyToIconDictionary[assemblyName];

            return _default;
        }

        public Bitmap GetImageIfSupportedObject(object o)
        {
            var server = o as ExternalDatabaseServer;
            var dumpServerUsage = o as IdentifierDumpServerUsageNode;

            if (dumpServerUsage != null)
                server = dumpServerUsage.IdentifierDumpServer;

            //if its not a server we aren't responsible for providing an icon for it
            if (server == null)
                return null;

            //the untyped server icon (e.g. user creates a reference to a server that he is going to use but isn't created/managed by a .Datbase assembly)
            var toReturn = _default;

            //if it is a .Database assembly managed database then use the appropriate icon instead (ANO, LOG, IDD etc)
            if (!string.IsNullOrWhiteSpace(server.CreatedByAssembly) && _assemblyToIconDictionary.ContainsKey(server.CreatedByAssembly))
                toReturn = _assemblyToIconDictionary[server.CreatedByAssembly];
                
            //add the database type overlay
            toReturn = _overlayProvider.GetOverlay(toReturn, _typeSpecificIconsProvider.GetOverlay(server.DatabaseType));

            if (dumpServerUsage != null)
                toReturn = _overlayProvider.GetOverlay(toReturn, OverlayKind.Link);

            return toReturn;
        }
    }
}