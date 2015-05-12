using System;
using PervasiveDigital.Net.Azure.MobileService;
using Microsoft.SPOT;

namespace AzureTest
{
    internal class MeasurementRow : IMobileServiceEntity
    {
        public string where { get; set; }
        public string what { get; set; }
        public float value { get; set; }
        public string uom { get; set; }

        public DateTime DateTimeStamp { get; set; }
        public int Id { get; set; }

        public string ToJson()
        {
            return "{ \"When\" : \"" + DateTimeStamp + "\", \"Where\" : \"" + where +
                   "\", \"What\" : \"" + what + "\", \"Value\" : \"" + value + "\", \"UoM\" : \"" + uom + "\"}";
        }

        public void Parse(string json)
        {
            throw new NotImplementedException();
        }
    }
}
