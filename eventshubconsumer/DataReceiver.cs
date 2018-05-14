using DistrictCommon;
using DistrictsActorService.Interfaces;
using Microsoft.Azure.EventHubs;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Temp


namespace eventshubconsumer
{
    public class DataReceiver
    {
        private EventHubClient eventHubClient;
        private string servicePartitionKey;
        private StatefulServiceContext serviceContext;
        private IReliableDictionary<string, string> offsetDictionary;
        private IReliableDictionary<string, long> epochDictionary;
        private IReliableStateManager stateManager;
        private Uri DistrictsActorServiceUri;

        public DataReceiver(ConfigurationSettings configSettings, 
                            long servicePartitionKey, 
                            StatefulServiceContext serviceContext,
                            IReliableDictionary<string, string> offsetDictionary,
                            IReliableDictionary<string, long> epochDictionary,
                            IReliableStateManager stateManager)
        {
            KeyedCollection<string, ConfigurationProperty> serviceParameters = configSettings.Sections["TemperatureCluster.EventsHubConsumer.Settings"].Parameters;
            this.eventHubClient = EventHubClient.CreateFromConnectionString(serviceParameters["EhConnectionString"].Value);
            this.DistrictsActorServiceUri = new Uri(string.Format("{0}/{1}",
                                            FabricRuntime.GetActivationContext().ApplicationName,
                                            serviceParameters["DistrictsActorService"].Value));
            this.servicePartitionKey = Convert.ToString(servicePartitionKey);
            this.serviceContext = serviceContext;
            this.offsetDictionary = offsetDictionary;
            this.epochDictionary = epochDictionary;
            this.stateManager = stateManager;
        }

        public async Task Receive()
        {
            try
            {
                using (ITransaction tx = stateManager.CreateTransaction())
                {
                    ConditionalValue<string> offsetResult = await offsetDictionary.TryGetValueAsync(tx, "offset", LockMode.Default);
                    ConditionalValue<long> epochResult = await epochDictionary.TryGetValueAsync(tx, "epoch", LockMode.Update);
                    long newEpoch = epochResult.HasValue ? epochResult.Value + 1 : 0;
                    await epochDictionary.SetAsync(tx, "epoch", newEpoch);
                    PartitionReceiver partitionReceiver;

                    if (offsetResult.HasValue)
                    {
                        partitionReceiver = eventHubClient.CreateEpochReceiver(PartitionReceiver.DefaultConsumerGroupName,
                                                                                   "3",//Convert.ToString(this.servicePartitionKey),
                                                                                   offsetResult.Value,
                                                                                   newEpoch);
                    }
                    else
                    {
                        partitionReceiver = eventHubClient.CreateEpochReceiver(PartitionReceiver.DefaultConsumerGroupName,
                                                                                   Convert.ToString(this.servicePartitionKey),
                                                                                   "-1",
                                                                                   newEpoch);
                    }

                    ServiceEventSource.Current.ServiceMessage(this.serviceContext, 
                                            $"Partition Id: {servicePartitionKey}; offset: {offsetResult.Value}; Epoch: {newEpoch}");
                    var ehEvents = await partitionReceiver.ReceiveAsync(1);
                    await GenerateActors(ehEvents, tx);

                    await tx.CommitAsync();
                }
            }
            catch(EventHubsException ex)
            {
                ServiceEventSource.Current.ServiceMessage(this.serviceContext, ex.Message);
            }
            catch(Exception ex)
            {
                ServiceEventSource.Current.ServiceMessage(this.serviceContext, ex.Message);
            }


        }

        public async Task GenerateActors(IEnumerable<EventData> ehEvents, ITransaction tx)
        {
            if (ehEvents != null)
            {
                int batchCount = 0;
                List<string> tempRecords = new List<string>();

                foreach (var ehEvent in ehEvents)
                {
                    string message = Encoding.UTF8.GetString(ehEvent.Body.Array);
                    ServiceEventSource.Current.ServiceMessage(this.serviceContext, $"Message Received: {message}");
                    string[] districtData = message.Split(',');
                    string distId = districtData[2];
                    string distName = districtData[0];
                    string distTemp = districtData[1];
                    //create actor for each district
                    ActorId districtId = new ActorId(distId);
                    var distData = new DistrictData(distName, distTemp);
                    IDistrictsActor districtsActor = ActorProxy.Create<IDistrictsActor>(districtId, this.DistrictsActorServiceUri);
                    // districtsActor
                    await districtsActor.ProcessDistData(distData);

                    if (batchCount >= 0)// == 4)
                    {
                        var offset = ehEvent.SystemProperties.Offset;
                        await offsetDictionary.SetAsync(tx, "offset", offset);
                    }
                    batchCount++;
                }


            }
        }
    }
}
