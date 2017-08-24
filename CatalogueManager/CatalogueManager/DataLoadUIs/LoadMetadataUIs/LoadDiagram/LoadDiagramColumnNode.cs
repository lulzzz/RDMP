﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.QueryBuilding;
using CatalogueManager.DataLoadUIs.LoadMetadataUIs.LoadDiagram.StateDiscovery;
using CatalogueManager.Icons.IconProvision;
using RDMPObjectVisualisation.Copying;
using RDMPObjectVisualisation.Copying.Commands;
using ReusableLibraryCode;
using ReusableLibraryCode.DatabaseHelpers.Discovery;
using ReusableUIComponents.Copying;

namespace CatalogueManager.DataLoadUIs.LoadMetadataUIs.LoadDiagram
{
    public class LoadDiagramColumnNode : ICommandSource, IHasLoadDiagramState
    {
        private readonly LoadDiagramTableNode _tableNode;
        private readonly IHasStageSpecificRuntimeName _column;
        private readonly LoadBubble _bubble;
        private string _expectedDataType;
        private string _discoveredDataType;
        public string ColumnName { get; private set; }

        public LoadDiagramState State { get; set; }

        public LoadDiagramColumnNode(LoadDiagramTableNode tableNode,IHasStageSpecificRuntimeName column,LoadBubble bubble)
        {
            _tableNode = tableNode;
            _column = column;
            _bubble = bubble;
            ColumnName = _column.GetRuntimeName(LoadStageToNamingConventionMapper.LoadBubbleToLoadStage(_bubble));
            
            var colInfo = _column as ColumnInfo;
            var preLoadDiscarded = _column as PreLoadDiscardedColumn;

            if (preLoadDiscarded != null)
                _expectedDataType = preLoadDiscarded.SqlDataType;
            else
            if (colInfo != null)
                _expectedDataType = colInfo.GetRuntimeDataType(LoadStageToNamingConventionMapper.LoadBubbleToLoadStage(_bubble));
            else
                throw new Exception("Expected _column to be ColumnInfo or PreLoadDiscardedColumn but it was:" + _column.GetType().Name);
        }

        public bool IsDynamicColumn
        {
            get
            {
                if (_column is PreLoadDiscardedColumn)
                    return true;

                var colInfo = (ColumnInfo)_column;

                return colInfo.IsPrimaryKey || colInfo.ANOTable_ID != null || colInfo.GetRuntimeName().StartsWith("hic_");
            }
        }

        public override string ToString()
        {
            return ColumnName;
        }

        public string GetDataType()
        {
            return State == LoadDiagramState.Different ? _discoveredDataType : _expectedDataType;
        }
        
        public ICommand GetCommand()
        {
            return new SqlTextOnlyCommand(SqlSyntaxHelper.EnsureFullyQualified(_tableNode.DatabaseName, _tableNode.TableName, ColumnName));
        }

        public object GetImage(ICoreIconProvider coreIconProvider)
        {

            //if its a ColumnInfo and RAW then use the basic ColumnInfo icon
            if (_column is ColumnInfo && _bubble <= LoadBubble.Raw)
                return coreIconProvider.GetImage(RDMPConcept.ColumnInfo);

            //otherwise use the default Live/PreLoadDiscardedColumn icon
            return coreIconProvider.GetImage(_column);
        }

        public void SetState(DiscoveredColumn discoveredColumn)
        {
            _discoveredDataType = discoveredColumn.DataType.SQLType;
            State = _discoveredDataType.Equals(_expectedDataType) ? LoadDiagramState.Found : LoadDiagramState.Different;
        }
    }
}
