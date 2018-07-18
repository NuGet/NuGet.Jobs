using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Status;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class MessageUpdater : IMessageUpdater
    {
        private static TimeSpan EventStartDelay = TimeSpan.FromMinutes(15);

        private ITableWrapper _table;

        public MessageUpdater(ITableWrapper table)
        {
            _table = table;
        }

        public async Task CreateMessageForEventStart(EventEntity eventEntity, DateTime nextCreationTime)
        {
            // Enough time must have passed before we create a start message for an event.
            // Only create a message if the event doesn't have messages associated with it.
            if (nextCreationTime > eventEntity.StartTime + EventStartDelay &&
                !_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any() &&
                TryGetMessageForEventStartForEvent(eventEntity, out var message))
            {
                var messageEntity = new MessageEntity(eventEntity, eventEntity.StartTime, message);
                await _table.InsertOrReplaceAsync(messageEntity);
            }
        }

        private static bool TryGetMessageForEventStartForEvent(EventEntity eventEntity, out string message)
        {
            return TryGetMessageForEventHelper(eventEntity, _innerMessageMapForEventStart, "is", "", out message);
        }

        private const string _youMayEncounterIssues = "You may encounter issues ";
        private static readonly IEnumerable<InnerMessageForComponentPathPrefix> _innerMessageMapForEventStart = new InnerMessageForComponentPathPrefix[]
        {
            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.GalleryName), 
                $"{_youMayEncounterIssues}browsing the NuGet Gallery."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName, Components.V3ProtocolName, Components.ChinaRegionName),
                $"{_youMayEncounterIssues}restoring packages from NuGet.org's V3 feed from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName, Components.V3ProtocolName),
                $"{_youMayEncounterIssues}restoring packages from NuGet.org's V3 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName, Components.V2ProtocolName),
                $"{_youMayEncounterIssues}restoring packages from NuGet.org's V2 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName),
                $"{_youMayEncounterIssues}restoring packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.SearchName, Components.ChinaRegionName),
                $"{_youMayEncounterIssues}searching for packages from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.SearchName),
                $"{_youMayEncounterIssues}searching for packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.UploadName),
                "New packages will likely take longer than usual before becoming available for download."),
        };

        public async Task CreateMessageForEventEnd(EventEntity eventEntity)
        {
            if (!eventEntity.EndTime.HasValue)
            {
                throw new ArgumentException("Must pass in an event with an end time!", nameof(eventEntity));
            }

            // Only create a message if the event already has messages associated with it.
            if (_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any())
            {
                if (TryGetMessageForEventEndForEvent(eventEntity, out var message))
                {
                    var messageEntity = new MessageEntity(eventEntity, eventEntity.EndTime.Value, message);
                    await _table.InsertOrReplaceAsync(messageEntity);
                }
            }
        }

        private static bool TryGetMessageForEventEndForEvent(EventEntity eventEntity, out string message)
        {
            return TryGetMessageForEventHelper(eventEntity, _innerMessageMapForEventEnd, "is no longer", " Thank you for your patience.", out message);
        }

        private const string _youShouldNoLongerEncounterAnyIssues = "You should no longer encounter any issues ";
        private static readonly IEnumerable<InnerMessageForComponentPathPrefix> _innerMessageMapForEventEnd = new InnerMessageForComponentPathPrefix[]
        {
            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.GalleryName),
                $"{_youShouldNoLongerEncounterAnyIssues}browsing the NuGet Gallery."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName, Components.V3ProtocolName, Components.ChinaRegionName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages from NuGet.org's V3 feed from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName, Components.V3ProtocolName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages from NuGet.org's V3 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName, Components.V2ProtocolName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages from NuGet.org's V2 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.RestoreName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.SearchName, Components.ChinaRegionName),
                $"{_youShouldNoLongerEncounterAnyIssues}searching for packages from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.SearchName),
                $"{_youShouldNoLongerEncounterAnyIssues}searching for packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(Components.RootName, Components.UploadName),
                "New packages should become available for download as quickly as usual."),
        };

        private static string[] _componentStatusNames = Enum.GetNames(typeof(ComponentStatus));
        private static bool TryGetMessageForEventHelper(
            EventEntity eventEntity, 
            IEnumerable<InnerMessageForComponentPathPrefix> innerMessageMap,
            string boldedPartInnerString, 
            string messageSuffix,
            out string message)
        {
            message = null;

            var path = eventEntity.AffectedComponentPath;
            var component = Components.CreateNuGetServiceRootComponent().GetByPath(path);
            if (component == null)
            {
                return false;
            }

            var componentNames = path.Split(Constants.ComponentPathDivider);
            var name = string.Join(" ", componentNames.Skip(1).Reverse());
            var boldedPart = $"<b>{name} {boldedPartInnerString} {_componentStatusNames[eventEntity.AffectedComponentStatus].ToLowerInvariant()}.</b>";

            string innerMessage = innerMessageMap
                .FirstOrDefault(m => m.Matches(path))?
                .InnerMessage;

            if (innerMessage == null)
            {
                return false;
            }

            message = $"{boldedPart} {innerMessage}{messageSuffix}";

            return !string.IsNullOrEmpty(message);
        }

        private class InnerMessageForComponentPathPrefix
        {
            public string ComponentPathPrefix { get; }
            public string InnerMessage { get; }

            public InnerMessageForComponentPathPrefix(string componentPathPrefix, string innerWarning)
            {
                ComponentPathPrefix = componentPathPrefix;
                InnerMessage = innerWarning;
            }

            public bool Matches(string componentPath)
            {
                return componentPath.StartsWith(ComponentPathPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
