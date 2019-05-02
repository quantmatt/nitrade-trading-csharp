using System;
using TradingLibrary;

namespace TestStrategy
{
    public class TestTrader : Strategy
    {

        private double size = 0.01;

        public void OnInit()
        {
            //set the asset details filename
            AssetDetailsFile = System.IO.Path.Combine("data_files", "AssetsDemo.csv");

            //Add in the 1 min timeframe
            RequiredData.Add(1, 211);

            //select the assets to trade
            TradeAssetList.AddRange(new string[] { "EURUSD", "EURJPY",});
            
            //Only take trade if spread is less than 4 pips
            SpreadFilter = 4;

            MinBars = 211;


            TradeDataLabels = new string[] { "ATR_4", "ATR_100"};


        }

        public void OnBar()
        {

            double atr4 = StandardIndicators.ATR(Datasets[1], 4);
            double atr100= StandardIndicators.ATR(Datasets[1], 100);
            

            Random rand = new Random();
            bool action = true;// rand.Next(2) > 0;

            if (action)
            {


                if (OpenTradeCount > 0)
                {

                    //CloseAllTrades(CurrentBar, Asset.Name);
                }
                else
                {
                    double stop = Asset.Pip * 3;
                    Trade trade = ExecuteTrade(Trade.TradeDirection.LONG, 0.01, stop, stop);
                    //Tag the trade with know values at the time of trade execution to use for data relationship analysis

                    trade.TradeData = new double?[] { atr4, atr100 };
                }
            }

            
        }
    }
}