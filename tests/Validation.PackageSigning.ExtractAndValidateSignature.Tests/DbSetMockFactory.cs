﻿using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Moq;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    internal static class DbSetMockFactory
    {
        internal static IDbSet<T> Create<T>(params T[] sourceList) where T : class
        {
            var list = new List<T>(sourceList);
            var queryable = list.AsQueryable();

            var dbSet = new Mock<IDbSet<T>>();
            dbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new DbAsyncQueryProviderMock(queryable));
            dbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            dbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            dbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(list.GetEnumerator());
            dbSet.Setup(m => m.Add(It.IsAny<T>())).Callback<T>(e => list.Add(e));

            return dbSet.Object;
        }
    }
}