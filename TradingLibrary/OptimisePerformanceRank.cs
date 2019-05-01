using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingLibrary
{
    [Serializable()]
    public class OptimisePerformanceRank
    {
        private List<int> ranks;


        public double Average
        {
            get
            {
                return ranks.Average();
            }
        }

        public double StdDev
        {
            get
            {
                double meanOfValues = ranks.Average();
                double sumOfValues = ranks.Select(v => (v - meanOfValues) * (v - meanOfValues)).Sum();
                int countOfValues = ranks.Count;
                double standardDeviationOfValues = Math.Sqrt(sumOfValues / (countOfValues));

                return standardDeviationOfValues;
            }
        }

        public OptimisePerformanceRank()
        {
            ranks = new List<int>();
        }

        public void Add(int rank)
        {
            ranks.Add(rank);
        }

        public string Band(int count)
        {
            string str = "";

            double min= Average - StdDev * 1.5;
            double max = Average + StdDev * 1.5;

            int minPercent = (int)(min / (double)count * 100);
            int maxPercent = (int)(max / (double)count * 100);
            if (minPercent < 0)
                minPercent = 0;
            if (maxPercent > 99)
                maxPercent = 99;

            for(int i=0; i < 100; i++)
            {
                if (i < minPercent || i > maxPercent)
                    str += ".";
                else if (i == minPercent)
                    str += "[";
                else if (i == maxPercent)
                    str += "]";
                else
                    str += "-";
            }

            return str;
        }

        public string List(int max = 0)
        {
            string str = "";

            foreach (int val in ranks)            
                str += val.ToString("000") + ";";
            

            return str;
        }

    }
}
