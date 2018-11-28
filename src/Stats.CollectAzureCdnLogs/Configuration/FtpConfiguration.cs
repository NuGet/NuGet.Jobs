// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Stats.CollectAzureCdnLogs.Configuration
{
    public class FtpConfiguration
    {
        /// <summary>
        /// The <see cref="Uri"/> of the FTP server.
        /// </summary>
        public string ServerUrl { get; set; }

        /// <summary>
        /// The username to be used to authenticate against the FTP server.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The password to be used to authenticate against the FTP server.
        /// </summary>
        public string Password { get; set; }

        public Uri GetServerUri()
        {
            var trimmedServerUrl = (ServerUrl ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedServerUrl))
            {
                throw new ArgumentException("FTP Server Url is null or empty.", nameof(ServerUrl));
            }

            // if no protocol was specified assume ftp
            var schemeRegex = new Regex(@"^[a-zA-Z]+://");
            if (!schemeRegex.IsMatch(trimmedServerUrl))
            {
                trimmedServerUrl = string.Concat(@"ftp://", trimmedServerUrl);
            }

            var uri = new Uri(trimmedServerUrl);
            if (!uri.IsAbsoluteUri)
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Url must be an absolute URI. Value: '{0}'.", trimmedServerUrl));
            }

            // only ftp is supported but we could support others
            if (!uri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase))
            {
                throw new UriFormatException(string.Format(CultureInfo.CurrentCulture, "FTP Server Url must use the 'ftp://' scheme. Value: '{0}'.", trimmedServerUrl));
            }

            return uri;
        }
    }
}
