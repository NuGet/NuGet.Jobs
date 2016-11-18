// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Owin;

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    internal sealed class ApplicationInsightsMiddleware : OwinMiddleware
    {
        public ApplicationInsightsMiddleware(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await Next.Invoke(context);
            }
            finally
            {
                stopwatch.Stop();

                TrackRequest(context, startTime, stopwatch.Elapsed);
            }
        }

        private static void TrackRequest(IOwinContext context, DateTimeOffset startTime, TimeSpan duration)
        {
            var telemetry = new RequestTelemetry()
            {
                Duration = duration,
                HttpMethod = context.Request.Method,
                Name = TelemetryConstants.Vcs,
                ResponseCode = context.Response.StatusCode.ToString(),
                StartTime = startTime,
                Success = context.Response.StatusCode < 400,
                Timestamp = DateTimeOffset.UtcNow,
                Url = context.Request.Uri
            };

            foreach (var header in context.Request.Headers)
            {
                var headerName = header.Key;
                var headerValues = header.Value == null ? null : string.Join(",", header.Value);

                telemetry.Properties.Add(headerName, headerValues);
            }

            TelemetryClient.TrackRequest(telemetry);
        }
    }
}