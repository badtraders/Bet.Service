using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Spot.MarketStream;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Trader.Services
{
    public interface ITerminal : INotifyPropertyChanged
    {
        BinanceStreamAggregatedTrade AssetRecentTradeData { get; set; }
        IEnumerable<IBinanceKline> KlinesData { get; set; }
        DateTime LastDataUpdate { get; set; }
        decimal LastTradePrice { get; set; }
        BinanceFuturesStreamMarkPrice MarkPriceData { get; set; }
        BinanceFuturesOpenInterest OpenInterestData { get; set; }
        IBinanceTick SymbolTickerData { get; set; }

        bool Playing { get; set; }
        BinanceFuturesUsdtSymbol Asset { get; set; }

    }
}