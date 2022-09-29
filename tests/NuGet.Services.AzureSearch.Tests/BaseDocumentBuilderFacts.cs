// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Protocol.Catalog;
using NuGet.Services.AzureSearch.Support;
using NuGet.Services.Entities;
using NuGetGallery.Frameworks;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class BaseDocumentBuilderFacts
    {
        public class PopulateMetadataWithCatalogLeaf : Facts
        {
            [Theory]
            [InlineData("any")]
            [InlineData("agnostic")]
            [InlineData("unsupported")]
            [InlineData("fakeframework1.0")]
            public void DoesNotIncludeDependencyVersionSpecialFrameworks(string framework)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = framework,
                            Dependencies = new List<Protocol.Catalog.PackageDependency>
                            {
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = "2.0.0",
                                },
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Frameworks",
                                    Range = "3.0.0",
                                },
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal("NuGet.Versioning:2.0.0|NuGet.Frameworks:3.0.0", full.FlattenedDependencies);
            }

            [Theory]
            [InlineData("any", ":")]
            [InlineData("net40", "::net40")]
            [InlineData("NET45", "::net45")]
            [InlineData(".NETFramework,Version=4.0", "::net40")]
            public void AddsEmptyDependencyGroup(string framework, string expected)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = framework,
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(expected, full.FlattenedDependencies);
            }

            [Fact]
            public void AddsEmptyArrayForNullTags()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    Tags = null,
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Empty(full.Tags);
            }

            [Fact]
            public void AddsEmptyStringForDependencyVersionAllRange()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = "net40",
                            Dependencies = new List<Protocol.Catalog.PackageDependency>
                            {
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = "(, )"
                                }
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal("NuGet.Versioning::net40", full.FlattenedDependencies);
            }

            [Theory]
            [InlineData("1.0.0", "1.0.0")]
            [InlineData("1.0.0-beta+git", "1.0.0-beta")]
            [InlineData("[1.0.0-beta+git, )", "1.0.0-beta")]
            [InlineData("[1.0.0, 1.0.0]", "[1.0.0]")]
            [InlineData("[1.0.0, 2.0.0)", "[1.0.0, 2.0.0)")]
            [InlineData("[1.0.0, )", "1.0.0")]
            public void AddShortFormOfDependencyVersionRange(string input, string expected)
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = new List<PackageDependencyGroup>
                    {
                        new PackageDependencyGroup
                        {
                            TargetFramework = "net40",
                            Dependencies = new List<Protocol.Catalog.PackageDependency>
                            {
                                new Protocol.Catalog.PackageDependency
                                {
                                    Id = "NuGet.Versioning",
                                    Range = input
                                }
                            },
                        },
                    },
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal("NuGet.Versioning:" + expected + ":net40", full.FlattenedDependencies);
            }

            [Fact]
            public void AllowNullDependencyGroups()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    DependencyGroups = null
                };
                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Null(full.FlattenedDependencies);
            }

            [Fact]
            public void IfLeafHasIconFile_LinksToFlatContainer()
            {
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    IconFile = "iconFile",
                    IconUrl = "iconUrl"
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(Data.FlatContainerIconUrl, full.IconUrl);
            }

            [Fact]
            public void IfLeafDoesNotHaveIconFile_UsesIconUrl()
            {
                var iconUrl = "iconUrl";
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    IconUrl = iconUrl
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(iconUrl, full.IconUrl);
            }

            [Fact]
            public void IfLeafDoesNotHaveIconFileButHasUrl_UsesFlatContainerIfConfigured()
            {
                Config.AllIconsInFlatContainer = true;
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion,
                    IconUrl = "iconUrl"
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Equal(Data.FlatContainerIconUrl, full.IconUrl);
            }

            [Fact]
            public void IfLeafDoesNotHaveAnyIconFile_NoIconUrlIsSet()
            {
                Config.AllIconsInFlatContainer = true;
                var leaf = new PackageDetailsCatalogLeaf
                {
                    PackageId = Data.PackageId,
                    PackageVersion = Data.NormalizedVersion
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.NormalizedVersion, leaf);

                Assert.Null(full.IconUrl);
            }
        }

        public class PopulateMetadataWithPackage : Facts
        {
            [Fact]
            public void PrefersVersionSpecificIdOverPackageRegistrationId()
            {
                var expected = "WINDOWSAZURE.storage";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "windowsazure.STORAGE",
                    },
                    Id = expected,
                    NormalizedVersion = Data.NormalizedVersion,
                    LicenseExpression = "Unlicense",
                    HasEmbeddedIcon = true,
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                VerifyFieldsDependingOnId(expected, full);
            }

            [Fact]
            public void PrefersPackageRegistrationIdOverProvidedId()
            {
                var expected = "windowsazure.STORAGE";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = expected,
                    },
                    NormalizedVersion = Data.NormalizedVersion,
                    LicenseExpression = "Unlicense",
                    HasEmbeddedIcon = true,
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                VerifyFieldsDependingOnId(expected, full);
            }

            [Fact]
            public void UsesProvidedPackageId()
            {
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                    LicenseExpression = "Unlicense",
                    HasEmbeddedIcon = true,
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                VerifyFieldsDependingOnId(Data.PackageId, full);
            }

            [Fact]
            public void IfPackageHasEmbeddedIcon_LinksToFlatContainer()
            {
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                    IconUrl = "iconUrl",
                    HasEmbeddedIcon = true
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                Assert.Equal(Data.FlatContainerIconUrl, full.IconUrl);
            }

            [Fact]
            public void IfPackageDoesNotHaveEmbeddedIcon_UsesIconUrl()
            {
                var iconUrl = "iconUrl";
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                    IconUrl = iconUrl
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                Assert.Equal(iconUrl, full.IconUrl);
            }

            [Fact]
            public void IfPackageDoesNotHaveIconFileButHasUrl_UsesFlatContainerIfConfigured()
            {
                Config.AllIconsInFlatContainer = true;
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                    IconUrl = "iconUrl"
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                Assert.Equal(Data.FlatContainerIconUrl, full.IconUrl);
            }

            [Fact]
            public void IfPackageDoesNotHaveAnyIconFile_NoIconUrlIsSet()
            {
                Config.AllIconsInFlatContainer = true;
                var package = new Package
                {
                    NormalizedVersion = Data.NormalizedVersion,
                };

                var full = new HijackDocument.Full();

                Target.PopulateMetadata(full, Data.PackageId, package);

                Assert.Null(full.IconUrl);
            }

            private static void VerifyFieldsDependingOnId(string expected, HijackDocument.Full full)
            {
                Assert.Equal(expected, full.PackageId);
                Assert.Equal(expected, full.TokenizedPackageId);
                Assert.Equal(expected, full.Title);
                Assert.Equal($"{Data.GalleryBaseUrl}packages/{expected}/{Data.NormalizedVersion}/license", full.LicenseUrl);
                Assert.Equal($"{Data.FlatContainerBaseUrl}{Data.FlatContainerContainerName}/{Data.LowerPackageId}/{Data.LowerNormalizedVersion}/icon", full.IconUrl);
            }

            [Theory]
            [MemberData(nameof(TargetFrameworkCases))]
            public void AddsFrameworksAndTfmsFromPackage(List<string> supportedFrameworks, List<string> expectedTfms, List<string> expectedFrameworks)
            {
                // arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "TestPackage",
                    },
                    Id = "TestPackage",
                    NormalizedVersion = Data.NormalizedVersion,
                    LicenseExpression = "Unlicense",
                    HasEmbeddedIcon = true,
                    SupportedFrameworks = supportedFrameworks
                                                .Select(f => new PackageFramework() { TargetFramework = f })
                                                .ToArray(),
                };

                var full = new HijackDocument.Full();

                // act
                Target.PopulateMetadata(full, Data.PackageId, package);

                // assert
                Assert.True(full.Tfms.Length == expectedTfms.Count);
                foreach (var item in expectedTfms)
                {
                    Assert.Contains(item, full.Tfms);
                }

                Assert.True(full.Frameworks.Length == expectedFrameworks.Count);
                foreach (var item in expectedFrameworks)
                {
                    Assert.Contains(item, full.Frameworks);
                }
            }

            public static IEnumerable<object[]> TargetFrameworkCases =>
                new List<object[]>
                {
                    new object[] {new List<string> {}, new List<string>(), new List<string> {}},
                    new object[] {new List<string> {"any"}, new List<string> {"any"}, new List<string> {}},
                    new object[] {new List<string> {"net"}, new List<string> {"net"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"win"}, new List<string> {"win"}, new List<string> {}},
                    new object[] {new List<string> {"foo"}, new List<string> {}, new List<string> {}}, // unsupported tfm is not included
                    new object[] {new List<string> {"dotnet"}, new List<string> {"dotnet"}, new List<string> {}},
                    new object[] {new List<string> {"net472"}, new List<string> {"net472"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"net5.0"}, new List<string> {"net5.0"}, new List<string> {"net"}},
                    new object[] {new List<string> {"netcoreapp3.0"}, new List<string> { "netcoreapp3.0" }, new List<string> { "netcore" } },
                    new object[] {new List<string> {"netstandard2.0"}, new List<string> { "netstandard2.0" }, new List<string> { "netstandard" } },
                    new object[] {new List<string> {"net40", "net45"}, new List<string> {"net40", "net45"}, new List<string> {"netframework"}},
                    new object[] {new List<string> {"net5.0-tvos", "net5.0-ios"}, new List<string> {"net5.0-ios", "net5.0-tvos"}, new List<string> {"net"}},
                    new object[] {new List<string> {"net5.0-tvos", "net5.0-ios13.0"}, new List<string> {"net5.0-ios13.0", "net5.0-tvos"}, new List<string> {"net"}},
                    new object[] {new List<string> {"net5.1-tvos", "net5.1", "net5.0-tvos"}, new List<string> {"net5.0-tvos", "net5.1", "net5.1-tvos"}, new List<string> {"net"}},
                    new object[] {new List<string> {"net5.0", "netcoreapp3.1", "native"}, new List<string> {"native", "net5.0", "netcoreapp3.1"}, new List<string> {"net", "netcore"}},

                    new object[] {new List<string> {"netcoreapp3.1", "netstandard2.0"}, new List<string> {"netcoreapp3.1", "netstandard2.0"},
                                    new List<string> {"netcore", "netstandard"}},

                    new object[] {new List<string> {"netstandard2.1", "net45", "net472", "tizen40"}, new List<string> {"netstandard2.1", "net45", "net472", "tizen40"},
                                    new List<string> {"netframework", "netstandard"}},

                    new object[] {new List<string>{"net40", "net471", "net5.0-watchos", "netstandard2.0", "netstandard2.1"},
                                    new List<string>{"net40", "net471", "net5.0-watchos", "netstandard2.0", "netstandard2.1"}, new List<string> {"netframework", "net", "netstandard"}},

                    new object[] {new List<string>{"net45", "netstandard2.1", "xamarinios"}, new List<string>{"net45", "netstandard2.1", "xamarinios"},
                                    new List<string> {"netframework", "netstandard"}},

                    new object[] {new List<string> {"portable-net45+sl4+win+wp71", "portable-net45+sl5+win+wp71+wp8"},
                                    new List<string> {"portable-net45+sl4+win+wp71", "portable-net45+sl5+win+wp71+wp8"}, new List<string> {}},

                    new object[] {new List<string> {"net20", "net35", "net40", "net45", "netstandard1.0", "netstandard1.3", "netstandard2.0",
                            "portable-net40+sl5+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"},
                                    new List<string> {"net20", "net35", "net40", "net45", "netstandard1.0", "netstandard1.3", "netstandard2.0",
                            "portable-net40+sl5+win8+wp8+wpa81", "portable-net45+win8+wp8+wpa81"}, new List<string> {"netframework", "netstandard"}}
                };
        }

        public abstract class Facts
        {
            public Facts()
            {
                Options = new Mock<IOptionsSnapshot<AzureSearchJobConfiguration>>();
                Config = new AzureSearchJobConfiguration
                {
                    GalleryBaseUrl = Data.GalleryBaseUrl,
                    FlatContainerBaseUrl = Data.FlatContainerBaseUrl,
                    FlatContainerContainerName = Data.FlatContainerContainerName,
                };

                Options.Setup(o => o.Value).Returns(() => Config);

                Target = new BaseDocumentBuilder(Options.Object);
            }

            public Mock<IOptionsSnapshot<AzureSearchJobConfiguration>> Options { get; }
            public AzureSearchJobConfiguration Config { get; }
            public BaseDocumentBuilder Target { get; }
        }
    }
}
