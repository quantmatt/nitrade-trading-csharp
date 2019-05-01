using System;
using System.Collections.Generic;


namespace TradingLibrary
{
    public class Asset
    {

        public string DataPath { get; set; }
        public string Name { get; set; }
        public byte[] Dataset { get; set; }
        public Dictionary<int, PreCalculatedFeatures> Data { get; set; }
        public Delegate DisplayMessage { get; set; }
        public int Digits { get; set; }
        public double Pip { get; set; }
        public double PipValue { get; set; }
        public Dictionary<int, bool> LookbackDownloaded { get; set; }

        public Asset()
        {
            Dataset = null;
            Digits = 5;
            Pip = 0.0001;
            LookbackDownloaded = new Dictionary<int, bool>();
            Data = new Dictionary<int, PreCalculatedFeatures>();
        }

        public static Dictionary<string, Asset> LoadAssetFile(string filename)
        {           

            Dictionary<string, Asset> assets = new Dictionary<string, Asset>();

            string[] assetLines = System.IO.File.ReadAllLines(filename);

            //loop through lines but skip the header
            for (int i=1; i < assetLines.Length; i++)
            {
                Asset asset = new Asset();
                string[] fields = assetLines[i].Split(new char[] { ',' });

                asset.Name = fields[0];
                asset.Digits = Convert.ToInt32(fields[1]);
                asset.PipValue = Convert.ToDouble(fields[2]);
                asset.Pip = Convert.ToDouble(fields[3]);
                asset.DataPath = fields[4];

                assets.Add(asset.Name, asset);

            }

            return assets;
        }

        public double GetPipCost(string localCurrency, Dictionary<string, double> assetRates)
        {
            string name = Name.Replace("/", "").Substring(0, 6);
            string leftCurr = name.Substring(0, 3);
            string rightCurr = name.Substring(3, 3);

            //pip cost is $10 per lot if currency is listed on the right
            double pipCost = 10;
            if (rightCurr == localCurrency)
                pipCost = 10;
            //If listed on the left just divide the pip cost by the current rate (ie. inverse)
            else if (leftCurr == localCurrency)
            {
                pipCost = pipCost / assetRates[Name];
            }
            else
            {
                //otherwise we need to find the currency pair that contains our local currency
                bool found = false;
                foreach (KeyValuePair<string, double> assetRate in assetRates)
                {
                    string name2 = assetRate.Key.Replace("/", "").Substring(0, 6);
                    string leftCurr2 = name2.Substring(0, 3);
                    string rightCurr2 = name2.Substring(3, 3);

                    //If the local currency is the left then just divide the pip cost by this rate
                    if (localCurrency == leftCurr2 && rightCurr == rightCurr2)
                    {
                        pipCost = pipCost / assetRate.Value;
                        found = true;
                        break;
                    }
                    //otherwise we need to get the inverse of this rate
                    else if (localCurrency == rightCurr2 && rightCurr == leftCurr2)
                    {
                        pipCost = pipCost / (1 / assetRate.Value);
                        found = true;
                        break;
                    }


                }
                if (!found)
                    throw new Exception("Could not calculate pip cost for " + Name + " in local currency " + localCurrency);
            }

            //For yen pairs need to multiply by 10 because Jen is 3 digits not 5 digits
            if (leftCurr.ToUpper() == "JPY" || rightCurr.ToUpper() == "JPY")
                pipCost = pipCost * 100;

            return pipCost;
        }

        

      

      
    }
}
