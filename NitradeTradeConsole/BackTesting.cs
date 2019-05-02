using System;
using System.IO;
using TradingLibrary;

namespace NitradeTradeConsole
{
    public static class BackTesting
    {
        private static string _logPath;
        private static string _tradesPath;
        private static string _reportPath;

        private static void OnCompleteBackTest(TestSummary testSummary)
        {
            
            //Save to storage for later use if required
            
            if (_tradesPath != null)
            {
                DisplayMessage("Saving trades csv...");
                testSummary.ToCsv(_tradesPath);
            }
            if (_reportPath != null)
            {
                DisplayMessage("Saving report binary...");
                testSummary.Save(_reportPath);
            }


            DisplayMessage("Done");
        }

        private static void DisplayMessage(string message, MessageType t = MessageType.Message)
        {
            if (t == MessageType.Update)
                Console.Write("\r" + message);
            else
                Console.WriteLine(message);

            if (t == MessageType.Log && _logPath != null)
                File.AppendAllText(_logPath, message + "\r\n");

        }

        public static void Optimise(string strategyReference, string strategyPath, string logPath = null, string tradesPath = null, string reportPath=null)
        {
            _logPath = logPath;
            _tradesPath = tradesPath;
            _reportPath = reportPath;

            try
            {
                BackTest bt = new BackTest(OnCompleteBackTest, new MessageDelegate(DisplayMessage));
                bt.Run(strategyReference, strategyPath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, MessageType.Error);
            }
        }
    }
}
