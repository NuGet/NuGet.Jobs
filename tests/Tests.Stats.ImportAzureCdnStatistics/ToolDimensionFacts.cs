// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Stats.ImportAzureCdnStatistics;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class ToolDimensionFacts
    {
        [Theory]
        [InlineData("win-x86-commandline", "v3.5.0", "NuGet.exe", "win-x86-commandline", "V3.5.0", "nuget.exe", 0)] // Lowercase and uppercase are equal
        public void ComparesToolsDimensionsCorrectly(string toolId1, string toolVersion1, string fileName1,
                                                    string toolId2, string toolVersion2, string fileName2, int expectedCount)
        {
            var t1 = new ToolDimension(toolId1, toolVersion1, fileName1);
            var t2 = new ToolDimension(toolId2, toolVersion2, fileName2);

            // Arrange
            var toolList1 = new List<ToolDimension>() { new ToolDimension(toolId1, toolVersion1, fileName1) };
            var toolList2 = new List<ToolDimension>() { new ToolDimension(toolId2, toolVersion2, fileName2) };

            //Act
            var diffCount = toolList1.Except(toolList2, new ToolDimensionOrdinalIgnoreCaseComparer()).Count();

            // Assert
            Assert.Equal(expectedCount, diffCount);
        }
    }
}
