using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class Bar
    {

        public DateTime OpenTime { get; set; }
        
        public float BidOpen { get; set; }
        public float BidHigh { get; set; }
        public float BidLow { get; set; }
        public float BidClose { get; set; }
        public float AskOpen { get; set; }
        public float AskHigh { get; set; }
        public float AskLow { get; set; }
        public float AskClose { get; set; }
        public int Volume { get; set; }

        public int byteIndex { get; set; }

        public Bar(Bar bar)
        {

            OpenTime = bar.OpenTime;

            BidOpen = bar.BidOpen;
            BidClose = bar.BidClose;
            BidHigh = bar.BidHigh;
            BidLow = bar.BidLow;
            AskOpen = bar.AskOpen;
            AskClose = bar.AskClose;
            AskHigh = bar.AskHigh;
            AskLow = bar.AskLow;
            Volume = bar.Volume;
        }

        public Bar()
        {
        }

        public void Update(float value, bool isBid)
        {
            if(isBid)
            {
                if (BidOpen == 0) //first bid of bar - may have initiated with a bid
                {
                    BidOpen = value;
                    BidHigh = value;
                    BidLow = value;
                }
                else
                {
                    if (value > BidHigh)
                        BidHigh = value;
                    else if (value < BidLow)
                        BidLow = value;
                }

                BidClose = value;

                
            }
            else
            {
                if (AskOpen == 0) //first ask of bar 
                {
                    AskOpen = value;
                    AskHigh = value;
                    AskLow = value;
                }
                else
                {
                    if (value > AskHigh)
                        AskHigh = value;
                    else if (value < AskLow)
                        AskLow = value;
                }                

                AskClose = value;
            }

            //just count the Bid ticks
            Volume++;


        }

        public Bar(DateTime barOpen, float value, bool isBid)
        {
            OpenTime = barOpen;
            Volume = 1;
            if(isBid)
            {
                BidOpen = value;
                BidClose = value;
                BidHigh = value;
                BidLow = value;
            }
            else
            {
                AskOpen = value;
                AskClose = value;
                AskHigh = value;
                AskLow = value;
            }
        }

        public Bar(string csv)
        {

            string[] fields = csv.Split(new char[] { ',' });

            string[] formats = { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd" };
            OpenTime = DateTime.ParseExact(fields[0], formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);
            BidClose = Convert.ToSingle(fields[2]);
            BidHigh = Convert.ToSingle(fields[3]);
            BidLow = Convert.ToSingle(fields[4]);
            AskOpen = Convert.ToSingle(fields[5]);
            AskClose = Convert.ToSingle(fields[6]);
            AskHigh = Convert.ToSingle(fields[7]);
            AskLow = Convert.ToSingle(fields[8]);

            Volume = Convert.ToInt32(fields[9]);
        }

        public double? GetVal(string label)
        {
            //Gets a double value of the passed string from either the hardcoded variables
            //or the additional variables in the data array such as precalcualted features
            switch (label)
            {
                case "BidOpen":
                    return BidOpen;
                case "BidClose":
                    return BidClose;
                case "BidHigh":
                    return BidHigh;
                case "BidLow":
                    return BidLow;
                case "AskOpen":
                    return AskOpen;
                case "AskClose":
                    return AskClose;
                case "AskHigh":
                    return AskHigh;
                case "AskLow":
                    return AskLow;
                case "Volume":
                    return Volume;
            };

            return 0;
        }


        public override string ToString()
        {
            return OpenTime.ToString("yyyy-MM-dd HH:mm:ss") +"," + BidOpen + "," + BidClose + "," + BidHigh + "," + BidLow + "," + AskOpen + "," + AskClose + "," + AskHigh + "," + AskLow + "," + Volume;
        }

    }
}
