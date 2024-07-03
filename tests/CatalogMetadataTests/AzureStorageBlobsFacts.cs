// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Moq;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogMetadataTests
{
    public class AzureStorageBlobsFacts
    {

        public class OnSaveAsync : FactBase
        {
            [Fact]
            public async Task WhenCompressedUploadsBlobWithGzipContentEncoding()
            {
                var headers = new BlobHttpHeaders();

                _blockBlobMock.Setup(bb => bb.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, BlobUploadOptions, CancellationToken>((s, o, c) => headers = o.HttpHeaders);

                var storage = new AzureStorageBlobs(_blobContainerMock.Object, true, NullThrottle.Instance);
                await storage.SaveAsync(_blobUri, _content, CancellationToken.None);

                _blobContainerMock.Verify(bc => bc.GetBlockBlobClient(_fileName));
                Assert.Equal(_contentType, headers.ContentType);
                Assert.Equal(_cacheControl, headers.CacheControl);
                Assert.Equal("gzip", headers.ContentEncoding);
            }

            [Fact]
            public async Task WhenUncompressedUploadsBlobWithNoContentEncoding()
            {
                var headers = new BlobHttpHeaders();

                _blockBlobMock.Setup(bb => bb.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                    .Callback<Stream, BlobUploadOptions, CancellationToken>((s, o, c) => headers = o.HttpHeaders);

                await _storage.SaveAsync(_blobUri, _content, CancellationToken.None);

                _blobContainerMock.Verify(bc => bc.GetBlockBlobClient(_fileName));
                Assert.Equal(_contentType, headers.ContentType);
                Assert.Equal(_cacheControl, headers.CacheControl);
                Assert.Null(headers.ContentEncoding);
            }

            [Fact]
            public async Task CreateBlobSnapshotIfNonCreated()
            {
                _blobContainerMock.Setup(bc => bc.HasOnlyOriginalSnapshot(_fileName)).Returns(true);

                await _storage.SaveAsync(_blobUri, _content, CancellationToken.None);

                _blockBlobMock.Verify(bb => bb.CreateSnapshotAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()));
            }

            [Fact]
            public async Task DontCreateBlobSnapshotAlreadyCreated()
            {
                _blobContainerMock.Setup(bc => bc.HasOnlyOriginalSnapshot(_fileName)).Returns(false);

                await _storage.SaveAsync(_blobUri, _content, CancellationToken.None);
                
                _blockBlobMock.Verify(bb => bb.CreateSnapshotAsync(It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        public class FactBase
        {
            protected readonly AzureStorageBlobs _storage;
            protected readonly Mock<IBlobContainerClient> _blobContainerMock;
            protected readonly Mock<BlockBlobClient> _blockBlobMock = new Mock<BlockBlobClient>();

            protected readonly Uri _baseAddress;
            protected readonly string _fileName;
            protected readonly Uri _blobUri;

            protected readonly string _contentType;
            protected readonly string _cacheControl;
            protected readonly StringStorageContent _content;

            public FactBase()
            {
                _baseAddress = new Uri("https://test");
                _fileName = "test.json";
                _blobUri = new Uri(_baseAddress, _fileName);
                _contentType = "application/json";
                _cacheControl = "no-store";
                _content = new StringStorageContent("1234", _contentType, _cacheControl);

                var properties = new BlobProperties();
                var response = Response.FromValue(properties, Mock.Of<Response>());
                _blockBlobMock.Setup(bb => bb.GetPropertiesAsync(It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);
                _blockBlobMock.Setup(bb => bb.Uri).Returns(_blobUri);
                _blockBlobMock.Setup(bb => bb.Name).Returns(_fileName);

                _blobContainerMock = new Mock<IBlobContainerClient>();
                _blobContainerMock.Setup(bc => bc.GetUri()).Returns(_baseAddress);
                _blobContainerMock.Setup(bc => bc.GetBlockBlobClient(_fileName)).Returns(_blockBlobMock.Object);

                _storage = new AzureStorageBlobs(_blobContainerMock.Object, false, NullThrottle.Instance);
            }
        }
    }
}
