using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public class StrategyParameterAttribute : Attribute
    {


        public StrategyParameterAttribute(string text, object initialValue, object minValue, object maxValue)
        {
            Text = text;
            InitialValue = initialValue;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public string Text { get; set; }
        public object InitialValue { get; set; }
        public object MinValue { get; set; }
        public object MaxValue { get; set; }
    }
}
