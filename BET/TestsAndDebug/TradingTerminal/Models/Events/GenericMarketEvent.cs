using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Spot.MarketStream;
using Prism.Events;
using System;
using System.Collections.Generic;

namespace TradingTerminal.Models
{
    /// <summary>
    /// SubscribeToAggregatedTradeUpdatesAsync
    /// @aggTrade
    /// </summary>
    public class AggregatedTradeEvent : GenericMarketEvent<BinanceStreamAggregatedTrade> { }

    /// <summary>
    /// GetKlines
    /// klines
    /// </summary>
    public class KlinesEvent : GenericMarketEvent<IEnumerable<IBinanceKline>> { }
    
    /// <summary>
    /// Asset Price changed: OnMarkPrice, OnTrade, OnOrderBookChanged
    /// </summary>
    public class AssetTickEvent : GenericMarketEvent<(DateTime updated, double price)> { }

    /// <summary>
    /// SubscribeToSymbolTickerUpdatesAsync
    /// @ticker
    /// </summary>
    public class SymbolTickerEvent : GenericMarketEvent<IBinanceTick> { }

    /// <summary>
    /// SubscribeToAllSymbolTickerUpdatesAsync
    /// </summary>
    public class AllSymbolsTickerEvent : GenericMarketEvent<IEnumerable<IBinanceTick>> { }

    /// <summary>
    /// SubscribeToLiquidationUpdatesAsync
    /// </summary>
    public class AssetLiquidationEvent : GenericMarketEvent<BinanceFuturesStreamLiquidation> { }
    
    /// <summary>
    /// GetOpenInterest
    /// openInterest
    /// </summary>
    public class OpenInterestEvent : GenericMarketEvent<BinanceFuturesOpenInterest> { }

    public class MarkPriceEvent : GenericMarketEvent<BinanceFuturesStreamMarkPrice> { }


    /// <summary>
    /// TRUE if ok
    /// FALSe if order book failed, connectiopn failed ,etc
    /// </summary>
    public class ConnectionStatusEvent : GenericMarketEvent<bool> { }


    public class GenericMarketEvent<T> : PubSubEvent<T>
    {
        public static bool Listened { get; private set; }

        protected override SubscriptionToken InternalSubscribe(IEventSubscription eventSubscription)
        {
            try
            {
                return base.InternalSubscribe(eventSubscription);
            }
            finally
            {
                Listened = Subscriptions.Count > 0;
            }
        }

        public override void Unsubscribe(SubscriptionToken token)
        {
            base.Unsubscribe(token);
            Listened = Subscriptions.Count > 0;
        }

        public override void Unsubscribe(Action<T> subscriber)
        {
            base.Unsubscribe(subscriber);
            Listened = Subscriptions.Count > 0;
        }
    }


}