﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using CatalogueLibrary.Reports;
using CatalogueLibrary.Repositories;
using MapsDirectlyToDatabaseTable;

namespace CatalogueLibrary.Data.Dashboarding
{
    /// <summary>
    /// Helps you create simple string based argument lists
    /// </summary>
    public class PersistStringHelper
    {
        /// <summary>
        /// The string to use to divide objects declared within a collection e.g. ',' in [RepoType:ObjectType:ID,RepoType:ObjectType:ID]
        /// </summary>
        public const string CollectionObjectSeparator = ",";

        /// <summary>
        /// The string to use to indicate the start of an objects collection e.g. '[' in  [RepoType:ObjectType:ID,RepoType:ObjectType:ID]
        /// </summary>
        public const string CollectionStartDelimiter = "[";

        /// <summary>
        /// The string to use to indicate the end of an objects collection e.g. ']' in  [RepoType:ObjectType:ID,RepoType:ObjectType:ID]
        /// </summary>
        public const string CollectionEndDelimiter = "]";

        /// <summary>
        /// The string to use to separate logic portions of a persistence string e.g. ':"  in  [RepoType:ObjectType:ID,RepoType:ObjectType:ID]
        /// </summary>
        public const char Separator = ':';

        /// <summary>
        /// Divider between Type section  (see <see cref="Separator"/> - what is the control) and args dictionary for IPersistableObjectCollection
        /// </summary>
        public const string ExtraText = "###EXTRA_TEXT###";

        /// <summary>
        /// Serializes the dictionary to a string of XML
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public string SaveDictionaryToString(Dictionary<string, string> dict)
        {
            XElement el = new XElement("root",
                dict.Select(kv => new XElement(kv.Key, kv.Value)));

            return el.ToString();
        }

        /// <summary>
        /// Creates a dictionary by deserializing the XML string provided (e.g. a string generated by <see cref="SaveDictionaryToString"/>) 
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public Dictionary<string, string> LoadDictionaryFromString(string str)
        {
            if(String.IsNullOrWhiteSpace(str))
                return new Dictionary<string, string>();

            XElement rootElement = XElement.Parse(str);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var el in rootElement.Elements())
                dict.Add(el.Name.LocalName, el.Value);

            return dict;
        }

        /// <summary>
        /// Fetches the given key from the serialized <paramref name="persistString"/> (e.g. a string generated by <see cref="SaveDictionaryToString"/>) 
        /// 
        /// <para>If you need lots of values you should probably just use <see cref="LoadDictionaryFromString"/> instead</para>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="persistString"></param>
        /// <returns></returns>
        public string GetValueIfExistsFromPersistString(string key, string persistString)
        {
            var dict = LoadDictionaryFromString(persistString);

            if(dict.ContainsKey(key))
                return dict[key];
            
            return null;
        }

        /// <summary>
        /// Returns the objects IDs formatted with the <see cref="CollectionStartDelimiter"/>, <see cref="CollectionEndDelimiter"/> and <see cref="CollectionObjectSeparator"/>
        /// </summary>
        /// <param name="objects"></param>
        /// <returns></returns>
        public string GetObjectCollectionPersistString(params IMapsDirectlyToDatabaseTable[] objects)
        {

            StringBuilder sb = new StringBuilder();

            //output [obj1,obj2,obj3]
            sb.Append(CollectionStartDelimiter);

            //where obj is <RepositoryType>:<DatabaseObjectType>:<ObjectID>
            sb.Append(String.Join(CollectionObjectSeparator, objects.Select(o => o.Repository.GetType().FullName + Separator + o.GetType().FullName + Separator + o.ID)));
            
            //ending bracket for the object collection
            sb.Append(CollectionEndDelimiter);

            return sb.ToString();
        }

        /// <summary>
        /// Returns the object list section of any <paramref name="persistenceString"/>. This string must take the format [RepoType:ObjectType:ID,RepoType:ObjectType:ID]
        /// 
        /// <para>Throws <see cref="PersistenceException"/> if there is not exactly 1 match or if the number of subtokens in each section is not 3.</para>
        /// </summary>
        /// <param name="persistenceString">persistence string in the format [RepoType:ObjectType:ID,RepoType:ObjectType:ID]</param>
        /// <returns></returns>
        public string MatchCollectionInString(string persistenceString)
        {
            try
            {
                //match the starting delimiter
                string pattern = Regex.Escape(CollectionStartDelimiter);
                pattern += "(.*)";//then anything
                pattern += Regex.Escape(CollectionEndDelimiter);//then the ending delimiter

                return Regex.Match(persistenceString, pattern).Groups[1].Value;
            }
            catch (Exception e)
            {
                throw new PersistenceException("Could not match ObjectCollection delimiters in persistenceString '" + persistenceString + "'", e);
            }
        }
         

        /// <summary>
        /// Fetches the listed objects out of the collection section of a persistence string by fetching the listed ObjectType by ID from the RepoType
        /// </summary>
        /// <param name="allObjectsString">A string with a list of objects ID's, should have the format [RepoType:ObjectType:ID,RepoType:ObjectType:ID]</param>
        /// <param name="repositoryLocator"></param>
        /// <returns></returns>
        public List<IMapsDirectlyToDatabaseTable> GetObjectCollectionFromPersistString(string allObjectsString, IRDMPPlatformRepositoryServiceLocator repositoryLocator)
        {
            var toReturn = new List<IMapsDirectlyToDatabaseTable>();

            allObjectsString = allObjectsString.Trim(CollectionStartDelimiter.ToCharArray()[0], CollectionEndDelimiter.ToCharArray()[0]);

            var objectStrings = allObjectsString.Split(new[] { CollectionObjectSeparator }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string objectString in objectStrings)
            {
                var objectTokens = objectString.Split(Separator);
                
                if (objectTokens.Length != 3)
                    throw new PersistenceException("Could not figure out what database object to fetch because the list contained an item with an invalid number of tokens (" + objectTokens.Length + " tokens).  The current object string is:" + Environment.NewLine + objectString);

                var dbObj = repositoryLocator.GetArbitraryDatabaseObject(objectTokens[0], objectTokens[1], Int32.Parse(objectTokens[2]));

                if (dbObj != null)
                    toReturn.Add(dbObj);
                else
                    throw new PersistenceException("DatabaseObject '" + objectString +
                                                   "' has been deleted meaning IPersistableObjectCollection could not be properly created/populated");
            }

            return toReturn;
        }

        /// <summary>
        /// Returns all text appearing after <see cref="ExtraText"/>
        /// </summary>
        /// <param name="persistString"></param>
        /// <returns></returns>
        public string GetExtraText(string persistString)
        {
            if (!persistString.Contains(ExtraText))
                return null;

            return persistString.Substring(persistString.IndexOf(ExtraText, StringComparison.Ordinal) + ExtraText.Length);
        }
    }
}
