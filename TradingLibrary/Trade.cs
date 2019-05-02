using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    [Serializable()]
    public class Trade
    {
        public enum TradeDirection { LONG, SHORT };
        public enum TradeStatus { OPEN_SENT, OPEN_ACCEPTED, OPEN_FILLED, OPEN_FILLED_STOP_TARGET, CLOSE_SENT, CLOSE_ACCEPTED, CLOSE_FILLED, CLOSE_FILLED_STOP_TARGET}
        public enum ExitType { Strategy, StopLoss, TakeProfit, Open };

        public DateTime OpenTime { get; set; }
        public DateTime ?CloseTime { get; set; }
        public DateTime ?OrderRequestTime { get; set; }
        public DateTime BarTime { get; set; }
        public double OpenLevel { get; set; }
        public double CloseLevel { get; set; }
        public double StopPoints { get; set; }
        public double TakeProfitPoints { get; set; }
        public double StopLevel { get; set; }
        public double TakeProfitLevel { get; set; }
        public TradeDirection Direction { get; set; }
        public TradeStatus Status { get; set; }
        public double SpreadPoints { get; set; }
        public double SpreadCost { get; set; }
        public double OpenSlippagePoints { get; set; }
        public double CloseSlippagePoints { get; set; }
        public double Commission { get; set; }
        public string Asset { get; set; }
        public string Strategy { get; set; }
        public ExitType Exit { get; set; }
        public double Profit { get; set; }
        public long TradeID { get; set; }
        public double OpenSlippageTimeFromLocal { get; set; }
        public double OpenSlippageTimeFromBroker { get; set; }
        public double OpenSlippageTimeFromBarStart { get; set; }
        public double CloseSlippageTimeFromLocal { get; set; }
        public double CloseSlippageTimeFromBroker { get; set; }
        public double CloseSlippageTimeFromBarStart { get; set; }

        public bool Skipped { get; set; }
   
        public string Comment { get; set; }
        public string ClientMsgId { get; set; }

        public double Size { get; set; }

        public double?[] TradeData { get; set; }

        public bool ExecuteOnNextBar { get; set; }
        public bool CloseOnNextBar { get; set; }



        public double PointChange
        {
            get
            {
                if(Direction == TradeDirection.LONG)
                    return (CloseLevel - OpenLevel);
                else
                    return (OpenLevel - CloseLevel);
            }
        }
        public Trade()
        {
            Exit = ExitType.Open;
        }

        public Trade(Trade trade)
        {
            
            OpenTime = trade.OpenTime;
            CloseTime = trade.CloseTime;
            OrderRequestTime = trade.OrderRequestTime;
            OpenLevel = trade.OpenLevel;
            CloseLevel = trade.CloseLevel;
            StopPoints = trade.StopPoints;
            TakeProfitPoints = trade.TakeProfitPoints;
            StopLevel = trade.StopLevel;
            TakeProfitLevel = trade.TakeProfitLevel;
            Direction = trade.Direction;
            Status = trade.Status;
            SpreadPoints = trade.SpreadPoints;
            SpreadCost = trade.SpreadCost;
            OpenSlippagePoints = trade.OpenSlippagePoints;
            CloseSlippagePoints = trade.CloseSlippagePoints;
            Commission = trade.Commission;
            Asset = trade.Asset;
            Strategy = trade.Strategy;
            Exit = trade.Exit;
            Profit = trade.Profit;
            TradeID = trade.TradeID;
            Skipped = trade.Skipped;
            Comment = trade.Comment;
            ClientMsgId = trade.ClientMsgId;
            Size = trade.Size;
            TradeData = trade.TradeData;
            OpenSlippageTimeFromBarStart = trade.OpenSlippageTimeFromBarStart;
            OpenSlippageTimeFromBroker = trade.OpenSlippageTimeFromBroker;
            OpenSlippageTimeFromLocal = trade.OpenSlippageTimeFromLocal;
            CloseSlippageTimeFromBarStart = trade.CloseSlippageTimeFromBarStart;
            CloseSlippageTimeFromBroker = trade.CloseSlippageTimeFromBroker;
            CloseSlippageTimeFromLocal = trade.CloseSlippageTimeFromLocal;
            BarTime = trade.BarTime;

        }

        public Trade(string asset, TradeDirection direction, double size, double stopPoints = 0, double takeProfitPoints = 0, string comment = null)
        {
            Exit = ExitType.Open;
            Asset = asset;
            Direction = direction;
            ExecuteOnNextBar = true;
            Size = size;
            StopPoints = stopPoints;
            TakeProfitPoints = takeProfitPoints;
            Comment = comment;

            TradeData = null;
        }

        public Trade(DateTime openTime, double openLevel)
        {
            Exit = ExitType.Open;
            OpenLevel = openLevel;
            OpenTime = openTime;
        }

        public bool StopHit(double bidlow, double askhigh)
        {
            //Stop not set
            if (StopPoints == 0)
                return false;
            
            if(Direction == TradeDirection.LONG)
            {
                if (bidlow <= StopLevel)
                    return true;
            }
            else
            {
                if (askhigh >= StopLevel)
                    return true;
            }

            return false;
        }

        public bool TakeProfitHit(double asklow, double bidhigh)
        {
            //Stop not set
            if (TakeProfitPoints == 0)
                return false;

            if (Direction == TradeDirection.LONG)
            {
                if (bidhigh >= TakeProfitLevel)
                    return true;
            }
            else
            {
                if (asklow <= TakeProfitLevel)
                    return true;
            }

            return false;
        }

        public static string GetCsvHeaders(string[] tradeDataLabels = null)
        {
            
            string header = "Asset,OpenTime,CloseTime,OpenLevel,CloseLevel,Profit,Size,SpreadPoints,SpreadCost,Commission";

            if(tradeDataLabels != null)
            {
                foreach (string label in tradeDataLabels)
                    header += "," + label;
            }

            return header;
            
        }

        public string TradeOpenDescription()
        {
            return OpenTime.ToString("yyyy-MM-dd HH:mm:ss") + " - " + TradeID + " " + Asset + " " + Size + " lots at " + OpenLevel;
        }

        public override string ToString()
        {
            string tradeString = "";
            if(CloseTime != null)
                tradeString = Asset + "," + OpenTime.ToString("yyyy-MM-dd HH:mm:ss") + "," + ((DateTime)CloseTime).ToString("yyyy-MM-dd HH:mm:ss") + "," + OpenLevel + "," +
                    CloseLevel + "," + Profit + "," + Size + "," + SpreadPoints + "," + SpreadCost + "," + Commission;
            else
                tradeString = Asset + "," + OpenTime.ToString("yyyy-MM-dd HH:mm:ss") + ",," + OpenLevel + "," +
                    CloseLevel + "," + Profit + "," + Size + "," + SpreadPoints + "," + Commission;

            //add in the trade data if any was tagged to the trade open - used for post analysis
            if (TradeData != null)
            {
                foreach (double val in TradeData)
                    tradeString += "," + val;         
            }

            return tradeString;
        }


    }
}
