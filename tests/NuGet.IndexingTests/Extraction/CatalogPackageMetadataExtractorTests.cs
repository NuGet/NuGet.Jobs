﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using Xunit;

namespace NuGet.IndexingTests.Extraction
{
    public class CatalogPackageMetadataExtractorTests
    {
        [Fact]
        public void ThrowsWhenCatalogItemIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                CatalogPackageMetadataExtraction.MakePackageMetadata(
                    catalogItem: null,
                    galleryBaseAddress: new Uri("https://test"),
                    flatContainerBaseAddress: new Uri("https://test"),
                    flatContainerContainerName: "fc"));

            Assert.Equal("catalogItem", ex.ParamName);
        }

        [Fact]
        public void DoesNotThrowWhenGalleryBaseUrlIsNull()
        {
            var ex = Record.Exception(() =>
                CatalogPackageMetadataExtraction.MakePackageMetadata(
                    catalogItem: CatalogEntry(new { }),
                    galleryBaseAddress: null,
                    flatContainerBaseAddress: new Uri("https://test"),
                    flatContainerContainerName: "fc"));
            Assert.Null(ex);
        }

        [Fact]
        public void ThrowsWhenFlatContainerBaseAddressIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                CatalogPackageMetadataExtraction.MakePackageMetadata(
                    catalogItem: CatalogEntry(new { }),
                    galleryBaseAddress: new Uri("https://test"),
                    flatContainerBaseAddress: null,
                    flatContainerContainerName: "fc"));

            Assert.Equal("flatContainerBaseAddress", ex.ParamName);
        }

        [Fact]
        public void ThrowsWhenFlatContainerContainerNameIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                CatalogPackageMetadataExtraction.MakePackageMetadata(
                    catalogItem: CatalogEntry(new { }),
                    galleryBaseAddress: new Uri("https://test"),
                    flatContainerBaseAddress: new Uri("https://test"),
                    flatContainerContainerName: null));

            Assert.Equal("flatContainerContainerName", ex.ParamName);
        }

        [Theory, MemberData(nameof(AddsListedData))]
        public void AddsListed(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, null, new Uri("https://test"), "fc");

            // Assert
            Assert.Contains(MetadataConstants.ListedPropertyName, metadata.Keys);
            Assert.Equal(expected, metadata[MetadataConstants.ListedPropertyName]);
        }

        [Theory, MemberData(nameof(AddsSemVerLevelKeyData))]
        public void AddsSemVerLevelKey(object catalogEntry, bool expectedToContainKey, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, null, new Uri("https://test"), "fc");


            // Assert
            Assert.Equal(expectedToContainKey, metadata.Keys.Contains(MetadataConstants.SemVerLevelKeyPropertyName));
            if (expectedToContainKey)
            {
                Assert.Equal(expected, metadata[MetadataConstants.SemVerLevelKeyPropertyName]);
            }
        }

        [Theory, MemberData(nameof(AddsSupportedFrameworksData))]
        public void AddsSupportedFrameworks(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, null, new Uri("https://test"), "fc");

            // Assert
            if (expected != null)
            {
                Assert.Contains(MetadataConstants.SupportedFrameworksPropertyName, metadata.Keys);
                Assert.Equal(expected.Split('|').OrderBy(f => f), metadata[MetadataConstants.SupportedFrameworksPropertyName].Split('|').OrderBy(f => f));
            }
            else
            {
                Assert.DoesNotContain(MetadataConstants.SupportedFrameworksPropertyName, metadata.Keys);
            }
        }

        [Theory, MemberData(nameof(AddsFlattenedDependenciesData))]
        public void AddsFlattenedDependencies(object catalogEntry, string expected)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, null, new Uri("https://test"), "fc");

            // Assert
            Assert.Contains(MetadataConstants.FlattenedDependenciesPropertyName, metadata.Keys);
            Assert.Equal(expected, metadata[MetadataConstants.FlattenedDependenciesPropertyName]);
        }

        [Fact]
        public void AllowsMissingVerbatimVersion()
        {
            // Arrange
            // We add the invalid portable package entry folder name since this causes a failure which reads the ID and
            // version from the generated .nuspec.
            var catalogEntryJObject = JObject.FromObject(new
            {
                id = "NuGet.Versioning",
                version = "4.6.0",
                packageEntries = new object[]
                {
                    new { fullName = "lib/net45/something.dll" },
                    new { fullName = "lib/portable-win-wpa8/something-else.dll" }, 
                },
            });

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, null, new Uri("https://test"), "fc");

            // Assert
            Assert.Equal(new[] { "id", "listed", "version" }, metadata.Keys.OrderBy(x => x));
            Assert.Equal("4.6.0", metadata["version"]);
        }

        [Theory, MemberData(nameof(AddsLicensesData))]
        public void AddsLicensesUrl(object catalogEntry, Uri galleryBaseAddress, string expectedLicenseurl)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(catalogEntry);

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, galleryBaseAddress, new Uri("https://test"), "fc");

            // Assert
            Assert.Contains(MetadataConstants.LicenseUrlPropertyName, metadata.Keys);
            Assert.Equal(expectedLicenseurl, metadata[MetadataConstants.LicenseUrlPropertyName]);
        }

        [Theory]
        [InlineData("testPackage", "1.0.0", null, null, "https://fc.test", "fc", null)]
        [InlineData("testPackage", "1.0.0", "http://icon.test", null, "https://fc.test", "fc", "http://icon.test")]
        [InlineData("testPackage", "1.0.0", "", null, "https://fc.test", "fc", "")]
        [InlineData("testPackage", "1.0.0", null, "iconfile", "https://fc.test", "fc", "https://fc.test/fc/testpackage/1.0.0/icon")]
        [InlineData("testPackage", "1.0.0", null, "", "https://fc.test", "fc", null)]
        [InlineData("testPackage", "1.0.0", "http://icon.test", "iconfile", "https://fc.test", "fc", "https://fc.test/fc/testpackage/1.0.0/icon")]
        [InlineData("testPackage", "1.0.0", "", "", "https://fc.test", "fc", "")]
        public void AddsIconUrl(string packageId, string packageVersion, string iconUrl, string iconFile, string flatContainerBase, string flatContainerContainerName, string expectedIconUrl)
        {
            var catalogEntryJObject = CatalogEntry(GetCatalogObject(packageId, packageVersion, iconUrl, iconFile));

            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, null, new Uri(flatContainerBase), flatContainerContainerName);

            if (expectedIconUrl == null)
            {
                Assert.False(metadata.ContainsKey(MetadataConstants.IconUrlPropertyName));
            }
            else
            {
                Assert.True(metadata.ContainsKey(MetadataConstants.IconUrlPropertyName));
                Assert.Equal(expectedIconUrl, metadata[MetadataConstants.IconUrlPropertyName]);
            }
        }

        private static object GetCatalogObject(string packageId, string packageVersion, string iconUrl, string iconFile)
        {
            if (iconUrl == null && iconFile == null)
            {
                return new { id = packageId, version = packageVersion };
            }
            if (iconUrl == null && iconFile != null)
            {
                return new { id = packageId, version = packageVersion, iconFile };
            }
            if (iconUrl != null && iconFile == null)
            {
                return new { id = packageId, version = packageVersion, iconUrl };
            }

            return new { id = packageId, version = packageVersion, iconUrl, iconFile };
        }

        [Theory]
        [InlineData("testPackage", "1.0.0", null, null, null)]
        [InlineData("testPackage", "1.0.0", null, null, "https://testnuget/")]
        [InlineData("testPackage", "1.0.0", "MIT", null, null)]
        [InlineData("testPackage", "1.0.0", null, "license.txt", null)]
        [InlineData(null, "1.0.0", "MIT", null, "https://testnuget/")]
        [InlineData("testPackage", null, "MIT", null, "https://testnuget/")]
        [InlineData(null, "1.0.0", null, "license.txt", "https://testnuget/")]
        [InlineData("testPackage", null, null, "license.txt", "https://testnuget/")]
        public void AddsNoLicensesUrl(string packageId, string packageVersion, string licenseExpression, string licenseFile, string galleryBaseAddress)
        {
            // Arrange
            var catalogEntryJObject = CatalogEntry(new { id = packageId, version = packageVersion, licenseExpression, licenseFile });

            // Act
            var metadata = CatalogPackageMetadataExtraction.MakePackageMetadata(catalogEntryJObject, galleryBaseAddress == null ? null : new Uri(galleryBaseAddress), new Uri("https://test"), "fc");

            // Assert
            Assert.False(metadata.ContainsKey(MetadataConstants.LicenseUrlPropertyName));
        }

        public static IEnumerable<object[]> AddsListedData
        {
            get
            {
                yield return new object[] { new { }, "true" };
                yield return new object[] { new { listed = (string)null }, "true" };
                yield return new object[] { new { listed = "TRUE" }, "TRUE" };
                yield return new object[] { new { listed = "False" }, "False" };
                yield return new object[] { new { listed = "Bad" }, "Bad" }; // validation is not done at this stage
                yield return new object[] { new { published = "1900-01-01T00:00:00" }, "false" };
                yield return new object[] { new { published = "1900-01-02T00:00:00" }, "true" };
                yield return new object[] { new { published = "1900-01-01T00:00:00", listed = "True" }, "True" };
            }
        }

        public static IEnumerable<object[]> AddsSemVerLevelKeyData
        {
            get
            {
                // no dependencies
                yield return new object[] { new { verbatimVersion = "1.0.0" }, false, null };
                yield return new object[] { new { verbatimVersion = "1.0.0-semver1" }, false, null };
                yield return new object[] { new { verbatimVersion = "1.0.0-semver2.0" }, true, "2" };
                yield return new object[] { new { verbatimVersion = "1.0.0-semver2.0+again" }, true, "2" };
                yield return new object[] { new { verbatimVersion = "1.0.0+aThirdTime" }, true, "2" };

                // dependencies
                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    false,
                    null
                };

                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0+semver2",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                // dependencies show semver2
                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11-semver2.0.dep" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11-semver2.0.dep+meta" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                // semver2 in real ranges
                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(4.5.11, 6.0.0-semver.2]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(4.5.11-semver.2, 6.0.0]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(4.5.11-semver.2, ]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(, 6.0.0-semver.2]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    true,
                    "2"
                };

                yield return new object[]
                {
                    new
                    {
                        verbatimVersion = "1.0.0",
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "(, 6.0.0]" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },
                    },
                    false,
                    null
                };
            }
        }

        public static IEnumerable<object[]> AddsSupportedFrameworksData
        {
            get
            {
                // framework assembly group
                yield return new object[] { WithFrameworkAssemblyGroup(".NETFramework4.0-Client"), "net40-client" };
                yield return new object[] { WithFrameworkAssemblyGroup(".NETFramework4.0-Client, .NETFramework4.5"), "net40-client|net45" };
                yield return new object[] { WithFrameworkAssemblyGroup("   .NETFramework4.0-Client, , , .NETFramework4.5  ,,"), "net40-client|net45" };
                yield return new object[]
                {
                    new
                    {
                        frameworkAssemblyGroup = new object[]
                        {
                            new { targetFramework = ".NETFramework4.0-Client" },
                            new { targetFramework = ".NETFramework4.0, .NETFramework4.5" },
                            new { targetFramework = "  " }
                        }
                    },
                    "net40-client|net40|net45"
                };

                // a single framework assembly
                yield return new object[]
                {
                    new
                    {
                        frameworkAssemblyGroup = new { targetFramework = ".NETFramework4.0, .NETFramework4.5" }
                    },
                    "net40|net45"
                };

                // package entries
                yield return new object[] { WithPackageEntry("lib/net40/something.dll"), "net40" };
                yield return new object[] { WithPackageEntry("lib/portable-net45%2Bwin%2Bwpa81%2Bwp80%2BMonoAndroid10%2BXamarin.iOS10%2BMonoTouch10/something.dll"), "portable-net45+win8+wp8+wpa81" };
                yield return new object[]
                {
                    new
                    {
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/net45/something.dll" },
                            new { fullName = "lib/net40/something-else.dll" },
                            new { fullName = "bad" }
                        }
                    },
                    "net45|net40"
                };

                // invalid PCL TFM
                yield return new object[]
                {
                    new
                    {
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/net45/something.dll" },
                            new { fullName = "lib/portable-win-wpa8/something-else.dll" }
                        }
                    },
                    null
                };

                // a single package entry
                yield return new object[] { new { packageEntries = new { fullName = "lib/net40/something.dll" } }, "net40" };

                // not target framework folder name
                yield return new object[]
                {
                    new
                    {
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/something.dll" },
                            new { fullName = "lib/net40/something-else.dll" }
                        }
                    },
                    "net40"
                };

                // both
                yield return new object[]
                {
                    new
                    {
                        frameworkAssemblyGroup = new object[]
                        {
                            new { targetFramework = ".NETFramework4.0-Client" },
                            new { targetFramework = ".NETFramework4.0, .NETFramework4.5" },
                            new { targetFramework = "  " }
                        },
                        packageEntries = new object[]
                        {
                            new { fullName = "lib/net45/something.dll" },
                            new { fullName = "lib/net20/something.dll" },
                            new { fullName = "bad" }
                        }
                    },
                    "net40-client|net40|net45|net20"
                };
            }
        }

        public static IEnumerable<object[]> AddsFlattenedDependenciesData
        {
            get
            {
                // multiple packages
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11" },
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },

                    },
                    "Newtonsoft.Json:4.5.11|Microsoft.Data.OData:5.6.2"
                };

                // multiple target frameworks
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Newtonsoft.Json", range = "4.5.11" }
                                },
                                targetFramework = ".NETFramework4.5"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                },
                                targetFramework = ".NETFramework4.0-client"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" },
                                    new { id = "", range = "" }
                                }
                            }
                        },

                    },
                    "Newtonsoft.Json:4.5.11:net45|Microsoft.Data.OData:5.6.2:net40-client|Microsoft.Data.OData:5.6.2"
                };

                // multiple target frameworks without direct package dependencies
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[0],
                                targetFramework = ".NETFramework4.5"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                },
                                targetFramework = ".NETFramework4.0-client"
                            },
                            new
                            {
                                dependencies = new object[]
                                {
                                    new { id = "Microsoft.Data.OData", range = "5.6.2" }
                                }
                            }
                        },

                    },
                    "::net45|Microsoft.Data.OData:5.6.2:net40-client|Microsoft.Data.OData:5.6.2"
                };

                // a single item
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new
                        {
                            dependencies = new object[]
                            {
                                new { id = "Newtonsoft.Json", range = "4.5.11" },
                                new { id = "Microsoft.Data.OData", range = "5.6.2" }
                            }
                        }
                    },
                    "Newtonsoft.Json:4.5.11|Microsoft.Data.OData:5.6.2"
                };

                // different target framework format
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", ".NETFramework4.5"), "Newtonsoft.Json:4.5.11:net45" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", ".NETFramework4.0"), "Newtonsoft.Json:4.5.11:net40" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", string.Empty), "Newtonsoft.Json:4.5.11" };
                yield return new object[] { WithDependency("Newtonsoft.Json", "4.5.11", null), "Newtonsoft.Json:4.5.11" };
                yield return new object[]
                {
                    new
                    {
                        dependencyGroups = new object[]
                        {
                            new
                            {
                                dependencies = new object[]
                                {
                                    new
                                    {
                                        id = "Newtonsoft.Json",
                                        range = "4.5.11"
                                    }
                                }
                            }
                        }
                    },
                    "Newtonsoft.Json:4.5.11"
                };
            }
        }

        public static IEnumerable<object[]> AddsLicensesData
        {
            get
            {     // licenseExpression      licenseFile     licenseUrl
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseExpression = "MIT", licenseUrl = "https://testlicenseurl"},
                    new Uri("https://testnuget"),
                    "https://testnuget/packages/testPackage/1.0.0/license" };
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseFile = "license.txt", licenseUrl = "https://testlicenseurl"},
                    new Uri("https://testnuget"),
                    "https://testnuget/packages/testPackage/1.0.0/license" };
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseUrl = "https://testlicenseurl"},
                    new Uri("https://testnuget"),
                    "https://testlicenseurl" };
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseExpression = "MIT", licenseUrl = "https://testlicenseurl"},
                    null,
                    "https://testlicenseurl" };
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseFile = "license.txt", licenseUrl = "https://testlicenseurl"},
                    null,
                    "https://testlicenseurl" };
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseExpression = "MIT"},
                    new Uri("https://testnuget"),
                    "https://testnuget/packages/testPackage/1.0.0/license" };
                yield return new object[] {
                    new { id = "testPackage", version = "1.0.0", licenseFile = "license.txt"},
                    new Uri("https://testnuget"),
                    "https://testnuget/packages/testPackage/1.0.0/license" };
            }
        }

        private static object WithDependency(string id, string range, string targetFramework)
        {
            return new
            {
                dependencyGroups = new object[]
                {
                    new
                    {
                        dependencies = new object[]
                        {
                            new { id, range }
                        },
                        targetFramework
                    }
                }
            };
        }

        private static object WithFrameworkAssemblyGroup(string targetFramework)
        {
            return new
            {
                frameworkAssemblyGroup = new object[]
                {
                    new { targetFramework }
                }
            };
        }

        private static object WithPackageEntry(string fullName)
        {
            return new
            {
                packageEntries = new object[]
                {
                    new { fullName }
                }
            };
        }

        private static JObject CatalogEntry(object obj)
        {
            var json = JObject.FromObject(obj);

            // Add required properties if they are missing.
            if (json[MetadataConstants.IdPropertyName] == null)
            {
                json[MetadataConstants.IdPropertyName] = "NuGet.Versioning";
            }

            if (json[MetadataConstants.VerbatimVersionPropertyName] == null)
            {
                json[MetadataConstants.VerbatimVersionPropertyName] = "4.6.2";
            }

            return json;
        }
    }
}
