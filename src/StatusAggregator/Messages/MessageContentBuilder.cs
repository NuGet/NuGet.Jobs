// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Factory;

namespace StatusAggregator.Messages
{
    public class MessageContentBuilder : IMessageContentBuilder
    {
        private readonly ILogger<MessageContentBuilder> _logger;

        public MessageContentBuilder(ILogger<MessageContentBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryGetContentsForMessageHelper(
            MessageType type,
            IComponent component,
            out string contents)
        {
            return TryGetContentsForMessageHelper(type, component, component.Status, out contents);
        }

        public bool TryGetContentsForMessageHelper(
            MessageType type,
            IComponent component,
            ComponentStatus status,
            out string contents)
        {
            return TryGetContentsForMessageHelper(type, component.Path, status, out contents);
        }

        private bool TryGetContentsForMessageHelper(
            MessageType type,
            string path,
            ComponentStatus status,
            out string contents)
        {
            using (_logger.Scope("Getting contents for message of type {MessageType} with path {ComponentPath} and status {ComponentStatus}.",
                type, path, status))
            {
                contents = null;

                if (!_messageTypeToMessageTemplate.TryGetValue(type, out string messageTemplate))
                {
                    _logger.LogWarning("Could not find a template for type.", type);
                    return false;
                }

                _logger.LogInformation("Using template {MessageTemplate}.", messageTemplate);

                var componentName = GetPrettyName(path);
                _logger.LogInformation("Using {ComponentName} for name of component.", componentName);

                var actionDescription = GetActionDescriptionFromPath(path);
                if (actionDescription == null)
                {
                    _logger.LogWarning("Could not find an action description for path.", path);
                    return false;
                }

                var componentStatus = status.ToString().ToLowerInvariant();
                contents = string.Format(messageTemplate, componentName, componentStatus, actionDescription);

                _logger.LogInformation("Returned {Contents} for contents of message.", contents);
                return !string.IsNullOrEmpty(contents);
            }
        }

        private string GetPrettyName(string path)
        {
            var componentNames = ComponentUtility.GetNames(path);
            return string.Join(" ", componentNames.Skip(1).Reverse());
        }

        private static readonly IDictionary<MessageType, string> _messageTypeToMessageTemplate = new Dictionary<MessageType, string>
        {
            { MessageType.Start, "**{0} is {1}.** You may encounter issues {2}." },
            { MessageType.End, "**{0} is no longer {1}.** You should no longer encounter any issues {2}. Thank you for your patience." },
        };

        private string GetActionDescriptionFromPath(string path)
        {
            return _actionDescriptionForComponentPathMap
                .FirstOrDefault(m => m.Matches(path))?
                .ActionDescription; ;
        }

        /// <remarks>
        /// This was not implemented as a dictionary because it is not possible to construct a <see cref="IEqualityComparer{T}.GetHashCode(T)"/> that works with component path prefixes.
        /// 
        /// Proof:
        /// A/B and A/C must have the same hashcode as A because A/B and A/C are both prefixed by A.
        /// However, A/B must not have the same hashcode as A/C because A/B is not a prefix of A/C and A/C is not a prefix of A/B.
        /// Therefore, A/B and A/C must have a hashcode that is both identical AND different.
        /// This is not possible.
        /// </remarks>
        private static readonly IEnumerable<ActionDescriptionForComponentPathPrefix> _actionDescriptionForComponentPathMap = new ActionDescriptionForComponentPathPrefix[]
        {
            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.GalleryName),
                $"browsing the NuGet Gallery"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName, NuGetServiceComponentFactory.ChinaRegionName),
                $"restoring packages from NuGet.org's V3 feed from China"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V3ProtocolName),
                $"restoring packages from NuGet.org's V3 feed"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName, NuGetServiceComponentFactory.V2ProtocolName),
                $"restoring packages from NuGet.org's V2 feed"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.RestoreName),
                $"restoring packages"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName, NuGetServiceComponentFactory.ChinaRegionName),
                $"searching for packages from China"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.SearchName),
                $"searching for packages"),

            new ActionDescriptionForComponentPathPrefix(
                ComponentUtility.GetPath(NuGetServiceComponentFactory.RootName, NuGetServiceComponentFactory.UploadName),
                "uploading new packages"),
        };

        private class ActionDescriptionForComponentPathPrefix
        {
            public string ComponentPathPrefix { get; }
            public string ActionDescription { get; }

            public ActionDescriptionForComponentPathPrefix(string componentPathPrefix, string actionDescription)
            {
                ComponentPathPrefix = componentPathPrefix;
                ActionDescription = actionDescription;
            }

            public bool Matches(string componentPath)
            {
                return componentPath.StartsWith(ComponentPathPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}