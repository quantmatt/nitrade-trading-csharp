using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class StrategyParameter
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public object Value { get; set; }

        public StrategyParameter(string name, Type type, object val)
        {
            Name = name;
            Type = type;
            Value = val;
        }
        
    }
}
