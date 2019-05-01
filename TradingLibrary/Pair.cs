using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class Pair
    {
        public object P1 { get; set; }
        public object P2 { get; set; }

        public Pair(object p1, object p2)
        {
            P1 = p1;
            P2 = p2;
        }
    }
}
