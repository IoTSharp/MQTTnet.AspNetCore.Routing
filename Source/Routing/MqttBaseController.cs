﻿// Copyright (c) Atlas Lift Tech Inc. All rights reserved.

using Microsoft.AspNetCore.Mvc;
using MQTTnet.AspNetCore.Routing.Attributes;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MQTTnet.AspNetCore.Routing
{
    [MqttController]
    public abstract class MqttBaseController
    {
        /// <summary>
        /// Connection context is set by controller activator. If this class is instantiated directly, it will be null.
        /// </summary>
        public InterceptingPublishEventArgs MqttContext => ControllerContext.MqttContext;

        /// <summary>
        /// Gets the <see cref="MqttApplicationMessage"/> for the executing action.
        /// </summary>
        public MqttApplicationMessage Message => MqttContext.ApplicationMessage;

        /// <summary>
        /// Gets the <see cref="MqttServer"/> for the executing action.
        /// </summary>
        public MqttServer Server => ControllerContext.MqttServer;

        public string ClientId => MqttContext.ClientId;
        public async Task<MqttClientStatus> GetClientStatusAsync()
        {
            var clients = await Server.GetClientsAsync();
            return clients.FirstOrDefault(c => c.Id == MqttContext.ClientId);
        }
        public async Task<MqttSessionStatus> GetSessionAsync()
        {
            var client = await GetClientStatusAsync();
            return client?.Session;
        }

        public async Task<T> GetSessionDataAsync<T>(string key)
        {
            var client = await GetClientStatusAsync();
            return (T)(client?.Session.Items[key]);
        }

        /// <summary>
        /// ControllerContext is set by controller activator. If this class is instantiated directly, it will be null.
        /// </summary>
        [MqttControllerContext]
        public MqttControllerContext ControllerContext { get; set; }

        /// <summary>
        /// Create a result that accepts the given message and publishes it to all subscribers on the topic.
        /// </summary>
        /// <returns>The created <see cref="Task"/> for the reponse.</returns>
        [NonAction]
        public virtual Task Ok()
        {
            MqttContext.ProcessPublish = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Create a result that accepts the given message and publishes it to all subscribers on the topic. This is an
        /// alias for the <see cref="Ok"/> result.
        /// </summary>
        /// <returns>The created <see cref="Task"/> for the reponse.</returns>
        [NonAction]
        public virtual Task Accepted() => Ok();

        /// <summary>
        /// Create a result that rejects the given message and prevents publishing it to any subscribers.
        /// </summary>
        /// <returns>The created <see cref="Task"/> for the reponse.</returns>
        [NonAction]
        public virtual Task BadMessage()
        {
            MqttContext.ProcessPublish = false;
            return Task.CompletedTask;
        }

        public T GetSessionItem<T>(string key)
        {
            return (T)MqttContext.SessionItems[key] ;
        }

        public T GetSessionItem<T>()
        {
            return (T)MqttContext.SessionItems[typeof(T).Name];
        }
    }
}