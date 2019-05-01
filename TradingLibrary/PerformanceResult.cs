using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingLibrary
{
    //a class just to hold the variables of performance statistics
    public class PerformanceResult
    {
        public string Description { get; set; }
        public int TradeCount { get; set; }
        public double ProfitFactor { get; set; }
        public double TotalProfit { get; set; }
        public double SpreadCost { get; set; }
        public double WinPercent { get; set; }

        private static int columnWidth = 12;
        private static int nameColumnWidth = 20;

        public PerformanceResult() { }

        public static string LineHeader()
        {
            return "Description".PadRight(nameColumnWidth) + "|" + "Trades".PadRight(columnWidth) + "|" + "PF".PadRight(columnWidth) + "|" +
                "Profit".PadRight(columnWidth) + "|" + "Spread Cost".PadRight(columnWidth) + "|" + "Win Percent".PadRight(columnWidth);
        }

        public string DisplayLine()
        {
            
            return Description.PadRight(nameColumnWidth) + "|" + TradeCount.ToString().PadRight(columnWidth) + "|" + ProfitFactor.ToString("0.00").PadRight(columnWidth) + "|$" +
                TotalProfit.ToString("0.00").PadRight(columnWidth) + "|$" + SpreadCost.ToString("0.00").PadRight(columnWidth) + "|" + 
                WinPercent.ToString("0.00").PadRight(columnWidth) + "%";
        }

        public static double CalculateProfitFactor(Trade[] trades)
        {
            double wins = trades.Where(x => x.Profit >= 0).Sum(x => x.Profit);
            double losses = trades.Where(x => x.Profit < 0).Sum(x => x.Profit);

            //more sensical value than infinity
            if (losses == 0)
                return 99;

            double profitFactor = wins / -losses;

            return profitFactor;
        }

        public static double CalculateWinPercent(Trade[] trades)
        {           
            double winPercent = (double)trades.Where(x => x.Profit >= 0).Count() / (double)trades.Count() * 100;
            return winPercent;
        }

    }
}
