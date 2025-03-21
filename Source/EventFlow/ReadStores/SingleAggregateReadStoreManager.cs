// The MIT License (MIT)
// 
// Copyright (c) 2015-2024 Rasmus Mikkelsen
// https://github.com/eventflow/EventFlow
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.EventStores;
using EventFlow.Extensions;
using Microsoft.Extensions.Logging;

namespace EventFlow.ReadStores
{
    public class SingleAggregateReadStoreManager<TAggregate, TIdentity, TReadModelStore, TReadModel> 
        : ReadStoreManager<TReadModelStore, TReadModel>
        where TAggregate : IAggregateRoot<TIdentity>
        where TIdentity : IIdentity
        where TReadModelStore : IReadModelStore<TReadModel>
        where TReadModel : class, IReadModel
    {
        private readonly IEventStore _eventStore;

        public SingleAggregateReadStoreManager(
            ILogger<SingleAggregateReadStoreManager<TAggregate, TIdentity, TReadModelStore, TReadModel>> logger,
            IServiceProvider serviceProvider,
            TReadModelStore readModelStore,
            IReadModelDomainEventApplier readModelDomainEventApplier,
            IReadModelFactory<TReadModel> readModelFactory,
            IEventStore eventStore)
            : base(logger, serviceProvider, readModelStore, readModelDomainEventApplier, readModelFactory)
        {
            _eventStore = eventStore;
        }

        protected override IReadOnlyCollection<ReadModelUpdate> BuildReadModelUpdates(
            IReadOnlyCollection<IDomainEvent> domainEvents)
        {
            return domainEvents
                .GroupBy(d => d.GetIdentity().Value)
                .Select(g => new ReadModelUpdate(g.Key, g.OrderBy(d => d.AggregateSequenceNumber).ToList()))
                .ToList();
        }

        private async Task<TReadModel> GetOrCreateReadModel(ReadModelEnvelope<TReadModel> readModelEnvelope,
            CancellationToken cancellationToken)
        {
            return readModelEnvelope.ReadModel
                   ?? await ReadModelFactory
                       .CreateAsync(readModelEnvelope.ReadModelId, cancellationToken)
                       .ConfigureAwait(false);
        }

        private async Task<ReadModelUpdateResult<TReadModel>> ApplyUpdatesAsync(
            IReadModelContext readModelContext,
            IReadOnlyCollection<IDomainEvent> domainEvents,
            ReadModelEnvelope<TReadModel> readModelEnvelope,
            CancellationToken cancellationToken)
        {
            TReadModel readModel = await GetOrCreateReadModel(readModelEnvelope, cancellationToken);

            await ReadModelDomainEventApplier
                .UpdateReadModelAsync(readModel, domainEvents, readModelContext, cancellationToken)
                .ConfigureAwait(false);

            var readModelVersion = Math.Max(
                domainEvents.Max(e => e.AggregateSequenceNumber),
                readModelEnvelope.Version.GetValueOrDefault());

            return readModelEnvelope.AsModifedResult(readModel, readModelVersion);
        }
        
        protected override async Task<ReadModelUpdateResult<TReadModel>> UpdateAsync(
            IReadModelContext readModelContext,
            IReadOnlyCollection<IDomainEvent> domainEvents,
            ReadModelEnvelope<TReadModel> readModelEnvelope,
            CancellationToken cancellationToken)
        {
            if (!domainEvents.Any()) throw new ArgumentException("No domain events");

            var expectedVersion = domainEvents.Min(d => d.AggregateSequenceNumber) - 1;
            var envelopeVersion = readModelEnvelope.Version;

            IReadOnlyCollection<IDomainEvent> eventsToApply;

            if (envelopeVersion.HasValue && expectedVersion != envelopeVersion)
            {
                var version = envelopeVersion.Value;
                if (expectedVersion < version)
                {
                    if (Logger.IsEnabled(LogLevel.Trace))
                    {
                        Logger.LogTrace(
                            "Read model {ReadModelType} with ID {Id} already has version {Version} compared to {ExpectedVersion}, skipping",
                            typeof(TReadModel),
                            readModelEnvelope.ReadModelId,
                            version,
                            expectedVersion);
                    }

                    return readModelEnvelope.AsUnmodifedResult();
                }

                // Apply missing events
                var identity = domainEvents.Cast<IDomainEvent<TAggregate, TIdentity>>().First().AggregateIdentity;
                eventsToApply = await _eventStore.LoadEventsAsync<TAggregate, TIdentity>(
                        identity,
                        (int) version + 1,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace(
                        "Read model {ReadModelType} with ID {Id} is missing some events {Version} < {ExpectedVersion}, adding them (got {MissingEventCount} events)",
                        typeof(TReadModel).PrettyPrint(),
                        readModelEnvelope.ReadModelId,
                        version,
                        expectedVersion,
                        eventsToApply.Count);
                }
            }
            else
            {
                eventsToApply = domainEvents;
                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace( 
                        "Read model {ReadModelType} with ID {Id} has version {ExpectedVersion} (or none), applying events",
                        typeof(TReadModel).PrettyPrint(),
                        readModelEnvelope.ReadModelId,
                        expectedVersion);
                }
            }

            return await ApplyUpdatesAsync(
                    readModelContext,
                    eventsToApply,
                    readModelEnvelope,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
