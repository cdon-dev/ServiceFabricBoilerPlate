using System;
using System.Collections.Generic;
using System.Text;

namespace CommunicationListener.EventHubs
{
	public class EventHubFabricReceiverConfiguration
	{
		public string ConnectionString { get; internal set; }
		public string OffsetDictionaryName { get; internal set; }
		public string EpochDictionaryName { get; internal set; }
		public string ConsumerGroup { get; internal set; }
	}
}
