using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class OptimiseParameter
    {
        public string Name { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double Step { get; set; }

        public OptimiseParameter(string name, double start, double end, double step)
        {
            Name = name;
            Start = start;
            End = end;
            Step = step;
        }

        public object[] GetValues()
        {
            List<object> values = new List<object>();

            double value = Start;
            while (value <= End)
            {
                values.Add(value);
                value += Step;
            }

            return values.ToArray();
        }
        
    }
}
