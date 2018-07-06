// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery;
using NuGet.Services.Validation.Vcs;

namespace NuGet.Services.Validation
{
    public class SymbolCriteriaEvaluator : ICriteriaEvaluator<SymbolPackage>
    {
        public bool IsMatch(ICriteria criteria, SymbolPackage entity)
        {
            throw new NotImplementedException();
        }
    }
}
