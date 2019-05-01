using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public static class StandardIndicators
    {

        public static double ATR(Bar[] bars, int periods)
        {

            double tally = 0;
            double count = 0;

            for(int i=1; i < bars.Length-1; i++)
            {
       

                if (count == periods)
                    break;

                if (bars[i] != null && bars[i + 1] != null)
                {

                    double a = bars[i].AskHigh - bars[i].AskLow;
                    double b = Math.Abs(bars[i].AskHigh - bars[i + 1].AskClose);
                    double c = Math.Abs(bars[i].AskLow - bars[i + 1].AskClose);

                    double max = Math.Max(a, Math.Max(b, c));

                    tally += (double)max;
                    count++;
                }
            }

            return tally / count;
        }

        public static double SMA(object[] values, int periods)
        {
            double tally = 0;
            double count = 0;

            foreach (object value in values)
            {

                if (count == periods)
                    break;

                if (value != null)
                {
                    tally += (double)value;
                    count++;
                }
            }

            return tally / count;

        }
    }
}
