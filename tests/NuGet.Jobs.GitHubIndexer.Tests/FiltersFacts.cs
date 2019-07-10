// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Jobs.GitHubIndexer.Tests
{
    public class FiltersFacts
    {
        public class TheGetConfigFileTypeFunction
        {
            [Fact]
            public void NullFileName()
            {
                Assert.Throws<ArgumentNullException>(() => { Filters.GetConfigFileType(null); });
            }

            [Fact]
            public void InvalidFileName()
            {
                Assert.Throws<ArgumentException>(() => { Filters.GetConfigFileType("\0"); });
            }

            [Fact]
            public void UnklnownConfigFileType()
            {
                Assert.Equal(Filters.ConfigFileType.None, Filters.GetConfigFileType("File.riad"));
                Assert.Equal(Filters.ConfigFileType.None, Filters.GetConfigFileType("File.config"));
            }

            [Fact]
            public void KnownConfigFileTypes()
            {
                Assert.Equal(Filters.ConfigFileType.Proj, Filters.GetConfigFileType("File.proj"));
                Assert.Equal(Filters.ConfigFileType.Proj, Filters.GetConfigFileType("File.csproj"));
                Assert.Equal(Filters.ConfigFileType.Props, Filters.GetConfigFileType("File.props"));
                Assert.Equal(Filters.ConfigFileType.Targets, Filters.GetConfigFileType("File.targets"));
                Assert.Equal(Filters.ConfigFileType.PkgConfig, Filters.GetConfigFileType("packages.config"));
                Assert.Equal(Filters.ConfigFileType.PkgConfig, Filters.GetConfigFileType(@"shadowsocks-csharp\packages.config"));
            }

            [Fact]
            public void KnownConfigFileTypesCaseInsensitive()
            {
                Assert.Equal(Filters.ConfigFileType.Proj, Filters.GetConfigFileType("File.pRoj"));
                Assert.Equal(Filters.ConfigFileType.Proj, Filters.GetConfigFileType("File.CsProj"));
                Assert.Equal(Filters.ConfigFileType.Props, Filters.GetConfigFileType("File.proPS"));
                Assert.Equal(Filters.ConfigFileType.Targets, Filters.GetConfigFileType("File.tarGETs"));
                Assert.Equal(Filters.ConfigFileType.PkgConfig, Filters.GetConfigFileType("paCkAges.cONfiG"));
            }
        }
    }
}
