﻿// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetCore.CAP.Infrastructure;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Models;
using Microsoft.Extensions.Logging;

namespace DotNetCore.CAP
{
    internal class ConsumerHandler : IConsumerHandler
    {
        private readonly IStorageConnection _connection;
        private readonly IConsumerClientFactory _consumerClientFactory;
        private readonly CancellationTokenSource _cts;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger _logger;
        private readonly TimeSpan _pollingDelay = TimeSpan.FromSeconds(1);
        private readonly MethodMatcherCache _selector;

        private Task _compositeTask;
        private bool _disposed;

        public ConsumerHandler(IConsumerClientFactory consumerClientFactory,
            IDispatcher dispatcher,
            IStorageConnection connection,
            ILogger<ConsumerHandler> logger,
            MethodMatcherCache selector)
        {
            _selector = selector;
            _logger = logger;
            _consumerClientFactory = consumerClientFactory;
            _dispatcher = dispatcher;
            _connection = connection;
            _cts = new CancellationTokenSource();
        }

        public void Start()
        {
            var groupingMatches = _selector.GetCandidatesMethodsOfGroupNameGrouped();

            foreach (var matchGroup in groupingMatches)
            {
                Task.Factory.StartNew(() =>
                {
                    using (var client = _consumerClientFactory.Create(matchGroup.Key))
                    {
                        RegisterMessageProcessor(client);

                        client.Subscribe(matchGroup.Value.Select(x => x.Attribute.Name));

                        client.Listening(_pollingDelay, _cts.Token);
                    }
                }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            _compositeTask = Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            try
            {
                _compositeTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex)
            {
                var innerEx = ex.InnerExceptions[0];
                if (!(innerEx is OperationCanceledException))
                {
                    _logger.ExpectedOperationCanceledException(innerEx);
                }
            }
        }

        public void Pulse()
        {
            //ignore
        }

        private void RegisterMessageProcessor(IConsumerClient client)
        {
            client.OnMessageReceived += (sender, message) =>
            {
                try
                {
                    var storedMessage = StoreMessage(message);

                    client.Commit();

                    _dispatcher.EnqueueToExecute(storedMessage);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An exception occurred when storage received message. Message:'{0}'.",
                        message);
                    client.Reject();
                }
            };

            client.OnLog += WriteLog;
        }

        private void WriteLog(object sender, LogMessageEventArgs logmsg)
        {
            switch (logmsg.LogType)
            {
                case MqLogType.ConsumerCancelled:
                    _logger.LogWarning("RabbitMQ consumer cancelled. reason: " + logmsg.Reason);
                    break;
                case MqLogType.ConsumerRegistered:
                    _logger.LogInformation("RabbitMQ consumer registered. " + logmsg.Reason);
                    break;
                case MqLogType.ConsumerUnregistered:
                    _logger.LogWarning("RabbitMQ consumer unregistered. reason: " + logmsg.Reason);
                    break;
                case MqLogType.ConsumerShutdown:
                    _logger.LogWarning("RabbitMQ consumer shutdown. reason:" + logmsg.Reason);
                    break;
                case MqLogType.ConsumeError:
                    _logger.LogError("Kakfa client consume error. reason:" + logmsg.Reason);
                    break;
                case MqLogType.ServerConnError:
                    _logger.LogCritical("Kafka server connection error. reason:" + logmsg.Reason);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private CapReceivedMessage StoreMessage(MessageContext messageContext)
        {
            var receivedMessage = new CapReceivedMessage(messageContext)
            {
                StatusName = StatusName.Scheduled
            };
            var id = _connection.StoreReceivedMessageAsync(receivedMessage).GetAwaiter().GetResult();
            receivedMessage.Id = id;
            return receivedMessage;
        }
    }
}