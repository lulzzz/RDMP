// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Linq;
using System.Threading;
using Rdmp.Core.CatalogueLibrary.Data;
using Rdmp.Core.CatalogueLibrary.Ticketing;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.Repositories;
using ReusableLibraryCode.Checks;

namespace Rdmp.Core.DataExport.DataRelease
{
    /// <summary>
    /// Evaluates things that are not within the control area of the DataExportManager but which might prevent a release e.g. ticketing system is not available
    ///  / tickets in wrong status / safehaven down for maintencence etc.
    /// </summary>
    public class ReleaseEnvironmentPotential : ICheckable
    {
        private readonly IDataExportRepository _repository;
        public IExtractionConfiguration Configuration { get; private set; }
        public IProject Project { get; private set; }

        public Exception Exception { get; private set; }
        public TicketingReleaseabilityEvaluation Assesment { get; private set; }
        public string Reason { get; private set; }

        
        public ReleaseEnvironmentPotential(IExtractionConfiguration configuration)
        {
            _repository = configuration.DataExportRepository;
            Configuration = configuration;
            Project = configuration.Project;
        }
        
        private void MakeAssessment()
        {
            Assesment = TicketingReleaseabilityEvaluation.TicketingLibraryMissingOrNotConfiguredCorrectly;

            var configuration = _repository.CatalogueRepository.GetAllObjectsWhere<TicketingSystemConfiguration>("IsActive",1).SingleOrDefault();
            if (configuration == null) return;

            TicketingSystemFactory factory = new TicketingSystemFactory(_repository.CatalogueRepository);
            ITicketingSystem ticketingSystem = factory.CreateIfExists(configuration);

            if (ticketingSystem == null) return;

            try
            {
                Exception e;
                string reason;
                Assesment = ticketingSystem.GetDataReleaseabilityOfTicket(Project.MasterTicket,
                    Configuration.RequestTicket, Configuration.ReleaseTicket, out reason, out e);
                Exception = e;
                Reason = reason;
            }
            catch (Exception e)
            {
                if (e is ThreadInterruptedException)
                    throw;

                Assesment = TicketingReleaseabilityEvaluation.TicketingLibraryCrashed;
                Exception = e;
            }
        }

        public void Check(ICheckNotifier notifier)
        {
            MakeAssessment();

            var message = "Environment Releasability is " + Assesment;
            if (!String.IsNullOrWhiteSpace(Reason))
                message += " - " + Reason;

            notifier.OnCheckPerformed(new CheckEventArgs("Environment Releasability is " + Assesment, GetCheckResultFor(Assesment), Exception));
        }

        private CheckResult GetCheckResultFor(TicketingReleaseabilityEvaluation assesment)
        {
            switch (assesment)
            {
                case TicketingReleaseabilityEvaluation.TicketingLibraryCrashed:
                case TicketingReleaseabilityEvaluation.CouldNotReachTicketingServer:
                case TicketingReleaseabilityEvaluation.CouldNotAuthenticateAgainstServer:
                case TicketingReleaseabilityEvaluation.NotReleaseable:
                    return CheckResult.Fail;
                case TicketingReleaseabilityEvaluation.TicketingLibraryMissingOrNotConfiguredCorrectly:
                    return CheckResult.Warning;
                case TicketingReleaseabilityEvaluation.Releaseable:
                    return CheckResult.Success;

                default:
                    throw new ArgumentOutOfRangeException("assesment");
            }
        }
    }
}
