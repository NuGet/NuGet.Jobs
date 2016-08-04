﻿// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System.Threading.Tasks;

namespace NuGet.Jobs.Validation.Common.Configuration
{
    public interface IConfigurationService
    {
        Task<string> Get(string key);
    }
}
