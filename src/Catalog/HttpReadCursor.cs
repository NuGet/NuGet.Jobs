﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public class HttpReadCursor : ReadCursor
    {
        private readonly Uri _address;
        private readonly Func<HttpMessageHandler> _handlerFunc;

        public HttpReadCursor(Uri address, Func<HttpMessageHandler> handlerFunc = null)
        {
            _address = address;
            _handlerFunc = handlerFunc;
        }

        public override async Task LoadAsync(CancellationToken cancellationToken)
        {
            HttpMessageHandler handler = (_handlerFunc != null) ? _handlerFunc() : new WebRequestHandler { AllowPipelining = true };

            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage response = await client.GetAsync(_address, cancellationToken))
            {
                Trace.TraceInformation("HttpReadCursor.Load {0}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                JObject obj = JObject.Parse(json);
                Value = obj["value"].ToObject<DateTime>();
            }

            Trace.TraceInformation("HttpReadCursor.Load: {0}", this);
        }
    }
}