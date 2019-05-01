using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace TradingLibrary
{
    // Declaration
    public enum MessageType { Message, Error, Log, Update }
    public delegate void MessageDelegate(string message, MessageType type = MessageType.Message);
    public delegate void OnBackTestComplete(TestSummary testSummary);
    public delegate void OnTestComplete(TestSet testSet);

    public class BackTest
    {
        Queue<BackTestTask>[] threadTasks;
        private DateTime start;
        public MessageDelegate MessageDelegate;
        public OnBackTestComplete OnComplete;
        public TestSummary TestSummary {get; private set;}

        public bool Optimise { get; set; }

        public BackTest()
        {
            TestSummary = new TestSummary();
        }

        private int tasksRequired;

        public BackTest(OnBackTestComplete onComplete, MessageDelegate messageDelegate = null)
        {
            OnComplete = onComplete;
            MessageDelegate = messageDelegate;
        }

        public void Run(string strategyName, string strategiesDLL, bool optimise = false)
        {
            start = DateTime.Now;

            TestSummary = new TestSummary();

            Optimise = optimise;

            //Get the strategy
            Strategy strategy = Strategy.Load(strategyName, strategiesDLL);

            //Copy across the strategy back test setup
            if (strategy.StartDate != null)
                TestSummary.StartDate = strategy.StartDate;
            if (strategy.EndDate != null)
                TestSummary.EndDate = strategy.EndDate;
            TestSummary.ReduceCorrelatedParams = strategy.ReduceCorrelatedParams;
            TestSummary.ReduceByRankParams = strategy.ReduceByRankParams;
            TestSummary.TradeDataLabels = strategy.TradeDataLabels;

            //Get all the possible variants from this strategies parameters
            StrategyVariant[] variants;
            if (optimise)
                variants = StrategyVariant.BruteForceGeneration(strategy.OptimiseParameters.ToArray());
            else
                variants = new StrategyVariant[] { StrategyVariant.Default(strategy.Parameters.ToArray()) };

            threadTasks = new Queue<BackTestTask>[strategy.Cpus];

            //Load in the asset details and add these to the strategy if selected in TradeAssetList
            Dictionary<string, Asset> assetDetails = Asset.LoadAssetFile(strategy.AssetDetailsFile);

            foreach (string assetName in strategy.TradeAssetList)
            {
                Asset asset = assetDetails[assetName];

                MessageDelegate?.Invoke("Loading in data " + assetName);

                //load in the data before we start
                foreach (ExternalFeatureData externalFeature in strategy.ExternalFeatures)
                {
                    if (!asset.Data.ContainsKey(externalFeature.Timeframe))
                        DataBuilder.LoadExternalFeatureBinary(asset, externalFeature, MessageDelegate);
                }

                //Read all the bytes from the datapath from the 1 min timeframe if the data is not already loaded
                //this is very fast about 0.9 seconds for 10 years of minute data
                if (asset.Dataset == null)
                    asset.Dataset = DataBuilder.LoadBinary(asset.DataPath);
            }

            tasksRequired = 0;

            int threadIndex = 0;
            foreach (string assetName in strategy.TradeAssetList)
            {
                foreach(StrategyVariant currentVariant in variants)
                {
                    Strategy localStrategy = Strategy.Load(strategyName, strategiesDLL);
                    Asset asset = assetDetails[assetName];
                    localStrategy.Assets.Add(assetName, asset);

                    //set the strategy parameters
                    for (int pIndex = 0; pIndex < localStrategy.Parameters.Count; pIndex++)
                        localStrategy.SetParameter(localStrategy.Parameters[pIndex].Name, currentVariant.Parameters[pIndex]);

                    localStrategy.Description = asset.Name + " " + currentVariant.ToString();

                    if (threadIndex >= threadTasks.Length)
                        threadIndex = 0;

                    BackTestTask btt = new BackTestTask(asset, localStrategy);

                    if (threadTasks[threadIndex] == null)
                        threadTasks[threadIndex] = new Queue<BackTestTask>();
                    threadTasks[threadIndex].Enqueue(btt);

                    tasksRequired++;

                    threadIndex++;

                    
                }

                
            }

            MessageDelegate?.Invoke("Starting " + tasksRequired + " tests ...");

            foreach (Queue<BackTestTask> threadQueue in threadTasks)
            {
                if (threadQueue != null)
                {
                    if (threadQueue.Count > 0)
                    {
                        Thread thread = new Thread(() => Test(threadQueue, OnCompleteTest));                        
                        thread.Start();
                    }
                }
            }

        }

        public void OnCompleteTest(TestSet testSet)
        {
            TestSummary.Add(testSet);

            int completed = TestSummary.TestSets.Length;
            MessageDelegate?.Invoke("Completed " + completed + " of " + tasksRequired, MessageType.Update);

            //All optimisations have been done
            if (completed == tasksRequired)
            {
                MessageDelegate?.Invoke("\nComplete backtest took: " + (DateTime.Now - start).TotalMinutes.ToString("0.00") + " minutes");
                OnComplete.Invoke(TestSummary);
            }
        }

        public void Test(Queue<BackTestTask> tasks, OnTestComplete onComplete)
        {

            while (tasks.Count > 0)
            {
                BackTestTask task = tasks.Dequeue();

                Strategy strategy = task.Strategy;
                Asset asset = task.Asset;

                //Record start time of process for time taken message
                DateTime now = DateTime.Now;


                byte[] bytes = asset.Dataset;

                //add in the required minute bars with a 1 bar lookback
                //If a strategy needs these bars it will be overwritted in the next foreach loop
                strategy.InitBarData();

                //keep a pointer of the last added bar and date so this can be modified while building up the higher timeframes
                Dictionary<int, Bar> lastAddedBars = new Dictionary<int, Bar>();
                Dictionary<int, DateTime> lastAddedDates = new Dictionary<int, DateTime>();


                //Traverse the byte array to build the bars - this can be done on separate threads for each asset for maximum speed
                int i = 0;
                Bar bar = null;
                while (i < bytes.Length)
                {
                    bar = DataBuilder.ReadBinaryBar(bytes, (i / 44));
                    //move to next line in byte array - 1 bar is 44 bytes
                    i += 44;

                    //skip edge of market bars like Zorro does
                    /*
                    if ((bar.OpenTime.DayOfWeek == DayOfWeek.Friday && bar.OpenTime.Hour > 18) ||
                        (bar.OpenTime.DayOfWeek == DayOfWeek.Sunday && bar.OpenTime.Hour < 22))
                        continue;
                    */

                    //go through each timeframe and either create a new bar or increment the current bar
                    foreach (KeyValuePair<int, Bar[]> barSet in strategy.Datasets)
                    {
                        ///////////////////////////////////////////////////////////////////////////////////
                        /////This marked section takes about half the processing time
                        ///////////////////////////////

                        //create a bardate pegged to the nearest timeframe 
                        DateTime barDate;
                        if (barSet.Key == 1)
                            barDate = bar.OpenTime;
                        else
                        {
                            //Peg the bar to the start of the timeframe
                            TimeSpan d = TimeSpan.FromMinutes(barSet.Key);
                            barDate = (new DateTime((bar.OpenTime.Ticks + d.Ticks) / d.Ticks * d.Ticks, bar.OpenTime.Kind)).AddMinutes(-barSet.Key);
                        }

                        //Keep a record of the last added bars date or set it to the current bar date on the first run
                        DateTime lastAddedDate;
                        if (lastAddedDates.ContainsKey(barSet.Key))
                            lastAddedDate = lastAddedDates[barSet.Key];
                        else
                            lastAddedDate = barDate;

                        DateTime nextBar = ((DateTime)lastAddedDate).AddMinutes(barSet.Key);
                        /////////////////////////////////////////////////////////////
                        /////////////
                        ///////////////////////////////////////////////////////////////

                        //add the bar to all timeframes if it doesnt exist
                        if ((nextBar <= barDate) || !lastAddedDates.ContainsKey(barSet.Key))
                        {
                            //need a new bar for each time frame so we don't have the same pointer in each timeframe
                            Bar setBar = new Bar(bar);

                            //shift the bar array
                            Array.Copy(barSet.Value, 0, barSet.Value, 1, barSet.Value.Length - 1);

                            barSet.Value[0] = setBar;
                            lastAddedBars[barSet.Key] = setBar;
                            lastAddedDates[barSet.Key] = barDate;

                            //Update the Trades                        
                            if (barSet.Key == 1)
                                strategy.UpdateTrades(strategy.Datasets[1][1]);

                            //run the strategy (only if this timeframe was in the required data 
                            //eg. minute data is always used but strategy may not request it and therefore won't run code on that timeframe
                            if (strategy.RequiredData.ContainsKey(barSet.Key))
                            {

                                if (TestSummary.StartDate == null)
                                    TestSummary.StartDate = bar.OpenTime;

                                //run the bar
                                strategy.Run(barSet.Key, asset.Name);
                            }

                            strategy.BarIndices[barSet.Key]++;
                        }
                        //don't need to increment bars on the 1 minute time frame
                        else if (barSet.Key > 1)
                        {
                            //get the lastAdded bar which is the start of this timeframe
                            Bar lastAdded = lastAddedBars[barSet.Key];

                            //We adjust the bar for the max,min,vol and close
                            if (bar.BidHigh > lastAdded.BidHigh)
                                lastAdded.BidHigh = bar.BidHigh;
                            if (bar.BidLow < lastAdded.BidLow)
                                lastAdded.BidLow = bar.BidLow;
                            if (bar.AskHigh > lastAdded.AskHigh)
                                lastAdded.AskHigh = bar.AskHigh;
                            if (bar.AskLow < lastAdded.AskLow)
                                lastAdded.AskLow = bar.AskLow;
                            lastAdded.BidClose = bar.BidClose;
                            lastAdded.AskClose = bar.AskClose;
                            lastAdded.Volume += bar.Volume;
                        }
                    }


                }


                if (TestSummary.EndDate == null)
                    TestSummary.EndDate = bar.OpenTime;

                if (strategy.CloseOnExit)
                    strategy.BTCloseAllTrades(bar);

                //create a performance report and add it to the list
                TestSet ts = new TestSet(asset.Name, strategy.Description, strategy.ClosedTrades);

                //clear the data ready for the nxt asset
                strategy.ResetData();

                onComplete.Invoke(ts);
            }

        }



        }
    }
