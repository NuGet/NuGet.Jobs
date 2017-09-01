// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Stats.CollectAzureChinaCDNLogs;
using Xunit;

namespace Tests.Stats.CollectAzureChinaCDNLogs
{
    public class ChinaCollectorTests
    {
        [Theory]
        [InlineData("40.125.202.231,7/27/2017 4:50:09 PM +00:00,GET,\"/v3-flatcontainer/system.net.primitives/index.json\",HTTP/1.1,200,1196,\"-\",\"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\",133,TCP_MISS,118.180.6.168", "1501149009 0 40.125.202.231 0 118.180.6.168 0 200 1196 GET /v3-flatcontainer/system.net.primitives/index.json - 133 0 - NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0) na na")]
        [InlineData("40.125.202.231,7/27/2017 4:50:09 PM +00:00,GET,\"/v3-flatcontainer/system.net.primitives/index.json\",HTTP/1.1,400,1196,\"-\",\"NuGet+Command+Line/4.3.0+(Microsoft+Windows+NT+6.2.9200.0)\",133,TCP_MISS,118.180.6.168", null)]
        [InlineData("c-ip, timestamp, cs-method, cs-uri-stem, http-ver, sc-status, sc-bytes, c-referer, c-user-agent, rs-duration(ms), hit-miss, s-ip", null)]

        public void TransformRawLogLine(string input, string expectedOutput)
        {
            var collector  = new ChinaStatsCollector();

            var tranformedinput = collector.TransformRawLogLine(input);
            string output = tranformedinput == null ? null : tranformedinput.ToString();
            Assert.Equal(expectedOutput,output);
        }
    }
}
