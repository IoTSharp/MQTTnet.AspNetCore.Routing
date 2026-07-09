using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Server;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MQTTnet.AspNetCore.Routing.Testing
{
    /// <summary>
    /// 面向单元测试的 MQTT routing 执行宿主。
    /// </summary>
    public sealed class MqttRoutingTestHost : IDisposable
    {
        private readonly bool _ownsServiceProvider;
        private bool _disposed;

        /// <summary>
        /// 使用现有服务容器创建测试宿主。
        /// </summary>
        /// <param name="services">已经注册 MQTT routing 服务的容器。</param>
        public MqttRoutingTestHost(IServiceProvider services)
            : this(services, ownsServiceProvider: false)
        {
        }

        private MqttRoutingTestHost(IServiceProvider services, bool ownsServiceProvider)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            _ownsServiceProvider = ownsServiceProvider;
        }

        /// <summary>
        /// 当前测试宿主使用的服务容器。
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// 当前 route catalog。
        /// </summary>
        public MqttRouteCatalog Catalog => Services.GetRequiredService<MqttRouteCatalog>();

        /// <summary>
        /// 创建测试宿主，并自动注册 logging 服务。
        /// </summary>
        /// <param name="configureServices">测试服务注册委托。</param>
        public static MqttRoutingTestHost Create(Action<IServiceCollection> configureServices)
        {
            if (configureServices == null)
            {
                throw new ArgumentNullException(nameof(configureServices));
            }

            var services = new ServiceCollection();
            services.AddLogging();
            configureServices(services);
            return new MqttRoutingTestHost(services.BuildServiceProvider(), ownsServiceProvider: true);
        }

        /// <summary>
        /// 匹配 topic，但不执行 action。
        /// </summary>
        /// <param name="topic">待匹配 topic。</param>
        public MqttRouteTestMatch Match(string topic)
        {
            if (topic == null)
            {
                throw new ArgumentNullException(nameof(topic));
            }

            var controllerMatch = MatchControllerRoute(topic);
            if (controllerMatch.IsMatched)
            {
                return controllerMatch;
            }

            return MatchSlimRoute(topic);
        }

        /// <summary>
        /// 直接执行 slim route 分发。
        /// </summary>
        /// <param name="message">MQTT 应用消息。</param>
        /// <param name="clientId">客户端标识。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<MqttApplicationMessageDispatchResult> DispatchApplicationMessageAsync(
            MqttApplicationMessage message,
            string? clientId = "mqtt-test-client",
            CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var dispatcher = Services.GetRequiredService<IMqttApplicationMessageDispatcher>();
            return dispatcher.DispatchAsync(message, clientId, cancellationToken);
        }

        /// <summary>
        /// 不启动 broker，直接调用 controller route。
        /// </summary>
        /// <param name="message">MQTT 应用消息。</param>
        /// <param name="clientId">客户端标识；为空时模拟 server 自身消息，router 会忽略。</param>
        /// <param name="userName">客户端用户名。</param>
        /// <param name="sessionItems">测试 session items。</param>
        /// <param name="allowUnmatchedRoutes">未匹配 route 时是否继续原始 PUBLISH。</param>
        /// <param name="mqttServer">测试 MQTT server；需要执行 publish result 时传入。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public async Task<MqttControllerRouteTestResult> InvokeControllerAsync(
            MqttApplicationMessage message,
            string? clientId = "mqtt-test-client",
            string? userName = null,
            IDictionary? sessionItems = null,
            bool allowUnmatchedRoutes = false,
            MqttServer? mqttServer = null,
            CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var router = Services.GetRequiredService<MqttRouter>();
            router.Server = mqttServer;
            var context = MqttTestContexts.CreateInterceptingPublishContext(
                message,
                clientId,
                userName,
                sessionItems,
                processPublish: true,
                cancellationToken);
            var result = await router
                .OnIncomingApplicationMessage(Services, context, allowUnmatchedRoutes)
                .ConfigureAwait(false);
            return new MqttControllerRouteTestResult(context, result);
        }

        /// <summary>
        /// 创建当前 route catalog 快照。
        /// </summary>
        public string CreateRouteSnapshot()
        {
            return Catalog.CreateSnapshot();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_ownsServiceProvider && Services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _disposed = true;
        }

        private MqttRouteTestMatch MatchControllerRoute(string topic)
        {
            var routeTable = Services.GetService<MqttRouteTable>();
            if (routeTable == null)
            {
                return new MqttRouteTestMatch(false, null, null);
            }

            var context = new MqttRouteMatchContext(topic);
            routeTable.Route(context);
            if (context.Handler == null)
            {
                return new MqttRouteTestMatch(false, null, null);
            }

            return new MqttRouteTestMatch(
                true,
                context.RouteModel,
                MqttRouteContext.ToRouteValues(context.Parameters));
        }

        private MqttRouteTestMatch MatchSlimRoute(string topic)
        {
            var routeTable = Services.GetService<MqttApplicationMessageRouteTable>();
            if (routeTable == null ||
                !routeTable.TryMatch(topic, out var route, out var routeValues) ||
                route == null)
            {
                return new MqttRouteTestMatch(false, null, null);
            }

            return new MqttRouteTestMatch(
                true,
                route.RouteModel,
                MqttRouteContext.ToRouteValues(routeValues));
        }
    }
}
