using CatalogueLibrary.Data;
using MapsDirectlyToDatabaseTable;

namespace DataExportLibrary.Interfaces.Data.DataTables
{
    /// <summary>
    /// See SelectedDatasetsForcedJoin
    /// </summary>
    public interface ISelectedDatasetsForcedJoin:IMapsDirectlyToDatabaseTable
    {
        int TableInfo_ID { get; }
        TableInfo TableInfo { get;}
    }
}