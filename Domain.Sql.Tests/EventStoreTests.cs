// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Data;
using System.Data.Entity.Core;
using FluentAssertions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Its.Domain.Tests.Infrastructure;
using Microsoft.Its.Recipes;
using NUnit.Framework;

namespace Microsoft.Its.Domain.Sql.Tests
{
    [TestFixture]
    public class EventStoreTests : EventStoreDbTest
    {
        [Test]
        public async Task When_one_exclusive_connection_is_granted_then_others_will_block()
        {
            var resourceName = Any.Word();
            var readerAttempts = Enumerable.Range(1, 10)
                                           .Select(_ => new EventStoreDbContext().ExclusiveConnection(resourceName,
                                                                                                      wait: TimeSpan.FromSeconds(10)))
                                           .ToArray();

            await Task.WhenAll(readerAttempts)
                      .Timeout(TimeSpan.FromSeconds(5), false);

            readerAttempts.Should().ContainSingle(r => r.Result == true);
        }

        [Test]
        public async Task When_an_exclusive_connection_is_acquired_then_the_EventStoreDbContext_can_be_used_to_query_events()
        {
            var resourceName = Any.Word();
            var dbContext = new EventStoreDbContext();
            var acquired = await dbContext.ExclusiveConnection(resourceName);
            acquired.Should().BeTrue();

            Action query = () => Console.WriteLine(dbContext.Events.Count());

            query.ShouldNotThrow();
        }

        [Test]
        public async Task When_an_exclusive_connection_was_not_acquired_then_the_EventStoreDbContext_cannot_be_used_to_query_events()
        {
            var resourceName = Any.Word();
            var dbContextWithLock = new EventStoreDbContext();
            var acquired = await dbContextWithLock.ExclusiveConnection(resourceName);
            acquired.Should().BeTrue();

            var dbContextWithoutLock = new EventStoreDbContext();
            var notAcquired = await dbContextWithoutLock.ExclusiveConnection(resourceName, TimeSpan.FromMilliseconds(2));
            notAcquired.Should().BeFalse();

            Action query = () => Console.WriteLine(dbContextWithoutLock.Events.Count());

            query.ShouldThrow<InvalidOperationException>();
        }

        [Test]
        public async Task When_an_exclusive_connection_is_still_being_awaited_then_the_EventStoreDbContext_cannot_be_used_to_query_events()
        {
            var resourceName = Any.Word();
            var dbContextWithLock = new EventStoreDbContext();
            var acquired = await dbContextWithLock.ExclusiveConnection(resourceName);
            acquired.Should().BeTrue();

            var dbContextWithoutLock = new EventStoreDbContext();
            dbContextWithoutLock.ExclusiveConnection(resourceName);

            Action query = () => Console.WriteLine(dbContextWithoutLock.Events.Count());

            query.ShouldThrow<EntityException>();
        }

        [Test]
        public async Task A_resource_is_immediately_available_for_locking_again_once_an_exclusive_connection_is_disposed
            ()
        {
            var resourceName = Any.Word();
            var firstDbContext = new EventStoreDbContext();
            var acquired = await firstDbContext.ExclusiveConnection(resourceName);
            acquired.Should().BeTrue();

            firstDbContext.Dispose();

            var secondConnection = new EventStoreDbContext();
            acquired = await secondConnection.ExclusiveConnection(resourceName);
            acquired.Should().BeTrue();
        }
    }
}