// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt
// in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace MQTTnet.AspNetCore.Routing
{
    internal interface ITypeActivatorCache
    {
        TInstance CreateInstance<TInstance>(
            IServiceProvider serviceProvider,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType);
    }
}
