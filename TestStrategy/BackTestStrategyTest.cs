using System;
using System.Collections.Generic;
using System.Text;
using TradingLibrary;

namespace TestStrategy
{
    public class BackTestStrategyTest : Strategy
    {

        public void OnInit()
        {
            //set the asset details filename
            AssetDetailsFile = @"C:\ForexData\Assets.csv";

            RequiredData.Add(60, 200);

            TradeAssetList.Add("TEST1");

            MinBars = 30;

            OptimiseParameters.Add(new OptimiseParameter("Period1", 3, 5, 1));
            OptimiseParameters.Add(new OptimiseParameter("Period2", 15, 17, 1));
            OptimiseParameters.Add(new OptimiseParameter("JumpFactor", 1.3, 1.6, 0.1));

            ExternalFeatures.Add(new ExternalFeatureData(60, @"C:\\ForexData\\ShareData\\TEST1_m60_test_strategy_f.bin",
                new string[] { "SMA_4", "SMA_20" }));
        }

        public void OnBar()
        {
           

            double? smaShort = GetData(Asset.Name, Timeframe, "SMA_4");
            double? smaLong = GetData(Asset.Name, Timeframe, "SMA_20");

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
