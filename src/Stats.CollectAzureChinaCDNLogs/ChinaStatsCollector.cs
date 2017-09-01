// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Stats.AzureCdnLogs.Common.Collect;

namespace Stats.CollectAzureChinaCDNLogs
{
    /// <summary>
    /// An implementation of the <see cref="Stats.AzureCdnLogs.Common.Collect.Collector" for China CDN logs./>
    /// </summary>
    public class ChinaStatsCollector : Collector
    {
        public ChinaStatsCollector(ILogSource source, ILogDestination destination) : base(source, destination)
        {}

        public ChinaStatsCollector()
        { }

        public override OutputLogLine TransformRawLogLine(string line)
        {
            if (line.Trim().StartsWith("c-ip", true, System.Globalization.CultureInfo.InvariantCulture))
            {
                //is the header
                return null;
            }

            string[] segments = GetSegments(line);
            string notAvailableString = "na";
            string notAvailableInt = "0";

            string timestamp = segments[1];
            DateTime dt = DateTime.Parse(timestamp);
            string timeStamp2 = ToUnixTimeStamp(dt);

            if(segments[5] == "400")
            {
                return null;
            }

            return new OutputLogLine(timestamp: timeStamp2,
                timetaken: notAvailableInt,
                cip:segments[0],
                filesize: notAvailableInt,
                sip: segments[11],
                sport: notAvailableInt,
                scstatus: segments[5],
                scbytes: segments[6],
                csmethod: segments[2],
                csuristem: segments[3],
                rsduration: segments[9],
                rsbytes: notAvailableInt,
                creferrer: segments[7],
                cuseragent: segments[8],
                customerid: notAvailableString,
                xeccustom1: notAvailableString
               );
        }

        private string[] GetSegments(string line)
        {
            string[] segments = line.Split(',').Select(s=>s.Trim()).ToArray();
            List<string> result = new List<string>();
            for(int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].Contains("\"") || (segments[i].StartsWith("\"") && segments[i].EndsWith("\"")))
                {
                    result.Add(segments[i]);
                }
                else
                {
                    //this case is when an entry is like "
                    string resultInt = segments[i++];
                    while(i< segments.Length && !segments[i].EndsWith("\""))
                    {
                        resultInt += segments[i++].Trim();
                    }
                    if (i < segments.Length) { resultInt += segments[i]; }
                    result.Add(resultInt);
                }
            }
            return result.Select( s => s.Replace("\"", "")).ToArray();
        }
    }
}
