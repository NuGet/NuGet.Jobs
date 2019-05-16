using NuGet.Jobs.Monitoring.PackageLag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Monitoring.PackageLag.Tests
{
    public static class TestHelpers
    {
        public static SearchResultResponse GetTestSearchResponse(DateTimeOffset indexStamp, DateTimeOffset createdStamp, DateTimeOffset editedStamp, bool listed = true)
        {
            return new SearchResultResponse
            {
                Index = "test",
                IndexTimeStamp = indexStamp,
                TotalHits = 1,
                Data = new SearchResult[1]
                {
                    new SearchResult
                    {
                        Created = createdStamp,
                        LastEdited = editedStamp,
                        Listed = listed
                    }
                }
            };
        }

        public static SearchResultResponse GetEmptyTestSearchResponse(DateTimeOffset indexStamp)
        {
            return new SearchResultResponse
            {
                Index = "test",
                IndexTimeStamp = indexStamp,
                TotalHits = 0,
                Data = new SearchResult[0]
            };
        }
    }
}
