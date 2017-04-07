using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Jobs.Validation.Common
{
    public static class TraceEvent
    {
        public static readonly EventId ValidatorException = EventId(0, "Validator exception");
        public static readonly EventId CommandLineProcessingFailed = EventId(1, "Failed to process Job's command line arguments");
        public static readonly EventId StartValidationAuditFailed = EventId(2, "Failed to save audit info regarding validation queueing");

        private const int StartId = 1186511685;
        private static EventId EventId(int offset, string name)
        {
            return new EventId(StartId + offset, name);
        }
    }
}
