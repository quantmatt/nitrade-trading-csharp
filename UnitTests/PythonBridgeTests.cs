using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TradingLibrary;

namespace UnitTests
{
    

    [TestClass]
    public class PythonBridgeTests
    {
        PythonBridge pb;

        public PythonBridgeTests()
        {
            pb = new PythonBridge(@"C:\Users\matth\Anaconda3\python");

        }

        [TestMethod]
        public void BuildFeaturesToStdOut()
        {
            //Test to see if the connection to the python interpreter is working correctly, not so much the accuracy of the results
            string[] labels = new string[]
            {
                "SMA_20", "ATR_3", "VOLAT_12", "VOL_12", "BB"
            };
            string[] commands = new string[] { "whole", @"C:\ForexData\TestData\EURUSD_m60_Share_live_test.bin", "200", "SMA(20,close);ATR(3,close,high,low);VOLATILITY_LOG_MA(12,high,low);VOLUME_LOG_MA(12,volume);BBANDS(20,1.8,1,close);" };
            string[] results = pb.RunScript("\"G:\\My Drive\\C Sharp Apps\\LinuxLiveTrader\\LinuxLiveTrader\\bin\\Debug\\build_features.py\"", commands);
            PreCalculatedFeatures pf = new PreCalculatedFeatures();
            pf.FillFromCsv(results, labels);

            Assert.IsTrue(EqualityChecks.DoubleNearlyEqual((double)pf.Data[new DateTime(2019, 03, 14, 11, 00, 00)]["ATR_3"], 0.001917, 0.001), "Did not recieve correct information back from python");

        }
    }
}
