﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Gallery.CredentialExpiration.Models
{
    public class ExpiredCredentialData
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string Description { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Expires { get; set; }

        private const string ApiKeyV1 = "apikey.v1";
        private const string ApiKeyV2 = "apikey.v2";

        public Boolean IsNonScopedApiKey()
        {
            return string.Equals(Type, ApiKeyV1, StringComparison.OrdinalIgnoreCase);
        }
    }
}
