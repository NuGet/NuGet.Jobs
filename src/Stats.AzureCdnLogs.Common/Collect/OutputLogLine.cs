// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Stats.AzureCdnLogs.Common.Collect
{
    /// <summary>
    /// The schema of the line in statistic logs. 
    /// Any log needs to have its lines formatted like this in order to be inserted in the stats db. 
    /// </summary>
    public class OutputLogLine
    {
        //timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n");
        public string TimeStamp { get; private set; }

        public string TimeTaken { get; private set; }

        public string CIp { get; private set; }

        public string FileSize { get; private set; }

        public string SIp { get; private set; }

        public string SPort { get; private set; }

        public string ScStatus { get; private set; }

        public string ScBytes { get; private set; }

        public string CsMethod { get; private set; }

        public string CsUriStem { get; private set; }

        public string RsDuration { get; private set; }

        public string RsBytes { get; private set; }

        public string CReferrer { get; private set; }

        public string CUserAgent { get; private set; }

        public string CustomerId { get; private set; }

        public string XEc_Custom_1 { get; private set; }

        public OutputLogLine(string timestamp,
                             string timetaken,
                             string cip,
                             string filesize,
                             string sip,
                             string sport,
                             string scstatus,
                             string scbytes,
                             string csmethod,
                             string csuristem,
                             string rsduration,
                             string rsbytes,
                             string creferrer,
                             string cuseragent,
                             string customerid,
                             string xeccustom1)
        {
            TimeStamp = timestamp;
            TimeTaken = timetaken;
            CIp = cip;
            FileSize = filesize;
            SIp = sip;
            SPort = sport;
            ScStatus = scstatus;
            ScBytes = scbytes;
            CsMethod = csmethod;
            CsUriStem = csuristem;
            RsDuration = rsduration;
            RsBytes = rsbytes;
            CReferrer = creferrer;
            CUserAgent = cuseragent;
            CustomerId = customerid;
            XEc_Custom_1 = xeccustom1;
        }

        public static string Header
        {
            get { return "#Fields: timestamp time-taken c-ip filesize s-ip s-port sc-status sc-bytes cs-method cs-uri-stem - rs-duration rs-bytes c-referrer c-user-agent customer-id x-ec_custom-1\n"; }
        }

        public override string ToString()
        {
            return $"{TimeStamp} {TimeTaken} {CIp} {FileSize} {SIp} {SPort} {ScStatus} {ScBytes} {CsMethod} {CsUriStem} - {RsDuration} {RsBytes} {CReferrer} {CUserAgent} {CustomerId} {XEc_Custom_1}";
        }
    }
}
