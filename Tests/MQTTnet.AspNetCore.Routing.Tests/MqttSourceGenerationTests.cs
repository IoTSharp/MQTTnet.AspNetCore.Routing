using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.AspNetCore.Routing.Testing;

namespace MQTTnet.AspNetCore.Routing.Tests;

[TestClass]
public sealed class MqttSourceGenerationTests
{
    [TestMethod]
    public void GeneratedController_MapsRoutesWithoutControllerReflectionRegistration()
    {
        using var host = MqttRoutingTestHost.Create(services =>
        {
            services.AddSingleton<GeneratedDependency>();
            services.AddMqttApplicationMessageSlimRouting(static routes =>
                global::MyGeneratedMqttEndpoints.Map(routes));
        });

        var match = host.Match("generated/devices/boiler/telemetry");

        match.EnsureMatched();
        Assert.AreEqual("boiler", match.GetRouteValue<string>("deviceId"));
        StringAssert.Contains(host.CreateRouteSnapshot(), "generated/devices/{deviceId}/telemetry");
    }
}

internal sealed class GeneratedDependency;

[MqttController]
[MqttGeneratedController]
[MqttRoute("generated/devices")]
internal sealed class GeneratedController(GeneratedDependency dependency) : MqttBaseController
{
    private readonly GeneratedDependency _dependency = dependency;

    [MqttRoute("{deviceId}/telemetry")]
    public MqttResult Telemetry([FromMqttRoute] string deviceId)
    {
        _ = _dependency;
        return Acknowledge(deviceId);
    }
}
