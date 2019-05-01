using System;
using System.Collections.Generic;
using System.Threading;

namespace TradingLibrary
{
    public class BackTestTask
    {
        public Asset Asset { get; set; }
        public Strategy Strategy { get; set; }
            
        public BackTestTask(Asset asset, Strategy strategy)
        {
            Asset = asset;
            Strategy = strategy;
        }
    }
}
