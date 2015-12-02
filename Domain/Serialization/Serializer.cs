// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Its.Recipes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Its.Domain.Serialization
{
    public static class Serializer
    {
        private static readonly Lazy<Func<JsonSerializerSettings, JsonSerializerSettings>> cloneSettings =
            new Lazy<Func<JsonSerializerSettings, JsonSerializerSettings>>(
                () => MappingExpression.From<JsonSerializerSettings>
                                       .ToNew<JsonSerializerSettings>()
                                       .Compile());

        private static readonly Lazy<JsonSerializerSettings> diagnosticSettings = new Lazy<JsonSerializerSettings>(() =>
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                ContractResolver = new OptionalContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Error = (sender, args) => args.ErrorContext.Handled = true
            };

            AddConverter(new OptionalConverter());
            AddConverter(new UriConverter());

            return jsonSerializerSettings;
        }); 

        private static readonly ConcurrentDictionary<string, Func<StoredEvent, IEvent>> deserializers = new ConcurrentDictionary<string, Func<StoredEvent, IEvent>>();
        
        private static JsonSerializerSettings settings;

        public static JsonSerializerSettings Settings
        {
            get
            {
                return settings;
            }
            set
            {
                if (value == null)
                {
                    ConfigureDefault();
                    return;
                }

                settings = value;
            }
        }

        static Serializer()
        {
            ConfigureDefault();
        }

        public static void ConfigureDefault()
        {
            Settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new OptionalContractResolver(),
                DefaultValueHandling = DefaultValueHandling.Include,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                DateParseHandling = DateParseHandling.DateTimeOffset
            };

            AddConverter(new OptionalConverter());
            AddConverter(new UriConverter());
        }

        public static void AddConverter(JsonConverter converter)
        {
            Settings.Converters.Add(converter);
        }

        public static void AddPrimitiveConverter<T>(
            Func<T, object> serialize,
            Func<object, T> deserialize)
        {
            if (!Settings.Converters.Any(c => c is PrimitiveConverter<T>))
            {
                Settings.Converters.Add(new PrimitiveConverter<T>(serialize, deserialize));
            }
        }

        public static JsonSerializerSettings CloneSettings()
        {
            return cloneSettings.Value(Settings);
        }

        public static string ToJson<T>(this T obj, Formatting formatting = Formatting.None)
        {
            return JsonConvert.SerializeObject(obj, formatting, Settings);
        }

        internal static string ToDiagnosticJson<T>(this T obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, diagnosticSettings.Value);
        }

        public static T FromJsonTo<T>(this string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        /// <summary>
        /// Deserializes a domain event.
        /// </summary>
        /// <param name="aggregateName">Name of the aggregate.</param>
        /// <param name="eventName">Name of the event type.</param>
        /// <param name="aggregateId">The aggregate identifier.</param>
        /// <param name="sequenceNumber">The sequence number of the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        /// <param name="body">The body of the event.</param>
        /// <param name="uniqueEventId">The unique event identifier.</param>
        /// <param name="serializerSettings">The serializer settings used when deserializing the event body.</param>
        /// <param name="etag">The ETag of the event.</param>
        /// <returns></returns>
        public static IEvent DeserializeEvent(string aggregateName,
                                              string eventName,
                                              Guid aggregateId,
                                              long sequenceNumber,
                                              DateTimeOffset timestamp,
                                              string body,
                                              dynamic uniqueEventId = null,
                                              JsonSerializerSettings serializerSettings = null, 
                                              string etag = null)
        {
            var deserializerKey = aggregateName + ":" + eventName;

            var deserializer = deserializers.GetOrAdd(deserializerKey, _ =>
            {
                var aggregateType = AggregateType.KnownTypes.SingleOrDefault(t => t.Name == aggregateName);

                if (aggregateType != null)
                {
                    serializerSettings = serializerSettings ?? Settings;

                    IEnumerable<Type> eventTypes = typeof (Event<>).MakeGenericType(aggregateType).Member().KnownTypes;

                    // some events contain specialized names, e.g. CommandScheduled:DoSomething. the latter portion is not interesting from a serialization standpoint, so we strip it off.
                    var eventNameComponents = eventName.Split(':');
                    eventName = eventNameComponents.First();
                    var eventType = eventTypes.SingleOrDefault(t =>
                                                               t.GetCustomAttributes(false)
                                                                .OfType<EventNameAttribute>()
                                                                .FirstOrDefault()
                                                                .IfNotNull()
                                                                .Then(
                                                                    att => att.EventName == eventName)
                                                                .Else(() =>
                                                                      // strip off generic specifications from the type name 
                                                                      t.Name.Split('`').First() == eventName));

                    if (eventType != null)
                    {
                        if (typeof(IScheduledCommand).IsAssignableFrom(eventType))
                        {
                            var commandType = Command.FindType(aggregateType, eventNameComponents.Last());
                            if (commandType == null)
                            {
                                return UseAnonymousEvent(serializerSettings, aggregateType);
                            }
                        }

                        return e =>
                        {
                            var deserializedEvent = (IEvent) JsonConvert.DeserializeObject(e.Body, eventType, serializerSettings);

                            var dEvent = deserializedEvent as Event;
                            if (dEvent != null)
                            {
                                dEvent.AggregateId = e.AggregateId;
                                dEvent.SequenceNumber = e.SequenceNumber;
                                dEvent.Timestamp = e.Timestamp;
                                dEvent.ETag = e.ETag;
                            }

                            return deserializedEvent;
                        };
                    }

                    // even if the domain no longer cares about some old event type, anonymous events are returned as placeholders in the EventSequence
                    return UseAnonymousEvent(serializerSettings, aggregateType);
                }

                return e =>
                {
                    dynamic deserializeObject = JsonConvert.DeserializeObject(e.Body, serializerSettings);
                    var dynamicEvent = new DynamicEvent(deserializeObject)
                    {
                        EventStreamName = e.StreamName,
                        EventTypeName = e.EventTypeName,
                        AggregateId = e.AggregateId,
                        SequenceNumber = e.SequenceNumber,
                        Timestamp = e.Timestamp,
                        ETag = e.ETag
                    };

                    return dynamicEvent;
                };
            });

            var @event =
                deserializer(
                    new StoredEvent
                        {
                            StreamName = aggregateName,
                            EventTypeName = eventName,
                            Body = body,
                            AggregateId = aggregateId,
                            SequenceNumber = sequenceNumber,
                            Timestamp = timestamp,
                            ETag = etag
                        });

            if (uniqueEventId != null)
            {
                @event.IfTypeIs<IHaveExtensibleMetada>()
                      .Then(e => e.Metadata.AbsoluteSequenceNumber = uniqueEventId);
            }

            return @event;
        }

        private static Func<StoredEvent, IEvent> UseAnonymousEvent(JsonSerializerSettings serializerSettings, Type aggregateType)
        {
            return e =>
                {
                    var anonymousEvent = (Event) JsonConvert.DeserializeObject(e.Body, typeof (AnonymousEvent<>).MakeGenericType(aggregateType), serializerSettings);
                    anonymousEvent.AggregateId = e.AggregateId;
                    anonymousEvent.SequenceNumber = e.SequenceNumber;
                    anonymousEvent.Timestamp = e.Timestamp;
                    anonymousEvent.ETag = e.ETag;
                    ((dynamic) anonymousEvent).Body = e.Body;

                    return anonymousEvent;
                };
        }

        public static IEnumerable<IEvent> FromJsonToEvents(string json)
        {
            JArray jsonEvents = JArray.Parse(json);

            return jsonEvents.OfType<JObject>()
                             .Select(o =>
                                     new
                                     {
                                         o.Properties().Single().Name,
                                         o.Properties().Single().Value
                                     })
                             .Select(nameAndBody =>
                             {
                                 var split = nameAndBody.Name.Split('.');
                                 var aggregateTypeName = split[0];
                                 var eventTypeName = split[1];
                                 var aggregateType = AggregateType.KnownTypes
                                                                  .SingleOrDefault(t => t.Name == aggregateTypeName);

                                 return new
                                 {
                                     EventType = Event.KnownTypesForAggregateType(aggregateType)
                                                      .SingleOrDefault(t => Equals(t.Name, eventTypeName)),
                                     Body = nameAndBody.Value.ToString()
                                 };
                             })
                             .Where(typeAndBody => typeAndBody.EventType != null)
                             .Select(typeAndBody =>
                                     JsonConvert.DeserializeObject(typeAndBody.Body, typeAndBody.EventType, Settings))
                             .Cast<IEvent>();
        }

        private struct StoredEvent
        {
            public string StreamName;
            public string EventTypeName;
            public string Body;
            public Guid AggregateId;
            public long SequenceNumber;
            public DateTimeOffset Timestamp;
            public string ETag { get; set; }
        }
    }
}
