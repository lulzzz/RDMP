using System;
using System.IO;
using System.Text.RegularExpressions;
using CatalogueLibrary.Data.DataLoad;
using MapsDirectlyToDatabaseTable;
using ReusableLibraryCode;
using ReusableLibraryCode.DatabaseHelpers.Discovery;

namespace DataLoadEngine.LoadExecution.Components.Arguments
{
    public class CommandLineHelper
    {
        public static string CreateArgString(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The argument 'name' parameter is empty");

            if (!char.IsUpper(name, 0))
                throw new ArgumentException("The name argument should be in Pascal case, the first character in " + name + " should be uppercase");

            if (value == null)
                throw new ArgumentException("The argument value is null");

            if (value is bool)
                if (Convert.ToBoolean(value))
                    return "-" + ConvertArgNameToString(name);
                else
                    return "";

            if (value is LoadStage)
                return CreateArgString(name, value.ToString());

            if (value is DiscoveredDatabase)
            {
                var dbInfo = value as DiscoveredDatabase;
                return CreateArgString("DatabaseName", dbInfo.GetRuntimeName()) + " " + CreateArgString("DatabaseServer", dbInfo.Server.Name);
            }
                        
            return "-" + ConvertArgNameToString(name) + "=" + GetValueString(value);
        }

        public static string ConvertArgNameToString(string name)
        {
            // Will split on capitals without breaking up capital sequences
            // e.g. 'TestArg' => 'test-arg' and 'TestTLAArg' => 'test-tla-arg'
            return Regex.Replace(name, @"((?<=[a-z])[A-Z]|[A-Z](?=[a-z]))", @"-$1").ToLower();
        }

        public static string GetValueString(object value)
        {
            if (value is string)
                if (value.ToString().Contains(" "))
                    return @"""" + value + @"""";//<- looks like a snake (or a golf club? GM)
                else
                    return value as string;

            if (value is DateTime)
            {
                var dt = (DateTime) value;
                return "\"" + (dt.TimeOfDay.TotalSeconds.Equals(0) ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm:ss")) + "\"";
            }

            if (value is FileInfo)
            {
                var fi = value as FileInfo;
                return "\"" + fi.FullName + "\"";
            }

            throw new ArgumentException("Cannot create a value string from an object of type " + value.GetType().FullName);
        }
    }
}