using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class MessageUpdater : IMessageUpdater
    {
        private static TimeSpan EventStartDelay = TimeSpan.FromMinutes(15);

        private readonly ITableWrapper _table;

        private readonly ILogger<MessageUpdater> _logger;

        public MessageUpdater(ITableWrapper table, ILogger<MessageUpdater> logger)
        {
            _table = table;
            _logger = logger;
        }

        public async Task CreateMessageForEventStart(EventEntity eventEntity, DateTime cursor)
        {
            using (_logger.Scope(
                "Beginning to create message for start of event.",
                "Finished creating message for start of event.",
                "Creating message for start of event."))
            {
                if (cursor > eventEntity.StartTime + EventStartDelay)
                {
                    if (!_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any())
                    {
                        if (TryGetContentsForEventStartForEvent(eventEntity, out var contents))
                        {
                            await CreateMessage(eventEntity, eventEntity.StartTime, contents);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to create a message for start of event!");
                        }
                    }
                    else
                    {
                        // If we've already told customers about an event, we don't need to tell them it's now impacting them.
                        _logger.LogInformation("Event has messages associated with it, cannot create message for its start.");
                    }
                }
                else
                {
                    // We don't want to show events that are too recent to avoid noisy events.
                    _logger.LogInformation("Event is too recent, cannot create message for its start.");
                }
            }
        }

        private bool TryGetContentsForEventStartForEvent(EventEntity eventEntity, out string contents)
        {
            return TryGetContentsForEventHelper(eventEntity, _innerMessageMapForEventStart, "is", "", out contents);
        }

        private const string _youMayEncounterIssues = "You may encounter issues ";
        private static readonly IEnumerable<InnerMessageForComponentPathPrefix> _innerMessageMapForEventStart = new InnerMessageForComponentPathPrefix[]
        {
            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.GalleryName), 
                $"{_youMayEncounterIssues}browsing the NuGet Gallery."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName, ComponentFactory.ChinaRegionName),
                $"{_youMayEncounterIssues}restoring packages from NuGet.org's V3 feed from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName),
                $"{_youMayEncounterIssues}restoring packages from NuGet.org's V3 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V2ProtocolName),
                $"{_youMayEncounterIssues}restoring packages from NuGet.org's V2 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName),
                $"{_youMayEncounterIssues}restoring packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.SearchName, ComponentFactory.ChinaRegionName),
                $"{_youMayEncounterIssues}searching for packages from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.SearchName),
                $"{_youMayEncounterIssues}searching for packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.UploadName),
                "New packages will likely take longer than usual before becoming available for download."),
        };

        public async Task CreateMessageForEventEnd(EventEntity eventEntity)
        {
            if (!eventEntity.EndTime.HasValue)
            {
                throw new ArgumentException("Must pass in an event with an end time!", nameof(eventEntity));
            }

            using (_logger.Scope(
                "Beginning to create message for end of event.",
                "Finished creating message for end of event.",
                "Creating message for end of event."))
            {
                if (_table.GetMessagesLinkedToEvent(eventEntity).ToList().Any())
                {
                    if (TryGetContentsForEventEndForEvent(eventEntity, out var contents))
                    {
                        await CreateMessage(eventEntity, eventEntity.EndTime.Value, contents);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create a message for start of event!");
                    }
                }
                else
                {
                    // If we've never told customers about an event, we don't need to tell them it's no longer impacting them.
                    _logger.LogInformation("Event has no messages associated with it, cannot create message for its end.");
                }
            }
        }

        private Task CreateMessage(EventEntity eventEntity, DateTime time, string contents)
        {
            var messageEntity = new MessageEntity(eventEntity, time, contents);
            _logger.LogInformation("Creating message for start of event with time {MessageTimestamp} and contents {MessageContents}",
                messageEntity.Time, messageEntity.Contents);
            return _table.InsertOrReplaceAsync(messageEntity);
        }

        private bool TryGetContentsForEventEndForEvent(EventEntity eventEntity, out string contents)
        {
            return TryGetContentsForEventHelper(eventEntity, _innerMessageMapForEventEnd, "is no longer", " Thank you for your patience.", out contents);
        }

        private const string _youShouldNoLongerEncounterAnyIssues = "You should no longer encounter any issues ";
        private static readonly IEnumerable<InnerMessageForComponentPathPrefix> _innerMessageMapForEventEnd = new InnerMessageForComponentPathPrefix[]
        {
            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.GalleryName),
                $"{_youShouldNoLongerEncounterAnyIssues}browsing the NuGet Gallery."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName, ComponentFactory.ChinaRegionName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages from NuGet.org's V3 feed from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V3ProtocolName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages from NuGet.org's V3 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName, ComponentFactory.V2ProtocolName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages from NuGet.org's V2 feed."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.RestoreName),
                $"{_youShouldNoLongerEncounterAnyIssues}restoring packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.SearchName, ComponentFactory.ChinaRegionName),
                $"{_youShouldNoLongerEncounterAnyIssues}searching for packages from China."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.SearchName),
                $"{_youShouldNoLongerEncounterAnyIssues}searching for packages."),

            new InnerMessageForComponentPathPrefix(
                ComponentUtility.GetPath(ComponentFactory.RootName, ComponentFactory.UploadName),
                "New packages should become available for download as quickly as usual."),
        };

        private static string[] _componentStatusNames = Enum.GetNames(typeof(ComponentStatus));
        private bool TryGetContentsForEventHelper(
            EventEntity eventEntity, 
            IEnumerable<InnerMessageForComponentPathPrefix> innerMessageMap,
            string boldedPartInnerString, 
            string messageSuffix,
            out string contents)
        {
            contents = null;

            var path = eventEntity.AffectedComponentPath;
            var component = ComponentFactory.CreateNuGetServiceRootComponent().GetByPath(path);
            if (component == null)
            {
                _logger.LogWarning("Could not find a component with path {ComponentPath}.", path);
                return false;
            }

            var componentNames = path.Split(Constants.ComponentPathDivider);
            var name = string.Join(" ", componentNames.Skip(1).Reverse());
            var boldedPart = $"<b>{name} {boldedPartInnerString} {_componentStatusNames[eventEntity.AffectedComponentStatus].ToLowerInvariant()}.</b>";

            string innerContents = innerMessageMap
                .FirstOrDefault(m => m.Matches(path))?
                .InnerMessage;

            if (innerContents == null)
            {
                _logger.LogWarning("Could not find an inner message for path {ComponentPath}.", path);
                return false;
            }

            contents = $"{boldedPart} {innerContents}{messageSuffix}";

            return !string.IsNullOrEmpty(contents);
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
