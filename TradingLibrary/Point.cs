using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public string XLabel { get; set; }
        public string YLabel { get; set; }

        public Point(double x, double y, string xLabel, string yLabel)
        {
            X = x;
            Y = y;
            XLabel = xLabel;
            YLabel = yLabel;
        }
    }
}
