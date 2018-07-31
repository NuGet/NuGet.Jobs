﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Validation;
using Validation.PackageSigning.Core.Tests.Support;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Validation.Symbols.Core.Tests
{
    public class SymbolsValidationEntitiesServiceFacts
    {
        private const int PackageKey = 1001;
        private const string PackageId = "NuGet.Versioning";
        private const string PackageVersion = "1.2.3";

        public class TheTryAddRequestAsyncMethod : FactsBase
        {
            public TheTryAddRequestAsyncMethod(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public void NullChecks()
            {
                // Arrange + Act + Assert
                Assert.ThrowsAsync< ArgumentNullException>(() => _target.AddSymbolsServerRequestAsync(null));
            }

            [Fact]
            public async Task TryAddReturnsAnExistentRequestIfAlreadyExistsInDatabase()
            {
                // Arrange 
                SymbolsServerRequest request1 = new SymbolsServerRequest()
                {
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    RequestName = PackageKey.ToString(),
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingested,
                    SymbolsKey = PackageKey
                };

                SymbolsServerRequest request2 = new SymbolsServerRequest()
                {
                    Created = new DateTime(2018, 4, 5),
                    LastUpdated = DateTime.UtcNow,
                    RequestName = PackageKey.ToString(),
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingested,
                    SymbolsKey = PackageKey
                };
                _validationEntitiesContext
                    .Setup(x => x.SymbolsServerRequests)
                    .Returns(DbSetMockFactory.Create(request1));

                // Act 
                var result = await _target.AddSymbolsServerRequestAsync(request2);

                // Assert 
                Assert.Equal(request1.Created, result.Created);
            }

            [Fact]
            public async Task TryAddReturnsTheSavedRequest()
            {
                // Arrange 
                SymbolsServerRequest request1 = new SymbolsServerRequest()
                {
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    RequestName = PackageKey.ToString(),
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingested,
                    SymbolsKey = PackageKey
                };

                SymbolsServerRequest request2 = new SymbolsServerRequest()
                {
                    Created = new DateTime(2018, 4, 5),
                    LastUpdated = DateTime.UtcNow,
                    RequestName = "Some name",
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingested,
                    SymbolsKey = PackageKey
                };

                _validationEntitiesContext
                    .Setup(x => x.SymbolsServerRequests)
                    .Returns(DbSetMockFactory.Create(request1));

                _validationEntitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .ReturnsAsync(7);

                // Act 
                var result = await _target.AddSymbolsServerRequestAsync(request2);

                // Assert
                Assert.Equal(request2.Created, result.Created);
                Assert.Equal(request2.RequestName, result.RequestName);
            }
        }

        public class TheTryUpdateSymbolsServerRequestAsyncMethod : FactsBase
        {
            public TheTryUpdateSymbolsServerRequestAsyncMethod(ITestOutputHelper output) : base(output)
            {

            }

            [Fact]
            public void NullChecks()
            {
                // Arrange + Act + Assert
                Assert.ThrowsAsync<ArgumentNullException>(() => _target.TryUpdateSymbolsServerRequestAsync(null, SymbolsPackageIngestRequestStatus.Ingested));
            }

            [Fact]
            public async Task TryUpdateSymbolsServerRequestAsyncReturnsFalseIfRequestNotInDatabase()
            {
                // Arrange
                SymbolsServerRequest request1 = new SymbolsServerRequest()
                {
                    Created = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    RequestName = PackageKey.ToString(),
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingesting,
                    SymbolsKey = PackageKey
                };

                SymbolsServerRequest request2 = new SymbolsServerRequest()
                {
                    Created = new DateTime(2018, 4, 5),
                    LastUpdated = DateTime.UtcNow,
                    RequestName = "Some Name",
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingesting,
                    SymbolsKey = PackageKey
                };
                _validationEntitiesContext
                    .Setup(x => x.SymbolsServerRequests)
                    .Returns(DbSetMockFactory.Create(request1));

                // Act 
                var result = await _target.TryUpdateSymbolsServerRequestAsync(request2, SymbolsPackageIngestRequestStatus.Ingested);

                // Assert
                Assert.False(result);
            }

            [Fact]
            public async Task TryUpdateSymbolsServerRequestAsyncUpdateRequestIfInDatabase()
            {
                // Arrange
                SymbolsServerRequest request = new SymbolsServerRequest()
                {
                    Created = new DateTime(2000, 4, 5),
                    LastUpdated = new DateTime(2000, 4, 5),
                    RequestName = PackageKey.ToString(),
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingesting,
                    SymbolsKey = PackageKey
                };
                _validationEntitiesContext
                    .Setup(x => x.SymbolsServerRequests)
                    .Returns(DbSetMockFactory.Create(request));

                _validationEntitiesContext
                   .Setup(x => x.SaveChangesAsync())
                   .ReturnsAsync(7);

                // Act 
                var result = await _target.TryUpdateSymbolsServerRequestAsync(request, SymbolsPackageIngestRequestStatus.Ingested);

                // Assert
                Assert.True(result);
                Assert.Equal(DateTime.UtcNow.Year, request.LastUpdated.Year);
            }

        }

        public class TheCreateFromValidationRequestMethod 
        {
            [Fact]
            public void CreateFromValidationRequestValidRequest()
            {
                // Arrange
                ValidationRequest request = new ValidationRequest(Guid.NewGuid(), PackageKey, PackageId, PackageVersion, "");

                // Act
                var result = SymbolsValidationEntitiesService.CreateFromValidationRequest(request, SymbolsPackageIngestRequestStatus.FailedIngestion);

                // Assert
                Assert.Equal(PackageKey, result.SymbolsKey);
                Assert.Equal(PackageKey.ToString(), result.RequestName);
                Assert.Equal(SymbolsPackageIngestRequestStatus.FailedIngestion, result.RequestStatusKey);
            }
        }

        public class TheConvertToIValidationResultMethod
        {
            [Fact]
            public void ConvertToIValidationResultFailedIngestion()
            {
                // Arrange
                SymbolsServerRequest request = new SymbolsServerRequest()
                {
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.FailedIngestion
                };


                // Act
                var result = SymbolsValidationEntitiesService.ConvertToIValidationResult(request);

                // Assert
                Assert.Equal(ValidationStatus.Failed, result.Status);
            }

            [Fact]
            public void ConvertToIValidationResultIngested()
            {
                // Arrange
                SymbolsServerRequest request = new SymbolsServerRequest()
                {
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingested
                };


                // Act
                var result = SymbolsValidationEntitiesService.ConvertToIValidationResult(request);

                // Assert
                Assert.Equal(ValidationStatus.Succeeded, result.Status);
            }

            [Fact]
            public void ConvertToIValidationResultIngesting()
            {
                // Arrange
                SymbolsServerRequest request = new SymbolsServerRequest()
                {
                    RequestStatusKey = SymbolsPackageIngestRequestStatus.Ingesting
                };


                // Act
                var result = SymbolsValidationEntitiesService.ConvertToIValidationResult(request);

                // Assert
                Assert.Equal(ValidationStatus.Incomplete, result.Status);
            }

            [Fact]
            public void ConvertToIValidationResultNull()
            {
                // Act
                var result = SymbolsValidationEntitiesService.ConvertToIValidationResult(null);

                // Assert
                Assert.Equal(ValidationStatus.NotStarted, result.Status);
            }
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IValidationEntitiesContext> _validationEntitiesContext;

            protected readonly SymbolsValidationEntitiesService _target;

            public FactsBase(ITestOutputHelper output)
            {
                _validationEntitiesContext = new Mock<IValidationEntitiesContext>();
                _target = new SymbolsValidationEntitiesService(_validationEntitiesContext.Object);
            }
        }

        public class ValidationRequest : IValidationRequest
        {
            public Guid ValidationId { get; }

            public int PackageKey { get; }

            public string PackageId { get; }

            public string PackageVersion { get; }

            public string NupkgUrl { get; }

            public ValidationRequest(Guid validationId, int packageKey, string packageId, string packageVersion, string nupkgUrl)
            {
                ValidationId = validationId;
                PackageKey = packageKey;
                PackageId = packageId;
                PackageVersion = packageVersion;
                NupkgUrl = nupkgUrl;
            }
        }
    }
}
