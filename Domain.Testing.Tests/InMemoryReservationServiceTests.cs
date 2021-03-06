// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Its.Domain.Sql;
using Microsoft.Its.Domain.Tests;
using NUnit.Framework;

namespace Microsoft.Its.Domain.Testing.Tests
{
    [TestFixture]
    public class InMemoryReservationServiceTests : ReservationServiceTests
    {
        protected override void Configure(Configuration configuration)
        {
            configuration.UseInMemoryReservationService()
                         .UseInMemoryEventStore()
                         .UseEventBus(new FakeEventBus());
        }

        protected override async Task<ReservedValue> GetReservedValue(string value, string promoCode)
        {
            var reservationService = (InMemoryReservationService) Configuration.Current.ReservationService();
            return await reservationService.GetReservedValue(value, promoCode);
        }
    }
}