using System;

namespace TradingLibrary
{
    public delegate void OrderAcceptedHandler(string clientMsgId, long positionId, bool isClosing);
    public delegate void OrderStopTargetAcceptedHandler(string clientMsgId, long positionId);
    public delegate void OrderFilledHandler(string clientMsgId, long positionId, double size, double commission, double actualPrice, 
        DateTime execTimestamp, DateTime createTimestamp, double marginRate, bool isClosing);
    public delegate void OrderCanceledHandler(string clientMsgId, long positionId);
}
