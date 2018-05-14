using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Fabric;
using System.Fabric.Description;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DistrictCommon;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CityClusterApiGateway.Controllers
{
    [Produces("application/json")]
    [Route("api/City")]
    public class CityController : Controller
    {
     

        // GET: api/City
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

       
        [HttpGet("{distName}")]
        public async Task<IActionResult> Get(string distName)
        {
            ConfigurationSettings settings = FabricRuntime.GetActivationContext().GetConfigurationPackageObject("Config").Settings;
            KeyedCollection<string, ConfigurationProperty> serviceParameters = settings.Sections["MyConfigSection"].Parameters;
            string reverseProxyPort = serviceParameters["ReverseProxyPort"].Value;
            string dataServiceName = serviceParameters["CityServiceName"].Value;
            string serviceUri = string.Format($"{FabricRuntime.GetActivationContext().ApplicationName}/{dataServiceName}");
            long partitionKey = FnvHash.Hash(distName);
            string proxyUrl =
            $"http://localhost:{reverseProxyPort}/{serviceUri.Replace("fabric:/", "")}/api/citydata/{distName}?PartitionKind=Int64Range&PartitionKey={partitionKey}";
            ServiceEventSource.Current.Message(proxyUrl);

            HttpResponseMessage response = await new HttpClient().GetAsync(proxyUrl);

            if(response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return this.StatusCode((int)response.StatusCode);
            }

            string distData = await response.Content.ReadAsStringAsync();
            var districtReport = JsonConvert.DeserializeObject<DistrictReport>(distData);

            return this.Json(districtReport);
        }
    }
        
        
    
}
