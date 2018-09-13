// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator.Factory
{
    public class ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>
        : IExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>
        where TAggregatedEntity : AggregatedEntity<TEntityAggregation>, new()
        where TEntityAggregation : ComponentAffectingEntity, new()
    {
        private readonly ITableWrapper _table;
        private readonly IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation> _linkHandler;

        private readonly ILogger<ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>> _logger;

        public ExistingAggregationProvider(
            ITableWrapper table,
            IExistingAggregationLinkHandler<TAggregatedEntity, TEntityAggregation> linkHandler,
            ILogger<ExistingAggregationProvider<TAggregatedEntity, TEntityAggregation>> logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _linkHandler = linkHandler ?? throw new ArgumentNullException(nameof(linkHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TEntityAggregation> GetExistingAggregation(ParsedIncident input)
        {
            TEntityAggregation aggregationEntity = null;

            // Iterate through the ancestors of the component that this parsed incident affects
            var pathParts = ComponentUtility.GetNames(input.AffectedComponentPath);
            for (var i = 1; i <= pathParts.Length; i++)
            {
                var possiblePath =
                    string.Join(
                        Constants.ComponentPathDivider.ToString(),
                        pathParts.Take(i).ToArray());

                // Find an aggregation to link to
                var possibleAggregations = _table
                    .CreateQuery<TEntityAggregation>()
                    .Where(e =>
                        // The aggregation must affect the same path
                        e.AffectedComponentPath == possiblePath &&
                        // The aggregation must begin before or at the same time
                        e.StartTime <= input.StartTime &&
                        // The aggregation must be active or the aggregation must end after this incident begins
                        (e.IsActive || (e.EndTime >= input.StartTime)))
                    .ToList();

                _logger.LogInformation("Found {AggregationCount} possible aggregations to link entity to with path {AffectedComponentPath}.", possibleAggregations.Count(), possiblePath);
                foreach (var possibleAggregation in possibleAggregations)
                {
                    if (await _linkHandler.CanLink(input, possibleAggregation))
                    {
                        _logger.LogInformation("Linking entity to aggregation.");
                        aggregationEntity = possibleAggregation;
                        break;
                    }
                }
                
                if (aggregationEntity != null)
                {
                    _logger.LogInformation("Found aggregation to link entity to with path {AffectedComponentPath}.", possiblePath);
                    break;
                }
            }

            return aggregationEntity;
        }
    }
}
