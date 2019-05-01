using System;
using System.Collections.Generic;
using System.Text;

namespace cTraderAPI
{
    public class DataDownloaderEventArgs
    {
        public OpenAPIBarData BarData { get; set; }
        public int Timeframe { get; set; }
        public string AssetName { get; set; }
        public object PostRunStrategies { get; set; }

        public DataDownloaderEventArgs() { }
    }
}
