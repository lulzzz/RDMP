﻿using System.Drawing;
using System.Linq;
using CatalogueLibrary.CommandExecution.AtomicCommands;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Cohort;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using DataExportLibrary.Data;
using DataExportLibrary.Data.DataTables;
using ReusableLibraryCode.Icons.IconProvision;

namespace DataExportManager.CommandExecution.AtomicCommands
{
    public class ExecuteCommandAssociateCohortIdentificationConfigurationWithProject:BasicUICommandExecution,IAtomicCommandWithTarget
    {
        private Project _project;
        private CohortIdentificationConfiguration _cic;
        private ProjectCohortIdentificationConfigurationAssociation[] _existingAssociations;

        public ExecuteCommandAssociateCohortIdentificationConfigurationWithProject(IActivateItems activator) : base(activator)
        {
            if(!activator.CoreChildProvider.AllCohortIdentificationConfigurations.Any())
                SetImpossible("There are no Cohort Identification Configurations yet");

            _existingAssociations = Activator.RepositoryLocator.DataExportRepository.GetAllObjects<ProjectCohortIdentificationConfigurationAssociation>();
        }

        public override string GetCommandHelp()
        {
            return "Specifies that the Cohort Identification Configuration (query) is only for use generating cohorts for extractions of the specified project";
        }

        public override void Execute()
        {
            if(_project == null)
            {
                //project is not known so get all projects 
                var valid = Activator.RepositoryLocator.DataExportRepository.GetAllObjects<Project>();

                //except if the cic is the launch point
                if (_cic != null)
                    valid =
                        valid.Where(v =>

                            //in which case only add projects which are not already associated with the cic launch point
                            !_existingAssociations.Any(
                                a => a.CohortIdentificationConfiguration_ID == _cic.ID && v.ID == a.Project_ID)).ToArray();

                Project p;
                if (SelectOne(valid, out p))
                    SetTarget(p);
                else
                    return;
            }

            if (_cic == null)
            {
                //cic is not known (but project is thanks to above block)
                var valid =
                    Activator.RepositoryLocator.CatalogueRepository.GetAllObjects<CohortIdentificationConfiguration>();
                
                //allow them to select any cic where it does not already belong to the project
                valid =
                    valid.Where(v =>
                        !_existingAssociations.Any(
                            a => a.Project_ID == _project.ID && v.ID == a.CohortIdentificationConfiguration_ID)).ToArray();


                CohortIdentificationConfiguration cic;
                if (SelectOne(valid, out cic))
                    SetTarget(cic);
                else
                    return;
            }

            //command might be impossible

            base.Execute();

            var assoc = new ProjectCohortIdentificationConfigurationAssociation(Activator.RepositoryLocator.DataExportRepository,_project, _cic);
            
            Publish(_project);
            Publish(_cic);

            Activator.WindowArranger.SetupEditAnything(this,assoc);
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            //if we know the cic the context is 'pick a project'
            if(_cic != null)
                return iconProvider.GetImage(RDMPConcept.Project,OverlayKind.Add);

            //if we know the _project the context is 'pick a cic'  (or if we don't know either then just use this icon too)
            return iconProvider.GetImage(RDMPConcept.CohortIdentificationConfiguration, OverlayKind.Link);
        }

        public IAtomicCommandWithTarget SetTarget(DatabaseEntity target)
        {
            if (target is Project)
                _project = (Project)target;

            if (target is CohortIdentificationConfiguration)
                _cic = (CohortIdentificationConfiguration)target;

            if (_project != null && _cic != null)
            {
                if(_project.GetAssociatedCohortIdentificationConfigurations().Contains(_cic))
                    SetImpossible("Cohort Identification Configuration is already associated with this Project");
            }

            return this;
        }
    }
}
