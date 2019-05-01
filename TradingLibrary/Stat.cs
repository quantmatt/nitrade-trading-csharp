using System;
using System.Collections.Generic;
using System.Linq;

namespace TradingLibrary
{
    public static class Stat
    {

        public static double R2(double[] x, double[] y)
        {
            if ((x != null) && (y != null) && (x.Length == y.Length) && (x.Length > 0))
            {
                double[] xy = new double[x.Length];
                double[] x2 = new double[x.Length];
                double[] y2 = new double[x.Length];

                for (int i = 0; i < x.Length; ++i)
                {
                    xy[i] = x[i] * y[i];
                    x2[i] = x[i] * x[i];
                    y2[i] = y[i] * y[i];
                }

                double n = x.Length;
                double sumxy = xy.Sum();
                double sumx = x.Sum();
                double sumy = y.Sum();
                double sumx2 = x2.Sum();
                double sumxall2 = x.Sum() * x.Sum();
                double sumy2 = y2.Sum();
                double sumyall2 = y.Sum() * y.Sum();

                double r = (n * sumxy - (sumx * sumy)) / Math.Sqrt((n * sumx2 - sumxall2) * (n * sumy2 - sumyall2));

                double r2 = r * r;

                return r2;
            }

            return 0;
        }
    }
}
