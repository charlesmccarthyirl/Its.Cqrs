﻿using System;
using Microsoft.Its.EventStore;

namespace Microsoft.Its.Domain.Testing
{
    public class InMemoryStoredEvent : IStoredEvent
    {
        public InMemoryStoredEvent()
        {
            Timestamp = Clock.Now();
        }

        public string Body { get; set; }
        
        public string ETag { get; set; }

        public string AggregateId { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string Type { get; set; }

        public long SequenceNumber { get; set; }
    }
}