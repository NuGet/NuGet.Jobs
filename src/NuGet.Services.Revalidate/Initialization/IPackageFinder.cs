// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Services.Revalidate
{
    public interface IPackageFinder
    {
        /// <summary>
        /// Find packages that are owned by the Microsoft account.
        /// </summary>
        /// <returns>A case insensitive set of package ids.</returns>
        CaseInsensitiveSet FindMicrosoftPackages();

        /// <summary>
        /// Find packages that are preinstalled by Visual Studio.
        /// </summary>
        /// <param name="except">A case insensitive set of package ids that should be removed from the result.</param>
        /// <returns>A case insensitive set of package ids.</returns>
        CaseInsensitiveSet FindPreinstalledPackages(CaseInsensitiveSet except);

        /// <summary>
        /// Find packages ALL dependencies of the root set of package ids.
        /// </summary>
        /// <param name="roots">The set of root package ids whose dependencies should be fetched.</param>
        /// <returns>A case insensitive set of package ids.</returns>
        CaseInsensitiveSet FindDependencyPackages(CaseInsensitiveSet roots);

        /// <summary>
        /// Find all packages remaining packages.
        /// </summary>
        /// <param name="except">The set of packages that should be removed from the result.</param>
        /// <returns>A case insensitive set of package ids.</returns>
        CaseInsensitiveSet FindAllPackages(CaseInsensitiveSet except);

        /// <summary>
        /// Find the relevant information about the given set of packages.
        /// </summary>
        /// <param name="setName">The name of this set of packages.</param>
        /// <param name="packageIds">The set of package ids.</param>
        /// <returns>Information about each package that exists in the database.</returns>
        List<PackageInformation> FindPackageInformation(string setName, CaseInsensitiveSet packageIds);

        /// <summary>
        /// Find versions that are appropriate for revalidations.
        /// </summary>
        /// <param name="packageIds">The packages whose versions should be fetched.</param>
        /// <returns>A dictionary where the keys are package ids and the values are the versions of that package.</returns>
        Dictionary<string, List<NuGetVersion>> FindAppropriateVersions(List<string> packageIds);
    }
}
