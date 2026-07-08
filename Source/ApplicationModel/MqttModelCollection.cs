using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal static class MqttModelCollection
    {
        public static IReadOnlyList<T> Empty<T>()
        {
            return Array.Empty<T>();
        }

        public static IReadOnlyList<T> ToReadOnlyList<T>(IEnumerable<T>? values)
        {
            if (values == null)
            {
                return Array.Empty<T>();
            }

            var array = values as T[] ?? values.ToArray();
            return array.Length == 0 ? Array.Empty<T>() : new ReadOnlyCollection<T>(array);
        }
    }
}
