// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace NuGet.Jobs.Validation.Common.Validators.Vcs
{
    internal sealed class GlobalExceptionHandlerMiddleware : OwinMiddleware
    {
        public GlobalExceptionHandlerMiddleware(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            try
            {
                await Next.Invoke(context);
            }
            catch (Exception ex)
            {
                TelemetryClient.TrackException(ex);
            }
        }
    }
}