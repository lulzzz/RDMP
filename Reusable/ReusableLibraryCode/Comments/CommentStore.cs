// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Text;
using System.Xml;

namespace ReusableLibraryCode.Comments
{
    /// <summary>
    /// Records documentation for classes and keywords (e.g. foreign key names).
    /// </summary>
    public class CommentStore : IEnumerable<KeyValuePair<string, string>>
    {
        private readonly Dictionary<string,string> _dictionary = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);

        private string[] _ignoreHelpFor = new[]
        {
            "CsvHelper.xml",
            "Google.Protobuf.xml",
            "MySql.Data.xml",
            "Newtonsoft.Json.xml",
            "NLog.xml",
            "NuDoq.xml",
            "ObjectListView.xml",
            "QuickGraph.xml",
            "Renci.SshNet.xml",
            "ScintillaNET.xml",
            "nunit.framework.xml"
        };
        
        public virtual void ReadComments(params string[] directoriesToLookInForComments)
        {
            Dictionary<string,ZipArchive> zips = new Dictionary<string, ZipArchive>(StringComparer.CurrentCultureIgnoreCase);
            HashSet<string> dirs = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            
            foreach(var d in directoriesToLookInForComments)
            {
                if(Path.HasExtension(d) && Path.GetExtension(d) == ".zip")
                {
                    if(File.Exists(d) && !zips.ContainsKey(d))
                        try
                        {
                            zips.Add(d,ZipFile.OpenRead(d));

                        }catch(System.Exception)
                        {
                            //couldn't open zip file :(
                            continue;
                        }
                }
                else
                    if(!dirs.Contains(d))
                        dirs.Add(d);
            }
                    

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var xmlFile = assembly.GetName().Name + ".xml";

                //we don't need to provide help for system classes
                if (xmlFile.StartsWith("System") || _ignoreHelpFor.Contains(xmlFile))
                    continue;

                //can we get it from the zips?
                foreach (var zip in zips.Values)
                {
                    var entry = zip.GetEntry(xmlFile);
                    if(entry != null)
                    {
                        var f = Path.GetTempFileName();
                        entry.ExtractToFile(f,true);

                        ReadComments(assembly,f);

                        File.Delete(f);
                        continue;

                    }
                }
                
                //can we get it from a dir
                foreach(var dir in dirs)
                    ReadComments(assembly,Path.Combine(dir, xmlFile));
            }

