using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DistrictCommon
{
    [DataContract]
    public class DistrictReport
    {
        public DistrictReport(string distName, double averageTemp, double lowTemp, double highTemp)
        {
            this.DistrictName = distName;
            this.AverageTemp = averageTemp;
            this.LowTemp = lowTemp;
            this.HighTemp = highTemp;
        }
        [DataMember]
        public string DistrictName { get; set; }
        [DataMember]
        public double AverageTemp { get; set; }
        [DataMember]
        public double LowTemp { get; set; }
        [DataMember]
        public double HighTemp { get; set; }
    }
}
