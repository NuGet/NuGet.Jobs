﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog
{
    public class FileSystemEmulatorHandler : DelegatingHandler
    {
        private Uri _rootUrl;
        private string _rootNormalized;

        public FileSystemEmulatorHandler() : base() { }
        public FileSystemEmulatorHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        public string RootFolder { get; set; }
        public Uri BaseAddress
        {
            get
            { return _rootUrl; }
            set
            {
                _rootUrl = value;
                _rootNormalized = GetNormalizedUrl(_rootUrl);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Compare the URL
            string requestNormalized = GetNormalizedUrl(request.RequestUri);
            if (requestNormalized.StartsWith(_rootNormalized))
            {
                var relative = requestNormalized.Substring(_rootNormalized.Length);
                return Intercept(relative, request, cancellationToken);
            }
            else
            {
                return base.SendAsync(request, cancellationToken);
            }
        }

        private Task<HttpResponseMessage> Intercept(string relative, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = Path.Combine(
                RootFolder,
                relative.Trim('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
            {
                // The framework will dispose of the original stream after "transferring" it

                string content;
                using (StreamReader reader = new StreamReader(path))
                {
                    content = reader.ReadToEnd();
                }

                HttpContent httpContent = new StringContent(content);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = httpContent
                });
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        private static string GetNormalizedUrl(Uri url)
        {
            return url.GetComponents(
                UriComponents.HostAndPort |
                UriComponents.UserInfo |
                UriComponents.Path,
                UriFormat.SafeUnescaped);
        }
    }
}
