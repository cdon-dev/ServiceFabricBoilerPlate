using Microsoft.ApplicationInsights;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;

namespace CommunicationListener.EventHubs
{
	public class EventhubsCommunicationListener : ICommunicationListener
	{
		private readonly EventHubFabricReceiverConfiguration _config;
		private readonly ILogger _logger;
		private readonly IServicePartition _servicePartition;
		private readonly IReliableStateManager _stateManager;
		private readonly TelemetryClient _telemetryClient;
		private EventHubClient _eventHubClient;
		private bool _initialized;
		private IReliableDictionary<string, string> _offsetDictionary;

		private Lazy<Task<PartitionReceiver>> _partitionReceiverFactory;

		public EventhubsCommunicationListener(
			IReliableStateManager stateManager,
			IServicePartition servicePartition,
			ILogger logger,
			TelemetryClient telemetryClient,
			EventHubFabricReceiverConfiguration config)
		{
			_telemetryClient = telemetryClient;
			_logger = logger;
			_config = config;
			_stateManager = stateManager;
			_servicePartition = servicePartition;
		}

		public void Abort()
		{
			_eventHubClient.Close();
		}

		public async Task CloseAsync(CancellationToken cancellationToken)
		{
			await (await _partitionReceiverFactory.Value).CloseAsync();
			await _eventHubClient.CloseAsync();
		}

		public Task<string> OpenAsync(CancellationToken cancellationToken)
		{
			_eventHubClient = EventHubClient.CreateFromConnectionString(_config.ConnectionString);

			var partitionInfo = (Int64RangePartitionInformation)_servicePartition.PartitionInfo;
			var servicePartitionKey = partitionInfo.LowKey;

			_partitionReceiverFactory = new Lazy<Task<PartitionReceiver>>(() => Task.Factory.StartNew(async () =>
			{
				var epochDictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, long>>(_config.EpochDictionaryName);
				_offsetDictionary = await _stateManager.GetOrAddAsync<IReliableDictionary<string, string>>(_config.OffsetDictionaryName);

				try
				{
					return await _eventHubClient.CreateServiceFabricReceiverAsync(_stateManager, _config.ConsumerGroup, servicePartitionKey, epochDictionary,
						_offsetDictionary, _logger);
				}
				catch (Exception e)
				{
					_logger.LogError(e, "Event processor service failed {Type}", GetType().Name);
					throw;
				}
			}, cancellationToken).Unwrap());


			_initialized = true;
			return Task.FromResult(_config.ConnectionString);
		}

		public async Task OnEventsAsync(CancellationToken cancellationToken, Func<IEnumerable<EventData>, CancellationToken, Task> f,
			int maxMessageCount, TimeSpan waitTime)
		{
			var receiver = await _partitionReceiverFactory.Value;
			await receiver.OnEventsAsync(
				cancellationToken,
				f,
				_stateManager,
				_offsetDictionary,
				_logger,
				_telemetryClient,
				() => _initialized,
				maxMessageCount,
				waitTime);
		}
	}
}
