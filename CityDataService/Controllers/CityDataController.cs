using System.Fabric;
using System.Threading.Tasks;
using DistrictCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace CityDataService.Controllers
{
    [Produces("application/json")]
    [Route("api/CityData")]
    public class CityDataController : Controller
    {
        private IReliableStateManager stateManager;
        private const string CityReports = "CityReports";
        public CityDataController(IReliableStateManager stateManager)
        {
            this.stateManager = stateManager;
        }

        [HttpPost("{distName}")]
        public async Task<IActionResult> Post(string distName, [FromBody] DistrictReport distReport)
        {
            try
            {
                IReliableDictionary<string, DistrictReport> distReports =
                   await stateManager.GetOrAddAsync<IReliableDictionary<string, DistrictReport>>(CityReports);

                using (ITransaction tx = this.stateManager.CreateTransaction())
                {
                    await distReports.SetAsync(tx, distName, distReport);
                    await tx.CommitAsync();
                }

                return this.Ok();
            }
            catch (FabricNotPrimaryException ex)
            {
                return new ContentResult { StatusCode = 410, Content = "The primary replica has moved. Please re-resolve the service." };
            }
            catch (FabricException ex)
            {
                return new ContentResult { StatusCode = 503, Content = "The service was unable to process the request. Please try again." };
            }
        }

        [HttpGet("{distName}")]
        public async Task<IActionResult> Get(string distName)
        {
            IReliableDictionary<string, DistrictReport> dictionary =
                await stateManager.GetOrAddAsync<IReliableDictionary<string, DistrictReport>>(CityReports);

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                ConditionalValue<DistrictReport> reports = await dictionary.TryGetValueAsync(tx, distName);

                if (reports.HasValue)
                {
                    return this.Ok(reports.Value);
                }

                return this.NotFound();
            }
        }

        
    }
}