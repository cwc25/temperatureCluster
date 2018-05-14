using System;
using System.Collections.Generic;
using System.Text;

namespace TemperatureCommon
{
    using System.Runtime.Serialization;
    [DataContract]
    public class DistrictData
    {
        public DistrictData(string districtName, string temperature)
        {
            this.DistrictName = districtName;
            this.Temperature = temperature;
        }

        [DataMember]
        public string DistrictName { get; set; }
        
        [DataMember]
        public string Temperature { get; set; }
    }
}
