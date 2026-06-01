// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

// Modifications Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace MQTTnet.AspNetCore.Routing
{
    /// <summary>
    /// Caches <see cref="ObjectFactory"/> instances produced by <see cref="ActivatorUtilities.CreateFactory(Type, Type[])"/>.
    /// </summary>
    internal class TypeActivatorCache : ITypeActivatorCache
    {
        private static ObjectFactory CreateFactory(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
        {
            return ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);
        }

        private readonly ConcurrentDictionary<Type, ObjectFactory> _typeActivatorCache = new ConcurrentDictionary<Type, ObjectFactory>();

        /// <inheritdoc/>
        public TInstance CreateInstance<TInstance>(
            IServiceProvider serviceProvider,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (implementationType == null)
            {
                throw new ArgumentNullException(nameof(implementationType));
            }

            if (!_typeActivatorCache.TryGetValue(implementationType, out var createFactory))
            {
                createFactory = CreateFactory(implementationType);
                _typeActivatorCache.TryAdd(implementationType, createFactory);
            }

            return (TInstance)createFactory(serviceProvider, arguments: null);
        }
    }
}
