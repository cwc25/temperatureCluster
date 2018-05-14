using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace eventshubconsumer
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class eventshubconsumer : StatefulService
    {
        private const string OffsetDictionaryName = "OffsetDictionary";
        private const string EpochDictionaryName = "EpochDictionary";
        public eventshubconsumer(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[0];
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            IReliableDictionary<string, string> offsetDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(OffsetDictionaryName);
            IReliableDictionary<string, long> epochDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>(EpochDictionaryName);

            while (!cancellationToken.IsCancellationRequested)
            {
                ConfigurationSettings configurationSettings = FabricRuntime.GetActivationContext().GetConfigurationPackageObject("Config").Settings;
                Int64RangePartitionInformation partitionInfo = (Int64RangePartitionInformation)this.Partition.PartitionInfo;
                long servicePartitionKey = partitionInfo.LowKey;
                DataReceiver receiver = new DataReceiver(configurationSettings, 
                                                         servicePartitionKey, 
                                                         this.Context,
                                                         offsetDictionary,
                                                         epochDictionary,
                                                         this.StateManager);
                ServiceEventSource.Current.ServiceMessage(this.Context, "Start to Receive...");
                await receiver.Receive();
                await Task.Delay(1000000);
            }
        }
    }
}
