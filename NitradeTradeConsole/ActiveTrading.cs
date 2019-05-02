using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using cTraderAPI;
using TradingLibrary;

namespace NitradeTradeConsole
{
    public static class ActiveTrading
    {

        static MainController controller;

        static bool ProgramRunning;

        static Queue<string> _messageQueue = new Queue<string>();
        static Queue<string> _errorQueue = new Queue<string>();

        static List<Strategy> liveStrategies = new List<Strategy>();

        static Dictionary<string, Asset> assetDetails;
        static Dictionary<string, Dictionary<int, Bar[]>> priceData;
        static Dictionary<string, Dictionary<int, int>> barIndices;
        static string pythonCalcCommands;
        static string[] pythonCalcLabels;
        static int[] pythonCalcTimeframes;

        static PythonBridge PythonBridge = null;

        static string[] consoleColumns = new string[3] { "", "", "" };
        static int columnWidth = 30;

        static string _strategyName;
        static string _dllPath;

        public static void Start(string strategyName, string dllPath)
        {
            ProgramRunning = true;
            _strategyName = strategyName;
            _dllPath = dllPath;


            controller = new MainController();
            controller.MessageHandler = new MessageHandler(DisplayMessage);
            controller.ErrorHandler = new ErrorHandler(DisplayError);
            controller.ConnectionLostHandler = new ConnectionLostHandler(LostConnection);
            controller.SymbolWriteMessageHandler = new SymbolWriteMessageHandler(DisplaySymbolWrite);
            controller.SymbolTickRequestHandler = new SymbolTickRequestHandler(DisplayTickRequest);
            controller.HeartBeatHandler = new HeartBeatHandler(HeartBeat);
            controller.SymbolTickHandler = new SymbolTickHandler(TickReceived);
            controller.OnAccountAuthorised = new AccountAuthorisationComplete(AccountAuthorised);

            WriteConsole("Starting cTrader Trading Application");

            //local directory will store all user relevant data
            if (!System.IO.Directory.Exists("local"))
                System.IO.Directory.CreateDirectory("local");
            if (!System.IO.Directory.Exists("local//users"))
                System.IO.Directory.CreateDirectory("local//users");


            try
            {
                controller.Config = new Config(@"local/");

                if (controller.Config.PythonPath != null)
                    PythonBridge = new PythonBridge(controller.Config.PythonPath);

            }
            catch (Exception ex)
            {
                //if the config file can't be loaded terminate the program
                DisplayError("Unable to load required config file - " + ex.Message);
                Console.ReadLine();
                return;
            }

            //Load all the user config files (need at least 1 user)
            string[] userFiles = System.IO.Directory.GetFiles(@"local/users/");
            UserConfig firstUser = null;
            foreach (string filename in userFiles)
            {
                UserConfig config = new UserConfig(filename);
                firstUser = config;
                //update the controller with any symbolIds that are stored in the user config
                controller.MergeSymbolInfo(config.Symbols);

                //store as a dictionary with access token as the key for easier referencing
                if (config != null)
                    controller.Users.Add(config.Token, config);
            }

            //Add in the trade event handlers
            controller.OnTradeFail = new TradeFailedHandler(TradeFailed);
            controller.OnOrderAccepted = new OrderAcceptedHandler(OrderAccepted);
            controller.OnStopTargetAccepted = new OrderStopTargetAcceptedHandler(OrderStopTargetAccepted);
            controller.OnOrderFilled = new OrderFilledHandler(OrderFilled);
            controller.OnOrderCancelled = new OrderCanceledHandler(OrderCancelled);

            //Hold all the price data here on the trading program - push this price data to each strategy
            priceData = new Dictionary<string, Dictionary<int, Bar[]>>();
            barIndices = new Dictionary<string, Dictionary<int, int>>();

            //Load in the strategies
            Strategy strategyMain = Strategy.Load(strategyName, dllPath);

            //Load in the asset details and set this strategy asset, a new strategy object is created for each asset
            assetDetails = Asset.LoadAssetFile(strategyMain.AssetDetailsFile); 

            //setup each strategy
            foreach (string assetName in new string[] { "EURUSD", "EURJPY" })
            {

                Strategy strategy = Strategy.Load(strategyName, dllPath);
                strategy.IsTesting = false;
                strategy.TradingApi = controller;
                strategy.TradingApiUser = (ITradingApiUser)firstUser;
                strategy.Description = assetName; //Plus add on all setup parameters here eg. AUDUSD 3 2 6
                strategy.OnMessage = new MessageDelegate(strategyMessage);

                strategy.Assets.Add(assetName, assetDetails[assetName]);
                strategy.Asset = assetDetails[assetName];

                //check the required data for each strategy and update the price data to the maximum lookback required for all strategies
                if (!priceData.ContainsKey(assetName))
                {
                    priceData.Add(assetName, new Dictionary<int, Bar[]>());
                    priceData[assetName].Add(1, new Bar[5]); //add at a minimum a 1 minute bar with 5 bar lookback

                    barIndices.Add(assetName, new Dictionary<int, int>());
                    barIndices[assetName].Add(1, 0);
                }



                foreach (KeyValuePair<int, int> lookbacks in strategy.RequiredData)
                {
                    //if price data isn't setup for this timeframe, add it.
                    if (!priceData[assetName].ContainsKey(lookbacks.Key))
                    {
                        priceData[assetName].Add(lookbacks.Key, new Bar[lookbacks.Value + 1]);
                        barIndices[assetName].Add(lookbacks.Key, 0);
                    }
                    //overwrite any existing setup with the higher lookback
                    else if (lookbacks.Value > priceData[assetName][lookbacks.Key].Length)
                        priceData[assetName][lookbacks.Key] = new Bar[lookbacks.Value + 1];
                }

                //Pass a reference of the datasets to the strategy
                strategy.Datasets = priceData[assetName];
                strategy.BarIndices = barIndices[assetName];

                liveStrategies.Add(strategy);

            }




            if (controller.Users.Count == 0)
            {
                DisplayError("Must have atleast 1 user config.txt file in the local/users/ directory");
            }
            else
            {
                //remove any symbols that are not required by the strategies
                SymbolCollection symbolDetails = controller.Users.FirstOrDefault().Value.Symbols;
                Symbol[] notRequired = symbolDetails.Where(p => !priceData.Keys.Any(p2 => p2 == p.Name)).ToArray();
                foreach (Symbol symbol in notRequired)
                    symbolDetails.Remove(symbol);

                //build an required python calculation commands and labels based on the requirements of the loaded strategies
                pythonCalcTimeframes = new int[] { 60 };
                pythonCalcCommands = "ATR(3,close,high,low);ATR(4,close,high,low);ATR(5,close,high,low);ATR(100,close,high,low);VOLATILITY_LOG_MA(12,high,low);VOLUME_LOG_MA(12,volume);BBANDS(20,1.8,1,close);BBANDS(20,1.8,2,close)";
                pythonCalcLabels = new string[] { "ATR_3", "ATR_4", "ATR_5", "ATR_100",
                        "VOLATILITY_LOG_MA_12", "VOLUME_LOG_MA_12",
                        "BBANDS_UP", "BBANDS_LOW" };


                //Start the Connection
                WriteConsole(controller.Config.ToString());
                controller.OpenConnection();

                //Use a seperate thread to write from a message queue
                //This should be the only thread to Dequeue from the message queus
                Thread t = new Thread(() => {
                    //make sure this thread exits when the program exits
                    Thread.CurrentThread.IsBackground = true;
                    try
                    {
                        WriteMessages();
                    }
                    catch (Exception ex)
                    {
                        WriteConsole("No more messages or errors will be recorded. Message thread has failed: " + ex.Message);
                    }
                });
                t.Start();
                
                DisplayMessage("Waiting for new complete bar before starting active trading.");
            }


            
        }


