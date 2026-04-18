![NuGet Version](https://img.shields.io/nuget/v/MQTTnet.AspNetCore.Routing?label=MQTTnet.AspNetCore.Routing)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/LICENSE)
[![CI](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/actions/workflows/ci.yml/badge.svg)](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/actions/workflows/ci.yml)
[![CI](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/actions/workflows/publish.yml/badge.svg)](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/actions/workflows/publish.yml)
![NuGet Downloads](https://img.shields.io/nuget/dt/MQTTnet.AspNetCore.Routing)


# MQTTnet AspNetCore Routing

MQTTnet AspNetCore Routing  is a fork of  https://github.com/Atlas-LiftTech/MQTTnet.AspNetCore.AttributeRouting

This addon to MQTTnet provides the ability to define controllers and use attribute-based routing against message topics in a manner that is very similar to AspNet Core.

## When should I use this library?

This library is a completely optional addon to MQTTnet (i.e. it is never required). You would WANT to use this if:

* Your primary use case is validating/processing the MQTT messages on your server
* Your server is not primarily sending data to clients via MQTT
* You appreciate encapsulating your message processing logic in controllers in a way similar to AspNetCore and WebAPI

You can do everything that this addon does directly by using MQTTnet delegates yourself.  However, as the amount of logic you write to validate or process incoming messages grows, the ability to organize your logic into controllers start to make much more sense.  This library helps with organizing that code as well as bringing together your dependency injection framework to MQTTnet.

## Features

* Encapsulate your incoming message logic in controllers
* Use familiar paradigms from AspNetCore in your MQTT logic (Controllers and Actions)
* First-class support for dependency injection using existing ServiceProvider implementaiton in your AspNetCore project
* Support for sync and async/await Actions on your controllers
* Use together with any other MQTTnet options

## Performance Note

This library has not been tested against a very high-load environment yet.  Ensure you do your own load testing prior to use in production.  All performance improvement PRs are welcome.

## Supported frameworks

* .NET 8
* .NET 10

## Supported MQTT versions

* 5.0.0
* 3.1.1
* 3.1.0

## Nuget

This library is available as a nuget package: <https://www.nuget.org/packages/MQTTnet.AspNetCore.Routing/>

## Usage

Install this package and MQTTnet from nuget.
For dotnet CLI: 
```bash
dotnet add package MQTTnet.AspNetCore.Routing
```

Example configuration on ASP.NET Core 6 MVC Configuration

```csharp
using MQTTnet.AspNetCore;
using MQTTnet.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
    {
        o.ListenAnyIP(iotaboardMqttSettings.Port, l => l.UseMqtt());
        o.ListenAnyIP(iotaboardHttpSettings.Port);
    }
);

// Configure MQTTServer service
builder.Services
    .AddHostedMqttServerWithServices(o =>
    {
        // other configurations
        o.WithoutDefaultEndpoint();
    })
    .AddMqttConnectionHandler()
    .AddConnections()
    .AddMqttControllers( // <== NOTICE THIS PART
    /*
        By default, all controllers within the executing assembly are
        discovered (just pass nothing here). To provide a list of assemblies
        explicitly, pass an array of Assembly[] here.
    */)
    /*
        optionally, set System.Text.Json serialization default for use with 
        [FromPayload] in the controllers. We can specify a JsonSerializerOptions
        or use JsonSerializerDefaults, useful for case-sensitivity or comment-handling
    */
    .AddMqttDefaultJsonOptions(new JsonSerializerOptions(JsonSerializerDefaults.Web)); 
    
var app = builder.Build();

app.MapControllers();
app.UseMqttServer(server => {  
    // other MqttServer configurations, for example client connect intercepts
    server.WithAttributeRouting(app.Services, allowUnmatchedRoutes: false);
});
app.Run();
```

Create your controllers by inheriting from MqttBaseController and adding actions to it like so:

```csharp
[MqttController]
[MqttRoute("[controller]")] // Optional route prefix
public class MqttWeatherForecastController : MqttBaseController // Inherit from MqttBaseController for convenience functions
{
	private readonly ILogger<MqttWeatherForecastController> _logger;

	// Controllers have full support for dependency injection just like AspNetCore controllers
	public MqttWeatherForecastController(ILogger<MqttWeatherForecastController> logger)
	{
		_logger = logger;
	}

	// Supports template routing with typed constraints just like AspNetCore
	// Action routes compose together with the route prefix on the controller level
	[MqttRoute("{zipCode:int}/temperature")]
	public Task WeatherReport(int zipCode)
	{
		// We have access to the MqttContext
		if (zipCode != 90210) { MqttContext.CloseConnection = true; }

		// We have access to the raw message
		var temperature = BitConverter.ToDouble(Message.Payload);

		_logger.LogInformation($"It's {temperature} degrees in Hollywood");

		// Example validation
		if (temperature <= 0 || temperature >= 130)
		{
			return BadMessage();
		}

		return Ok();
	}
	
	// Supports binding JSON message payload to parameters with [FromPayload] attribute,
	// Similar to ASP.NET Core [FromBody]
	[MqttRoute("{deviceName}/telemetry")]
	public async Task NewTelemetry(string deviceName, [FromPayload] Telemetry telemetry)
	{
	    // here telemetry is JSON-deserialized from message payload to type Telemetry
		bool success = await DoSomething(telemetry);
		if (success) {
		    await Ok();
		    return;
		}
		else {
		    await BadMessage();
		    return;
		}
	}
}
```

[See server example project here](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/tree/master/ExampleServer)

[See client example project here](https://github.com/IoTSharp/MQTTnet.AspNetCore.Routing/tree/master/ExampleClient)

## Contributions

Contributions are welcome. Please open an issue to discuss your idea prior to sending a PR.

## MIT License

See https://github.com/Atlas-LiftTech/MQTTnet.AspNetCore.AttributeRouting/LICENSE.

## About Atlas LiftTech

This library is sponsored by [Atlas Lift Tech](https://atlaslifttech.com/).  Atlas Lift Tech is a [safe patient handling and mobility (SPHM)](https://atlaslifttech.com/program-management/) solution provider, helping hospitals improve patient outcomes and improve patient mobility.
