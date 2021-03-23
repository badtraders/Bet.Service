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

            await _socketClient.FuturesUsdt.SubscribeToKlineUpdatesAsync(symbol, TimeFrame, (eventData) =>
            {
                if (!_playing)
                    return;

                var kline = eventData.Data;

                if (KlinesEvent.Listened)
                    _eventAggregator.GetEvent<KlinesEvent>().Publish(new[] { kline });

                AssetKlinesData.ReplaceOrAdd(kline, (item) => item.OpenTime == kline.OpenTime);
                AssetLatestDataUpdate = DateTime.UtcNow;

                if (AssetKlinesDataChart.Count > 0 && AssetKlinesDataChart[^1].OpenTime == kline.OpenTime)
                    AssetKlinesDataChart[^1] = kline;
                else
                    AssetKlinesDataChart.Add(kline);

            });

            /// Interval Tasks

            // GetOpenInterest
            RunTaskRegularly(() =>
            {
                var response = _apiClient.FuturesUsdt.Market.GetOpenInterest(symbol, _intervalTasksCancellationToken);
                if (response.Success)
                {
                    if (OpenInterestEvent.Listened)
                        _eventAggregator.GetEvent<OpenInterestEvent>().Publish(response.Data);

                    AssetOpenInterestData = response.Data;
                    AssetLatestDataUpdate = DateTime.UtcNow;
                }
                else
                {
                    // TODO: notify data is old
                }
            }, fiveMinutes);

            // GetKlines
            RunTaskRegularly(() =>
            {
                var response = _apiClient.FuturesUsdt.Market.GetKlines(symbol, TimeFrame, limit: DefaultLimit, ct: _intervalTasksCancellationToken);
                if (response.Success)
                {
                    if (KlinesEvent.Listened)
                        _eventAggregator.GetEvent<KlinesEvent>().Publish(response.Data);

                    AssetKlinesData.Clear();
                    AssetKlinesData.AddRange(response.Data);
                    AssetLatestDataUpdate = DateTime.UtcNow;

                    AssetKlinesDataChart.Clear();
                    AssetKlinesDataChart.AddRange(response.Data);
                }
                else
                {
                    // TODO: notify data is old
                }
            }, fiveMinutes);

            // Long Short Ratios
            RunTaskRegularly(() =>
            {
                var response = _apiClient.FuturesUsdt.Market.GetGlobalLongShortAccountRatio(symbol, KlineIntervalToPeriodInterval(TimeFrame), limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                if (response.Success)
                {
                    if (GlobalLongShortAccountRatioEvent.Listened)
                        _eventAggregator.GetEvent<GlobalLongShortAccountRatioEvent>().Publish(response.Data);

                    GlobalLongShortAccountRatioHistoryData = response.Data;
                }
                else
                {
                    // TODO: notify data is old
                }

                var response2 = _apiClient.FuturesUsdt.Market.GetTopLongShortAccountRatio(symbol, KlineIntervalToPeriodInterval(TimeFrame), limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                if (response2.Success)
                {
                    if (TopLongShortAccountRatioEvent.Listened)
                        _eventAggregator.GetEvent<TopLongShortAccountRatioEvent>().Publish(response2.Data);

                    TopLongShortAccountRatioHistoryData = response2.Data;
                }
                else
                {
                    // TODO: notify data is old
                }

                var response3 = _apiClient.FuturesUsdt.Market.GetTopLongShortPositionRatio(symbol, KlineIntervalToPeriodInterval(TimeFrame), limit: DefaultLimit, startTime: null, endTime: null, ct: _intervalTasksCancellationToken);
                if (response3.Success)
                {
                    if (TopLongShortPositionRatioEvent.Listened)
                        _eventAggregator.GetEvent<TopLongShortPositionRatioEvent>().Publish(response3.Data);

                    TopLongShortPositionRatioHistoryData = response3.Data;
                }
                else
                {
                    // TODO: notify data is old
                }


                AssetLatestDataUpdate = DateTime.UtcNow;
            }, fiveMinutes);


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
            private set
            {
                SetValue(value);
                AssetLatestDataUpdate = DateTime.UtcNow;
            }
        }

        public BinanceStreamAggregatedTrade AssetLatestTradeData
        {
            get => GetValue<BinanceStreamAggregatedTrade>();
            private set
            {
                if (SetValue(value))
                {
                    AssetLatestDataUpdate = DateTime.UtcNow;
                    AssetLatestPrice = value.Price;
                }
            }
        }

        public BinanceFuturesOpenInterest AssetOpenInterestData
        {
            get => GetValue<BinanceFuturesOpenInterest>();
            private set
            {
                if (SetValue(value))
                    AssetLatestDataUpdate = DateTime.UtcNow;
            }
        }
        public ObservableCollectionExtension<IBinanceAggregatedTrade> AssetLatestTradesData { get; } = new ObservableCollectionExtension<IBinanceAggregatedTrade>(ObservableCollectionExtensionType.Reversed, 20);
        public ObservableCollectionExtension<BinanceFuturesStreamLiquidation> AssetLiquidationsData { get; } = new ObservableCollectionExtension<BinanceFuturesStreamLiquidation>(ObservableCollectionExtensionType.Reversed, 100);

        public ObservableCollectionExtension<IBinanceKline> AssetKlinesData { get; } = new ObservableCollectionExtension<IBinanceKline>(ObservableCollectionExtensionType.List);
        public LiveCharts.ChartValues<IBinanceKline> AssetKlinesDataChart { get; } = new ();
        
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
    }
}