        static void WriteConsole(string text, int column = 0, bool newLine = true)
        {
            if (column < consoleColumns.Length - 1)
                consoleColumns[column] = text;
            else if (column == 2)
            {
                if (consoleColumns[column].Length >= columnWidth - 1)
                    consoleColumns[column] = text;
                else
                    consoleColumns[column] += text;
            }

            string fullLine = consoleColumns[0].PadRight(columnWidth) + consoleColumns[1].PadRight(columnWidth) + consoleColumns[2].PadRight(columnWidth);
            if (newLine)
                Console.WriteLine("\r" + consoleColumns[0].PadRight(columnWidth * 3));
            else
                Console.Write("\r" + consoleColumns[1].PadRight(columnWidth * 2) + consoleColumns[2].PadRight(columnWidth));

        }

        static void AccountAuthorised(long accountId)
        {
            controller.GetSymbols(accountId);
        }

        static void HeartBeat()
        {
            WriteConsole(".", 2, false);
        }

        static void LostConnection()
        {
            //try and reopen the connection
            Start(_strategyName, _dllPath);
        }

        static void TickReceived(UserConfig user, long symbolId, bool isBid, ulong value, DateTime tickTime)
        {

            string symbolName = user.Symbols.SymbolName((int)symbolId);

            //do nothing if this tick is not required
            if (!priceData.ContainsKey(symbolName))
                return;

            string bidText = "Ask";
            if (isBid)
                bidText = "Bid";
            float val = (float)value / 100000;
            //display the tick
            string text = symbolName + " " + tickTime.ToString("dd-MM-yy HH:mm:ss.fff") + " " + val + " " + bidText;
            WriteConsole(text, 1, false);

            //get the strategies affected by this tick
            Strategy[] runStrategies = liveStrategies.Where(x => x.Asset.Name == symbolName).ToArray();

            //update the price data
            Dictionary<int, Bar[]> datasets = priceData[symbolName];
            Dictionary<int, int> indicies = barIndices[symbolName];

            

            foreach (KeyValuePair<int, Bar[]> data in datasets)
            {

                //on first run we need to initialse a bar but don't set the BidOpen or AskOpen so we know the tick recorded didn't start from the start of the bar
                if (data.Value[0] == null)
                {
                    Bar bar = new Bar();

                    TimeSpan d = TimeSpan.FromMinutes(data.Key);
                    DateTime barDate = (new DateTime((tickTime.Ticks + d.Ticks) / d.Ticks * d.Ticks, tickTime.Kind)).AddMinutes(-data.Key);

                    bar.OpenTime = barDate;
                    data.Value[0] = bar;
                }

                //if this is not a new bar just update bar at zero
                if (tickTime < data.Value[0].OpenTime.AddMinutes(data.Key))
                    data.Value[0].Update(val, isBid);
                //////////////////////////////////////
                //// NEW BAR
                ////////////////////////////////////////
                else // this is a new bar
                {
                    //shift the bar array
                    Array.Copy(data.Value, 0, data.Value, 1, data.Value.Length - 1);

                    //peg the date to the nearest timeframe
                    TimeSpan d = TimeSpan.FromMinutes(data.Key);
                    DateTime barDate = (new DateTime((tickTime.Ticks + d.Ticks) / d.Ticks * d.Ticks, tickTime.Kind)).AddMinutes(-data.Key);

                    //Create the new incomplete bar
                    data.Value[0] = new Bar(barDate, val, isBid);

                    //update the bar index - this would have started with a value of lookback and continues to count up indefinately - mainly used in backtesting
                    indicies[data.Key]++;

                    //get the strategies for this timeframe only
                    Strategy[] tfStrategies = runStrategies.Where(x => x.RequiredData.ContainsKey(data.Key)).ToArray();

                    //we need to download the historic data to fill the lookback array
                    //Note it is best to do this on the start of a new bar because we can only download full bars from history
                    //Therefore by waiting until a new bar is formed we ensure that the new bar collects all ticks and historic bars are complete
                    DowloadLookbackData(user, symbolName, data, tfStrategies);
                }
            }

            //update the trades with the most recent incomplete bar ie. the current minute bar will have the close prices at the current spot price
            //This needs to run after the OnBar event because OnBar will process the strategy for the last closed bar then UpdateTrades will execute any
            //trades that were generated from OnBar
            foreach (Strategy strategy in runStrategies)
            {
                strategy.UpdateTrades(datasets[1][0]);
            }


        }

