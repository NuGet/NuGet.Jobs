// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Jobs.Validation;
using Xunit;

namespace Validation.Common.Job.Tests.TempFiles
{
    public class DeleteOnCloseReadOnlyTempFileFacts
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
        public void DeletesFileOnDispose()
        {
            string filename = null;
            WithTempFile(tempFile => filename = tempFile.FullName);
            Assert.False(File.Exists(filename));
        }

        [Fact]
        public void AllowsReadingAll()
        {
            const string content = "SomeContent";

            string readContent = null;
            WithTempFile(content, tempFile =>
            {
                using (var sr = new StreamReader(tempFile.ReadStream))
                {
                    readContent = sr.ReadToEnd();
                }
            });

            Assert.Equal(content, readContent);
        }

        private void WithTempFile(string content, Action<ITempReadOnlyFile> action)
        {
            WithTempFile(action, content);
        }

        private void WithTempFile(Action<ITempReadOnlyFile> action, string content = null)
        {
            var tempFileName = Path.GetTempFileName();
            if (content != null)
            {
                File.WriteAllText(tempFileName, content);
            }
            using (var tempFile = new DeleteOnCloseReadOnlyTempFile(tempFileName))
            {
                action(tempFile);
            }
        }
    }
}
