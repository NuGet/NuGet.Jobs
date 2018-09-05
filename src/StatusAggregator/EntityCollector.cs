using System;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public class EntityCollector : IEntityCollector
    {
        private readonly ICursor _cursor;
        private readonly IEntityCollectorProcessor _processor;

        public EntityCollector(
            ICursor cursor,
            IEntityCollectorProcessor processor)
        {
            _cursor = cursor;
            _processor = processor;
        }

        public string Name => _processor.Name;

        public async Task<DateTime> FetchLatest()
        {
            var lastCursor = await _cursor.Get(Name);
            var nextCursor = await _processor.FetchSince(lastCursor);
            if (nextCursor.HasValue)
            {
                await _cursor.Set(Name, nextCursor.Value);
            }

            return nextCursor ?? lastCursor;
        }
    }
}
