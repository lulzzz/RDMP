using System;
using System.Collections.Generic;

namespace CatalogueLibrary.Data.EntityNaming
{
    /// <summary>
    /// Determines how to translate a TABLE (not database!) name based on the load stage of a DLE RAW=>STAGING=>LIVE migration.  E.g. Raw tables
    /// already have the same name as Live tables (they are in a RAW database) but Staging tables and Archive tables have the suffixes specified
    /// </summary>
    public class SuffixBasedNamer : INameDatabasesAndTablesDuringLoads
    {
        protected static Dictionary<LoadBubble, string> Suffixes = new Dictionary<LoadBubble, string>
        {
            {LoadBubble.Raw, ""},
            {LoadBubble.Staging, "_STAGING"},
            {LoadBubble.Live, ""},
            {LoadBubble.Archive, "_Archive"}
        };
        
        public virtual string GetDatabaseName(string rootDatabaseName, LoadBubble stage)
        {
            switch (stage)
            {
                case LoadBubble.Raw:
                    return rootDatabaseName + "_RAW";
                case LoadBubble.Staging:
                    return rootDatabaseName + "_STAGING";
                case LoadBubble.Live:
                    return rootDatabaseName;
                default:
                    throw new ArgumentOutOfRangeException("stage");
            }
        }

        public virtual string GetName(string tableName, LoadBubble convention)
        {
            if (!Suffixes.ContainsKey(convention))
                throw new ArgumentException("Do not have a suffix for convention: " + convention);

            return tableName + Suffixes[convention];
        }
        
    }
}