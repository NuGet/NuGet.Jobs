// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;
using NuGet.Services.Validation.Orchestrator;

namespace NuGet.Services.Validation.Vcs
{
    /// <summary>
    /// Evaluates whether a given entity matches some criteria.
    /// </summary>
    public interface IPackageCriteriaEvaluator<T> where T:IEntity
    {
        bool IsMatch(IPackageCriteria criteria, T entity);
    }
}
