using System;
using System.Collections.Generic;
using System.Text;

namespace TradingLibrary
{
    public class StrategyVariant
    {
        public List<Trade> OpenTrades;
        public List<Trade> ClosedTrades;
        public List<object> Parameters;

        public StrategyVariant()
        {
            OpenTrades = new List<Trade>();
            ClosedTrades = new List<Trade>();
            Parameters = new List<object>();
        }

        public override string ToString()
        {
            string values = "";
            foreach (object o in Parameters)
                values += o + " ";

            return values;
        }

        public static StrategyVariant Default(StrategyParameter[] strateyParameters)
        {
            StrategyVariant svar = new StrategyVariant();

            foreach (StrategyParameter sp in strateyParameters)
                svar.Parameters.Add(sp.Value);

            return svar;
        }

        public static StrategyVariant[] BruteForceGeneration(OptimiseParameter[] ops)
        {
            List<StrategyVariant> variants = new List<StrategyVariant>();

            List<List<object>> values = new List<List<object>>();

            foreach (OptimiseParameter op in ops)
                values.Add(new List<object>(op.GetValues()));

            int[] indexArray = new int[ops.Length];

            int floater = indexArray.Length - 1;

            bool exitLoop = false;
            while (indexArray[floater] < values[floater].Count && !exitLoop)
            {
                StrategyVariant variant = new StrategyVariant();
                variants.Add(variant);
                for (int i = 0; i < indexArray.Length; i++)
                {
                    variant.Parameters.Add(values[i][indexArray[i]]);
                }


                if (indexArray[floater] >= values[floater].Count - 1)
                {
                    indexArray[floater] = 0;

                    int carry = floater - 1;
                    while (carry >= 0)
                    {
                        if (indexArray[carry] >= values[carry].Count - 1)
                        {
                            indexArray[carry] = 0;
                            carry--;
                            if (carry < 0)
                                exitLoop = true;
                        }
                        else
                        {
                            indexArray[carry]++;
                            break;
                        }

                    }
                }
                else
                {
                    indexArray[floater]++;
                }
            }

            return variants.ToArray();
        }
    }
}
