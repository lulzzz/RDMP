﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using CatalogueLibrary.DataFlowPipeline.Requirements;
using CsvHelper;
using ReusableLibraryCode.Progress;
using ReusableLibraryCode.Extensions;

namespace LoadModules.Generic.DataFlowSources.SubComponents
{
    /// <summary>
    /// This class is a sub component of <see cref="DelimitedFlatFileDataFlowSource"/>, it is responsible for adding rows read from the CSV file to
    /// the DataTable built by <see cref="FlatFileColumnCollection"/>.
    /// </summary>
    public class FlatFileToDataTablePusher
    {
        private readonly FlatFileToLoad _fileToLoad;
        private readonly FlatFileColumnCollection _headers;
        private readonly Func<string, object> _hackValuesFunc;
        private readonly bool _attemptToResolveNewlinesInRecords;
        
        /// <summary>
        /// Used in the event of reading too few cells for the current line.  The pusher will peek at the next lines to see if they
        /// make up a coherent row e.g. if a free text field is splitting up the document with newlines.  If the peeked lines do not
        /// resolve the problem then the line will be marked as BadData and the peeked records must be reprocessed by <see cref="DelimitedFlatFileDataFlowSource"/>
        /// </summary>
        public FlatFileLine PeekedRecord;

        /// <summary>
        /// All line numbers of the source file being read that could not be processed.  Allows BadDataFound etc to be called multiple times without skipping
        /// records by accident.
        /// </summary>
        public HashSet<int> BadLines = new HashSet<int>();


        /// <summary>
        /// This is incremented when too many values are read from the file to match the header count BUT the values read were null/empty
        /// </summary>
        private long _bufferOverrunsWhereColumnValueWasBlank = 0;

        /// <summary>
        /// We only complain once about headers not matching the number of cell values
        /// </summary>
        private bool _haveComplainedAboutColumnMismatch;

        public FlatFileToDataTablePusher(FlatFileToLoad fileToLoad, FlatFileColumnCollection headers, Func<string, object> hackValuesFunc, bool attemptToResolveNewlinesInRecords)
        {
            _fileToLoad = fileToLoad;
            _headers = headers;
            _hackValuesFunc = hackValuesFunc;
            _attemptToResolveNewlinesInRecords = attemptToResolveNewlinesInRecords;
        }

        public int PushCurrentLine(CsvReader reader,FlatFileLine lineToPush, DataTable dt,IDataLoadEventListener listener, FlatFileEventHandlers eventHandlers)
        {
            //skip the blank lines
            if (lineToPush.Cells.Length == 0 || lineToPush.Cells.All(h => h.IsBasicallyNull()))
                return 0;

            int headerCount = _headers.CountNotNull;
            
            //if the number of not empty headers doesn't match the headers in the data table
            if (dt.Columns.Count != headerCount)
                if (!_haveComplainedAboutColumnMismatch)
                {
                    listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Flat file '" + _fileToLoad.File.Name + "' line number '" + reader.Context.RawRow + "' had  " + headerCount + " columns while the destination DataTable had " + dt.Columns.Count + " columns.  This message apperas only once per file"));
                    _haveComplainedAboutColumnMismatch = true;
                }

            Dictionary<string, object> rowValues = new Dictionary<string, object>();

            if (lineToPush.Cells.Length < headerCount)
                if (!DealWithTooFewCellsOnCurrentLine(reader, lineToPush, listener, eventHandlers))
                    return 0;

            bool haveIncremented_bufferOverrunsWhereColumnValueWasBlank = false;


            for (int i = 0; i < lineToPush.Cells.Length; i++)
            {
                //about to do a buffer overrun
                if (i >= _headers.Length)
                    if (lineToPush[i].IsBasicallyNull())
                    {
                        if (!haveIncremented_bufferOverrunsWhereColumnValueWasBlank)
                        {
                            _bufferOverrunsWhereColumnValueWasBlank++;
                            haveIncremented_bufferOverrunsWhereColumnValueWasBlank = true;
                        }

                        continue; //do not bother buffer overruning with null whitespace stuff
                    }
                    else
                    {
                        string errorMessage = string.Format("Column mismatch on line {0} of file '{1}', it has too many columns (expected {2} columns but line had  {3})",
                             reader.Context.RawRow,
                             dt.TableName, 
                             _headers.Length,
                             lineToPush.Cells.Length);

                        if (_bufferOverrunsWhereColumnValueWasBlank > 0)
                            errorMessage += " ( " + _bufferOverrunsWhereColumnValueWasBlank +
                                            " Previously lines also suffered from buffer overruns but the overrunning values were empty so we had ignored them up until now)";
                        
                        listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, errorMessage));
                        eventHandlers.BadDataFound(lineToPush);
                        break;
                    }

                //if we are ignoring this header
                if(_headers.IgnoreColumnsList.Contains(_headers[i]))
                    continue;
                
                //its an empty header, dont bother populating it
                if (_headers[i].IsBasicallyNull())
                    if (!lineToPush[i].IsBasicallyNull())
                        throw new FileLoadException("The header at index " + i + " in flat file '" +dt.TableName+ "' had no name but there was a value in the data column (on Line number " + reader.Context.RawRow + ")");
                    else
                        continue;