        private static Strategy OrderBelongsTo(string clientMsgId)
        {
            //need to find the strategy the message belongs to
            foreach (Strategy strategy in liveStrategies)
            {
                if (strategy.HasPendingOrder(clientMsgId))
                {
                    return strategy;
                }
            }

            return null;
        }

        private static void TradeFailed(long accountId, string message)
        {
            DisplayMessage("Failed trade command: " + message);
        }

        private static void OrderAccepted(string clientMsgId, long positionId, bool isClosing)
        {
            //need to find the strategy the message belongs to
            Strategy strategy = OrderBelongsTo(clientMsgId);
            if(strategy != null)
                strategy.OrderAccepted(clientMsgId, positionId, isClosing);
        }

        private static void OrderStopTargetAccepted(string clientMsgId, long positionId)
        {
            //need to find the strategy the message belongs to
            Strategy strategy = OrderBelongsTo(clientMsgId);
            if (strategy != null)
                strategy.OrderStopTargetAccepted(clientMsgId, positionId);
        }

        private static void OrderFilled(string clientMsgId, long positionId, double size, double commission, double actualPrice,
        DateTime execTimestamp, DateTime createTimestamp, double marginRate, bool isClosing)
        {
            //need to find the strategy the message belongs to
            Strategy strategy = OrderBelongsTo(clientMsgId);
            if (strategy != null)
                strategy.OrderFilled(clientMsgId, positionId, size, commission, actualPrice, execTimestamp, createTimestamp, marginRate, isClosing);
        }

