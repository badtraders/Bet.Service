using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Spot.MarketStream;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

using Trader.Extensions;

namespace Trader.Services
{
    public interface ITerminal
    {
        bool Playing { get; set; }
    }
}