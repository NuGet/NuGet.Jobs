using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stats.AzureCdnLogs.Common.Collect
{
    public abstract class Collector
    {
        private static readonly DateTime _unixTimestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        protected ILogSource _source;
        protected ILogDestination _destination;

        public Collector(ILogSource source, ILogDestination destination)
        {
            _source = source;
            _destination = destination;
        }

        public async Task ProcessAsync()
        {
            var files = _source.GetFiles();
            
            foreach(var file in files)
            {
                if(_source.TryLock(file))
                {
                    try
                    {
                        var inputStream = await _source.OpenReadAsync(file);
                        _destination.TryWriteAsync(inputStream, "todo");

                    }
                    catch
                    {

                    }
                }
            }
        }

        public virtual OutputLogLine TransformRawLogLine(string line)
        {
            // the default implementation will assume that the entries are space separated and in the correct order
            string[] entries = line.Split(' ');

            return new OutputLogLine(entries[0],
                                    entries[1],
                                    entries[2],
                                    entries[3],
                                    entries[4],
                                    entries[5],
                                    entries[6],
                                    entries[7],
                                    entries[8],
                                    entries[9],
                                    entries[10],
                                    entries[11],
                                    entries[12],
                                    entries[13],
                                    entries[14],
                                    entries[15]);
        }

        private void ProcessLogStream(Stream sourceStream, Stream targetStream)
        {
            // note: not using async/await pattern as underlying streams do not support async
            using (var sourceStreamReader = new StreamReader(sourceStream))
            {
                using (var targetStreamWriter = new StreamWriter(targetStream))
                {
                    targetStreamWriter.Write(OutputLogLine.Header);

                    var lineNumber = 0;
                    do
                    {
                        var rawLogLine = TransformRawLogLine(sourceStreamReader.ReadLine()).ToString();
                        lineNumber++;

                        var logLine = GetParsedModifiedLogEntry(lineNumber, rawLogLine);
                        if (!string.IsNullOrEmpty(logLine))
                        {
                            targetStreamWriter.Write(logLine);
                        }
                    }
                    while (!sourceStreamReader.EndOfStream);
                }
            }
        }

        private string GetParsedModifiedLogEntry(int lineNumber, string rawLogEntry)
        {
            var parsedEntry = CdnLogEntryParser.ParseLogEntryFromLine(
                lineNumber,
                rawLogEntry,
                null);

            if (parsedEntry == null)
            {
                return null;
            }

            const string spaceCharacter = " ";
            const string dashCharacter = "-";

            var stringBuilder = new StringBuilder();

            // timestamp
            stringBuilder.Append(ToUnixTimeStamp(parsedEntry.EdgeServerTimeDelivered) + spaceCharacter);
            // time-taken
            stringBuilder.Append((parsedEntry.EdgeServerTimeTaken.HasValue ? parsedEntry.EdgeServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);

            // REMOVE c-ip
            stringBuilder.Append(dashCharacter + spaceCharacter);

            // filesize
            stringBuilder.Append((parsedEntry.FileSize.HasValue ? parsedEntry.FileSize.Value.ToString() : dashCharacter) + spaceCharacter);
            // s-ip
            stringBuilder.Append((parsedEntry.EdgeServerIpAddress ?? dashCharacter) + spaceCharacter);
            // s-port
            stringBuilder.Append((parsedEntry.EdgeServerPort.HasValue ? parsedEntry.EdgeServerPort.Value.ToString() : dashCharacter) + spaceCharacter);
            // sc-status
            stringBuilder.Append((parsedEntry.CacheStatusCode ?? dashCharacter) + spaceCharacter);
            // sc-bytes
            stringBuilder.Append((parsedEntry.EdgeServerBytesSent.HasValue ? parsedEntry.EdgeServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
            // cs-method
            stringBuilder.Append((parsedEntry.HttpMethod ?? dashCharacter) + spaceCharacter);
            // cs-uri-stem
            stringBuilder.Append((parsedEntry.RequestUrl ?? dashCharacter) + spaceCharacter);

            // -
            stringBuilder.Append(dashCharacter + spaceCharacter);

            // rs-duration
            stringBuilder.Append((parsedEntry.RemoteServerTimeTaken.HasValue ? parsedEntry.RemoteServerTimeTaken.Value.ToString() : dashCharacter) + spaceCharacter);
            // rs-bytes
            stringBuilder.Append((parsedEntry.RemoteServerBytesSent.HasValue ? parsedEntry.RemoteServerBytesSent.Value.ToString() : dashCharacter) + spaceCharacter);
            // c-referrer
            stringBuilder.Append((parsedEntry.Referrer ?? dashCharacter) + spaceCharacter);
            // c-user-agent
            stringBuilder.Append((parsedEntry.UserAgent ?? dashCharacter) + spaceCharacter);
            // customer-id
            stringBuilder.Append((parsedEntry.CustomerId ?? dashCharacter) + spaceCharacter);
            // x-ec_custom-1
            stringBuilder.AppendLine((parsedEntry.CustomField ?? dashCharacter) + spaceCharacter);

            return stringBuilder.ToString();
        }

        private static string ToUnixTimeStamp(DateTime dateTime)
        {
            var secondsPastEpoch = (dateTime - _unixTimestamp).TotalSeconds;
            return secondsPastEpoch.ToString(CultureInfo.InvariantCulture);
        }
    }
}
