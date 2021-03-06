// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Microsoft.Its.Domain.Serialization
{
    [DebuggerStepThrough]
    public class UriConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof (Uri);

        public override bool CanWrite => true;

        public override bool CanRead => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var uri = value as Uri;
            if (uri == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteValue(uri.OriginalString);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    return CreateUri((string) reader.Value);
                case JsonToken.Null:
                    return null;
                default:
                    var msg = $"Unable to deserialize Uri from token type {reader.TokenType}";
                    throw new InvalidOperationException(msg);
            }
        }

        private static Uri CreateUri(string uriString) => new Uri(uriString);
    }
}
