using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingLibrary
{
    public interface ITradingApiUser
    {

        long AccountId { get; set; }
        string Token { get; set; }


    }
}
