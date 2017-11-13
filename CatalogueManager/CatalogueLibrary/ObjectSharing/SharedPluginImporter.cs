﻿using System;
using System.Collections.Generic;
using System.Linq;
using CatalogueLibrary.Data;
using MapsDirectlyToDatabaseTable.ObjectSharing;
using Newtonsoft.Json;
using ReusableLibraryCode.Checks;

namespace CatalogueLibrary.ObjectSharing
{
    public class SharedPluginImporter
    {
        private MapsDirectlyToDatabaseTableStatelessDefinition _plugin;
        private List<MapsDirectlyToDatabaseTableStatelessDefinition> _lma;

        public SharedPluginImporter(Plugin plugin)
        {
            _plugin = new MapsDirectlyToDatabaseTableStatelessDefinition(plugin);
            _lma = plugin.LoadModuleAssemblies.Select(s => new MapsDirectlyToDatabaseTableStatelessDefinition(s)).ToList();
        }

        public SharedPluginImporter(MapsDirectlyToDatabaseTableStatelessDefinition plugin, MapsDirectlyToDatabaseTableStatelessDefinition[] loadModuleAssemblies)
        {
            _plugin = plugin;
            _lma = loadModuleAssemblies.ToList();
        }

        public SharedPluginImporter(string plugin, string loadModuleAssemblies)
        {
            _plugin = JsonConvert.DeserializeObject<MapsDirectlyToDatabaseTableStatelessDefinition>(plugin);

            //Because Json doesn't know how to serialise Type properties
            _plugin.Type = typeof(Plugin);

            _lma = JsonConvert.DeserializeObject<MapsDirectlyToDatabaseTableStatelessDefinition[]>(loadModuleAssemblies).ToList();
            foreach (var l in _lma)
                l.Type = typeof(LoadModuleAssembly);

        }

        public Plugin Import(SharedObjectImporter importer,ICheckNotifier collisionsAndSuggestions)
        {
            var collision = importer.TargetRepository.GetAllObjects<Plugin>().SingleOrDefault(p => p.Name.Equals(_plugin.Properties["Name"]));

            if (collision != null)
            {
                var fix = collisionsAndSuggestions.OnCheckPerformed(new CheckEventArgs("Update existing Plugin with Name '" + collision.Name + "'", CheckResult.Warning));

                if (fix)
                {
                    //it's an update so get the current LoadModuleAssembly of the remote (out of date) Plugin
                    foreach (LoadModuleAssembly lma in collision.LoadModuleAssemblies)
                    {
                        var match = _lma.SingleOrDefault(n => lma.Name.Equals(n.Properties["Name"]));
                        
                        //LoadModuleAssembly is in the new Plugin too so overwrite it
                        if (match != null)
                        {
                            lma.Dll = (byte[]) match.Properties["Dll"];
                            lma.Pdb = (byte[]) match.Properties["Pdb"];
                            lma.Committer = (string) match.Properties["Committer"];
                            lma.Description = (string)match.Properties["Description"];
                            lma.DllFileVersion = (string)match.Properties["DllFileVersion"];
                            lma.UploadDate = (DateTime)match.Properties["UploadDate"];
                            lma.SaveToDatabase();

                            //record that the LoadModuleAssembly was dealt with 
                            _lma.Remove(match);
                        }
                        else
                        {
                            //no longer part of the plugin so remove it
                            lma.DeleteInDatabase();
                        }
                    }

                    //now send any new dlls that aren't yet in the destination
                    foreach (var newNotArrivedYet in _lma)
                    {
                        newNotArrivedYet.Properties["Plugin_ID"] = collision.ID;
                        importer.ImportObject(newNotArrivedYet);
                    }

                    //it was an update to an existing one so return the existing one
                    return collision;
                }
                
                //user chose not to overwrite
                _plugin.Properties["Name"] = _plugin.Properties["Name"] + "(Copy " +  Guid.NewGuid().ToString().Substring(0,5) + ")"; //append a unique guid
            }
            
            //import the plugin (now that it has a unique name or never collided in the first place)
            var newPlugin = (Plugin)importer.ImportObject(_plugin);
            var newPluginID = newPlugin.ID;

            foreach (var definition in _lma)
            {
                definition.Properties["Plugin_ID"] = newPluginID;
                importer.ImportObject(definition);
            }

            return newPlugin;
        }
    }
}
