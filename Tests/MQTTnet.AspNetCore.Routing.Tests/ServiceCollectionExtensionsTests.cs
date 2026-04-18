using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace MQTTnet.AspNetCore.Routing.Tests
{
    [TestClass]
    public class ServiceCollectionExtensionsTests
    {
        [TestMethod]
        public void AddMqttControllers_WithOptions_FindsControllersInCallingAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMqttControllers(opt => opt.WithJsonSerializerOptions());
            using var serviceProvider = services.BuildServiceProvider();
            var routeTable = serviceProvider.GetRequiredService<MqttRouteTable>();

            // Assert
            Assert.IsTrue(routeTable.Routes.Any(route =>
                route.Handler.DeclaringType == typeof(TestMqttController) &&
                route.Template.TemplateText == "integration/Ping"));
        }

        [TestMethod]
        public void AddMqttControllers_WithoutOptions_FindsControllersInCallingAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddMqttControllers();
            using var serviceProvider = services.BuildServiceProvider();
            var routeTable = serviceProvider.GetRequiredService<MqttRouteTable>();

            // Assert
            Assert.IsTrue(routeTable.Routes.Any(route =>
                route.Handler.DeclaringType == typeof(TestMqttController) &&
                route.Template.TemplateText == "integration/Ping"));
        }
    }

    [MqttRoute("integration")]
    internal sealed class TestMqttController : MqttBaseController
    {
        public void Ping()
        {
        }
    }
}
