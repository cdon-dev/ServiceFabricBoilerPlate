using Common;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommunicationListener.EventHubs
{
	public static class Extensions
	{
		public static async Task<PartitionReceiver> CreateServiceFabricReceiverAsync(
				 this EventHubClient eventHubClient,
				 IReliableStateManager stateManager,
				 string consumerGroup,
				 long servicePartitionKey,
				 IReliableDictionary<string, long> epochDictionary,
				 IReliableDictionary<string, string> offsetDictionary,
				 ILogger logger)
		{
			var eventHubRuntimeInfo = await eventHubClient.GetRuntimeInformationAsync();
			var eventHubPartitionId = eventHubRuntimeInfo.PartitionIds[servicePartitionKey];

			PartitionReceiver eventHubReceiver;
			using (var tx = stateManager.CreateTransaction())
			{
				var offsetResult = await offsetDictionary.TryGetValueAsync(tx, "offset", LockMode.Default);
				var epochResult = await epochDictionary.TryGetValueAsync(tx, "epoch", LockMode.Update);

				var newEpoch = epochResult.HasValue
					? epochResult.Value + 1
					: 0;

				var position = EventPosition.FromStart();

				try
				{
					if (offsetResult.HasValue) position = EventPosition.FromOffset(offsetResult.Value);


					logger.LogInformation(
						"Creating Event hub reciever with {ConsumerGroup} {PartitionId} {Epoch} {Position}", consumerGroup, eventHubPartitionId,
						newEpoch, position);

					eventHubReceiver = eventHubClient.CreateEpochReceiver(consumerGroup,
						servicePartitionKey.ToString(), position, newEpoch);


					// epoch is recorded each time the service fails over or restarts.
					await epochDictionary.SetAsync(tx, "epoch", newEpoch);
					await tx.CommitAsync();
				}
				catch (Exception e)
				{
					logger.LogError(e, "Failed to create event hub receiver {ConsumerGroup} {PartitionId} {Epoch}", consumerGroup, eventHubPartitionId, newEpoch);
					throw;
				}
			}

			return eventHubReceiver;
		}

		public static Task OnEventsAsync(this PartitionReceiver partitionReceiver,
		   CancellationToken cancellationToken,
		   Func<IEnumerable<EventData>, CancellationToken, Task> f,
		   IReliableStateManager stateManager,
		   IReliableDictionary<string, string> offsetDictionary,
		   ILogger logger,
		   TelemetryClient telemetryClient,
		   Func<bool> initialized,
		   int maxMessageCount,
		   TimeSpan waitTime)
		   => ServiceFabricExecution.WhileAsync(async ct =>
		   {
			   if (!initialized())
			   {
				   logger.LogDebug($"{nameof(EventhubsCommunicationListener)} not {nameof(initialized)} retries in 1 second");
				   await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
				   return;
			   }

			   var eventData = await partitionReceiver.ReceiveAsync(maxMessageCount, waitTime);

			   if (eventData == null)
			   {
				   logger.LogDebug("No available events");
				   return;
			   }

			   var events = eventData.ToList();

			   if (!events.Any())
			   {
				   logger.LogDebug("Empty event list");
				   return;
			   }

			   logger.LogDebug("Handling events");

			   using (var operation = telemetryClient.StartOperation<RequestTelemetry>("ProcessEvents"))
			   {
				   await f(events, cancellationToken);
			   }

			   using (var tx = stateManager.CreateTransaction())
			   {
				   var lastEvent = events.Last();

				   var offset = lastEvent.SystemProperties.Offset;

				   logger.LogDebug("Saving offset {Offset}", offset);

				   await offsetDictionary.SetAsync(tx, "offset", offset, TimeSpan.FromSeconds(3), cancellationToken);
				   await tx.CommitAsync();
			   }
		   }, nameof(OnEventsAsync), cancellationToken, logger);
	}
}
