using System;
using System.Collections.Generic;
using System.Linq;
using TradingLibrary;

namespace TestStrategy
{
    public class YenSquaredDemo : Strategy
    {
        [StrategyParameter("Period1", 5, 1, 20)]
        public int Period1 { get; set; }

        [StrategyParameter("Period2", 16, 1, 20)]
        public int Period2 { get; set; }

        [StrategyParameter("JumpFactor", 1.7, 0, 20)]
        public double JumpFactor { get; set; }

        private double size = 0.01;

        public void OnInit()
        {
            //set the asset details filename
            AssetDetailsFile = System.IO.Path.Combine("data_files", "AssetsDemo.csv");

            //Add in the 60 min timeframe
            RequiredData.Add(60, 211);

            //select the assets to trade
            TradeAssetList.AddRange(new string[] { "EURUSD", "EURJPY"});


            //Only take trade if spread is less than 4 pips
            SpreadFilter = 4;

            MinBars = 211;

            Cpus = 12;
            ReduceCorrelatedParams = new ReduceCorrelatedParams(80, 0.75, 1.0);
            ReduceByRankParams = new ReduceByRankParams(110, 1.0);

            TradeDataLabels = new string[] { "Period1", "Period2", "JumpFactor", "jumpSma", "volatilityLogMA12", "volumeLogMA12", "atrNow", "atrTotal", "lastMove", "bbRange" };

            OptimiseParameters.Add(new OptimiseParameter("Period1", 3, 5, 1));
            OptimiseParameters.Add(new OptimiseParameter("Period2", 14, 18, 1));
            OptimiseParameters.Add(new OptimiseParameter("JumpFactor", 1.2, 1.8, 0.1));

            //buildpythonfeatures 60 USDJPY C:\ForexData\Assets.csv C:\ForexData\ShareData\[ASSET]_m60_Share_f.bin ATR(3,close,high,low);ATR(4,close,high,low);ATR(5,close,high,low);ATR(100,close,high,low);VOLATILITY_LOG_MA(12,high,low);VOLUME_LOG_MA(12,volume);BBANDS(20,1.8,1,close);BBANDS(20,1.8,2,close)
            /*ExternalFeatures.Add(new ExternalFeatureData(60, @"C:\\ForexData\\ShareData\\[ASSET]_m60_Share_f.bin", 
                new string[] { "ATR(3, close, high, low)", "ATR(4, close, high, low)", "ATR(5, close, high, low)", "ATR(100, close, high, low)",
                        "VOLATILITY_LOG_MA(12, high, low)", "VOLUME_LOG_MA(12, volume)",
                        "BBANDS(20, 1.8, 1, close)", "BBANDS(20, 1.8, 2, close)" }));
                        */
            ExternalFeatures.Add(new ExternalFeatureData(60, System.IO.Path.Combine(new string[] {"data_files", "share_data", "[ASSET]_m60_Share_f.bin" }),
               new string[] { "ATR_3", "ATR_4", "ATR_5", "ATR_100",
                        "VOLATILITY_LOG_MA_12", "VOLUME_LOG_MA_12",
                        "BBANDS_UP", "BBANDS_LOW" }));
        }

        public void OnBar()
        {

            double atrNow = StandardIndicators.ATR(Datasets[60], Period1);
            double atrTotal = StandardIndicators.ATR(Datasets[60], 100);
            double? volatilityLogMA12 = GetData(Asset.Name, Timeframe, "VOLATILITY_LOG_MA_12");
            double? volumeLogMA12 = GetData(Asset.Name, Timeframe, "VOLUME_LOG_MA_12");
            double? bbandsUpper = GetData(Asset.Name, Timeframe, "BBANDS_UP");
            double? bbandsLower = GetData(Asset.Name, Timeframe, "BBANDS_LOW");

            double lastMove = Math.Abs(Datasets[60][Period1 + 1].AskClose - Datasets[60][1].AskClose) / Asset.Pip;
            double? bbRange = (bbandsUpper - bbandsLower) / Asset.Pip;

            double stop = 75 * Asset.Pip;

            double jump = (Datasets[60][Period1 + 1].AskClose - Datasets[60][Period2 + 1].AskClose) / Datasets[60][Period1 + 1].AskClose;
            PushToSeries("jump", Math.Abs(jump));
            PushToSeries("jumpOffset", GetSeries("jump")[Period2]);
            double jumpSma = StandardIndicators.SMA(GetSeries("jump"), 100);

            if (OpenTradeCount > 0 && (Ask > bbandsUpper || Ask < bbandsLower))
            {
                CloseAllTrades(CurrentBar, Asset.Name);
            }

            /*
             * //Diagnostic Stuff here
            if (CurrentBar.OpenTime <= new DateTime(2019, 02, 08) || CurrentBar.OpenTime >= new DateTime(2019, 02, 14))
                return;

            Console.WriteLine(CurrentBar.OpenTime.AddHours(1).ToString("yy-MM-dd HH:mm") + ":" + 
                Ask + ", atrnow:" + atrNow + ", atrtot:" + atrTotal + ", " +
                ", bU:" + bbandsUpper + ", bL" + bbandsLower + ", " +
                + jump + " " + jump_sma* jump_factor);
            */

            //if (OpenTradeCount == 0 && (lastMove < Asset.Pip * 10) && (volatilityLogMA12 > -0.1 || (volumeLogMA12 >= 0.96 && volumeLogMA12 <= 0.99)))
            if (OpenTradeCount == 0 && lastMove < 12.7 && (volumeLogMA12 >= 0.95 && volumeLogMA12 <= 0.98) && (bbRange > 81.92 && bbRange < 214) && CurrentBar.OpenTime.AddMinutes(60).Hour < 10)
            //if (OpenTradeCount == 0)
            {
                double tradeSize = size;// + (ClosedTrades.Sum(x=> x.Profit) / 100000);

                //if (volatilityLogMA12 > -0.1 && volumeLogMA12 >= 0.96 && volumeLogMA12 <= 0.99)
                // tradeSize = size * 2;

                if (atrNow < (atrTotal * 0.8) && jump > jumpSma * JumpFactor && (Ask < bbandsUpper - Asset.Pip * 5))
                {
                    Trade trade = ExecuteTrade(Trade.TradeDirection.LONG, tradeSize, stop, 0);
                    //Tag the trade with know values at the time of trade execution to use for data relationship analysis
                    trade.TradeData = new double?[] { Period1, Period2, JumpFactor, jumpSma, volatilityLogMA12, volumeLogMA12, atrNow, atrTotal, lastMove, bbRange };
                }

                if (atrNow < (atrTotal * 0.8) && jump < -(jumpSma * JumpFactor) && (Ask > bbandsLower + Asset.Pip * 5))
                {
                    Trade trade = ExecuteTrade(Trade.TradeDirection.SHORT, tradeSize, stop, 0);
                    //Tag the trade with know values at the time of trade execution to use for data relationship analysis
                    trade.TradeData = new double?[] { Period1, Period2, JumpFactor, jumpSma, volatilityLogMA12, volumeLogMA12, atrNow, atrTotal, lastMove, bbRange };
                }

            }
        }
    }
}

