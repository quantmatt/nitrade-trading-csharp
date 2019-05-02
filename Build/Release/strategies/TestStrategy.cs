using System;
using TradingLibrary;

namespace TestStrategy
{
    public class TestStrategy : Strategy
    {
        public TestStrategy()
        {

        }

        public void OnBar()
        {
            int barIndex = BarIndices[Timeframe];

            //barIndex of 0 is current incomplete bar, barIndex of 1 is last closed bar
            //Need to specify what timeframe to run this code on ie. ignore otehr timeframes
            //because they might be just used for indicator values
            if (Timeframe != 60)
                return;

            double? smaShort = GetData(Asset.Name, Timeframe, "SMA(4)");
            double? smaLong = GetData(Asset.Name, Timeframe, "SMA(20)");

            if (OpenTradeCount == 0)
            {
                if (smaShort > smaLong)
                {
                    //Open Trade - this trade will be executed on the next bar
                    ExecuteTrade(Trade.TradeDirection.SHORT, 0.01, 0.005, 0.005);
                }
                else if (smaShort < smaLong)
                {
                    //Open Trade - this trade will be executed on the next bar
                    ExecuteTrade(Trade.TradeDirection.LONG, 0.01, 0.005, 0.005);
                }
            }
        }
    }
}
