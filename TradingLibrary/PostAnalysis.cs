using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingLibrary
{
    public static class PostAnalysis
    {
        public static TestSummary ReduceCorrelated(TestSummary testSummary)
        {          

            //Work out the percentage of trades to use to train
            DateTime startDate = (DateTime)testSummary.StartDate;
            DateTime endDate = (DateTime)testSummary.EndDate;
            double trainDays = (endDate - startDate).TotalDays * ((double)testSummary.ReduceCorrelatedParams.TrainTestSplit/100);
            DateTime endTrain = startDate.AddDays(trainDays);

            testSummary.EndTrainDate = endTrain;

            //Create a copy of the testSummary so we don't modify the original testSummary when we reduce the testsets
            TestSummary filteredTestSummary = new TestSummary(testSummary);

            //remove any non-performing sets
            List<TestSet> nonPerformers = new List<TestSet>();
            int totalProfitable = 0;
            foreach (TestSet t in testSummary.TestSets)
            {
                double p = PerformanceResult.CalculateProfitFactor(t.Trades.Where(x => x.CloseTime <= filteredTestSummary.EndTrainDate).ToArray());
                if (p > 1)
                    totalProfitable++;
                if (p < testSummary.ReduceCorrelatedParams.MinMetric)
                    filteredTestSummary.Remove(t);
            }

            //remove all the test sets if at least half are profitable
            double pProfitable = (double)totalProfitable / (double)testSummary.TestSets.Length;
            if (pProfitable < 0.5)
            {
                foreach (TestSet t in testSummary.TestSets)
                    filteredTestSummary.Remove(t);
            }

            //Calcualte weekly profit for every week so we can do a correlation based on weekly profits
            Dictionary<string, KeyValuePair<int, double>[]> WeeklyProfits = new Dictionary<string, KeyValuePair<int, double>[]>();
            foreach (TestSet ts in testSummary.TestSets)
            {

                
                //get all the trades in the train period and calculate weekly profit
                var result =
                     from s in ts.Trades.Where(x => x.CloseTime <= endTrain)
                     group s by new { week = (s.OpenTime.Year - startDate.Year) * 52 + (s.OpenTime.DayOfYear / 7) } into g
                     select new KeyValuePair<int, double>(g.Key.week, g.Sum(x => x.Profit));

                WeeklyProfits.Add(ts.Description, result.ToArray());
            }

            //Create a grid of r2 values by comparing each testset with each other test set
            Dictionary<Pair, double> r2Values = new Dictionary<Pair, double>();
            foreach (KeyValuePair<string, KeyValuePair<int, double>[]> wpRow in WeeklyProfits)
            {
                foreach (KeyValuePair<string, KeyValuePair<int, double>[]> wpColumn in WeeklyProfits)
                {
                    //skip identical resuklt sets
                    if (wpColumn.Key == wpRow.Key)
                        continue;
                    //calculate the r2 value from these lists
                    

                    //Line up the weeks to get an x and y for the current pair
                    Dictionary<int, Point> list = new Dictionary<int, Point>();
                    foreach(KeyValuePair<int, double> res in wpRow.Value)
                    {
                        list.Add(res.Key, new Point(res.Value, 0, wpRow.Key, null));
                    }
                    foreach (KeyValuePair<int, double> res in wpColumn.Value)
                    {
                        if (!list.ContainsKey(res.Key))
                            list.Add(res.Key, new Point(0, res.Value, null, wpColumn.Key));
                        else
                        {
                            list[res.Key].Y = res.Value;
                            list[res.Key].YLabel = wpColumn.Key;
                        }

                    }
                    double[] x = list.Select(v => v.Value.X).ToArray();
                    double[] y = list.Select(v => v.Value.Y).ToArray();

       
                    //calculate the r2 and store in dictionary with the testset description pair as the Key
                    r2Values.Add(new Pair(wpRow.Key, wpColumn.Key), Stat.R2(x, y));
                }
            }

            

            foreach(KeyValuePair<Pair, double> res in r2Values)
            {
                //if too corelated remove the worst performer
                if(res.Value > testSummary.ReduceCorrelatedParams.R2Cutoff)
                {
                    //get the train set of trades only
                    Trade[] xTrades = filteredTestSummary.GetTradeSet(Convert.ToString(res.Key.P1), TestSummary.TradeSet.Train);
                    Trade[] yTrades = filteredTestSummary.GetTradeSet(Convert.ToString(res.Key.P2), TestSummary.TradeSet.Train);

                    //if both exist in our filtered test sets remove worst performer - it may have already been removed from previous pair r2 comparisons
                    if (xTrades != null && yTrades != null)
                    {
                        double xMetric = PerformanceResult.CalculateProfitFactor(xTrades);
                        double yMetric = PerformanceResult.CalculateProfitFactor(yTrades);

                        if (xMetric > yMetric)
                        {
                            filteredTestSummary.Remove(Convert.ToString(res.Key.P2));                           
                        }
                        else
                        {
                            filteredTestSummary.Remove(Convert.ToString(res.Key.P1));
                        }
                    }
                }
            }


            return filteredTestSummary;

        }

        public static TestSummary ReduceByRank(TestSummary testSummary)
        {
            //dn't filter anymore if there is less than 2 testSets
            if (testSummary.TestSets.Count() < 2)
                return testSummary;

            //copy the original test summary so we don't modify it directily
            TestSummary filteredTestSummary = new TestSummary(testSummary);

            //cycle through this process of deleteing the worst average ranking test set until the remaining test sets have no real rank differentation
            //These test sets should have already been reduced to remove correlated test sets so we should end up with a number of uncorrelated test sets that all perform about the same
            double rankDiff = 0;
            do
            {
                //setup the dates for starting the train and test periods
                DateTime startDate = (DateTime)testSummary.StartDate;
                DateTime endDate = (DateTime)testSummary.EndTrainDate; // this is set in the remove correlated method
                DateTime testStartDate = startDate;
                DateTime testEndDate = testStartDate.AddDays(testSummary.ReduceByRankParams.PeriodDays);

                //keep a list of rankings of each test set compared to the other parameter sets for each cycle
                Dictionary<string, OptimisePerformanceRank> ranks = new Dictionary<string, OptimisePerformanceRank>();

                //cycle through the dates until the end of the train set
                while (testStartDate < endDate)
                {
                    //compile an dictonary of the results for this cycle the key is the parameter set string
                    Dictionary<string, double> testResults = new Dictionary<string, double>();

                    //Calcualte a profit factor for each of the test sets between the current cycle dates
                    foreach (TestSet ts in filteredTestSummary.TestSets)
                    {
                        Trade[] trades = ts.Trades.Where(x => x.OpenTime > testStartDate && x.CloseTime <= testEndDate).ToArray();                        
                        testResults.Add(ts.Description, PerformanceResult.CalculateProfitFactor(trades));
                    }

                    //rank the test sets
                    var ordered = testResults.OrderByDescending(x => x.Value);
                    int rank = 1;
                    foreach (KeyValuePair<string, double> result in ordered)
                    {
                        if (!ranks.ContainsKey(result.Key))          
                            ranks.Add(result.Key, new OptimisePerformanceRank());                            
                        
                        ranks[result.Key].Add(rank);

                        rank++;
                    }

                    //move the dates along
                    testStartDate = testStartDate.AddDays(testSummary.ReduceByRankParams.PeriodDays);
                    testEndDate = testEndDate.AddDays(testSummary.ReduceByRankParams.PeriodDays);
                    if (testEndDate > endDate)
                        testEndDate = endDate;
                }

                //Print out the table of the parameter sets ranked for each cycle and the average overall rank
                var orderedRanks = ranks.OrderBy(x => x.Value.Average).ToDictionary(t => t.Key, t => t.Value);
                
                //calcaulte the maximum difference in rank between the test sets - we exit the loop if this difference is small enough
                rankDiff = orderedRanks.Max(x => x.Value.Average) - orderedRanks.Min(x => x.Value.Average);

                //copy the original test summary so we don't modify it directily
                filteredTestSummary = new TestSummary(filteredTestSummary);

                //remove the lowest ranking test set
                filteredTestSummary.Remove(orderedRanks.LastOrDefault().Key);

            } while (testSummary.ReduceByRankParams.MaxRankDifference > 0 && rankDiff > testSummary.ReduceByRankParams.MaxRankDifference);

            return filteredTestSummary;

        }

        /*

        public static PerformanceReport[] ParameterStabilityReduction(PerformanceReport[] reports, int testDays, double maxRankRange=0, string assetName = null)
        {
            //declare an array to use for filtering reports for one particular asset - will remain unchanged if asstName isn;t provided
            PerformanceReport[] assetReports = reports;

            //if an assetname is provided just get the reports for this asset
            if (assetName != null)
                assetReports = reports.Where(x => x.Asset == assetName).ToArray();

            double rankDiff = 0;

            do
            {
                //setup the dates for starting the train and test periods
                DateTime startDate = reports.FirstOrDefault().StartDate;
                DateTime endDate = reports.FirstOrDefault().TrainDate; // this is set in the remove correlated method
                DateTime testStartDate = startDate;
                DateTime testEndDate = testStartDate.AddDays(testDays);
 
                //keep a list of rankings of each test set compared to the other parameter sets for each cycle
                Dictionary<string, OptimisePerformanceRank> ranks = new Dictionary<string, OptimisePerformanceRank>();

                //cycle through the dates until the end of the train set
                while (testStartDate < endDate)
                {
                    //compile an dictonary of the results for this cycle the key is the parameter set string
                    Dictionary<string, PerformanceReport> testResults = new Dictionary<string, PerformanceReport>();

                    //create the train and test sets for each trade
                    foreach (PerformanceReport pr in assetReports)
                    {
                        PerformanceReport testPr = new PerformanceReport(pr.Trades.Where(x => x.OpenTime > testStartDate && x.CloseTime <= testEndDate).ToArray());
                        testPr.QuickSummary();
                        testResults.Add(pr.Description, testPr);
                    }

                    //rank the test sets
                    var ordered = testResults.OrderByDescending(x => x.Value.ProfitFactor);
                    int rank = 1;
                    foreach (KeyValuePair<string, PerformanceReport> result in ordered)
                    {
                        if (!ranks.ContainsKey(result.Key))
                        {
                            OptimisePerformanceRank rankResult = new OptimisePerformanceRank();
                            ranks.Add(result.Key, rankResult);
                            result.Value.RankResults = rankResult; //save this in the performance report for use later
                        }
                        ranks[result.Key].Add(rank);

                        rank++;
                    }

                    //move the dates along
                    testStartDate = testStartDate.AddDays(testDays);
                    testEndDate = testEndDate.AddDays(testDays);
                }

                //Print out the table of the parameter sets ranked for each cycle and the average overall rank
                Dictionary<string, OptimisePerformanceRank> orderedRanks = ranks.OrderBy(x => x.Value.Average).ToDictionary(t => t.Key, t => t.Value);
                Console.WriteLine("Set of parameters".PadRight(20) + "|" + "Avg".PadRight(6) + "|StdDev");
                List<string> orderedKeys = new List<string>();
                foreach (KeyValuePair<string, OptimisePerformanceRank> rank in orderedRanks)
                {
                    Console.WriteLine(rank.Key.PadRight(20) + " | " + rank.Value.Average.ToString("0.0").PadRight(6) + " | " + rank.Value.StdDev.ToString("0.0").PadRight(6) + " | " + rank.Value.Band(ranks.Count));
                    orderedKeys.Add(rank.Key);
                }

                rankDiff = orderedRanks.Max(x => x.Value.Average) - orderedRanks.Min(x => x.Value.Average);

                List<PerformanceReport> keepReports = new List<PerformanceReport>();
                //take the top length - drop rank reports and use this to select the keep reports from the original reports.
                foreach (KeyValuePair<string, OptimisePerformanceRank> rank in orderedRanks.Take(orderedRanks.Count() - 1))
                {
                    keepReports.Add(reports.Where(x => x.Description == rank.Key).FirstOrDefault());
                }

                assetReports = keepReports.ToArray();

            } while (maxRankRange > 0 && rankDiff > maxRankRange);

            return assetReports;

        }

        public static PerformanceReport WFO(PerformanceReport[] reports, int trainDays, int testDays, string assetName = null)
        {
            //keep a trade list of the trades from the test sets with the selected parameted for that cylcle
            List<Trade> stichedWFOTrades = new List<Trade>();
            //keep a description of the dates and parameters that were selected for each cycle
            List<string> parameterSelection = new List<string>();

            //declare an array to use for filtering reports for one particular asset - will remain unchanged if asstName isn;t provided
            PerformanceReport[] assetReports = reports;

            //if an assetname is provided just get the reports for this asset
            if (assetName != null)
                assetReports = reports.Where(x => x.Asset == assetName).ToArray();

            //setup the dates for starting the train and test periods
            DateTime startDate = reports.FirstOrDefault().StartDate;
            DateTime endDate = reports.FirstOrDefault().EndDate;
            DateTime currentTrainDate = startDate.AddDays(trainDays);
            DateTime testEndDate = currentTrainDate.AddDays(testDays);

            //keep a result of all the test sets for every parameter combination
            Dictionary<string, List<Trade>> stichedTestResults = new Dictionary<string, List<Trade>>();

            //keep a list of rankings of each test set compared to the other parameter sets for each cycle
            Dictionary<string, OptimisePerformanceRank> ranks = new Dictionary<string, OptimisePerformanceRank>();
            ranks.Add("WFO", new OptimisePerformanceRank());

            //cycle through the dates until the end of the test
            while (currentTrainDate < endDate)
            {
                //compile an dictonary of the results for this cycle train set, the key is the parameter set string
                Dictionary<string, PerformanceReport> trainResults = new Dictionary<string, PerformanceReport>();
                Dictionary<string, PerformanceReport> testResults = new Dictionary<string, PerformanceReport>();

                //create the train and test sets for each trade
                foreach(PerformanceReport pr in assetReports)
                {
                    PerformanceReport trainPr = new PerformanceReport(pr.Trades.Where(x => x.OpenTime > startDate && x.CloseTime <= currentTrainDate).ToArray());
                    PerformanceReport testPr = new PerformanceReport(pr.Trades.Where(x => x.OpenTime > currentTrainDate && x.CloseTime <= testEndDate).ToArray());
                    trainPr.QuickSummary();
                    testPr.QuickSummary();
                    trainResults.Add(pr.Description, trainPr);
                    testResults.Add(pr.Description, testPr);

                    //for all test results keep a running list of trades for each parameter setup
                    if (!stichedTestResults.ContainsKey(pr.Description))
                        stichedTestResults.Add(pr.Description, new List<Trade>());
                    stichedTestResults[pr.Description].AddRange(testPr.Trades);

                }


                //select the best set of the train results
                var bestSet = trainResults.OrderByDescending(x => x.Value.ProfitFactor).FirstOrDefault();
                //get the trades from the test set and add it to the stiched wfo trades
                Trade[] testTrades = testResults.Where(x => x.Key == bestSet.Key).FirstOrDefault().Value.Trades;
                stichedWFOTrades.AddRange(testTrades);
                parameterSelection.Add(currentTrainDate.ToString("dd/MM/yyyy") + " - " + testEndDate.ToString("dd/MM/yyyy") + " " + bestSet.Key);

                //rank the test sets
                var ordered = testResults.OrderByDescending(x => x.Value.ProfitFactor);
                int rank = 1;
                foreach (KeyValuePair<string, PerformanceReport> result in ordered)
                {
                    if (!ranks.ContainsKey(result.Key))
                    {
                        OptimisePerformanceRank rankResult = new OptimisePerformanceRank();
                        ranks.Add(result.Key, rankResult);
                        result.Value.RankResults = rankResult; //save this in the performance report for use later
                    }
                    ranks[result.Key].Add(rank);

                    //store the rank of the selected best set in the WFO key
                    if (result.Key == bestSet.Key)
                        ranks["WFO"].Add(rank);

                    rank++;
                }

                //move the dates along
                startDate = startDate.AddDays(testDays);
                currentTrainDate = currentTrainDate.AddDays(testDays);
                testEndDate = currentTrainDate.AddDays(testDays);
            }

            //Print out the table of the parameter sets ranked for each cycle and the average overall rank
            var orderedRanks = ranks.OrderBy(x => x.Value.Average);
            Console.WriteLine("Set of parameters".PadRight(20) +"|"+"Avg".PadRight(6)+"|StdDev");
            foreach (KeyValuePair<string, OptimisePerformanceRank> rank in orderedRanks)
            {
                Console.WriteLine(rank.Key.PadRight(20) + " | " + rank.Value.Average.ToString("0.0").PadRight(6) + " | " + rank.Value.StdDev.ToString("0.0").PadRight(6) + " | " + rank.Value.Band(ranks.Count));
            }

            //create a WFO report from the wfo stiched test trades
            PerformanceReport wfoReport = new PerformanceReport(stichedWFOTrades.ToArray());
            wfoReport.SetupDescription = parameterSelection.ToArray();
            wfoReport.Description = "WFO";
            wfoReport.QuickSummary();

            //start compiling a list of the reports to compare the wfo performance with that of static parameter sets
            List<PerformanceReport> allReports = new List<PerformanceReport>();
            allReports.Add(wfoReport);
            foreach(KeyValuePair<string, List<Trade>> result in stichedTestResults)
            {
                PerformanceReport pr = new PerformanceReport(result.Value.ToArray());
                pr.Description = result.Key;
                pr.QuickSummary();
                allReports.Add(pr);
            }

            //order the results and display 
            var orderedPrs = allReports.OrderByDescending(x => x.ProfitFactor);
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("Result".PadRight(20) + "|" +  "PF".PadRight(10) +"|" +"Profit".PadRight(10) + "|Trades");
            foreach(PerformanceReport pr in orderedPrs)
            {
                Console.WriteLine(pr.SummaryLine());
            }

            return wfoReport;

        }
        */


    }
}
