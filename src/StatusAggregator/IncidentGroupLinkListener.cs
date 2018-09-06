// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class IncidentGroupLinkListener : IEntityAggregationLinkListener<IncidentEntity, IncidentGroupEntity>
    {
        private readonly ITableWrapper _table;

        public IncidentGroupLinkListener(ITableWrapper table)
        {
            _table = table;
        }

        public async Task OnLink(IncidentGroupEntity groupEntity, IncidentEntity incidentEntity)
        {
            if (incidentEntity.AffectedComponentStatus > groupEntity.AffectedComponentStatus)
            {
                groupEntity.AffectedComponentStatus = incidentEntity.AffectedComponentStatus;
                await _table.ReplaceAsync(groupEntity);
            }
        }
    }
}