            //dispose zips
            foreach(IDisposable d in zips.Values)
                d.Dispose();
        }

        private void ReadComments(Assembly assembly,string filename)
        {
            if (File.Exists(filename))
            {
                var doc = new XmlDocument();
                doc.Load(filename);

                doc.IterateThroughAllNodes(AddXmlDoc);
            }
            
        }
        
        /// <summary>
        /// Adds the given member xml doc to the <see cref="CommentStore"/>
        /// </summary>
        /// <param name="obj"></param>
        public void AddXmlDoc(XmlNode obj)
        {
            if(obj == null)
                return;

            if (obj.Name == "member" && obj.Attributes != null)
            {
                string memberName = obj.Attributes["name"]?.Value;
                var summary = GetSummaryAsText(obj["summary"]);

                if(memberName == null || string.IsNullOrWhiteSpace(summary))
                    return;
                
                //it's a Property get Type.Property (not fully specified)
                if (memberName.StartsWith("P:") || memberName.StartsWith("T:"))
                    Add(GetLastTokens(memberName),summary.Trim());
            }
        }

        private string GetSummaryAsText(XmlElement summaryTag)
        {
            if (summaryTag == null)
                return null;
            
            StringBuilder sb = new StringBuilder();
            
            summaryTag.IterateThroughAllNodes(
                    n =>
                    {
                        if (n.Name == "see" && n.Attributes != null)
                            sb.Append(GetLastTokens(n.Attributes["cref"]?.Value) + " "); // a <see cref="omg"> tag
                        else if (n.Name == "para")
                            TrimEndSpace(sb).Append(Environment.NewLine + Environment.NewLine);  //open para tag (next tag is probably #text)
                        else if (n.Value != null) //e.g. #text
                            sb.Append(TrimSummary(n.Value) + " ");
                    });

            return sb.ToString();
        }

        private string TrimSummary(string value)
        {
            if (value == null)
                return null;

            return Regex.Replace(value,@"\s{2,}", " ").Trim();
        }

        /// <summary>
        /// Returns the last x parts from a string like M:Abc.Def.Geh.AAA(fff,mm).  In this case it would return AAA for 1, Geh.AAA for 2 etc.
        /// </summary>
        /// <param name="memberName"></param>
        /// <param name="partsToGet"></param>
        /// <returns></returns>
        private string GetLastTokens(string memberName, int partsToGet)
        {
            //throw away any preceding "T:", "M:" etc
            memberName = memberName.Substring(memberName.IndexOf(':') + 1);

            var idxBracket = memberName.LastIndexOf('(');
            if (idxBracket != -1)
                memberName = memberName.Substring(0, idxBracket);

            var matches = memberName.Split('.');

            if (matches.Length < partsToGet)
                return memberName;

            return string.Join(".", matches.Reverse().Take(partsToGet).Reverse());
        }

        private string GetLastTokens(string memberName)
        {
            if (memberName.StartsWith("P:"))
                return GetLastTokens(memberName, 2);

            if (memberName.StartsWith("T:"))
                return GetLastTokens(memberName, 1);

            if (memberName.StartsWith("M:"))
                return GetLastTokens(memberName, 2);
            
            return memberName;
        }

        public static StringBuilder TrimEndSpace(StringBuilder sb)
        {
            if (sb == null || sb.Length == 0) return sb;

            int i = sb.Length - 1;
            for (; i >= 0; i--)
                if (sb[i] != ' ')
                    break;

            if (i < sb.Length - 1)
                sb.Length = i + 1;

            return sb;
        }

        public void Add(string name, string summary)
        {
            //these are not helpful!
            if(name == "C" || name == "R")
                return;

            if(_dictionary.ContainsKey(name))
                return;

            _dictionary.Add(name,summary);

        }

        public bool ContainsKey(string keyword)
        {
            return _dictionary.ContainsKey(keyword);
        }

        /// <summary>
        /// Returns documentation for the keyword or null if no documentation exists
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string this[string index]    // Indexer declaration  
        {
            get
            {
                if (_dictionary.ContainsKey(index))
                    return _dictionary[index];

                return null;
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns documentation for the class specified up to maxLength characters (after which ... is appended).  Returns null if no documentation exists for the class
        /// </summary>
        /// <param name="maxLength"></param>
        /// <param name="type"></param>
        /// <param name="allowInterfaceInstead">If no docs are found for Type X then look for IX too</param>
        /// <param name="formatAsParagraphs"></param>
        /// <returns></returns>
        public string GetTypeDocumentationIfExists(int maxLength, Type type, bool allowInterfaceInstead = true,bool formatAsParagraphs = false)
        {
            var docs = this[type.Name];

            //if it's a generic try looking for an non generic or abstract base etc
            if (docs == null && type.Name.EndsWith("`1"))
                docs = this[type.Name.Substring(0, type.Name.Length - "`1".Length)];

            if (docs == null && allowInterfaceInstead && !type.IsInterface)
                docs = this["I" + type.Name];

            if (string.IsNullOrWhiteSpace(docs))
                return null;

            if (formatAsParagraphs)
                docs = FormatAsParagraphs(docs);

            maxLength = Math.Max(10, maxLength - 3);

            if (docs.Length <= maxLength)
                return docs;

            return docs.Substring(0, maxLength) + "...";
        }
        /// <inheritdoc cref="GetTypeDocumentationIfExists(int,Type,bool,bool)"/>
        public string GetTypeDocumentationIfExists(Type type, bool allowInterfaceInstead = true, bool formatAsParagraphs = false)
        {
            return GetTypeDocumentationIfExists(int.MaxValue, type, allowInterfaceInstead, formatAsParagraphs);
        }

        /// <summary>
        /// Searches the CommentStore for variations of the <paramref name="word"/> and returns the documentation if found (or null)
        /// </summary>
        /// <param name="word"></param>
        /// <param name="fuzzyMatch"></param>
        /// <param name="formatAsParagraphs">true to pass result string through <see cref="FormatAsParagraphs"/></param>
        /// <returns></returns>
        public string GetDocumentationIfExists(string word, bool fuzzyMatch, bool formatAsParagraphs = false)
        {
            var match = GetDocumentationKeywordIfExists(word,fuzzyMatch);

            if (match == null)
                return null;

            return formatAsParagraphs ? FormatAsParagraphs(this[match]) : this[match];
        }

        /// <summary>
        /// Searches the CommentStore for variations of the <paramref name="word"/> and returns the key that matches (which might be word verbatim).
        /// 
        /// <para>This does not return the actual documentation, use <see cref="GetDocumentationIfExists"/> for that</para>
        /// </summary>
        /// <param name="word"></param>
        /// <param name="fuzzyMatch"></param>
        /// <returns></returns>
        public string GetDocumentationKeywordIfExists(string word, bool fuzzyMatch)
        {
            if (ContainsKey(word))
                return word;

            //try the plural if we didnt match the basic word
            if (word.EndsWith("s") && fuzzyMatch)
                return GetDocumentationKeywordIfExists(word.TrimEnd('s'), true);

            //try the interface
            if (fuzzyMatch)
                return GetDocumentationKeywordIfExists("I" + word, false);

            return null;
        }

        /// <summary>
        /// Formats a string read from xmldoc into paragraphs and gets rid of namespace prefixes introduced by cref="" notation.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public string FormatAsParagraphs(string message)
        {
            message = Regex.Replace(message, "\r\n\\s*","\r\n\r\n");
            message = Regex.Replace(message, @"(\.?[A-z]{2,}\.)+([A-z]+)", (m) => m.Groups[2].Value);
            
            return message;
        }
    }
}