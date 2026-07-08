using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet.AspNetCore.Routing.Attributes;
using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public sealed partial class RouteCatalogTests
    {
        [TestMethod]
        public void ControllerRouting_RegistersApplicationModelAndRouteCatalog()
        {
            using var services = new ServiceCollection()
                .AddMqttControllers<CatalogController>(options =>
                {
                    options.WithJsonSerializerContext(CatalogJsonContext.Default);
                })
                .BuildServiceProvider();

            var catalog = services.GetRequiredService<MqttRouteCatalog>();
            var controller = catalog.ApplicationModel.Controllers.Single();
            var telemetryRoute = catalog.Routes.Single(route => route.Template == "catalog/devices/{deviceId:guid}/telemetry");
            var telemetryAction = controller.Actions.Single(action => action.Name == nameof(CatalogController.Telemetry));
            var payloadParameter = telemetryAction.Parameters.Single(parameter => parameter.Name == "payload");
            var deviceIdParameter = telemetryRoute.RouteParameters.Single(parameter => parameter.Name == "deviceId");
            var r3Action = controller.Actions.Single(action => action.Name == nameof(CatalogController.R3Binding));
            var r3Route = catalog.Routes.Single(route => route.Template == "catalog/devices/{deviceId:guid}/r3");
            var r3PayloadParameter = r3Action.Parameters.Single(parameter => parameter.Name == "payload");
            var snapshot = catalog.CreateSnapshot();

            Assert.IsFalse(catalog.HasErrors);
            Assert.AreEqual(typeof(CatalogController), controller.ControllerType);
            Assert.AreEqual(MqttRouteKind.ControllerAction, telemetryRoute.Kind);
            Assert.AreEqual(typeof(CatalogController), telemetryRoute.ControllerType);
            Assert.AreEqual(nameof(CatalogController.Telemetry), telemetryRoute.ActionMethod.Name);
            Assert.AreEqual(typeof(CatalogPayload), telemetryRoute.PayloadType);
            Assert.AreEqual(MqttBindingSource.Payload, payloadParameter.BindingSource);
            Assert.AreEqual(MqttBindingSource.Route, deviceIdParameter.BindingSource);
            Assert.AreEqual(MqttBindingSource.Route, r3Action.Parameters.Single(parameter => parameter.Name == "deviceId").BindingSource);
            Assert.AreEqual(MqttBindingSource.Payload, r3Action.Parameters.Single(parameter => parameter.Name == "payload").BindingSource);
            Assert.AreEqual(MqttBindingSource.Client, r3Action.Parameters.Single(parameter => parameter.Name == "clientId").BindingSource);
            Assert.AreEqual(MqttBindingSource.UserProperty, r3Action.Parameters.Single(parameter => parameter.Name == "traceId").BindingSource);
            Assert.AreEqual(MqttBindingSource.Session, r3Action.Parameters.Single(parameter => parameter.Name == "tenant").BindingSource);
            Assert.AreEqual(MqttBindingSource.Context, r3Action.Parameters.Single(parameter => parameter.Name == "requestContext").BindingSource);
            Assert.AreEqual("deviceId", r3Action.Parameters.Single(parameter => parameter.Name == "deviceId").BindingName);
            Assert.AreEqual("trace-id", r3Action.Parameters.Single(parameter => parameter.Name == "traceId").BindingName);
            Assert.AreEqual("application/vnd.catalog+json", r3Action.DeclaredContentType);
            Assert.AreEqual("json", r3Action.DeclaredPayloadFormatterName);
            Assert.AreEqual("application/vnd.catalog+json", r3Route.DeclaredContentType);
            Assert.AreEqual("json", r3Route.DeclaredPayloadFormatterName);
            Assert.AreEqual("application/vnd.catalog+json", r3PayloadParameter.DeclaredContentType);
            Assert.AreEqual("json", r3PayloadParameter.FormatterName);
            CollectionAssert.Contains(deviceIdParameter.RouteConstraints.ToArray(), "guid");
            StringAssert.Contains(snapshot, "ControllerAction catalog/devices/{deviceId:guid}/telemetry");
            StringAssert.Contains(snapshot, "payload=CatalogPayload");
            StringAssert.Contains(snapshot, "contentType=application/vnd.catalog+json");
            StringAssert.Contains(snapshot, "formatter=json");
        }

        [TestMethod]
        public void SlimRouting_RegistersApplicationModelAndRouteCatalog()
        {
            using var services = new ServiceCollection()
                .AddMqttApplicationMessageSlimRouting(routes =>
                {
                    routes.MapJson(
                        "catalog/slim/{deviceId}/telemetry",
                        CatalogJsonContext.Default.CatalogPayload,
                        static (_, _) => ValueTask.CompletedTask);

                    routes.Map(
                        "catalog/slim/{deviceId}/ping",
                        static _ => ValueTask.CompletedTask);
                })
                .BuildServiceProvider();

            var catalog = services.GetRequiredService<MqttRouteCatalog>();
            var telemetryRoute = catalog.Routes.Single(route => route.Template == "catalog/slim/{deviceId}/telemetry");
            var deviceIdParameter = telemetryRoute.RouteParameters.Single();
            var snapshot = catalog.CreateSnapshot();

            Assert.IsFalse(catalog.HasErrors);
            Assert.AreEqual(0, catalog.ApplicationModel.Controllers.Count);
            Assert.AreEqual(2, catalog.Routes.Count);
            Assert.AreEqual(MqttRouteKind.ApplicationMessage, telemetryRoute.Kind);
            Assert.AreEqual(typeof(CatalogPayload), telemetryRoute.PayloadType);
            Assert.AreEqual("application/json", telemetryRoute.DeclaredContentType);
            Assert.AreEqual("json", telemetryRoute.DeclaredPayloadFormatterName);
            Assert.AreEqual("deviceId", deviceIdParameter.Name);
            Assert.AreEqual(MqttBindingSource.Route, deviceIdParameter.BindingSource);
            Assert.IsNotNull(telemetryRoute.ActionMethod);
            StringAssert.Contains(snapshot, "ApplicationMessage catalog/slim/{deviceId}/telemetry");
            StringAssert.Contains(snapshot, "contentType=application/json formatter=json");
        }

        [TestMethod]
        public void ControllerRouting_DetectsAmbiguousRoutesAtStartup()
        {
            using var services = new ServiceCollection()
                .AddMqttControllers<AmbiguousCatalogController>()
                .BuildServiceProvider();

            var exception = AssertThrows<InvalidOperationException>(
                () => services.GetRequiredService<MqttRouteCatalog>());

            StringAssert.Contains(exception.Message, MqttRouteCatalogDiagnostics.AmbiguousRouteCode);
        }

        [TestMethod]
        public void SlimRouting_DetectsAmbiguousRoutesAtStartup()
        {
            var exception = AssertThrows<InvalidOperationException>(() =>
            {
                new ServiceCollection()
                    .AddMqttApplicationMessageSlimRouting(routes =>
                    {
                        routes.Map("catalog/ambiguous/{deviceId}", static _ => ValueTask.CompletedTask);
                        routes.Map("catalog/ambiguous/{assetId}", static _ => ValueTask.CompletedTask);
                    })
                    .BuildServiceProvider();
            });

            StringAssert.Contains(exception.Message, MqttRouteCatalogDiagnostics.AmbiguousRouteCode);
        }

        private static TException AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail($"Expected {typeof(TException).Name}, but caught {exception.GetType().Name}: {exception.Message}");
            }

            Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }

        [MqttController]
        [MqttRoute("catalog/devices")]
        private sealed class CatalogController
        {
            [MqttRoute("{deviceId:guid}/telemetry")]
            public Task Telemetry(Guid deviceId, [FromPayload] CatalogPayload payload)
            {
                return Task.CompletedTask;
            }

            [MqttRoute("{deviceId:guid}/status")]
            public void Status(Guid deviceId)
            {
            }

            [MqttRoute("{deviceId:guid}/r3")]
            public void R3Binding(
                [FromMqttRoute("deviceId")] Guid deviceId,
                [FromMqttPayload("application/vnd.catalog+json", FormatterName = "json")] CatalogPayload payload,
                [FromMqttClient] string clientId,
                [FromMqttUserProperty("trace-id")] string traceId,
                [FromMqttSession("tenant")] string tenant,
                MqttRequestContext requestContext)
            {
            }
        }

        [MqttController]
        [MqttRoute("catalog/ambiguous")]
        private sealed class AmbiguousCatalogController
        {
            [MqttRoute("{deviceId}")]
            public void ByDevice(string deviceId)
            {
            }

            [MqttRoute("{assetId}")]
            public void ByAsset(string assetId)
            {
            }
        }

        public sealed class CatalogPayload
        {
            public double Value { get; set; }
        }

        [JsonSerializable(typeof(CatalogPayload))]
        public sealed partial class CatalogJsonContext : JsonSerializerContext
        {
        }
    }
}
