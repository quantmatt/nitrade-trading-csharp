using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CSharp;
using System.CodeDom.Compiler;
using System.Reflection;

namespace TradingLibrary
{

    public delegate void StrategyFunction(Strategy self, int timeframe, string assetName);

    public class Strategy
    {

        //allows interaction with the trading ai to send orders ect.
        public ITradingApi TradingApi { get; set; }
        public ITradingApiUser TradingApiUser {get;set;} //the user this strategy belongs to

        private List<Trade> openTrades;
        private List<Trade> closedTrades;
        private List<Trade> pendingTrades;

        public string AssetDetailsFile { get; set; }
        public Dictionary<string, Asset> Assets { get; set; }
        public List<string> TradeAssetList { get; set; }

        //Key is timeframe, bar array is the lookback data for that timeframe
        public Dictionary<int, Bar[]> Datasets { get; set; }

        //key is timeframe, value is current index of the bar for backtesting 
        public Dictionary<int, int> BarIndices { get; set; }

        //Key is timeframe, Dictionary is a list of user defined series with the same lookback as the timeframe
        public Dictionary<int, Dictionary<string, object[]>> Series { get; set; }

        //key is timeframe, containing a precalcualted features object that contains timestamps and a dictionary of values
        public Dictionary<int, PreCalculatedFeatures> PreCalcFeatures { get; set; }

        //required time frames for onBar events
        public Dictionary<int, int> RequiredData = new Dictionary<int, int>();

        //external features eg from python
        //Key is timeframe, Value is string fo semi-colon sepearted feature commands
        public List<ExternalFeatureData> ExternalFeatures;

        public double SpreadFilter { get; set; }
        public bool CloseOnExit { get; set; }
        public int Cpus { get; set; }
        public List<StrategyParameter> Parameters;
        public List<OptimiseParameter> OptimiseParameters;
        public StrategyVariant[] Variants;
        public string[] TradeDataLabels;

        public int Timeframe;
        public Asset Asset;
        public Bar CurrentBar;
        public double Ask;
        public double Bid;
        public int Volume;
        public int CurrentIndex;
        public int MinBars;

        //BackTest setup
        public DateTime? StartDate;
        public DateTime? EndDate;
        public ReduceCorrelatedParams ReduceCorrelatedParams = null;
        public ReduceByRankParams ReduceByRankParams = null;

        public MessageDelegate OnMessage = null;

        public string Description;

        public bool IsTesting;

        public Strategy()
        {
            //Only used for live trading
            TradingApi = null;

            openTrades = new List<Trade>();
            closedTrades = new List<Trade>();
            pendingTrades = new List<Trade>();
            Datasets = new Dictionary<int, Bar[]>();
            BarIndices = new Dictionary<int, int>();

            Assets = new Dictionary<string, Asset>();

            TradeAssetList = new List<string>();

            Parameters = new List<StrategyParameter>();
            OptimiseParameters = new List<OptimiseParameter>();
            TradeDataLabels = null;

            Series = new Dictionary<int, Dictionary<string, object[]>>();
            ExternalFeatures = new List<ExternalFeatureData>();

            IsTesting = true;

            CloseOnExit = true;
            SpreadFilter = 0;
            Cpus = 1;

            MinBars = 1;
        }



        public void InitBarData()
        {
            //add in the required minute bars with a 1 bar lookback
            //If a strategy needs these bars it will be overwritted in the next foreach loop
            Datasets.Add(1, new Bar[5]);
            BarIndices.Add(1, 0);

            foreach (KeyValuePair<int, int> required in RequiredData)
            {
                int timeframe = required.Key;
                int lookback = required.Value;

                //overwrite the minute bar details if the strategy requires this
                if (Datasets.ContainsKey(timeframe))
                {
                    Datasets[timeframe] = new Bar[lookback];
                }
                else
                {
                    Datasets.Add(timeframe, new Bar[lookback]);
                    BarIndices.Add(timeframe, 0);
                }

            }
        }
       
        public void SetParameter(string parameterName, object value)
        {
            PropertyInfo prop = GetType().GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance);
            

