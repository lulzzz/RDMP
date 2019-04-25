// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using Rdmp.Core.CatalogueLibrary.Data.Cohort;
using Rdmp.Core.CatalogueLibrary.Nodes.LoadMetadataNodes;
using Rdmp.Core.DataExport.Data;
using Rdmp.Core.DataExport.ExtractionTime.ExtractionPipeline.Destinations;

namespace Rdmp.Core.DataExport.Providers.Nodes
{
    /// <summary>
    /// Location on disk in which linked project extracts are generated for a given <see cref="Project"/> (assuming you are extracting to disk
    /// e.g. with an <see cref="ExecuteDatasetExtractionFlatFileDestination"/>).
    /// </summary>
    public class ExtractionDirectoryNode : IDirectoryInfoNode, IOrderable
    {
        public Project Project { get; private set; }

        public ExtractionDirectoryNode(Project project)
        {
            Project = project;
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Project.ExtractionDirectory))
                return "???";

            return Project.ExtractionDirectory;
        }

        protected bool Equals(ExtractionDirectoryNode other)
        {
            return Equals(Project, other.Project);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExtractionDirectoryNode) obj);
        }

        public override int GetHashCode()
        {
            return (Project != null ? Project.GetHashCode() : 0);
        }

        public DirectoryInfo GetDirectoryInfoIfAny()
        {
            if (string.IsNullOrWhiteSpace(Project.ExtractionDirectory))
                return null;

            return new DirectoryInfo(Project.ExtractionDirectory);
        }

        public int Order{ get { return 4; } set { }}
    }
}
