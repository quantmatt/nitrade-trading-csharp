using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    [Serializable()]
    public class ReduceCorrelatedParams
    {
        public int TrainTestSplit { get; set; }
        public double R2Cutoff { get; set; }
        public double MinMetric { get; set; }

        public ReduceCorrelatedParams() { }

        public ReduceCorrelatedParams(int trainTestSplit, double r2Cutoff, double minMetric)
        {
            TrainTestSplit = trainTestSplit;
            R2Cutoff = r2Cutoff;
            MinMetric = minMetric;
        }
    }
}