            if (prop != null && prop.CanWrite)
                if (Type.GetTypeCode(prop.PropertyType) == TypeCode.Int32)
                    prop.SetValue(this, Convert.ToInt32(value), null);
                else if (Type.GetTypeCode(prop.PropertyType) == TypeCode.Double)
                    prop.SetValue(this, Convert.ToDouble(value), null);

            else
                throw new Exception("Property " + parameterName + " does not exist or can't be set.");
        }

        public void ResetData()
        {
            Datasets = new Dictionary<int, Bar[]>();
            BarIndices = new Dictionary<int, int>();
            openTrades = new List<Trade>();
            closedTrades = new List<Trade>();
        }

        public void PushToSeries(string seriesName, object value)
        {
            //first check if a series dictionary exists for this time frame and create if not
            if (!Series.ContainsKey(Timeframe))
                Series.Add(Timeframe, new Dictionary<string, object[]>());
            //check if this series name already exists for this timeframe otherwsie create it
            if (!Series[Timeframe].ContainsKey(seriesName))
                Series[Timeframe].Add(seriesName, new object[Datasets[Timeframe].Length]);

            //shift the series array
            Array.Copy(Series[Timeframe][seriesName], 0, Series[Timeframe][seriesName], 1, Series[Timeframe][seriesName].Length - 1);

            //set the first element to this value
            Series[Timeframe][seriesName][0] = value;
        }

        public object[] GetSeries(string seriesName)
        {
            if (!Series.ContainsKey(Timeframe))
                throw new Exception("Series " + seriesName + " has not been created");
            if (!Series[Timeframe].ContainsKey(seriesName))
                throw new Exception("Series " + seriesName + " has not been created");

            return Series[Timeframe][seriesName];
        }

        public static Strategy Load(string strategyReference, string strategyPath, string[] additionalDllPaths = null)
        {
            //Compile the .cs file at runtime - this uses a temp dll file that is cleaned up on exit
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateInMemory = true;

            //load in any assemblies required for this strategy
            parameters.ReferencedAssemblies.Add("TradingLibrary.dll");
            if(additionalDllPaths != null)
            {
                foreach(string path in additionalDllPaths)
                    parameters.ReferencedAssemblies.Add(path);
            }

            //make the path suitable for all operating systems
            strategyPath = System.IO.Path.Combine(strategyPath.Split(new char[] { '\\' }));

            //compile and build the strategy object
            string[] code = new string[] { System.IO.File.ReadAllText(strategyPath) };
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);
            var DLL = results.CompiledAssembly;
            Type type = DLL.GetType(strategyReference);
            Strategy strategy = (Strategy)Activator.CreateInstance(type);

            //Call the init function if it has one
            if(type.GetMembers().Where(x=> x.Name == "OnInit").Count() != 0)
                type.InvokeMember("OnInit", BindingFlags.InvokeMethod, null, strategy, null);

            //Get the strategy Paramters
            foreach (MemberInfo memberInfo in type.GetMembers())
            {
                //MemberInfo memberInfo = typeof(member);
                object[] attributes = memberInfo.GetCustomAttributes(true);

                foreach (object attribute in attributes)
                {
                    StrategyParameterAttribute paramAttribute = attribute as StrategyParameterAttribute;

                    if (paramAttribute != null)
                    {
                        Type propType = ((PropertyInfo)memberInfo).PropertyType;
                        strategy.Parameters.Add(new StrategyParameter(paramAttribute.Text, propType, paramAttribute.InitialValue));
                        //To set from optimiser
                        strategy.SetParameter(paramAttribute.Text, paramAttribute.InitialValue);
                    }
                }
            }

