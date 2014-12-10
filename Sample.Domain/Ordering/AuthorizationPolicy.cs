﻿using Microsoft.Its.Domain.Authorization;
using Sample.Domain.Ordering.Commands;

namespace Sample.Domain.Ordering
{
    public static class AuthorizationPolicy
    {
        static AuthorizationPolicy()
        {
            AuthorizationFor<Customer>.ToApply<AddItem>.ToA<Order>
                                      .Requires((customer, addItem, order) =>
                                                customer.IsAuthenticated && customer.Id == order.CustomerId);
        }
    }
}