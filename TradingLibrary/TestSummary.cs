using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace TradingLibrary
{

    [Serializable()]
    public class TestSummary
    {

        public enum TradeSet { All, Train, Test };

        //Start of the testing period
        public DateTime? StartDate { get; set; }

        //End of the testing period
        public DateTime? EndDate { get; set; }

        //End of the train period in a Train/Test spilt
        public DateTime? EndTrainDate { get; set; }

        public string Description { get; set; }
        public string[] TradeDataLabels { get; set; }

        private List<TestSet> testSets;

        public ReduceCorrelatedParams ReduceCorrelatedParams { get; set; }
        public ReduceByRankParams ReduceByRankParams { get; set; }

        public TestSet[] TestSets
        {
            get
            {
                return testSets.ToArray();
            }
        }

        public Trade[] Trades
        {
            get
            {
                List<Trade> tradesList = new List<Trade>();
                //get the trades from all test sets
                foreach (TestSet testSet in testSets)
                    tradesList.AddRange(testSet.Trades);

                return tradesList.ToArray();
            }
        }

        public TestSummary()
        {
            testSets = new List<TestSet>();
        }

        public void Add(TestSet set)
        {
            testSets.Add(set);
        }

        public void Remove(TestSet set)
        {
            testSets.Remove(set);
        }

        public void Remove(string description)
        {
            TestSet ts = testSets.Where(x => x.Description == description).FirstOrDefault();
            if(ts != null)
                testSets.Remove(ts);

        }

        private void Copy(TestSummary original, TestSummary newObject)
        {
            newObject.StartDate = original.StartDate;
            newObject.EndDate = original.EndDate;
            newObject.EndTrainDate = original.EndTrainDate;
            newObject.ReduceCorrelatedParams = original.ReduceCorrelatedParams;
            newObject.ReduceByRankParams = original.ReduceByRankParams;
            newObject.TradeDataLabels = original.TradeDataLabels;
        }

        public TestSummary(TestSummary original)
        {
            Copy(original, this);
            testSets = new List<TestSet>(original.TestSets);
        }

        //makes a copy of the test summary but filters out the tests that relate to the passed assetName
        public TestSummary(TestSummary original, string assetName)
        {
            Copy(original, this);
            Description = assetName;
            testSets = new List<TestSet>(original.TestSets.Where(x => x.Asset == assetName).ToArray());
        }

        public PerformanceResult GetPerformanceResult(TradeSet set = TradeSet.All)
        {
            
            List<Trade> tradesList = new List<Trade>();

            //get the trades from all test sets
            foreach (TestSet testSet in testSets)
            {
                //can split these into trades from train and test set if TradeSet parameter is passed
                if (set == TradeSet.All)
                    tradesList.AddRange(testSet.Trades);
                else if (set == TradeSet.Test)
                    tradesList.AddRange(testSet.Trades.Where(x => x.OpenTime > EndTrainDate));
                else if(set == TradeSet.Train)
                    tradesList.AddRange(testSet.Trades.Where(x => x.CloseTime <= EndTrainDate));
            }

            Trade[] trades = tradesList.ToArray();

            PerformanceResult pr = new PerformanceResult();
            pr.Description = Description + " " + set.ToString();
            pr.TradeCount = trades.Count();
            pr.TotalProfit = trades.Sum(x => x.Profit);
            pr.SpreadCost = trades.Sum(x => x.SpreadCost);
            pr.WinPercent = PerformanceResult.CalculateWinPercent(trades);
            pr.ProfitFactor = PerformanceResult.CalculateProfitFactor(trades);

            return pr;

        }

        public Trade[] GetTradeSet(string description, TradeSet set = TradeSet.All)
        {
            TestSet testSet = testSets.Where(x => x.Description == description).FirstOrDefault();

            if (testSet == null)
                return null;

            //can split these into trades from train and test set if TradeSet parameter is passed
            if (set == TradeSet.All)
                return testSet.Trades;
            else if (set == TradeSet.Test)
                return testSet.Trades.Where(x => x.OpenTime > EndTrainDate).ToArray();
            else if (set == TradeSet.Train)
                return testSet.Trades.Where(x => x.CloseTime <= EndTrainDate).ToArray();

            return null;

        }

        public void ToCsv(string filename, TradeSet set = TradeSet.All)
        {
            Trade[] trades = Trades;
            if (set == TradeSet.Test)
                trades = Trades.Where(x => x.CloseTime > EndTrainDate).ToArray();
            else if (set == TradeSet.Train)
                trades = Trades.Where(x => x.CloseTime <= EndTrainDate).ToArray();

            List<string> tradeStrings = new List<string>();

            tradeStrings.Add(Trade.GetCsvHeaders(TradeDataLabels));

            foreach (Trade trade in trades)
                tradeStrings.Add(trade.ToString());

            File.WriteAllLines(filename, tradeStrings);
        }

        public TestSummary[] SplitByAsset()
        {
            //this function is useful to split out results by asset for asset specific analysis

            List<TestSummary> grouped = new List<TestSummary>();

            //get all the asset names that exist in the test sets.
            string[] assets = TestSets.Select(x => x.Asset).Distinct().ToArray();

            //a new test summary object will be created for every asset containing only the test sets that relate to that asset
            foreach (string asset in assets)
            {
                //creaet a new test summary with test sets filtered by the asset name
                grouped.Add(new TestSummary(this, asset));
            }

            return grouped.ToArray();

        }

        public static TestSummary Merge(TestSummary[] tests)
        {
            //some initial error checking, train dates must all be the same to make sense of performance results in train and test periods
            if (tests.Select(x => x.EndTrainDate).Distinct().Count() != 1)
                throw new Exception("Can only merge trades that have the same end train date!");
            else if (tests.Length == 0)
                return null;

            //merges an array of TestSummaries back into one ie. for combining seperate asset sets back into one for analysis 
            TestSummary merged = new TestSummary();

            foreach(TestSummary ts in tests)
            {
                merged.testSets.AddRange(ts.TestSets);
            }

            //copy across the dates to the new testsummary
            merged.StartDate = tests.Min(x => x.StartDate);
            merged.EndDate = tests.Max(x => x.EndDate);
            merged.EndTrainDate = tests.FirstOrDefault().EndTrainDate;
            

            return merged;
        }

        public void Save(string filename)
        {
            Stream SaveFileStream = File.Create(filename);
            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter serializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            serializer.Serialize(SaveFileStream, this);
            SaveFileStream.Close();
        }

        public static TestSummary Load(string filename)
        {

            TestSummary ts = null;

            if (File.Exists(filename))
            {
                Stream openFileStream = File.OpenRead(filename);
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter deserializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                if (openFileStream.Length > 0)
                    ts = (TestSummary)deserializer.Deserialize(openFileStream);
                openFileStream.Close();
            }

            return ts;
        }
    }
}