            return strategy;           

        }

        public int OpenTradeCount
        {
            get
            {
                return openTrades.Count;
            }
        }

        public Trade[] OpenTrades
        {
            get
            {
                return openTrades.ToArray();
            }
        }

        public Trade[] ClosedTrades
        {
            get
            {
                return closedTrades.ToArray();
            }
        }


        public double? GetData(string assetName, int timeframe, string dataName, int offset = 1)
        {
            if (!Datasets.ContainsKey(timeframe) || !Assets.ContainsKey(assetName) || !Assets[assetName].Data.ContainsKey(timeframe))                
                return null;

            //get the open time of the bar with the passed offset
            Bar bar = Datasets[timeframe][offset];

            if (!Assets[assetName].Data[timeframe].Data.ContainsKey(bar.OpenTime))
                return null;

            //the Data object will have the corresponding data in a dictionary with the key as the bar open time
            //look this up and return it.
            return Assets[assetName].Data[timeframe].Data[bar.OpenTime][dataName];
        }


        public virtual void Run(int timeframe, string assetName)
        {
            Timeframe = timeframe;
            Asset = Assets[assetName];
            CurrentBar = Datasets[Timeframe][1];
            CurrentIndex = BarIndices[Timeframe];


            if (CurrentBar != null && CurrentIndex >= MinBars)
            {                
                Bid = Datasets[Timeframe][1].BidClose;
                Ask = Datasets[Timeframe][1].AskClose;
                Volume = Datasets[Timeframe][1].Volume;
                GetType().InvokeMember("OnBar", BindingFlags.InvokeMethod, null, this, null);
            }

             
        }

        public Trade ExecuteTrade(Trade.TradeDirection direction, double size, double stopPoints = 0, double takeProfitPoints = 0, string comment = null)
        {
            Trade newTrade = new Trade(Asset.Name, direction, size, stopPoints, takeProfitPoints, comment);

            //if no trading api connected ie. backtesting - just create the trade and add it
            if (TradingApi == null)
            {
                
                openTrades.Add(newTrade);
            }
            else //otherwise the trade needs to be sent to the trade server as a real trade
            {
                //create a description for tracking the order progress, use strategy description + a unix timestamp
                newTrade.ClientMsgId = Description + (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                newTrade.OrderRequestTime = DateTime.Now;
                newTrade.Status = Trade.TradeStatus.OPEN_SENT;
                newTrade.SpreadPoints = Ask - Bid;
                newTrade.BarTime = CurrentBar.OpenTime;

                //store the current price to work out slippage later
                if (direction == Trade.TradeDirection.LONG)
                    newTrade.OpenLevel = Ask;
                else
                    newTrade.OpenLevel = Bid;

                //add to the pending trades array so we can check on the updates
                pendingTrades.Add(newTrade);

                TradingApi.SendOpenTrade(TradingApiUser, newTrade.Asset, newTrade.Direction.ToString(), newTrade.Size, newTrade.ClientMsgId, "TestStrategy", stopPoints, takeProfitPoints);
            }

            return newTrade;
        }

        public void CloseAllTrades(Bar currentTick, string assetName = null)
        {
            //if no trading api connected ie. backtesting - just close on the next bar (or tick)
            if (TradingApi == null)
            {
                foreach (Trade openTrade in OpenTrades)
                {
                    openTrade.Exit = Trade.ExitType.Strategy;
                    openTrade.CloseOnNextBar = true;
                }
            }
            else
            { //otherwise the trade command needs to be sent to the trade server
                foreach (Trade openTrade in OpenTrades)
                {
                    openTrade.Status = Trade.TradeStatus.CLOSE_SENT;
                    openTrade.Exit = Trade.ExitType.Strategy;

                    //add to the pending trades array so we can check on the updates
                    pendingTrades.Add(openTrade);

                    TradingApi.SendCloseTrade(TradingApiUser, openTrade.TradeID, openTrade.Size, openTrade.ClientMsgId);
                }
            }
        }

        public void BTCloseAllTrades(Bar currentTick, string assetName = null)
        {

            List<Trade> tradesToClear = new List<Trade>();

            foreach (Trade trade in openTrades)
            {
                if (assetName == null || trade.Asset == assetName)
                {
                    trade.Exit = Trade.ExitType.Strategy;

                    trade.CloseOnNextBar = false;
                    if (trade.Direction == Trade.TradeDirection.LONG)
                        trade.CloseLevel = currentTick.BidOpen;
                    else
                        trade.CloseLevel = currentTick.AskOpen;

                    trade.CloseTime = currentTick.OpenTime;
                    closedTrades.Add(trade);
                    tradesToClear.Add(trade);
                }
            }

            foreach (Trade trade in tradesToClear)
                openTrades.Remove(trade);
            
        }

        private void CloseTrade(Trade trade, double closeLevel, DateTime closeTime, Trade.ExitType exit)
        {
            if (TradingApi == null)
            {
                double slippage = 0;

                trade.Exit = exit;
                trade.CloseOnNextBar = false;
                trade.CloseLevel = closeLevel + slippage;
                trade.Profit = trade.PointChange / Assets[trade.Asset].Pip * Assets[trade.Asset].PipValue * trade.Size;
                trade.CloseTime = closeTime;
                closedTrades.Add(trade);
            }
            else
            {
                trade.Exit = exit;
                trade.Status = Trade.TradeStatus.CLOSE_SENT;
                trade.OrderRequestTime = DateTime.Now;
                trade.BarTime = CurrentBar.OpenTime;

                //store the current price to work out slippage later
                if (trade.Direction == Trade.TradeDirection.LONG)
                    trade.CloseLevel = Ask;
                else
                    trade.CloseLevel = Bid;

                //add to the pending trades array so we can check on the updates
                pendingTrades.Add(trade);

                TradingApi.SendCloseTrade(TradingApiUser, trade.TradeID, trade.Size, trade.ClientMsgId);
            }

        }


        public void OrderAccepted(string clientMsgId, long positionId, bool isClosing)
        {
            foreach (Trade trade in pendingTrades)
            {
                if (trade.ClientMsgId == clientMsgId)
                {
                    if (!isClosing)
                    {
                        trade.Status = Trade.TradeStatus.OPEN_ACCEPTED;
                        trade.TradeID = positionId;
                        
                    }
                    else if (trade.TradeID == positionId)
                    {
                        trade.Status = Trade.TradeStatus.CLOSE_ACCEPTED;
                    }
                }
            }
        }

        public void OrderStopTargetAccepted(string clientMsgId, long positionId)
        {
            Trade foundTrade = null;
            foreach (Trade trade in pendingTrades)
            {
                if (trade.ClientMsgId == clientMsgId && trade.TradeID == positionId)
                {
                    trade.Status = Trade.TradeStatus.OPEN_FILLED_STOP_TARGET;
                    foundTrade = trade;
                }
            }

            //this trade is no longer pending
            if (foundTrade != null)
                pendingTrades.Remove(foundTrade);
        }

        public void OrderFilled(string clientMsgId, long positionId, double size, double commission, double actualPrice,
            DateTime execTimestamp, DateTime createTimestamp, double marginRate, bool isClosing)
        {
            Trade trade = null;
            bool removeFromPending = false;

            //look in the pending trades
            foreach (Trade thisTrade in pendingTrades)
            {
                if (thisTrade.ClientMsgId == clientMsgId && thisTrade.TradeID == positionId)
                {
                    trade = thisTrade;
                }
            }

            //if not found look in the open trades because this might  be a stoploss or profit target being hit
            foreach (Trade thisTrade in openTrades)
            {
                if (thisTrade.ClientMsgId == clientMsgId && thisTrade.TradeID == positionId)
                {
                    trade = thisTrade;
                }
            }

            //this trade is no longer pending
            if (trade != null)
            {
                if (!isClosing)
                {
                    
                    trade.Status = Trade.TradeStatus.OPEN_FILLED;

                    //fill in the trade details from the order
                    trade.OpenSlippagePoints = trade.OpenLevel - actualPrice;
                    trade.OpenLevel = actualPrice;
                    trade.OpenTime = execTimestamp;
                    trade.Size = size; //might not have been completly filled
                    trade.OpenSlippageTimeFromBroker = (execTimestamp - createTimestamp).TotalSeconds;
                    trade.OpenSlippageTimeFromLocal = (execTimestamp - (DateTime)trade.OrderRequestTime).TotalSeconds;
                    trade.OpenSlippageTimeFromBarStart = (execTimestamp - trade.BarTime).TotalSeconds;
                    trade.Commission = commission;

                    if (trade.Direction == Trade.TradeDirection.SHORT)
                        trade.SpreadPoints = -trade.SpreadPoints;

                    OnMessage?.Invoke("Trade opened: " + trade.TradeOpenDescription());
                    openTrades.Add(trade);

                    try
                    {
                        System.IO.File.AppendAllText(System.IO.Path.Combine("output", "open_trades.csv"), trade.ToString() + "\n");
                    }
                    catch (Exception e)
                    {
                        OnMessage?.Invoke("Failed to write open trade: " + e.Message, MessageType.Error);
                    }

                    if (trade.StopPoints == 0 && trade.TakeProfitPoints == 0)
                        removeFromPending = true;

                }
                else
                {
                    //PARTIAL TRADE CLOSE - NOT PROPERLY IMPLEMENTED
                    if (size < trade.Size)
                    {
                        trade.Size -= Convert.ToInt64(size * 10000000);
                        trade.Status = Trade.TradeStatus.OPEN_FILLED_STOP_TARGET;
                        


                        //fill in the trade details from the order

                        Trade partialTrade = new Trade(trade);
                        partialTrade.Size = Convert.ToInt64(size * 10000000);
                        trade.CloseTime = execTimestamp;
                        trade.CloseLevel = actualPrice;
                        trade.Commission += commission;
                        OnMessage?.Invoke("Trade partially closed: " + partialTrade.ToString());

                        try
                        {
                            System.IO.File.AppendAllText(System.IO.Path.Combine("output", "closed_trades.csv"), partialTrade.ToString() + "\n");
                        }
                        catch (Exception e)
                        {
                            OnMessage?.Invoke("Failed to write partial closed trade: " + e.Message, MessageType.Error);
                        }
                    }
                    else
                    {
                        


                        trade.Status = Trade.TradeStatus.CLOSE_FILLED;
                        //fill in the trade details from the order
                        trade.Size = Convert.ToInt64(size * 10000000);
                        trade.Status = Trade.TradeStatus.OPEN_FILLED_STOP_TARGET;
                        trade.CloseTime = execTimestamp;
                        trade.CloseSlippagePoints = trade.CloseLevel - actualPrice;
                        trade.CloseLevel = actualPrice;
                        trade.Commission += commission; //commission on both sides of the order
                        trade.CloseSlippageTimeFromBroker = (execTimestamp - createTimestamp).TotalSeconds;
                        trade.CloseSlippageTimeFromLocal = (execTimestamp - (DateTime)trade.OrderRequestTime).TotalSeconds;
                        trade.CloseSlippageTimeFromBarStart = (execTimestamp - trade.BarTime).TotalSeconds;

                        //check for stop loss or profit hit
                        if (trade.Exit == Trade.ExitType.Open)
                        {
                            if (trade.PointChange > 0)
                                trade.Exit = Trade.ExitType.TakeProfit;
                            else
                                trade.Exit = Trade.ExitType.StopLoss;
                        }

                        OnMessage?.Invoke("Trade closed: " + trade.ToString());
                        closedTrades.Add(trade);
                        openTrades.Remove(trade);

                        try
                        {
                            System.IO.File.AppendAllText(System.IO.Path.Combine("output", "closed_trades.csv"), trade.ToString() + "\n");
                        }
                        catch (Exception e)
                        {
                            OnMessage?.Invoke("Failed to write closed trade: " + e.Message, MessageType.Error);
                        }

                        if (trade.StopPoints == 0 && trade.TakeProfitPoints == 0)
                            removeFromPending = true;
                    }
                }

                if(removeFromPending)
                    pendingTrades.Remove(trade);
            }
        }

        public void OrderCancelled(string clientMsgId, long positionId)
        {
            Trade foundTrade = null;
            foreach (Trade trade in pendingTrades)
            {
                if (trade.ClientMsgId == clientMsgId && trade.TradeID == positionId)
                {
                    trade.Status = Trade.TradeStatus.CLOSE_FILLED_STOP_TARGET;
                    foundTrade = trade;
                }
            }

            //this trade is no longer pending
            if(foundTrade != null)
                pendingTrades.Remove(foundTrade);
        }

        public bool HasPendingOrder(string clientMsgId)
        {
            foreach(Trade trade in pendingTrades)
            {
                if (trade.ClientMsgId == clientMsgId)
                    return true;
            }

            return false;
        }

        public void UpdateTrades(Bar currentTick)
        {

            //for every trade update the close and hence profit

            List<Trade> cTrade = new List<Trade>();
            foreach (Trade trade in openTrades)
            {
                //A decision to make a trade is made at the end of a bar so mark trade as ready to execute
                //the next available opportunity to take a trade is at the start of a bar
                if (trade.ExecuteOnNextBar)
                {
                    trade.ExecuteOnNextBar = false;

                    //Apply a spread filter that can be set in the strategy (Spread filter of zero means accept all trades)
                    if (SpreadFilter == 0 || currentTick.AskOpen - currentTick.BidOpen < Asset.Pip * SpreadFilter)
                    {
                        trade.OpenTime = currentTick.OpenTime;
                        trade.SpreadPoints = Math.Round(currentTick.AskOpen - currentTick.BidOpen, 5);
                        trade.SpreadCost = trade.SpreadPoints / Assets[trade.Asset].Pip * Assets[trade.Asset].PipValue * trade.Size;

                        if (trade.Direction == Trade.TradeDirection.LONG)
                        {
                            trade.OpenLevel = currentTick.AskOpen;
                            //for fixed stop and targets we want to set the levels based on the data feed the trade will close on
                            //therefore for a 50PIP stop and target there will be a 50/0 chance of success and the spread will 
                            //esentially work like a commission eg. profit/loss = 50PIP profit - spread or 50PIP loss - spread
                            if (trade.StopPoints > 0)
                                trade.StopLevel = currentTick.BidOpen - trade.StopPoints;
                            if (trade.TakeProfitPoints > 0)
                                trade.TakeProfitLevel = currentTick.BidOpen + trade.TakeProfitPoints;
                        }
                        else
                        {
                            trade.OpenLevel = currentTick.BidOpen;

                            //for fixed stop and targets we want to set the levels based on the data feed the trade will close on
                            //therefore for a 50PIP stop and target there will be a 50/0 chance of success and the spread will 
                            //esentially work like a commission eg. profit/loss = 50PIP profit - spread or 50PIP loss - spread
                            if (trade.StopPoints > 0)
                                trade.StopLevel = currentTick.AskOpen + trade.StopPoints;
                            if (trade.TakeProfitPoints > 0)
                                trade.TakeProfitLevel = currentTick.AskOpen - trade.TakeProfitPoints;
                        }

                        //update the current price level
                        if (trade.Direction == Trade.TradeDirection.LONG)
                            trade.CloseLevel = currentTick.BidClose;
                        else
                            trade.CloseLevel = currentTick.AskClose;
                    }
                    else //this trade will be removed without executing on the next clean
                    {
                        trade.Skipped = true;
                        trade.OpenTime = currentTick.OpenTime;
                        trade.CloseTime = currentTick.OpenTime;
                    }
                }

                if (trade.CloseOnNextBar)
                {
                    if (trade.Direction == Trade.TradeDirection.LONG)
                        CloseTrade(trade, currentTick.BidOpen, currentTick.OpenTime, Trade.ExitType.Strategy);
                    else
                        CloseTrade(trade, currentTick.AskOpen, currentTick.OpenTime, Trade.ExitType.Strategy);

                }
                else if(!trade.Skipped)
                {
                    //update the current price level
                    if (trade.Direction == Trade.TradeDirection.LONG)
                        trade.CloseLevel = currentTick.BidClose;
                    else
                        trade.CloseLevel = currentTick.AskClose;

                    //Stops and Limits
                    if (trade.StopHit(currentTick.BidLow, currentTick.AskHigh))
                    {
                        //add some random estimation of the seconds into the bar that the trade was closed
                        CloseTrade(trade, trade.StopLevel, currentTick.OpenTime.AddSeconds(30), Trade.ExitType.StopLoss);
                    }
                    else if (trade.TakeProfitHit(currentTick.AskLow, currentTick.BidHigh))
                    {
                        //add some random estimation of the seconds into the bar that the trade was closed
                        CloseTrade(trade, trade.TakeProfitLevel, currentTick.OpenTime.AddSeconds(30), Trade.ExitType.TakeProfit);                       

                    }


                }
            }

            //remove all the closed open trades - needs to be in a different enumeration so it doesn't break
            Trade[] cTrades = openTrades.Where(x => x.CloseTime != null).ToArray();
            foreach (Trade trade in cTrades)
                openTrades.Remove(trade);
        }
    }
}
