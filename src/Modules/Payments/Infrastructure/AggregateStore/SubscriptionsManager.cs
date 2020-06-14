﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using CompanyName.MyMeetings.Modules.Payments.Application.Configuration.Projections;
using CompanyName.MyMeetings.Modules.Payments.Domain.SeedWork;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration;
using Newtonsoft.Json;
using SqlStreamStore;
using SqlStreamStore.Streams;

namespace CompanyName.MyMeetings.Modules.Payments.Infrastructure.AggregateStore
{
    public class SubscriptionsManager
    {
        private readonly IStreamStore _streamStore;

        public SubscriptionsManager(
            IStreamStore streamStore)
        {
            _streamStore = streamStore;
        }

        public void Start()
        {
            _streamStore.SubscribeToAll(null, StreamMessageReceived);
        }

        private async Task StreamMessageReceived(
            IAllStreamSubscription subscription, StreamMessage streamMessage, CancellationToken cancellationToken)
        {
            Type type = DomainEventTypeMappings.Dictionary[streamMessage.Type];
            var jsonData = await streamMessage.GetJsonData(cancellationToken);
            var domainEvent = JsonConvert.DeserializeObject(jsonData, type) as IDomainEvent;

            using (var scope = PaymentsCompositionRoot.BeginLifetimeScope())
            {
                var projectors = scope.Resolve<IList<IProjector>>();

                var tasks = projectors
                    .Select(async projector =>
                    {
                        await projector.Project(domainEvent);
                    });

                await Task.WhenAll(tasks);
            }
        }
    }
}