        private static void OrderCancelled(string clientMsgId, long positionId)
        {
            //need to find the strategy the message belongs to
            Strategy strategy = OrderBelongsTo(clientMsgId);
            if (strategy != null)
                strategy.OrderCancelled(clientMsgId, positionId);
        }


        private static void DowloadLookbackData(UserConfig user, string symbolName, KeyValuePair<int, Bar[]> data, Strategy[] strategiesToRunOnComplete)
        {
            if (!assetDetails[symbolName].LookbackDownloaded.ContainsKey(data.Key) || !assetDetails[symbolName].LookbackDownloaded[data.Key])
            {
                //delay by 5 seconds so that enough time has elapsed for the bar to close
                int lookback = data.Value.Length;
                DataDownloader dd = new DataDownloader(user, symbolName, data.Key, DateTime.UtcNow.AddMinutes(-lookback * data.Key * 3), DateTime.UtcNow,
                    new PriceDataDownloadedEventHandler(DownloadPriceDataCompleted));
                dd.OnError = new ErrorHandler(DisplayError);
                dd.PostRunStrategies = strategiesToRunOnComplete;
                Timer t = new Timer(DelayedDataDownload, dd, 5000, -1);

            }
            else
            {
                //load in the python calculated data if required - only need the most recent bar         
                if (PythonBridge != null)
                    CalculatePythonData(PythonBridge, symbolName, data.Key, 1);

                foreach (Strategy strategy in strategiesToRunOnComplete)
                {
                    strategy.Run(data.Key, symbolName);
                }
            }
        }

        private static void DelayedDataDownload(Object o)
        {
            DataDownloader dd = (DataDownloader)o;
            //update the end date to make sure we inclued the most recent data
            dd.EndDate = DateTime.UtcNow;

            //set the from date to 3 times the lookback to take into account weeknds and public holidays
            dd.GetPriceDataAsync();
        }

        static void DownloadPriceDataCompleted(object sender, DataDownloaderEventArgs e)
        {
            OpenAPIBar[] data = e.BarData.Data.OrderByDescending(x => x.OpenTime).ToArray();

            lock (priceData[e.AssetName][e.Timeframe])
            {
                for (int i = 1; i < priceData[e.AssetName][e.Timeframe].Length && i < data.Length; i++)
                {
                    OpenAPIBar oBar = data[i - 1];
                    Bar bar = new Bar();
                    bar.OpenTime = oBar.OpenTime;
                    bar.BidOpen = oBar.Open;
                    bar.AskOpen = oBar.Open;
                    bar.BidClose = oBar.Close;
                    bar.AskClose = oBar.Close;
                    bar.BidHigh = oBar.High;
                    bar.AskHigh = oBar.High;
                    bar.BidLow = oBar.Low;
                    bar.AskLow = oBar.Low;
                    bar.Volume = oBar.Volume;
                    priceData[e.AssetName][e.Timeframe][i] = bar;
                }
            }

            //uodate the bar index so that strategies can run eg. barIndex will now be > MinBars
            barIndices[e.AssetName][e.Timeframe] = priceData[e.AssetName][e.Timeframe].Length;

            //flag that this timeframe has been downloaded
            if (!assetDetails[e.AssetName].LookbackDownloaded.ContainsKey(e.Timeframe))
                assetDetails[e.AssetName].LookbackDownloaded.Add(e.Timeframe, true);
            else
                assetDetails[e.AssetName].LookbackDownloaded[e.Timeframe] = true;

            //Now we need to send this data to Python to calculate any required data using the whole lookback period
            if (PythonBridge != null)
                CalculatePythonData(PythonBridge, e.AssetName, e.Timeframe, priceData[e.AssetName][e.Timeframe].Length);

            DisplayMessage("Active trading ready for " + e.AssetName + " on " + e.Timeframe + "min bars");

            //Run the strategies after the look back price data has been downloade (if strategies attached)
            if (e.PostRunStrategies != null)
            {
                foreach (Strategy strategy in (Strategy[])e.PostRunStrategies)
                    strategy.Run(e.Timeframe, e.AssetName);
            }

        }

