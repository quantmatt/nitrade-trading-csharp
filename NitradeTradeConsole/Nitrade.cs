using System;

namespace NitradeTradeConsole
{
    class Nitrade
    {
        
        static void Main(string[] args)
        {
            
            Console.WriteLine("Nitrade Trading Software - Prerelease version");
            Console.WriteLine("Press (Q) to quite application.");

            if (args.Length > 0)
            {
                if (args[0].Contains(".txt"))
                {
                    Console.WriteLine("Not yet implemented.");
                }
                else
                {
                    switch (args[0])
                    {
                        case "trade":
                            ActiveTrading.Start("TestStrategy.TestTrader","strategies\\TestTrader.cs");
                            break;
                        case "optimise":
                            BackTesting.Optimise("TestStrategy.YenSquaredDemo",
                                "strategies\\YenSquaredDemo.cs",
                                System.IO.Path.Combine("output", "backtest.log"), 
                                System.IO.Path.Combine("output", "all_trades.csv"),
                                System.IO.Path.Combine("output", "report.bin"));
                            break;
                        case "help":
                            DisplayHelp();
                            break;
                        default:
                            Console.WriteLine("Unrecognised command.");
                            break;
                    }
                }
            }
            else
                DisplayHelp();

            ConsoleKeyInfo key = Console.ReadKey(true);
            while(key.Key != ConsoleKey.Q)
            {
                key = Console.ReadKey(true);
            }
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("Must pass command line arguments.");
            Console.WriteLine("trade (Will start the active trading application)");
            Console.WriteLine("optimise (Will start the optimisation engine)");
            Console.WriteLine("script.txt (Runs a script of commands)");
        }

        
    }

}