                //sometimes flat files have ,NULL,NULL,"bob" in instead of ,,"bob"
                if (lineToPush[i].IsBasicallyNull())
                    rowValues.Add(_headers[i], DBNull.Value);
                else
                {
                    object hackedValue = _hackValuesFunc(lineToPush[i]);

                    if (hackedValue is string)
                        hackedValue = ((string)hackedValue).Trim();

                    try
                    {
                        //if we are trying to load a boolean value out of the flat file into the strongly typed C# data type
                        if (dt.Columns[_headers[i]].DataType == typeof(Boolean))
                        {
                            bool boolean;
                            int integer;
                            if (hackedValue is string)
                            {
                                if (Boolean.TryParse((string) hackedValue, out boolean)) //could be the text "true"
                                    hackedValue = boolean;
                                else
                                    if (int.TryParse((string)hackedValue, out integer)) //could be the number string "1" or "0"
                                        hackedValue = integer;

                                //else god knows what it is as a datatype, hopefully Convert.ChangeType will handle it
                            }
                            else if (int.TryParse(hackedValue.ToString(), out integer)) //could be the number 1 or 0 or something else that ToStrings into a legit value
                                hackedValue = integer;

                        }
                        //make it an int because apparently C# is too stupid to convert "1" into a bool but is smart enough to turn 1 into a bool.... seriously?!!?

                        rowValues.Add(_headers[i], Convert.ChangeType(hackedValue, dt.Columns[_headers[i]].DataType));
                        //convert to correct datatype (datatype was setup in SetupTypes)
                    }
                    catch (Exception e)
                    {
                        throw new FileLoadException("Error reading file '" + dt.TableName + "'.  Problem loading value " + lineToPush[i] + " into data table (on Line number " + reader.Context.RawRow + ") the header we were trying to populate was " + _headers[i] + " and was of datatype " + dt.Columns[_headers[i]].DataType, e);
                    }
                }
            }

            if(!BadLines.Contains(reader.Context.RawRow))
            {
                DataRow currentRow = dt.Rows.Add();
                foreach (KeyValuePair<string, object> kvp in rowValues)
                    currentRow[kvp.Key] = kvp.Value;
                
                return 1;
            }

            return 0;
        }
        
        private bool DealWithTooFewCellsOnCurrentLine(CsvReader reader, FlatFileLine lineToPush, IDataLoadEventListener listener,FlatFileEventHandlers eventHandlers)
        {
            if(!_attemptToResolveNewlinesInRecords)
            {
                //we read too little cell count but we don't want to solve the problem
                listener.OnNotify(this, new NotifyEventArgs(ProgressEventType.Warning, "Too few columns on line " + reader.Context.RawRow + " of file '" + _fileToLoad + "', it has too many columns (expected " + _headers.Length + " columns but line had " + lineToPush.Cells.Length + ")." + (_bufferOverrunsWhereColumnValueWasBlank > 0 ? "( " + _bufferOverrunsWhereColumnValueWasBlank + " Previously lines also suffered from buffer overruns but the overrunning values were empty so we had ignored them up until now)" : "")));
                eventHandlers.BadDataFound(lineToPush);

                //didn't bother trying to fix the problem
                return false;
            }

            //We want to try to fix the problem by reading more data

            //Create a composite row
            List<string> newCells = new List<string>(lineToPush.Cells);
            
            //track what we are Reading incase it doesn't work
            var allPeekedLines = new List<FlatFileLine>();

            do
            {
                FlatFileLine peekedLine;

                //try adding the next row
                if (reader.Read())
                {
                    peekedLine = new FlatFileLine(reader.Context);

                    //peeked line was 'valid' on it's own
                    if (peekedLine.Cells.Length >= _headers.Length)
                    {
                        //queue it for reprocessing
                        PeekedRecord = peekedLine;

                        //and mark everything else as bad
                        AllBad(lineToPush, allPeekedLines,eventHandlers);
                        return false;
                    }

                    //peeked line was invalid (too short) so we can add it onto ourselves
                    allPeekedLines.Add(peekedLine);
                }
                else
                {
                    //Ran out of space in the file without fixing the problem so it's all bad
                    AllBad(lineToPush, allPeekedLines,eventHandlers);

                    //couldn't fix the problem
                    return false;
                }

                //add the peeked line to the current cells
                //add the first record as an extension of the last cell in current row
                if (peekedLine.Cells.Length != 0)
                    newCells[newCells.Count - 1] += Environment.NewLine + peekedLine.Cells[0];
                else
                    newCells[newCells.Count - 1] += Environment.NewLine; //the next line was completely blank! just add a new line

                //add any further cells on after that
                newCells.AddRange(peekedLine.Cells.Skip(1));

            } while (newCells.Count() < _headers.Length);


            //if we read too much or reached the end of the file
            if (newCells.Count() > _headers.Length)
            {
                AllBadExceptLastSoRequeueThatOne(lineToPush,allPeekedLines,eventHandlers);
                return false;
            }

            if (newCells.Count() != _headers.Length)
                throw new Exception("We didn't over read or reach end of file, how did we get here?");

            //we managed to create a full row
            lineToPush.Cells = newCells.ToArray();

            //problem was fixed
            return true;
        }

        private void AllBadExceptLastSoRequeueThatOne(FlatFileLine lineToPush, List<FlatFileLine> allPeekedLines, FlatFileEventHandlers eventHandlers)
        {
            //the current line is bad
            eventHandlers.BadDataFound(lineToPush);

            //last line resulted in the overrun so requeue it
            PeekedRecord = allPeekedLines.Last();

            //but throw away everything else we read
            foreach (FlatFileLine line in allPeekedLines.Take(allPeekedLines.Count() - 1))
                eventHandlers.BadDataFound(line);
        }

        private void AllBad(FlatFileLine lineToPush, List<FlatFileLine> allPeekedLines, FlatFileEventHandlers eventHandlers)
        {
            //the current line is bad
            eventHandlers.BadDataFound(lineToPush);

            foreach (FlatFileLine line in allPeekedLines)
                eventHandlers.BadDataFound(line);
        }
    }
}
