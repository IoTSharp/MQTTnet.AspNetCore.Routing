using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public class RouteTableTests
    {
        [TestMethod]
        public void Route_Constructor()
        {
            // Arrange
            var routes = new string[]
            {
                "super/awesome",
                "super/cool",
                "other/route"
            };

            var MockRoutes = new MqttRoute[] {
                new MqttRoute(
                    new RouteTemplate(routes[0], new List<TemplateSegment>() {
                        new TemplateSegment(routes[0], "super", false),
                        new TemplateSegment(routes[0], "awesome", false),
                    }), null, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[1], new List<TemplateSegment>() {
                        new TemplateSegment(routes[1], "super", false),
                        new TemplateSegment(routes[1], "cool", false),
                    }), null, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[2], new List<TemplateSegment>() {
                        new TemplateSegment(routes[2], "other", false),
                        new TemplateSegment(routes[2], "route", false),
                    }), null, new string[] { }, typeof(RouteTableTests)),
            };

            // Act
            var MockTable = new MqttRouteTable(MockRoutes);

            // Assert
            CollectionAssert.AreEqual(MockRoutes, MockTable.Routes);
        }

        [TestMethod]
        public void Route_Match()
        {
            // Arrange
            var routes = new string[]
            {
                "super/awesome",
                "super/cool",
                "other/route"
            };

            var MockMethod = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Match");
            var MockMethod2 = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Constructor");
            var MockRoutes = new MqttRoute[] {
                new MqttRoute(
                    new RouteTemplate(routes[0], new List<TemplateSegment>() {
                        new TemplateSegment(routes[0], "super", false),
                        new TemplateSegment(routes[0], "awesome", false),
                    }), MockMethod, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[1], new List<TemplateSegment>() {
                        new TemplateSegment(routes[1], "super", false),
                        new TemplateSegment(routes[1], "cool", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[2], new List<TemplateSegment>() {
                        new TemplateSegment(routes[2], "other", false),
                        new TemplateSegment(routes[2], "route", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
            };
            var context = new MqttRouteMatchContext("super/awesome");

            // Act
            var MockTable = new MqttRouteTable(MockRoutes);

            MockTable.Route(context);

            // Assert
            Assert.IsNotNull(MockMethod);
            Assert.IsNotNull(MockMethod2);
            Assert.AreNotSame(MockMethod, MockMethod2);
            Assert.AreSame(context.Handler, MockMethod);
        }

        [TestMethod]
        public void Route_MatchWildcard()
        {
            // Arrange
            var routes = new string[]
            {
                "{*path}",
                "super/cool",
                "other/route"
            };

            var MockMethod = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Match");
            var MockMethod2 = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Constructor");
            var MockRoutes = new MqttRoute[] {
                new MqttRoute(
                    new RouteTemplate(routes[0], new List<TemplateSegment>() {
                        new TemplateSegment(routes[0], "*path", true),
                    }), MockMethod, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[1], new List<TemplateSegment>() {
                        new TemplateSegment(routes[1], "super", false),
                        new TemplateSegment(routes[1], "cool", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[2], new List<TemplateSegment>() {
                        new TemplateSegment(routes[2], "other", false),
                        new TemplateSegment(routes[2], "route", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
            };

            var context = new MqttRouteMatchContext("super/duper");

            // Act
            var MockTable = new MqttRouteTable(MockRoutes);

            MockTable.Route(context);

            // Assert
            Assert.IsNotNull(MockMethod);
            Assert.IsNotNull(MockMethod2);
            Assert.AreNotSame(MockMethod, MockMethod2);
            Assert.AreSame(context.Handler, MockMethod);
        }

        [TestMethod]
        public void Route_MatchWildcardOrder()
        {
            // Arrange
            var routes = new string[]
            {
                "{*path}",
                "super/cool",
                "other/route"
            };

            var MockMethod = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Match");
            var MockMethod2 = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Constructor");
            var MockRoutes = new MqttRoute[] {
                new MqttRoute(
                    new RouteTemplate(routes[1], new List<TemplateSegment>() {
                        new TemplateSegment(routes[1], "super", false),
                        new TemplateSegment(routes[1], "cool", false),
                    }), MockMethod, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[2], new List<TemplateSegment>() {
                        new TemplateSegment(routes[2], "other", false),
                        new TemplateSegment(routes[2], "route", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[0], new List<TemplateSegment>() {
                        new TemplateSegment(routes[0], "*path", true),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
            };

            var context = new MqttRouteMatchContext("super/cool");

            // Act
            var MockTable = new MqttRouteTable(MockRoutes);

            MockTable.Route(context);

            // Assert
            Assert.IsNotNull(MockMethod);
            Assert.IsNotNull(MockMethod2);
            Assert.AreNotSame(MockMethod, MockMethod2);
            Assert.AreSame(context.Handler, MockMethod);
        }

        [TestMethod]
        public void Route_Miss()
        {
            // Arrange
            var routes = new string[]
            {
                "super/awesome",
                "super/cool",
                "other/route"
            };

            var MockMethod = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Match");
            var MockMethod2 = Type.GetType("MQTTnet.AspNetCore.Routing.Tests.RouteTableTests").GetMethod("Route_Constructor");
            var MockRoutes = new MqttRoute[] {
                new MqttRoute(
                    new RouteTemplate(routes[0], new List<TemplateSegment>() {
                        new TemplateSegment(routes[0], "super", false),
                        new TemplateSegment(routes[0], "awesome", false),
                    }), MockMethod, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[1], new List<TemplateSegment>() {
                        new TemplateSegment(routes[1], "super", false),
                        new TemplateSegment(routes[1], "cool", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
                new MqttRoute(
                    new RouteTemplate(routes[2], new List<TemplateSegment>() {
                        new TemplateSegment(routes[2], "other", false),
                        new TemplateSegment(routes[2], "route", false),
                    }), MockMethod2, new string[] { }, typeof(RouteTableTests)),
            };
            var context = new MqttRouteMatchContext("super/miss");

            // Act
            var MockTable = new MqttRouteTable(MockRoutes);

            MockTable.Route(context);

            // Assert
            Assert.IsNotNull(MockMethod);
            Assert.IsNotNull(MockMethod2);
            Assert.AreNotSame(MockMethod, MockMethod2);
            Assert.IsNull(context.Handler);
        }

        [TestMethod]
        public void RouteTableFactory_AllowsMultipleControllerRoutePrefixes()
        {
            var routeTable = MqttRouteTableFactory.CreateFromControllerType<MultiplePrefixController>();

            var primary = Route(routeTable, "multi/primary/status");
            var alias = Route(routeTable, "multi/alias/status");

            Assert.AreEqual(nameof(MultiplePrefixController.Status), primary.Handler.Name);
            Assert.AreEqual(typeof(MultiplePrefixController), primary.ControllerType);
            Assert.AreEqual(nameof(MultiplePrefixController.Status), alias.Handler.Name);
            Assert.AreEqual(typeof(MultiplePrefixController), alias.ControllerType);
        }

        [TestMethod]
        public void RouteTableFactory_InheritedControllerActionsUseDerivedControllerPrefix()
        {
            var routeTable = MqttRouteTableFactory.CreateFromControllerTypes(new[]
            {
                typeof(BaseGatewayController),
                typeof(ThingsBoardGatewayController)
            });

            var platform = Route(routeTable, "gateway/telemetry");
            var alias = Route(routeTable, "v1/gateway/telemetry");
            var childAlias = Route(routeTable, "v1/gateway/child-001/connect");

            Assert.AreEqual(nameof(BaseGatewayController.Telemetry), platform.Handler.Name);
            Assert.AreEqual(typeof(BaseGatewayController), platform.ControllerType);
            Assert.AreEqual(nameof(BaseGatewayController.Telemetry), alias.Handler.Name);
            Assert.AreEqual(typeof(ThingsBoardGatewayController), alias.ControllerType);
            Assert.AreEqual(nameof(BaseGatewayController.Connect), childAlias.Handler.Name);
            Assert.AreEqual(typeof(ThingsBoardGatewayController), childAlias.ControllerType);
            Assert.AreEqual("child-001", childAlias.Parameters["device"]);
        }

        private static MqttRouteMatchContext Route(MqttRouteTable routeTable, string topic)
        {
            var context = new MqttRouteMatchContext(topic);
            routeTable.Route(context);
            Assert.IsNotNull(context.Handler, $"Expected '{topic}' to match a route.");
            Assert.IsNotNull(context.ControllerType, $"Expected '{topic}' to resolve a controller type.");
            return context;
        }

        [MqttController]
        [MqttRoute("multi/primary")]
        [MqttRoute("multi/alias")]
        private sealed class MultiplePrefixController
        {
            [MqttRoute("status")]
            public void Status()
            {
            }
        }

        [MqttController]
        [MqttRoute("gateway")]
        private class BaseGatewayController
        {
            [MqttRoute("telemetry")]
            public void Telemetry()
            {
            }

            [MqttRoute("{device}/connect")]
            public void Connect(string device)
            {
            }
        }

        [MqttController]
        [MqttRoute("v1/gateway")]
        private sealed class ThingsBoardGatewayController : BaseGatewayController
        {
        }
    }
}
