// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.ApplicationInsights.DataContracts;

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    internal static class TelemetryClient
    {
        private static readonly Microsoft.ApplicationInsights.TelemetryClient _client = new Microsoft.ApplicationInsights.TelemetryClient();

        internal static void TrackEvent(EventTelemetry telemetry)
        {
            _client.TrackEvent(telemetry);
        }

        internal static void TrackException(Exception ex)
        {
            _client.TrackException(ex);
        }

        internal static void TrackRequest(RequestTelemetry telemetry)
        {
            _client.TrackRequest(telemetry);
        }
    }
}