        static void CalculatePythonData(PythonBridge pb, string assetName, int timeframe, int barCount)
        {
            //do nothing if this timeframe is not listed as required
            if (!pythonCalcTimeframes.Contains(timeframe))
                return;

            //get the relevant bars (whole lookback period) but not the zero index bar as this is incomplete
            Bar[] bars = priceData[assetName][timeframe].Skip(1).ToArray();

            //save a temporary binary file to disk for python to read (faster than sending all the data via the stdout
            string tempData = @"C:\ForexData\ShareData\" + assetName + "_m" + timeframe + "_Share_live.bin";
            DataBuilder.DatasetToBinary(tempData, bars, DataFeedType.Ask);

            //Create the python bridge and run the calculation commands            
            string[] commands = new string[] { "whole", tempData, barCount.ToString(), pythonCalcCommands };
            string[] results = pb.RunScript(System.IO.Path.Combine("python_scripts","build_features.py"), commands);

            PreCalculatedFeatures pcFeatures = new PreCalculatedFeatures();
            try
            {
                pcFeatures.FillFromCsv(results, pythonCalcLabels);
            }
            catch (Exception e)
            {
                DisplayError(e.Message);
            }

            //add this data to the asset if it doesnt exist
            if (!assetDetails[assetName].Data.ContainsKey(timeframe))
                assetDetails[assetName].Data.Add(timeframe, pcFeatures);
            else
            {
                //remove the oldest one to save on memory creep
                assetDetails[assetName].Data[timeframe].RemoveOldest();

                //we just add the data to the current set of data
                assetDetails[assetName].Data[timeframe].Merge(pcFeatures);
            }

        }

        static void DisplayTickRequest(Symbol symbol)
        {
            string msg = DateTime.Now.ToString() + ": " + symbol.Name + " spots requested.";
            WriteConsole(msg);
            _messageQueue.Enqueue(msg);
        }

        static void strategyMessage(string message, MessageType mType)
        {
            if (mType == MessageType.Error)
                DisplayError(message);
            else
                DisplayMessage(message);
        }

        static void DisplayMessage(string message)
        {
            string msg = DateTime.Now.ToString() + ": " + message;
            WriteConsole(msg);
            _messageQueue.Enqueue(msg);


        }

        static void DisplayError(string message)
        {
            string error = DateTime.Now.ToString() + ": " + message;
            WriteConsole(error);
            _errorQueue.Enqueue(error);

        }

        static void DisplaySymbolWrite(string symbolName, int symbolId, string dateString, bool isBid)
        {
            string feedType = "Ask";
            if (isBid)
                feedType = "Bid";

            string message = symbolName + " " + dateString + " " + feedType;
            WriteConsole("Writing ticks for " + message, 1, false);

        }

        static void WriteMessages()
        {
            while (ProgramRunning)
            {
                //batch write data from the messages queue
                int count = 0;
                string content = "";
                //limit this loop to 10000 messages at a time
                while (_messageQueue.Count > 0 && count < 10000)
                {
                    string message = _messageQueue.Dequeue();
                    content += message;
                    count++;
                }
                //write a batch of messages
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine("output", "activity.log"), content);
                }
                catch (Exception ex)
                {
                    DisplayError("Could not record messages. " + ex.Message);
                }

                //batch write data from the errors queue
                count = 0;
                content = "";
                //limit this loop to 10000 messages at a time
                while (_errorQueue.Count > 0 && count < 10000)
                {
                    string error = _errorQueue.Dequeue();
                    content += error;
                    count++;
                }
                //write a batch of messages
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine("output", "errors.log"), content + "\n");
                }
                catch (Exception ex)
                {
                    WriteConsole("Could not record errors. " + ex.Message);
                }

                //Wait 1 second before trying to write again
                Thread.Sleep(1000);
            }

        }
    }
}
