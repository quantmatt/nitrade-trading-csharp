using System;
using System.Collections.Generic;
using System.Text;
using TradingLibrary;

namespace TestStrategy
{
    public class BackTestTimeFrameTest : Strategy
    {
        private Bar expectedBar1_1H;

        public void OnInit()
        {
            //set the asset details filename
            AssetDetailsFile = @"C:\ForexData\Assets.csv";

            expectedBar1_1H = new Bar();
            expectedBar1_1H.OpenTime = DateTime.ParseExact("2017-09-15 00:00:00", "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            expectedBar1_1H.BidOpen = 1.19207f;
            expectedBar1_1H.BidClose = 1.19075f;
            expectedBar1_1H.BidHigh = 1.19232f;
            expectedBar1_1H.BidLow = 1.19067f;
            expectedBar1_1H.AskOpen = 1.19219f;
            expectedBar1_1H.AskClose = 1.19088f;
            expectedBar1_1H.AskHigh = 1.19244f;
            expectedBar1_1H.AskLow = 1.1908f;
            expectedBar1_1H.Volume = 9217;

            RequiredData.Add(60, 200);

            TradeAssetList.Add("TEST1");
        }

        public void OnBar()
        {
            if (Timeframe == 60)
            {
                Bar bar = CurrentBar;
                if (bar != null && bar.OpenTime == expectedBar1_1H.OpenTime)
                {
                    //just create a bunch of trades with the data for expected and actual so these can be checked in the UnitTests
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.BidOpen, expectedBar1_1H.BidOpen, "BidOpen_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.BidClose, expectedBar1_1H.BidClose, "BidClose_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.BidHigh, expectedBar1_1H.BidHigh, "BidHigh_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.BidLow, expectedBar1_1H.BidLow, "BidLow_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.AskOpen, expectedBar1_1H.AskOpen, "AskOpen_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.AskClose, expectedBar1_1H.AskClose, "AskClose_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.AskHigh, expectedBar1_1H.AskHigh, "AskHigh_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.AskLow, expectedBar1_1H.AskLow, "AskLow_60_first");
                    ExecuteTrade(Trade.TradeDirection.LONG, 0, bar.AskLow, expectedBar1_1H.AskLow, "Volume_60_first");
                }
            }
        }
    }
}
