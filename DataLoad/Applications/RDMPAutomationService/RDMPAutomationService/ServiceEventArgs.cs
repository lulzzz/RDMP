using System.Diagnostics;

namespace RDMPAutomationService
{
    public class ServiceEventArgs
    {
        public string Message { get; set; }
        public EventLogEntryType EntryType { get; set; }
    }
}