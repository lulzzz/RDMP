﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.ImportExport;
using CatalogueLibrary.Data.Serialization;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.Copying.Commands;
using CatalogueManager.ItemActivation;

namespace CatalogueManager.CommandExecution
{
    internal class ExecuteCommandImportCatalogueDescriptionsFromShare : BasicUICommandExecution
    {
        private readonly FileCollectionCommand _sourceFileCollection;
        private readonly Catalogue _targetCatalogue;

        public ExecuteCommandImportCatalogueDescriptionsFromShare(IActivateItems activator, FileCollectionCommand sourceFileCollection, Catalogue targetCatalogue): base(activator)
        {
            if(!sourceFileCollection.IsShareDefinition)
                SetImpossible("Only ShareDefinition files can be imported");

            _sourceFileCollection = sourceFileCollection;
            _targetCatalogue = targetCatalogue;
        }

        public override void Execute()
        {
            base.Execute();

            var json = File.ReadAllText(_sourceFileCollection.Files.Single().FullName);
            var sm = new ShareManager(Activator.RepositoryLocator);
            
            List<ShareDefinition> shareDefinitions = sm.GetShareDefinitionList(json);

            var first = shareDefinitions.First();

            if(first.Type != typeof(Catalogue))
                throw new Exception("ShareDefinition was not for a Catalogue");

            if(_targetCatalogue.Name != (string) first.Properties["Name"])
                throw new Exception("Catalogue Name is '"+_targetCatalogue.Name + "' but ShareDefinition is for, '" + first.Properties["Name"] +"'");


            sm.ImportPropertiesOnly(_targetCatalogue, first);
            _targetCatalogue.SaveToDatabase();

            var liveCatalogueItems = _targetCatalogue.CatalogueItems;
            
            foreach (ShareDefinition sd in shareDefinitions.Skip(1))
            {
                if(sd.Type != typeof(CatalogueItem))
                    throw new Exception("Unexpected shared object of Type " + sd.Type + " (Expected ShareDefinitionList to have 1 Catalogue + N CatalogueItems)");

                var shareName =(string) sd.Properties["Name"];

                var existingMatch = liveCatalogueItems.FirstOrDefault(ci => ci.Name.Equals(shareName));

                if(existingMatch == null)
                    existingMatch = new CatalogueItem(Activator.RepositoryLocator.CatalogueRepository,_targetCatalogue,shareName);

                sm.ImportPropertiesOnly(existingMatch,sd);
                existingMatch.SaveToDatabase();
            }

            Publish(_targetCatalogue);
        }
    }
}