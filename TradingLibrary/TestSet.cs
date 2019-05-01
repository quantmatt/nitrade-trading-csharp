using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    [Serializable()]
    public class TestSet
    {
        public Trade[] Trades { get; set; }
        public string Asset { get; set; }
        public string Description { get; set; }

        public TestSet()
        {

        }
        
        public TestSet(string asset, string description, Trade[] trades)
        {
            Asset = asset;
            Description = description;
            Trades = trades;
               
        }

    }
}
