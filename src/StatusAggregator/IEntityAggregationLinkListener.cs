﻿using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.Status.Table;

namespace StatusAggregator
{
    public interface IEntityAggregationLinkListener<TAggregatedEntity, TEntityAggregation>
        where TEntityAggregation : ComponentAffectingEntity
        where TAggregatedEntity : ChildComponentAffectingEntity<TEntityAggregation>
    {
        Task OnLink(TEntityAggregation entityAggregation, TAggregatedEntity aggregatedEntity);
    }
}
