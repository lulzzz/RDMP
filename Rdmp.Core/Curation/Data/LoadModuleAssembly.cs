// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Attributes;
using MapsDirectlyToDatabaseTable.Injection;
using Rdmp.Core.CommandLine.Runners;
using Rdmp.Core.Curation.Data.ImportExport;
using Rdmp.Core.Curation.Data.Serialization;
using Rdmp.Core.Repositories;

namespace Rdmp.Core.Curation.Data
{
    /// <summary>
    /// This entity stores the large binary blob for the <see cref="Plugin"/> class.  The <see cref="Bin"/> can be written to disk
    /// as a nuget file which can be loaded at runtime to add plugin functionality for rdmp.
    /// </summary>
    public class LoadModuleAssembly : DatabaseEntity, IInjectKnown<Plugin>
    {
       #region Database Properties
        private Byte[] _bin;
        private string _committer;
        private DateTime _uploadDate;
        private int _plugin_ID;
        private Lazy<Plugin> _knownPlugin;


        /// <summary>
        /// The assembly (dll) file as a Byte[], use File.WriteAllBytes to write it to disk
        /// </summary>
        public Byte[] Bin
        {
	        get { return _bin;}
	        set { SetField(ref _bin,value);}
        }
        
        /// <summary>
        /// The user who uploaded the dll
        /// </summary>
        public string Committer
        {
	        get { return _committer;}
	        set { SetField(ref _committer,value);}
        }

        /// <summary>
        /// The date the dll was uploaded
        /// </summary>
        public DateTime UploadDate
        {
	        get { return _uploadDate;}
	        set { SetField(ref _uploadDate,value);}
        }
        
        /// <summary>
        /// The plugin this file forms a part of (each <see cref="Plugin"/> will usually have multiple dlls as part of it's dependencies)
        /// </summary>
        [Relationship(typeof(Plugin), RelationshipType.SharedObject)]
        public int Plugin_ID
        {
	        get { return _plugin_ID;}
	        set { SetField(ref _plugin_ID,value);}
        }

        #endregion

        #region Relationships
        
        /// <inheritdoc cref="Plugin_ID"/>
        [NoMappingToDatabase]
        public Plugin Plugin { get { return _knownPlugin.Value; }}

        #endregion

        /// <summary>
        /// Uploads the given dll file to the catalogue database ready for use as a plugin within RDMP (also uploads any pdb file in the same dir)
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="f"></param>
        /// <param name="plugin"></param>
        public LoadModuleAssembly(ICatalogueRepository repository, FileInfo f, Plugin plugin)
        {
            var dictionaryParameters = GetDictionaryParameters(f, plugin);

            //so we can reference it in fetch requests to check for duplication (normaly Repository is set during hydration by the repo)
            Repository = repository;

            Repository.InsertAndHydrate(this,dictionaryParameters);
            ClearAllInjections();
        }

        internal LoadModuleAssembly(ICatalogueRepository repository, DbDataReader r)
            : base(repository, r)
        {
            Bin = r["Bin"] as byte[];
            Committer = r["Committer"] as string;
            UploadDate = Convert.ToDateTime(r["UploadDate"]);
            Plugin_ID = Convert.ToInt32(r["Plugin_ID"]);
            ClearAllInjections();
        }
        
        internal LoadModuleAssembly(ShareManager shareManager, ShareDefinition shareDefinition)
        {
            shareManager.UpsertAndHydrate(this, shareDefinition);
            ClearAllInjections();
        }

        
        /// <summary>
        /// Downloads the plugin dll/pdb/src to the given directory
        /// </summary>
        /// <param name="downloadDirectory"></param>
        public string DownloadAssembly(DirectoryInfo downloadDirectory)
        {
            string targetDirectory = downloadDirectory.FullName;

            if (targetDirectory == null)
                throw new Exception("Could not get currently executing assembly directory");

            if (!downloadDirectory.Exists)
                downloadDirectory.Create();

            string targetFile = Path.Combine(targetDirectory, Plugin.Name);
            
            //file already exists
            if (File.Exists(targetFile))
                if(AreEqual(File.ReadAllBytes(targetFile), Bin))
                    return targetFile;

            int timeout = 5000;

            TryAgain:
            try
            {
                //if it has changed length or does not exist, write it out to the hardisk
                File.WriteAllBytes(targetFile, Bin);
            }
            catch (Exception)
            {
                timeout -= 100;
                Thread.Sleep(100);

                if (timeout <= 0)
                    throw;

                goto TryAgain;
            }

            return targetFile;
        }

        private Dictionary<string, object> GetDictionaryParameters(FileInfo f, Plugin plugin)
        {
            if(f.Extension != PackPluginRunner.PluginPackageSuffix)
                throw new Exception("Expected LoadModuleAssembly file to be a " + PackPluginRunner.PluginPackageSuffix);

            byte[] allBytes = File.ReadAllBytes(f.FullName);

            var dictionaryParameters = new Dictionary<string, object>()
                {
                    {"Bin",allBytes},
                    {"Committer",Environment.UserName},
                    {"Plugin_ID",plugin.ID}
                };

            return dictionaryParameters;
        }

        private bool AreEqual(byte[] readAllBytes, byte[] dll)
        {
            if (readAllBytes.Length != dll.Length)
                return false;

            for (int i = 0; i < dll.Length; i++)
                if (!readAllBytes[i].Equals(dll[i]))
                    return false;

            return true;
        }

        public void InjectKnown(Plugin instance)
        {
            _knownPlugin = new Lazy<Plugin>(() => instance);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "LoadModuleAssembly_" + ID;
        }

        public void ClearAllInjections()
        {
            _knownPlugin = new Lazy<Plugin>(() => Repository.GetObjectByID<Plugin>(Plugin_ID));
        }
    }
}
