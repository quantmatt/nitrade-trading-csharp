using System;
using System.Collections.Generic;
using System.Linq;
/*
namespace TradingLibrary
{
    [Serializable()]
    public class PerformanceReport
    {
        

        public PerformanceReport[] IndividualReports { get; set; }
        public OptimisePerformanceRank RankResults { get; set; }

        public string[] SetupDescription { get; set; }

        public Trade[] Trades { get; set; }
        public string Asset { get; set; }
        public string Description { get; set; }
        public double ProfitFactor { get; private set; }
        public double TotalProfit { get; private set; }
        public double TotalTrades { get; private set; }
        public double WinPercent { get; private set; }
        public double SpreadCost { get; private set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime TrainDate { get; set; }

        public PerformanceReport(Trade[] trades)
        {
            Trades = trades;            
        }
               
        public double CalculateProfitFactor(TradeSet set = TradeSet.All, DateTime ? start = null, DateTime? end = null)
        {
            Trade[] trades = Trades;

            if (start != null && end != null)
                trades = Trades.Where(x => x.CloseTime > start && x.CloseTime <= end).ToArray();
            else if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > TrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= TrainDate).ToArray();

            double wins = trades.Where(x=> x.Profit >=0).Sum(x => x.Profit);
            double losses = trades.Where(x => x.Profit < 0).Sum(x => x.Profit);

            //more sensical value than infinity
            if (losses == 0)
                return 99;

            ProfitFactor = wins / -losses;

            return ProfitFactor;
        }

        public double CalculateTotalProfit(TradeSet set = TradeSet.All)
        {
            Trade[] trades = Trades;
            if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > TrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= TrainDate).ToArray();

            TotalProfit = trades.Sum(x => x.Profit);
            return TotalProfit;
        }

        public double CalculateTotalTrades(TradeSet set = TradeSet.All)
        {
            Trade[] trades = Trades;
            if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > TrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= TrainDate).ToArray();

            TotalTrades = trades.Count();
            return TotalTrades;
        }

        public double CalculateWinPercent(TradeSet set = TradeSet.All)
        {
            Trade[] trades = Trades;
            if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > TrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= TrainDate).ToArray();

            WinPercent = (double)trades.Where(x => x.Profit >= 0).Count() / (double)trades.Count() * 100;
            return WinPercent;
        }

        public double CalculateSpreadCost(TradeSet set = TradeSet.All)
        {
            Trade[] trades = Trades;
            if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > TrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= TrainDate).ToArray();

            SpreadCost = trades.Sum(x => x.SpreadCost);
            return SpreadCost;
        }

        public string QuickSummary(TradeSet set = TradeSet.All)

        {
            string val = "Total profit: $" + CalculateTotalProfit(set).ToString("#.##") + "\n" +
                "Profit Factor: " + CalculateProfitFactor(set).ToString("#.##") + "\n" +
                "Trade Count: " + CalculateTotalTrades(set) + "\n" +
                "Win Percent: " + CalculateWinPercent(set).ToString("#.##") + "%\n" +
                "Spread Cost: $" + CalculateSpreadCost(set).ToString("#.##");

            return val;

        }

        public string SummaryLine()
        {
            string str = Description.PadRight(20) + " | " + ProfitFactor.ToString("0.00").PadRight(10) + " | $" + TotalProfit.ToString("0.00").PadRight(10) + " | " + TotalTrades.ToString().PadRight(6);
            return str;
        }

        public void ToCsv(string filename, TradeSet set = TradeSet.All)
        {
            Trade[] trades = Trades;
            if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > TrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= TrainDate).ToArray();

            List<string> tradeStrings = new List<string>();

            tradeStrings.Add(Trade.CsvHeaders);

            foreach (Trade trade in trades)
                tradeStrings.Add(trade.ToString());

            System.IO.File.WriteAllLines(filename, tradeStrings);
        }

        public static PerformanceReport MergeReports(PerformanceReport[] reports)
        {
            List<Trade> trades = new List<Trade>();
            foreach (PerformanceReport pr in reports)
                trades.AddRange(pr.Trades);

            PerformanceReport finalpr = new PerformanceReport(trades.ToArray());
            finalpr.StartDate = reports.FirstOrDefault().StartDate;
            finalpr.EndDate = reports.FirstOrDefault().EndDate;
            finalpr.TrainDate = reports.FirstOrDefault().TrainDate;

            return finalpr;

        }

        
    }
}
*/