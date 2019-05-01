using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TradingLibrary
{
    public enum DataFeedType { Bid, Ask, Both, BidOpen, BidClose, BidHigh, BidLow, AskOpen, AskClose, AskHigh, AskLow, Volume };

    public static class DataBuilder
    {
        static string FeatureBuildPath = Path.Combine("python_scripts","build_features.py");        

        public static DateTime ReadMinBinaryBar(byte[] bytes, int barIndex)
        {
            //Each bar is 44 bytes long
            int index = barIndex * 44;

            //Read the required data from the byte array and increment the index
            long timestamp = BitConverter.ToInt64(bytes, index);
 

            DateTime dt = DateTime.FromBinary(timestamp / 100).AddYears(1969);
          

            return dt;
        }

        public static Bar ReadBinaryBar(byte[] bytes, int barIndex)
        {
            //Each bar is 44 bytes long
            int index = barIndex * 44;

            //Read the required data from the byte array and increment the index
            long timestamp = BitConverter.ToInt64(bytes, index);
            index += 8;
            
            DateTime dt = DateTime.FromBinary(timestamp / 100).AddYears(1969);
            Bar bar = new Bar();
            bar.OpenTime = dt;
            bar.BidOpen = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.BidClose = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.BidHigh = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.BidLow = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.AskOpen = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.AskClose = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.AskHigh = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.AskLow = BitConverter.ToSingle(bytes, index);
            index += 4;
            bar.Volume = BitConverter.ToUInt16(bytes, index);

            return bar;
        }

       

        public static byte[] LoadBinary(string filename)
        {
            byte[] bytes = File.ReadAllBytes(filename);
            return bytes;
        }

        public static void CsvToBinary(string csvFilename, string binaryFilename, bool hasHeader, MessageDelegate messageDelegate = null)
        {
            //Record start time of process for time taken message
            DateTime now = DateTime.Now;

            //Loads from CSV
            messageDelegate?.Invoke(csvFilename + " reading...", MessageType.Message);
            string[] lines = File.ReadAllLines(csvFilename);

            //skip the header if it has one in the csv
            int startIndex = 0;
            if (hasHeader)
                startIndex = 1;

            //convert the csv into an array of Bar objects OHLC for both bid and ask + volume
            Bar[] dataset = new Bar[lines.Length - startIndex];
            for (int i = startIndex; i < lines.Length; i++)
                dataset[i-startIndex] = new Bar(lines[i]);

            //Remove duplicates and order from oldest to newest
            dataset = dataset.OrderBy(x => x.OpenTime).Distinct(x => x.OpenTime).ToArray();

            //Save it to binary
            DatasetToBinary(binaryFilename, dataset);

            messageDelegate?.Invoke(csvFilename + " csv to binary " + binaryFilename +  " took: " + (DateTime.Now - now).TotalSeconds + " secs");
        }

        public static void DumpBarsToCsv(Bar[] bars, string filename)
        {
            //dump a dataset to csv so it can be read by humans
            List<string> lines = new List<string>();

            foreach (Bar bar in bars)
            {
                lines.Add(bar.ToString());
            }

            File.WriteAllLines(filename, lines);

        }

        public static void DatasetToBinary(string filename, Bar[] dataset, DataFeedType type = DataFeedType.Both)
        {
            //Writes an array of bar data to a binary file
            //Usually used by reading a csv file into a Bar array and then writing to binary.
            using (BinaryWriter writer = new BinaryWriter(File.Open(filename, FileMode.Create)))
            {
                foreach (Bar bar in dataset)
                {
                    if (bar != null)
                    {
                        //convert from python to .net date
                        writer.Write(bar.OpenTime.AddYears(-1969).ToBinary() * 100);
                        if (type == DataFeedType.Both || type == DataFeedType.Bid)
                        {
                            writer.Write(bar.BidOpen);
                            writer.Write(bar.BidClose);
                            writer.Write(bar.BidHigh);
                            writer.Write(bar.BidLow);
                        }
                        if (type == DataFeedType.Both || type == DataFeedType.Ask)
                        {
                            writer.Write(bar.AskOpen);
                            writer.Write(bar.AskClose);
                            writer.Write(bar.AskHigh);
                            writer.Write(bar.AskLow);
                        }
                        writer.Write(bar.Volume);
                    }

                }

            }
        }

        public static void DatasetToBinarySingle(string filename, Bar[] dataset, DataFeedType type)
        {
            //this function is used to create a binary time series of just one datafeed - used for
            //creating features in python for example.
            using (BinaryWriter writer = new BinaryWriter(File.Open(filename, FileMode.Create)))
            {
                foreach (Bar bar in dataset)
                {
                    //convert from python to .net date
                    writer.Write((bar.OpenTime.AddYears(-1969).ToBinary() * 100));      
                    switch(type)
                    {
                        case DataFeedType.BidOpen:
                            writer.Write(bar.BidOpen);
                            break;
                        case DataFeedType.BidClose:
                            writer.Write(bar.BidClose);
                            break;
                        case DataFeedType.BidHigh:
                            writer.Write(bar.BidHigh);
                            break;
                        case DataFeedType.BidLow:
                            writer.Write(bar.BidLow);
                            break;
                        case DataFeedType.AskOpen:
                            writer.Write(bar.AskOpen);
                            break;
                        case DataFeedType.AskClose:
                            writer.Write(bar.AskClose);
                            break;
                        case DataFeedType.AskHigh:
                            writer.Write(bar.AskHigh);
                            break;
                        case DataFeedType.AskLow:
                            writer.Write(bar.AskLow);
                            break;
                        case DataFeedType.Volume:
                            writer.Write((float)bar.Volume);
                            break;
                        default:
                            throw new Exception("Invalid datafeed type. Must be a single datefeed value for the DatasetToBinarySingle.");                
                    }

    
                }

            }
        }

        

        //This is very similar to the back test but decided to duplicate the code because the BackTest needs to very fast and didn't want to build timeframes and then backtest
        //instead backtesting builds the timeframes as it goes.
        public static Dictionary<int, Bar[]> BuildTimeFrames(Asset asset, int[] requiredTimeframes, MessageDelegate messageDelegate = null)
        {
            //Record start time of process for time taken message
            DateTime now = DateTime.Now;

            ///Can run this on separate threads per asset if we aren;t worried about drawdown calculations ect..
            messageDelegate?.Invoke(asset.Name + " Building timeframes ...");

            //key is the timeframe in minutes
            Dictionary<int, List<Bar>> datasets = new Dictionary<int, List<Bar>>();


            //Read all the bytes from the datapath from the 1 min timeframe if the data is not already loaded
            //this is very fast about 0.9 seconds for 10 years of minute data
            if (asset.Dataset == null)
                asset.Dataset = DataBuilder.LoadBinary(asset.DataPath);

            byte[] bytes = asset.Dataset;

            //add in the required minute bars with a 1 bar lookback
            //If a strategy needs these bars it will be overwritted in the next foreach loop
            datasets.Add(1, new List<Bar>());

            foreach (int timeframe in requiredTimeframes)
            {
                //overwrite the minute bar details if the strategy requires this
                if (timeframe != 1)
                {             
                    datasets.Add(timeframe, new List<Bar>());
                }

            }

            //keep a pointer of the last added bar and date so this can be modified while building up the higher timeframes
            Dictionary<int, Bar> lastAddedBars = new Dictionary<int, Bar>();
            Dictionary<int, DateTime> lastAddedDates = new Dictionary<int, DateTime>();


            //Traverse the byte array to build the bars - this can be done on separate threads for each asset for maximum speed
            int i = 0;
            while (i < bytes.Length)
            {
                Bar bar = DataBuilder.ReadBinaryBar(bytes, (i / 44));
                //move to next line in byte array - 1 bar is 44 bytes
                i += 44;

                //go through each timeframe and either create a new bar or increment the current bar
                foreach (KeyValuePair<int, List<Bar>> barSet in datasets)
                {
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

                    //add the bar to all timeframes if it doesnt exist
                    if ((nextBar <= barDate) || !lastAddedDates.ContainsKey(barSet.Key))
                    {
                        //need a new bar for each time frame so we don't have the same pointer in each timeframe
                        Bar setBar = new Bar(bar);

                        //Add to the bar list
                        barSet.Value.Add(setBar);

                        lastAddedBars[barSet.Key] = setBar;
                        lastAddedDates[barSet.Key] = barDate;

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

            //remove any duplicates and order from oldest to newest
            Dictionary<int, Bar[]> sortedData = new Dictionary<int, Bar[]>();
            foreach(KeyValuePair<int, List<Bar>> barSet in datasets)
            {
                //don't need the minute bar - that was just used for the bar build process
                if (barSet.Key != 1)
                    sortedData.Add(barSet.Key, barSet.Value.OrderBy(x => x.OpenTime).Distinct(x=> x.OpenTime).ToArray());
            }

            messageDelegate?.Invoke(asset.Name + " timeframe build took: " + (DateTime.Now - now).TotalSeconds + " secs");
            

            return sortedData;
        }

        public static void LoadExternalFeatureBinary(Asset asset, ExternalFeatureData externalFeature, MessageDelegate messageDelegate)
        {
            //Read the results into the bars from the binary file that python has written over
            PreCalculatedFeatures pcFeatures = new PreCalculatedFeatures();

            //[ASSET] is used as a placeholder so insert the assetname here
            string filename = externalFeature.BinaryFilepath.Replace("[ASSET]", asset.Name);

            byte[] bytes = File.ReadAllBytes(filename);

            //Traverse the byte array to add in the values to the Data attribute of the corresponding bar
            int i = 0;
            while (i < bytes.Length)
            {
                //Read the required data from the byte array and increment the index
                long timestamp = BitConverter.ToInt64(bytes, i);
                i += 8;
                //convert from python to .net date
                DateTime dt = DateTime.FromBinary(timestamp / 100).AddYears(1969);

                //Add a new bar 
                Dictionary<string, double?> barData = new Dictionary<string, double?>();
                pcFeatures.Data.Add(dt, barData);

                foreach (string field in externalFeature.FieldNames)
                {
                    double val = BitConverter.ToDouble(bytes, i);
                    barData.Add(field, val);
                    i += 8;
                }

            }

            //add this data to the asset (or overwrite if exists)
            if (!asset.Data.ContainsKey(externalFeature.Timeframe))
                asset.Data.Add(externalFeature.Timeframe, pcFeatures);
            else
                asset.Data[externalFeature.Timeframe] = pcFeatures;
        }
            
        public static void PythonFeatureBuilder(PythonBridge pb, Asset asset, ExternalFeatureData externalFeatureData, MessageDelegate messageDelegate = null)
        {
           
            messageDelegate?.Invoke(asset.Name + " Precalcualting Features ...");

            //Record start time of process for time taken message
            DateTime now = DateTime.Now;

            //build the features into a semicolon sepearated string
            string featureCommand = externalFeatureData.FeatureCommands;

            //get the dataset for this timeframe
            Dictionary<int, Bar[]> timeframes = DataBuilder.BuildTimeFrames(asset, new int[] { externalFeatureData.Timeframe });

            //Use a temporary Share directory for this data
            string filename = asset.DataPath.Replace(".bin", "_Share.bin");

            //Generate a tempory binary file containing the datafeed to be used for calculation and save to disk
            string datasetType = "single";
            if (externalFeatureData.CalculateOn == DataFeedType.Ask || externalFeatureData.CalculateOn == DataFeedType.Bid) //otherwise include all bid or ask OHLC and volume.
            {
                datasetType = "whole";
                DataBuilder.DatasetToBinary(filename, timeframes[externalFeatureData.Timeframe], externalFeatureData.CalculateOn);
            }
            else  //Faster way if just need a single data feed
                DataBuilder.DatasetToBinarySingle(filename, timeframes[externalFeatureData.Timeframe], externalFeatureData.CalculateOn);

            //[ASSET] is a placeholder 
            string transformedFilename = externalFeatureData.BinaryFilepath.Replace("[ASSET]", asset.Name);

            //Bridge python to calculate the data            
            string[] commands = new string[] { datasetType, filename, transformedFilename,
                externalFeatureData.FeatureCommands };
            pb.RunScript(FeatureBuildPath, commands);

            File.Delete(filename);
            
            messageDelegate?.Invoke(asset.Name + " feature calculations took: " + (DateTime.Now - now).TotalSeconds + " secs");
 
        }



        public static IEnumerable<TSource> Distinct<TSource, TResult>(this IEnumerable<TSource> items,
                                                                      Func<TSource, TResult> selector)
        {
            var set = new HashSet<TResult>();
            if (items != null)
                foreach (TSource item in items)
                {
                    TResult hash = selector(item);
                    if (!set.Contains(hash))
                    {
                        set.Add(hash);
                        yield return item;
                    }
                }
        }



    }
}
