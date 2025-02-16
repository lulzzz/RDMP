// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using ReusableUIComponents.ScintillaHelper;
using ScintillaNET;

namespace ReusableUIComponents
{
    /// <summary>
    /// Allows you to view a class file from the RDMP codebase.  See ExceptionViewerStackTraceWithHyperlinks for the mechanics of how this works (or UserManual.docx).  A green line will
    /// highlight the line on which the message or error occurred.
    /// </summary>
    [TechnicalUI]
    public partial class ViewSourceCodeDialog : Form
    {
        private Scintilla QueryEditor;

        private static HashSet<FileInfo> SupplementalSourceZipFiles = new HashSet<FileInfo>();
        private static object oSupplementalSourceZipFilesLock = new object();
        private const string MainSourceCodeRepo = "SourceCodeForSelfAwareness.zip";

        public static void AddSupplementalSourceZipFile(FileInfo f)
        {
            lock (oSupplementalSourceZipFilesLock)
            {
                SupplementalSourceZipFiles.Add(f);
            }
        }

        public ViewSourceCodeDialog(string filename, int lineNumber, Color highlightColor)
        {
            string toFind = Path.GetFileName(filename);
            
            InitializeComponent();

            if(filename == null)
                return;
            
            bool designMode = (LicenseManager.UsageMode == LicenseUsageMode.Designtime);

            if (designMode) //dont add the QueryEditor if we are in design time (visual studio) because it breaks
                return;

            QueryEditor = new ScintillaTextEditorFactory().Create(null, "csharp");

            panel1.Controls.Add(QueryEditor);

            LoadSourceCode(toFind,lineNumber,highlightColor);

            var worker = new BackgroundWorker();
            worker.DoWork += WorkerOnDoWork;
            worker.RunWorkerAsync();
        }

        private void LoadSourceCode(string toFind, int lineNumber, Color highlightColor)
        {
            lock (oSupplementalSourceZipFilesLock)
            {
                string readToEnd = GetSourceForFile(toFind);

                //entry was found
                if (readToEnd != null)
                {
                    QueryEditor.Text = readToEnd;

                    if (lineNumber != -1)
                    {
                        QueryEditor.FirstVisibleLine = Math.Max(0, lineNumber - 10);
                        new ScintillaLineHighlightingHelper().HighlightLine(QueryEditor, lineNumber - 1, highlightColor);
                    }
                }
                else
                    throw new FileNotFoundException("Could not find file called '" + toFind + "' in any of the zip archives");
            }

            Text = toFind;
        }

        private void WorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            HashSet<string> entries = new HashSet<string>();

            var zipArchive = new FileInfo(MainSourceCodeRepo);
            foreach (var zipFile in new[] { zipArchive }.Union(SupplementalSourceZipFiles))
            {
                //if the zip exists
                if (zipFile.Exists)
                    //read the entry (if it is there)
                    using (var z = ZipFile.OpenRead(zipFile.FullName))
                        foreach (var entry in z.Entries)
                            entries.Add(entry.Name);
            }


            olvSourceFiles.AddObjects(entries.ToArray());
        }
        public ViewSourceCodeDialog(string filename):this(filename,-1,Color.White)
        {
        }

        public static string GetSourceForFile(string toFind)
        {
            try
            {
                var zipArchive = new FileInfo(MainSourceCodeRepo);

                //for each zip file (starting with the main archive)
                foreach (var zipFile in new[] { zipArchive }.Union(SupplementalSourceZipFiles))
                {
                    //if the zip exists
                    if (zipFile.Exists)
                    {
                        //read the entry (if it is there)
                        using (var z = ZipFile.OpenRead(zipFile.FullName))
                        {
                            var readToEnd = GetEntryFromZipFile(z, toFind);

                            if (readToEnd != null) //the entry was found and read
                                return readToEnd;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            //couldn't find any text
            return null;
        }

        public static bool SourceCodeIsAvailableFor(string s)
        {
            try
            {
                return GetSourceForFile(s) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private static string GetEntryFromZipFile(ZipArchive z,string toFind)
        {
            var entry = z.Entries.FirstOrDefault(e => e.Name == toFind);

            if (entry == null)
                return null;

            return new StreamReader(entry.Open()).ReadToEnd();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            olvSourceFiles.UseFiltering = true;
            olvSourceFiles.ModelFilter = new TextMatchFilter(olvSourceFiles,textBox1.Text,StringComparison.CurrentCultureIgnoreCase);
        }

        private void olvSourceFiles_ItemActivate(object sender, EventArgs e)
        {
            var str = olvSourceFiles.SelectedObject as string;
            if (str != null)
                LoadSourceCode(str, -1, Color.White);
        }

    }
}
