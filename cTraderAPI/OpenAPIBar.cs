using System;
using System.Collections.Generic;
using System.Text;

namespace cTraderAPI
{
    public class OpenAPIBar
    {


        public string Timestamp { get; set; }
        public float High { get; set; }
        public float Low { get; set; }
        public float Open { get; set; }
        public float Close { get; set; }
        public int Volume { get; set; }

        public DateTime OpenTime {
            get
            {
                //From Epoch time
                DateTime dtDateTime = DateTime.FromBinary(Convert.ToInt64(Timestamp) * 10000).AddYears(1969);

                return dtDateTime;

            }
        }

        public OpenAPIBar() { }
    }
}
