using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Actors.Client;
using DistrictsActorService.Interfaces;
using DistrictCommon;
using Microsoft.ServiceFabric.Data;
using System.Fabric;
using System.Fabric.Description;
using System.Collections.ObjectModel;
using Microsoft.ServiceFabric.Services.Client;
using System.Fabric.Query;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;

namespace DistrictsActorService
{
    /// <remarks>
    /// This class represents an actor.
    /// Every ActorID maps to an instance of this class.
    /// The StatePersistence attribute determines persistence and replication of actor state:
    ///  - Persisted: State is written to disk and replicated.
    ///  - Volatile: State is kept in memory only and replicated.
    ///  - None: State is kept in memory only and not replicated.
    /// </remarks>
    [StatePersistence(StatePersistence.Persisted)]
    internal class DistrictsActor : Actor, IDistrictsActor, IRemindable
    {
        private const string TempRecords = "TempRecords";
        private const string DistrictName = "DistrictName";
        private const string SendDistrictDataReminder = "SendDistrictDataReminder";
      

        public DistrictsActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {

        }
       
        public async Task ProcessDistData(DistrictData districtData)
        {
            ConditionalValue<string> distName = await this.StateManager.TryGetStateAsync<string>(DistrictName);

            if (!distName.HasValue) await this.StateManager.SetStateAsync(DistrictName, districtData.DistrictName);

            List<string> tempRecords = await this.StateManager.GetStateAsync<List<string>>(TempRecords);
            tempRecords.Add(districtData.Temperature);
            await this.StateManager.SetStateAsync(TempRecords, tempRecords);

            await SendDistrictData();
            
        }

        private async Task RegisterReminder()
        {
            await this.RegisterReminderAsync(SendDistrictDataReminder, null, TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(1000));
        }

        async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            switch (reminderName)
            {
                case SendDistrictDataReminder:
                    await this.SendDistrictData();
                    break;
                default:
                    ActorEventSource.Current.Message(string.Format("Reminder {0} is not implemented on BandActor.", reminderName));
                    break;
            }
        }

        private async Task SendDistrictData()
        {
            ConfigurationSettings configurationSettings = FabricRuntime.GetActivationContext().GetConfigurationPackageObject("Config").Settings;
            KeyedCollection<string, ConfigurationProperty> serviceParameters = configurationSettings.Sections["DistrictsActorServiceReplicatorConfig"].Parameters;
            string cityServiceName = serviceParameters["CityServiceName"].Value;
            string applicationName = FabricRuntime.GetActivationContext().ApplicationName;
            string reverseProxyPort = serviceParameters["ReverseProxyPort"].Value;
            string serviceUri = string.Format("{0}/{1}", applicationName, cityServiceName);
            string distName = await this.StateManager.GetStateAsync<string>(DistrictName);
            List<string> tempList = await this.StateManager.GetStateAsync<List<string>>(TempRecords);
            long partitionKey = FnvHash.Hash(distName);
            //long partitionKey = -9223372036854775808;
            double averageTempData = GetAverageTempData(tempList);
            //ServicePartitionList partitions = await new FabricClient().QueryManager.GetPartitionListAsync(new Uri(serviceUri));

            string proxyUrl =
                    $"http://localhost:{reverseProxyPort}/{serviceUri.Replace("fabric:/", "")}/api/citydata/{distName}?PartitionKind=Int64Range&PartitionKey={partitionKey}";
            ActorEventSource.Current.Message(proxyUrl);
            string payload = $"{{ 'AverageTemp' : '{averageTempData}' }}";
            StringContent postContent = new StringContent(payload, Encoding.UTF8, "application/json");
            postContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response = await new HttpClient().PostAsync(proxyUrl, postContent);
            ActorEventSource.Current.Message(response.ToString());
            ActorEventSource.Current.Message(string.Format("In District Actor id: {0}. District Name: {1}. Temp List: {2}.", 
                                             this.Id,
                                             distName,
                                             tempList.ToString()));
        }


        private double GetAverageTempData(List<string> tempList)
        {
            int count = tempList.Count();
            double total = 0;

            foreach(var tempData in tempList)
            {
                total += Convert.ToDouble(tempData);
            }

            return Math.Round(total / count, 1);
        }
        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected async override Task OnActivateAsync()
        {
            ActorEventSource.Current.Message($"Actor activated. {this.Id}");
            List<string> tempList = new List<string>();
            await this.StateManager.TryAddStateAsync<List<string>>(TempRecords, tempList);
            await this.RegisterReminder();
        }

        protected async override Task OnDeactivateAsync()
        {
            await this.UnregisterReminderAsync(this.GetReminder(SendDistrictDataReminder));
        }


    }
}
