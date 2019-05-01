using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingLibrary
{
    public class PreCalculatedFeatures
    {
        public Dictionary<DateTime, Dictionary<string, double?>>  Data { get; set; }

        public PreCalculatedFeatures()
        {
            Data = new Dictionary<DateTime, Dictionary<string, double?>>();
        }

        public void FillFromCsv(string[] csv, string[] labels)
        {
            // Read the results into the bars from the binary file that python has written over

            foreach (string str in csv)
            {
                
                if (str.Length > 0)
                {
                    string[] parts = str.Split(new char[] { ',' });

                    int index = 0;

                    //convert date
                    DateTime dt = DateTime.ParseExact(parts[index++], "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

                    //Add a new bar 
                    Dictionary<string, double?> featureData = new Dictionary<string, double?>();
                    Data.Add(dt, featureData);

                    foreach (string field in labels)
                    {
                        if (parts[index].Length > 0)
                            featureData.Add(field, Convert.ToDouble(parts[index]));
                        else
                            featureData.Add(field, null);
                        index++;
                    }
                }
                
            }
        }

        public void RemoveOldest()
        {
            Data.Remove(Data.Last().Key);
        }

        public void Merge(PreCalculatedFeatures newPcs)
        {
            foreach(KeyValuePair<DateTime,Dictionary<string, double?>> bar in newPcs.Data)
            {
                if (!Data.ContainsKey(bar.Key))
                    Data.Add(bar.Key, bar.Value);
                else
                    Data[bar.Key] = bar.Value;
            }
        }
    }
}
