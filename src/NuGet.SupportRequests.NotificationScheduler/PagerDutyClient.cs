// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.SupportRequests.NotificationScheduler
{
    internal sealed class PagerDutyClient
    {
        private readonly string _apiKey;
        private readonly string _onCallUrl;

        internal PagerDutyClient(string accountName, string apiKey)
        {
            _apiKey = apiKey;

            // Configure defaults
            _onCallUrl = string.Format(CultureInfo.InvariantCulture, "https://{0}.pagerduty.com/api/v1/users/on_call", accountName);
        }

        public async Task<string> GetPrimaryOnCallAsync()
        {
            var username = string.Empty;

            string response;
            using (var httpClient = new HttpClient())
            {
                var token = "Token token=" + _apiKey;
                httpClient.DefaultRequestHeaders.Add("Authorization", token);

                response = await httpClient.GetStringAsync(_onCallUrl);
            }

            if (!string.IsNullOrEmpty(response))
            {
                username = GetEmailAliasFromOnCallUser(response, "PQP8V6O");
            }

            return username;
        }

        internal static string GetEmailAliasFromOnCallUser(string response, string policyId)
        {
            var username = string.Empty;

            var root = JObject.Parse(response);
            var users = (JArray)root["users"];

            foreach (var item in users)
            {
                foreach (var onCall in item["on_call"])
                {
                    if (Convert.ToInt32(onCall["level"], CultureInfo.InvariantCulture) == 1)
                    {
                        var escalationPolicyId = onCall["escalation_policy"]["id"].Value<string>();
                        if (string.Equals(escalationPolicyId, policyId, StringComparison.Ordinal))
                        {
                            var email = item["email"].ToString();
                            var length = email.IndexOf("@", 0, StringComparison.OrdinalIgnoreCase);
                            var alias = email.Substring(0, length);

                            // Find the primary that is not nugetcore
                            if (!alias.Equals("nugetcore", StringComparison.OrdinalIgnoreCase))
                            {
                                username = alias;
                                break;
                            }
                        }
                    }
                }
            }

            return username;
        }
    }
}
