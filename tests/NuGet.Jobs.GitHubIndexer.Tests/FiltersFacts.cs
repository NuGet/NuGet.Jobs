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
                Assert.Equal(Filters.ConfigFileType.NONE, Filters.GetConfigFileType("File.riad"));
                Assert.Equal(Filters.ConfigFileType.NONE, Filters.GetConfigFileType("File.config"));
            }

            [Fact]
            public void KnownConfigFileTypes()
            {
                Assert.Equal(Filters.ConfigFileType.PROJ, Filters.GetConfigFileType("File.proj"));
                Assert.Equal(Filters.ConfigFileType.PROJ, Filters.GetConfigFileType("File.csproj"));
                Assert.Equal(Filters.ConfigFileType.PROPS, Filters.GetConfigFileType("File.props"));
                Assert.Equal(Filters.ConfigFileType.TARGETS, Filters.GetConfigFileType("File.targets"));
                Assert.Equal(Filters.ConfigFileType.PKG_CONFIG, Filters.GetConfigFileType("packages.config"));
                Assert.Equal(Filters.ConfigFileType.PKG_CONFIG, Filters.GetConfigFileType(@"shadowsocks-csharp\packages.config"));
            }

            [Fact]
            public void KnownConfigFileTypesCaseInsensitive()
            {
                Assert.Equal(Filters.ConfigFileType.PROJ, Filters.GetConfigFileType("File.pRoj"));
                Assert.Equal(Filters.ConfigFileType.PROJ, Filters.GetConfigFileType("File.CsProj"));
                Assert.Equal(Filters.ConfigFileType.PROPS, Filters.GetConfigFileType("File.proPS"));
                Assert.Equal(Filters.ConfigFileType.TARGETS, Filters.GetConfigFileType("File.tarGETs"));
                Assert.Equal(Filters.ConfigFileType.PKG_CONFIG, Filters.GetConfigFileType("paCkAges.cONfiG"));
            }
        }
    }
}
