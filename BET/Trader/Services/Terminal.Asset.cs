using AutoMapper;

using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.MarketData;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketStream;
using Binance.Net.SymbolOrderBooks;

using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Interfaces;

using Prism.Events;
using Prism.Mvvm;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Trader.Extensions;
using Trader.Models;
using Trader.Models.Bindables;

namespace Trader.Services
{
    partial class Terminal
    {
        //private const int DefaultLimit = 500;
        private const int DefaultLimit = 100;

        private async Task PlayInternalAssetArea(string symbol)
        {
            /// Initial Data
            AssetOrderBook = new GroupedBinanceFuturesUsdtSymbolOrderBook(symbol);
            AssetOrderBook.OnChange += () =>
            {
                if (!_playing)
                    return;

                AssetLatestPrice = AssetOrderBook.BestBid.Price;
                RaisePropertyChanged(nameof(AssetOrderBook));

                AssetLatestDataUpdate = DateTime.UtcNow;
            };
            await AssetOrderBook.StartAsync();

            /// Streams
            await _socketClient.FuturesUsdt.SubscribeToMarkPriceUpdatesAsync(symbol, updateInterval: 3000, (eventData) =>
            {
                if (!_playing)
                    return;

                if (MarkPriceEvent.Listened)
                    _eventAggregator.GetEvent<MarkPriceEvent>().Publish(eventData);

                AssetMarkPriceData = eventData;

                AssetLatestDataUpdate = DateTime.UtcNow;
            });

            await _socketClient.FuturesUsdt.SubscribeToSymbolTickerUpdatesAsync(symbol, (eventData) =>
            {
                if (!_playing)
                    return;

                if (SymbolTickerEvent.Listened)
                    _eventAggregator.GetEvent<SymbolTickerEvent>().Publish(eventData);

                AssetTickerData = eventData;
                AssetLatestDataUpdate = DateTime.UtcNow;
            });

            await _socketClient.FuturesUsdt.SubscribeToAggregatedTradeUpdatesAsync(symbol, (eventData) =>
            {
                if (!_playing)
                    return;

                if (AggregatedTradeEvent.Listened)
                    _eventAggregator.GetEvent<AggregatedTradeEvent>().Publish(eventData);

                AssetLatestTradeData = eventData;
                AssetLatestPrice = eventData.Price;

                App.RunUI(() => AssetLatestTradesData.ReplaceOrAdd(
                    newItem: eventData,
                    findExisting: i => i.BuyerIsMaker == eventData.BuyerIsMaker && (eventData.TradeTime - i.TradeTime).TotalSeconds <= 10,
                    beforeReplacementAction: (existing) =>
                    {
                        var totalQuantity = existing.Quantity + eventData.Quantity;
                        var vol1 = existing.Quantity * existing.Price;
                        var vol2 = eventData.Quantity * eventData.Price;
                        var totalVolume = vol1 + vol2;
                        var avgPrice = totalVolume / totalQuantity;

                        eventData.Quantity = totalQuantity;
                        eventData.Price = avgPrice;
                        eventData.TradeTime = existing.TradeTime;
                    }));

                AssetLatestDataUpdate = DateTime.UtcNow;
            });

            await _socketClient.FuturesUsdt.SubscribeToLiquidationUpdatesAsync(symbol, (eventData) =>
            {
                if (!_playing)
                    return;

                if (AssetLiquidationEvent.Listened)
                    _eventAggregator.GetEvent<AssetLiquidationEvent>().Publish(eventData);

                App.RunUI(() => AssetLiquidationsData.Insert(0, eventData));

                AssetLatestDataUpdate = DateTime.UtcNow;
            });

            await _socketClient.FuturesUsdt.SubscribeToKlineUpdatesAsync(symbol, TimeFrame.KlineInterval, (eventData) =>
            {
                if (!_playing)
                    return;

                var kline = eventData.Data;

                if (KlinesEvent.Listened)
                    _eventAggregator.GetEvent<KlinesEvent>().Publish(new[] { kline });

                AssetKlinesData.ReplaceOrAdd(kline, (item) => item.OpenTime == kline.OpenTime);
                AssetLatestDataUpdate = DateTime.UtcNow;



            });

            /// Interval Tasks

            RunTasksRegularly(fiveMinutes,
                onLoop: () =>
                {
                    AssetLatestDataUpdate = DateTime.UtcNow;
                },
                onFail: () =>
                {
                    // TODO: notify system has data loading failed
                },
                () =>
                { // GetKlines
                    var response = _apiClient.FuturesUsdt.Market.GetKlines(symbol, TimeFrame.KlineInterval, limit: DefaultLimit, ct: _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (KlinesEvent.Listened)
                            _eventAggregator.GetEvent<KlinesEvent>().Publish(response.Data);

                        AssetKlinesData.Clear();
                        AssetKlinesData.AddRange(response.Data);
                    }
                    return response.Success;
                },
                () =>
                { // GetOpenInterest
                    var response = _apiClient.FuturesUsdt.Market.GetOpenInterest(symbol, _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (OpenInterestEvent.Listened)
                            _eventAggregator.GetEvent<OpenInterestEvent>().Publish(response.Data);

                        AssetOpenInterestData = response.Data;
                    }
                    return response.Success;
                },
                () =>
                { // GetOpenInterestHistory
                    var response = _apiClient.FuturesUsdt.Market.GetOpenInterestHistory(symbol, TimeFrame.AsPeriodInterval, limit: DefaultLimit, startTime: null, endTime: null, _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (OpenInterestHistoryEvent.Listened)
                            _eventAggregator.GetEvent<OpenInterestHistoryEvent>().Publish(response.Data);

                        AssetOpenInterestHistoryData = response.Data;
                    }
                    return response.Success;
                },
                () =>
                { // GetGlobalLongShortAccountRatio
                    var response = _apiClient.FuturesUsdt.Market.GetGlobalLongShortAccountRatio(symbol, TimeFrame.AsPeriodInterval, limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (GlobalLongShortAccountRatioEvent.Listened)
                            _eventAggregator.GetEvent<GlobalLongShortAccountRatioEvent>().Publish(response.Data);

                        GlobalLongShortAccountRatioHistoryData = response.Data;
                    }
                    return response.Success;
                },
                () =>
                { // GetTopLongShortAccountRatio
                    var response = _apiClient.FuturesUsdt.Market.GetTopLongShortAccountRatio(symbol, TimeFrame.AsPeriodInterval, limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (TopLongShortAccountRatioEvent.Listened)
                            _eventAggregator.GetEvent<TopLongShortAccountRatioEvent>().Publish(response.Data);

                        TopLongShortAccountRatioHistoryData = response.Data;
                    }
                    return response.Success;
                },
                () =>
                { // GetTopLongShortPositionRatio
                    var response = _apiClient.FuturesUsdt.Market.GetTopLongShortPositionRatio(symbol, TimeFrame.AsPeriodInterval, limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (TopLongShortPositionRatioEvent.Listened)
                            _eventAggregator.GetEvent<TopLongShortPositionRatioEvent>().Publish(response.Data);

                        TopLongShortPositionRatioHistoryData = response.Data;
                    }
                    return response.Success;
                },
                () =>
                { // GetTakerBuySellVolumeRatio
                    var response = _apiClient.FuturesUsdt.Market.GetTakerBuySellVolumeRatio(symbol, TimeFrame.AsPeriodInterval, limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                    if (response.Success)
                    {
                        if (TakerLongShortPositionRatioEvent.Listened)
                            _eventAggregator.GetEvent<TakerLongShortPositionRatioEvent>().Publish(response.Data);

                        TakerLongShortPositionRatioHistoryData = response.Data;
                    }
                    return response.Success;
                });



        }
        private async Task StopInternalAssetArea()
        {
            await AssetOrderBook.StopAsync();
            AssetOrderBook.Dispose();
            AssetOrderBook = null;
        }

        public GroupedBinanceFuturesUsdtSymbolOrderBook AssetOrderBook { get; private set; }

        public BinanceFuturesStreamMarkPrice AssetMarkPriceData
        {
            get => GetValue<BinanceFuturesStreamMarkPrice>();
            private set => SetValue(value);
        }

        public DateTime AssetLatestDataUpdate
        {
            get => GetValue<DateTime>();
            private set => SetValue(value);
        }

        public decimal AssetLatestPrice
        {
            get => GetValue<decimal>();
            private set
            {
                if (SetValue(value) && AssetTickEvent.Listened)
                    _eventAggregator.GetEvent<AssetTickEvent>().Publish((DateTime.UtcNow, value));
            }
        }

        public IBinanceTick AssetTickerData
        {
            get => GetValue<IBinanceTick>();
            private set => SetValue(value);

        }

        public BinanceStreamAggregatedTrade AssetLatestTradeData
        {
            get => GetValue<BinanceStreamAggregatedTrade>();
            private set => SetValue(value);
        }

        public BinanceFuturesOpenInterest AssetOpenInterestData
        {
            get => GetValue<BinanceFuturesOpenInterest>();
            private set => SetValue(value);
        }

        public IEnumerable<BinanceFuturesOpenInterestHistory> AssetOpenInterestHistoryData
        {
            get => GetValue<IEnumerable<BinanceFuturesOpenInterestHistory>>();
            private set => SetValue(value);
        }
        public ObservableCollectionExtension<IBinanceAggregatedTrade> AssetLatestTradesData { get; } = new ObservableCollectionExtension<IBinanceAggregatedTrade>(ObservableCollectionExtensionType.Reversed, 20);
        public ObservableCollectionExtension<BinanceFuturesStreamLiquidation> AssetLiquidationsData { get; } = new ObservableCollectionExtension<BinanceFuturesStreamLiquidation>(ObservableCollectionExtensionType.Reversed, 100);

        public ObservableCollectionExtension<IBinanceKline> AssetKlinesData { get; } = new ObservableCollectionExtension<IBinanceKline>(ObservableCollectionExtensionType.List);

        public IEnumerable<BinanceFuturesLongShortRatio> GlobalLongShortAccountRatioHistoryData
        {
            get => GetValue<IEnumerable<BinanceFuturesLongShortRatio>>();
            private set
            {
                if (SetValue(value))
                    GlobalLongShortAccountRatioLatestData = value.LastOrDefault();
            }
        }

        public BinanceFuturesLongShortRatio GlobalLongShortAccountRatioLatestData
        {
            get => GetValue<BinanceFuturesLongShortRatio>();
            private set => SetValue(value);
        }

        public IEnumerable<BinanceFuturesLongShortRatio> TopLongShortAccountRatioHistoryData
        {
            get => GetValue<IEnumerable<BinanceFuturesLongShortRatio>>();
            private set
            {
                if (SetValue(value))
                    TopLongShortAccountRatioLatestData = value.LastOrDefault();
            }
        }

        public BinanceFuturesLongShortRatio TopLongShortAccountRatioLatestData
        {
            get => GetValue<BinanceFuturesLongShortRatio>();
            private set => SetValue(value);
        }

        public IEnumerable<BinanceFuturesLongShortRatio> TopLongShortPositionRatioHistoryData
        {
            get => GetValue<IEnumerable<BinanceFuturesLongShortRatio>>();
            private set
            {
                if (SetValue(value))
                    TopLongShortPositionRatioLatestData = value.LastOrDefault();
            }
        }

        public BinanceFuturesLongShortRatio TopLongShortPositionRatioLatestData
        {
            get => GetValue<BinanceFuturesLongShortRatio>();
            private set => SetValue(value);
        }

        public IEnumerable<BinanceFuturesBuySellVolumeRatio> TakerLongShortPositionRatioHistoryData
        {
            get => GetValue<IEnumerable<BinanceFuturesBuySellVolumeRatio>>();
            private set
            {
                if (SetValue(value))
                    TakerLongShortPositionRatioLatesData = value.LastOrDefault();
            }
        }

        public BinanceFuturesBuySellVolumeRatio TakerLongShortPositionRatioLatesData
        {
            get => GetValue<BinanceFuturesBuySellVolumeRatio>();
            private set => SetValue(value);
        }
    }
}
