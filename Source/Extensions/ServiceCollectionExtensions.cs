// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.AspNetCore.Routing;
using MQTTnet.AspNetCore.Routing.Routing;
using MQTTnet.Server;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

// This is needed to make internal classes visible to UnitTesting projects
[assembly: InternalsVisibleTo("MQTTnet.AspNetCore.Routing.Tests, PublicKey=00240000048000009" +
    "4000000060200000024000052534131000400000100010089369e254b2bf47119265eb7514c522350b2e61beda20ccc9" +
    "a9ddc3f8dab153d59d23011476cc939860d9ae7d09d1bade2915961d01f9ec1f1852265e4d54b090f4c427756f7044e8" +
    "65ffcd47bf99f18af6361de42003808f7323d20d5d2c66fe494852b5e2438db793ec9fd845b80e1ce5c9b17ff053f386" +
    "bc0f06080e9d0ba")]

namespace MQTTnet.AspNetCore.Routing
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMqttControllers(this IServiceCollection services, Assembly[] fromAssemblies)
        {
            return services.AddMqttControllers(opt => opt.FromAssemblies = fromAssemblies);
        }
        public static IServiceCollection AddMqttControllers(this IServiceCollection services)
        {
            return services.AddMqttControllers(opt => { });
        }
        public static IServiceCollection AddMqttControllers(this IServiceCollection services, Action<MqttRoutingOptions> _options)
        {
            var _opt = new MqttRoutingOptions();
            _opt.WithJsonSerializerOptions();
            _opt.FromAssemblies = null;
            _opt.RouteInvocationInterceptor = null;
            _options?.Invoke(_opt);

            services.AddSingleton(_opt);
            services.AddSingleton(_ =>
            {

                var fromAssemblies = _opt.FromAssemblies;
                if (fromAssemblies != null && fromAssemblies.Length == 0)
                {
                    throw new ArgumentException("'fromAssemblies' cannot be an empty array. Pass null or a collection of 1 or more assemblies.", nameof(fromAssemblies));
                }
                var assemblies = fromAssemblies ?? new Assembly[] { Assembly.GetEntryAssembly() };

                return MqttRouteTableFactory.Create(assemblies);
            });
            
            services.AddSingleton<ITypeActivatorCache>(new TypeActivatorCache());
            services.AddTransient<MqttRouter>();
            if (_opt.RouteInvocationInterceptor != null)
            {
                services.AddSingleton(typeof(IRouteInvocationInterceptor), _opt.RouteInvocationInterceptor);
            }
            return services;
        }
        public static void WithRouteInvocationInterceptor<T>(this MqttRoutingOptions opt) where T : IRouteInvocationInterceptor
        {
            opt.RouteInvocationInterceptor = typeof(T);
        }
        public static MqttRoutingOptions WithJsonSerializerOptions(this MqttRoutingOptions opt)
        {
#if NET5_0_OR_GREATER
            var jopt = new JsonSerializerOptions(JsonSerializerDefaults.Web);
#else
            var jopt = new JsonSerializerOptions();
            jopt.PropertyNameCaseInsensitive = true;
#endif
            jopt.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            opt.SerializerOptions = jopt;
            return opt;
        }

        public static MqttRoutingOptions WithJsonSerializerOptions(this MqttRoutingOptions opt, JsonSerializerOptions options)
        {
            opt.SerializerOptions = options;
            return opt;
        }

        public static MqttRoutingOptions FromAssemblies(this MqttRoutingOptions opt, params Assembly[] assemblies)
        {
            opt.FromAssemblies = assemblies;
            return opt;
        }

        [Obsolete("Use 'services.AddMqttControllers(opt => opt.SerializerOptions= new JsonSerializerOptions());' instead ")]
        public static IServiceCollection AddMqttDefaultJsonOptions(this IServiceCollection services,
            JsonSerializerOptions options)
        {
            services.AddSingleton(new MqttRoutingOptions() { SerializerOptions = options });
            return services;
        }

        public static IApplicationBuilder UseAttributeRouting(this IApplicationBuilder app, bool allowUnmatchedRoutes = false)
        {
            var server = app.ApplicationServices.GetRequiredService<MqttServer>();
            server.WithAttributeRouting(app.ApplicationServices, allowUnmatchedRoutes);
            return app;
        }

        public static void WithAttributeRouting(this MqttServer server, IServiceProvider svcProvider, bool allowUnmatchedRoutes = false)
        {
            server.WithAttributeRouting(svcProvider, new ServerEventProvider(server), allowUnmatchedRoutes);
        }

        public static void WithAttributeRouting(this MqttServer server, IServiceProvider svcProvider, IPublishEventProvider publishEventProvider, bool allowUnmatchedRoutes = false)
        {
            var router = svcProvider.GetRequiredService<MqttRouter>();
            router.Server = server;
            IRouteInvocationInterceptor? interceptor = svcProvider.GetService<IRouteInvocationInterceptor>();

            publishEventProvider.OnPublishAsync += async (args) =>
            {
                object correlationObject = null;
                if (interceptor != null)
                {
                    correlationObject = await interceptor.RouteExecuting(args);
                }

                try
                {
                    await router.OnIncomingApplicationMessage(svcProvider, args, allowUnmatchedRoutes);
                    if (interceptor != null)
                    {
                        await interceptor.RouteExecuted(correlationObject, args, null);
                    }
                }
                catch (Exception ex)
                {
                    if (interceptor != null)
                    {
                        await interceptor.RouteExecuted(correlationObject, args, ex);
                    }
                    throw;
                }
            };
        }
    }
}