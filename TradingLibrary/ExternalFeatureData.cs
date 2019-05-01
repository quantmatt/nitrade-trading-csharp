using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class ExternalFeatureData
    {

        public int Timeframe { get; set; }
        //binary filepath is the transformed data
        public string BinaryFilepath { get; set; }
        public string FeatureCommands { get; set; }
        public string[] FieldNames { get; set; }
        public DataFeedType CalculateOn { get; set; }

        public ExternalFeatureData(int timeframe, string binaryFilepath, string[] fieldNames)
        {
            Timeframe = timeframe;
            BinaryFilepath = binaryFilepath;
            FieldNames = fieldNames;
        }

        public string FieldNamesAsString
        {
            get
            {
                string str = "";
                foreach (string s in FieldNames)
                    str += s + ";";
                return str;
            }
        }
    }
}
