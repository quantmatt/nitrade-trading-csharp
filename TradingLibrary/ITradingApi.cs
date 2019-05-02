using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingLibrary
{
    public interface ITradingApi
    {
        void SendOpenTrade(ITradingApiUser config, string symbolName, string direction, double size, string clientMsgId, string orderComment, double stopPoints, double takeProfitPoints);
        void SendCloseTrade(ITradingApiUser config, long positionId, double size, string clientMsgId);
        void OpenConnection();
    }
}
