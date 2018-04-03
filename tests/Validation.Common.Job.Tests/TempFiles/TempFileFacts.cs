// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests
{
    public class TempFileFacts
    {
        [Fact]
        public void ProvidesFullFilePath()
        {
            WithTempFile(tempFile =>
            {
                Assert.True(PathUtility.IsFilePathAbsolute(tempFile.FullName));
            });
        }

        [Fact]
        public void CreatesEmptyFile()
        {
            WithTempFile(tempFile =>
            {
                Assert.True(File.Exists(tempFile.FullName));
                var fi = new FileInfo(tempFile.FullName);
                Assert.Equal(0, fi.Length);
            });
        }

        [Fact]
        public void DeletesFileOnDispose()
        {
            string filename = null;
            WithTempFile(tempFile => filename = tempFile.FullName);
            Assert.False(File.Exists(filename));
        }

        [Fact]
        public void CreatedFileIsWritable()
        {
            var ex = Record.Exception(() => WithTempFile(tempFile => File.WriteAllText(tempFile.FullName, "some content")));
            Assert.Null(ex);
        }

        [Fact]
        public void DoesNotThrowIfFileDoesNotExistOnDispose()
        {
            var ex = Record.Exception(() => WithTempFile(tempFile => File.Delete(tempFile.FullName)));
            Assert.Null(ex);
        }

        private void WithTempFile(Action<ITempFile> action)
        {
            using (var tempFile = new TempFile())
            {
                action(tempFile);
            }
        }
    }
}
