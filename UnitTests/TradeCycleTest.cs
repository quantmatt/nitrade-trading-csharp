using System;
using cTraderAPI;
using TradingLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    /// <summary>
    /// Summary description for TradeCycleTest
    /// </summary>
    [TestClass]
    public class TradeCycleTest
    {
        static MainController controller;
        UserConfig cTraderConfig;
        bool loggedIn = false;
        bool complete = false;
        bool failed = true;
        Trade trade;

        public TradeCycleTest()
        {
            //Initialise a connection
            controller = new MainController();

            // Load the config file with the API access details
            controller.Config = new Config(@"C:\ForexData\TestData\config\");

            //Load the user file
            cTraderConfig = new UserConfig(@"C:\ForexData\TestData\config\12345_config.txt");
            controller.Users.Add(cTraderConfig.Token, cTraderConfig);


            //open the connection
            controller.OpenConnection();

            controller.OnAccountAuthorised = new AccountAuthorisationComplete(accountAuthorised);
            controller.OnTradeFail = new TradeFailedHandler(tradeFailed);
            controller.OnOrderAccepted = new OrderAcceptedHandler(orderAccepted);
            controller.OnStopTargetAccepted = new OrderStopTargetAcceptedHandler(orderStopTargetAccepted);
            controller.OnOrderFilled = new OrderFilledHandler(orderFilled);
            controller.OnOrderCancelled = new OrderCanceledHandler(orderCancelled);
        }

                

        private void accountAuthorised(long accountId)
        {
            loggedIn = true;
        }

        private void tradeFailed(long accountId, string message)
        {
            failed = true;
            Assert.IsTrue(false, "Order failed to send: " + message);
            complete = true;
        }

        private void orderAccepted(string clientMsgId, long positionId, bool isClosing)
        {
            if (trade.ClientMsgId == clientMsgId)
            {
                if (!isClosing)
                {
                    trade.Status = Trade.TradeStatus.OPEN_ACCEPTED;
                    trade.TradeID = positionId;
                    Console.WriteLine("Open Accepted.");
                }
                else if (trade.TradeID == positionId)
                {
                    trade.Status = Trade.TradeStatus.CLOSE_ACCEPTED;
                    Console.WriteLine("Close Accepted.");
                }
            }
        }

        private void orderStopTargetAccepted(string clientMsgId, long positionId)
        {
            if (trade.ClientMsgId == clientMsgId && trade.TradeID == positionId)
            {
                trade.Status = Trade.TradeStatus.OPEN_FILLED_STOP_TARGET;
                Console.WriteLine("Stop Target Accepted.");
                Console.WriteLine("Closing...");
                controller.SendCloseTrade((ITradingApiUser)cTraderConfig, trade.TradeID, trade.Size, trade.ClientMsgId);
            }
        }

        private void orderFilled(string clientMsgId, long positionId, double size, double commission, double actualPrice,
        DateTime execTimestamp, DateTime createTimestamp, double marginRate, bool isClosing)
        {
            if (trade.ClientMsgId == clientMsgId && trade.TradeID == positionId)
            {
                if (!isClosing)
                {
                    Console.WriteLine("Open Filled.");
                    trade.Status = Trade.TradeStatus.OPEN_FILLED;
                }
                else
                {

                    if (size < trade.Size)
                    {
                        trade.Size -= Convert.ToInt64(size * 10000000);
                        trade.Status = Trade.TradeStatus.OPEN_FILLED_STOP_TARGET;
                        Console.WriteLine("Close Partial Fill");
                    }
                    else
                    {
                        trade.Status = Trade.TradeStatus.CLOSE_FILLED;
                        Console.WriteLine("Closed.");
                    }
                }
            }
        }

        private void orderCancelled(string clientMsgId, long positionId)
        {
            if (trade.ClientMsgId == clientMsgId && trade.TradeID == positionId)
            {
                trade.Status = Trade.TradeStatus.CLOSE_FILLED_STOP_TARGET;
                Console.WriteLine("Stop Target Cancelled.");
                Assert.IsTrue(true, "Full trade process complete.");
                failed = false;
                complete = true;
            }
        }

        [TestMethod]
        public void FullTradeOrderingClosingTest()
        {
            //wait until the account has been logged in
            do
            {
                System.Threading.Thread.Sleep(500);
            } while (!loggedIn);

            //Set Client id to be "StrategyDescription" ie YenSqaured EURUSD 3 4 3
            //Add trade to tradeProgress queue
            //Mark the trade with the order send time set, set status to OPEN_SENT, mark trade with clientID
            //On order accepted set status to OPEN_ACCEPTED
            //On order filled set status to OPEN_FILLED - this is a valid open state
            //On order Accepted, type stoplosstakeprofit set status to OPEN_SUCCESS_STOP_TARGET - this is a valid open state

            //Inside the OPEN_SUCESS_STOP_TARGET delegate send close trade message
            //Only allow close trade if status is OPEN_ACCEPTED or OPEN_SUCCESS_STOP_TARGET
            //set sendtime to now, set status to CLOSE_SENT

            //On Closeing accepted set status to CLOSE_FILLED 

            //On Closing order cancelled set status to CLOSE_FILLED_STOP_TARGET - finally closed
            string strategyDesc = "TestStrategy EURUSD 3 4 5";
            trade = new Trade();
            trade.Size = 0.5;
            trade.ClientMsgId = strategyDesc;
            trade.OrderRequestTime = DateTime.Now;
            trade.Status = Trade.TradeStatus.OPEN_SENT;
            Console.WriteLine("Opening...");
            controller.SendOpenTrade((ITradingApiUser)cTraderConfig, "EURUSD", "SHORT", trade.Size, trade.ClientMsgId, "TestStrategy", 100, 200);

            //wait until the test is complete
            do
            {
                System.Threading.Thread.Sleep(500);
            } while (!complete);

            if(failed)
                Assert.IsTrue(false, "Order failed to send");
        }
    }
}
