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

using TradingTerminal.Extensions;
using TradingTerminal.Models;
using TradingTerminal.Models.Bindables;

namespace TradingTerminal.Services
{

    public class Terminal : ExtendedBindableBase, ITerminal
    {
        private bool _inited;
        private bool _playing;
        private BinanceSocketClient _socketClient;
        private BinanceClient _apiClient;
        private string _asset;
        private bool _subscribed;
        private CancellationTokenSource _intervalTasksCts;

        public Terminal(IEventAggregator eventAggregator, IMapper mapper) : base(eventAggregator, mapper)
        {
        }

        public async Task Play(string asset)
        {
            if (_playing)
                return;

            if (!_inited)
                await Init();

            if (_asset != asset)
            {
                if (_subscribed)
                {
                    await _socketClient.UnsubscribeAll();
                    await OrderBook.StopAsync();
                    OrderBook.Dispose();
                    OrderBook = null;

                    _subscribed = false;
                }

                _asset = asset;

                await _socketClient.FuturesUsdt.SubscribeToMarkPriceUpdatesAsync(_asset, updateInterval: 3000, (eventData) =>
                {
                    if (!_playing)
                        return;

                    if (MarkPriceEvent.Listened)
                        _eventAggregator.GetEvent<MarkPriceEvent>().Publish(eventData);

                    MarkPriceData = eventData;
                });

                await _socketClient.FuturesUsdt.SubscribeToSymbolTickerUpdatesAsync(_asset, (eventData) =>
                {
                    if (!_playing)
                        return;

                    if (SymbolTickerEvent.Listened)
                        _eventAggregator.GetEvent<SymbolTickerEvent>().Publish(eventData);

                    SymbolTickerData = eventData;
                });

                await _socketClient.FuturesUsdt.SubscribeToAggregatedTradeUpdatesAsync(_asset, (eventData) =>
                {
                    if (!_playing)
                        return;

                    if (AggregatedTradeEvent.Listened)
                        _eventAggregator.GetEvent<AggregatedTradeEvent>().Publish(eventData);

                    AssetRecentTradeData = eventData;

                    App.RunUI(() => AssetRecentTradesData.ReplaceOrAdd(
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
                });

                await _socketClient.FuturesUsdt.SubscribeToLiquidationUpdatesAsync(_asset, (eventData) =>
                {
                    if (!_playing)
                        return;

                    if (AssetLiquidationEvent.Listened)
                        _eventAggregator.GetEvent<AssetLiquidationEvent>().Publish(eventData);

                    App.RunUI(() => AssetLiquidationsData.Insert(0, eventData));
                });

                await _socketClient.FuturesUsdt.SubscribeToKlineUpdatesAsync(_asset, KlineInterval.FiveMinutes, (eventData) =>
                {
                    if (!_playing)
                        return;

                    if (KlinesEvent.Listened)
                        _eventAggregator.GetEvent<KlinesEvent>().Publish(new[] { eventData.Data });

                });

                await _socketClient.FuturesUsdt.SubscribeToAllSymbolTickerUpdatesAsync((eventData) =>
                {
                    if (!_playing)
                        return;

                    if (AllSymbolsTickerEvent.Listened)
                        _eventAggregator.GetEvent<AllSymbolsTickerEvent>().Publish(eventData);

                    App.RunUI(() => eventData.ForEach((ticker) => MarketTickers.ReplaceOrAdd(ticker, i => i.Symbol == ticker.Symbol)));
                });


                OrderBook = new GroupedBinanceFuturesUsdtSymbolOrderBook(symbol: _asset);
                OrderBook.OnChange += () => RaisePropertyChanged(nameof(OrderBook));
            
                await OrderBook.StartAsync();

                _subscribed = true;

                if (_intervalTasksCts != null && !_intervalTasksCts.IsCancellationRequested)
                    _intervalTasksCts.Cancel(true);
                _intervalTasksCts = null;

                InitIntervalTasks();


            }

            _playing = true;
        }
        public async Task Pause()
        {
            if (!_playing)
                return;

            // TODO: PAUSE
            _playing = false;

            await Task.CompletedTask;
        }
        public async Task Stop()
        {
            if (!_playing)
                return;

            if (_subscribed)
            {
                await _socketClient.UnsubscribeAll();
                await OrderBook.StopAsync();
                OrderBook.Dispose();
                OrderBook = null;
                _subscribed = false;
            }

            if (_intervalTasksCts != null && !_intervalTasksCts.IsCancellationRequested)
                _intervalTasksCts.Cancel(true);
            _intervalTasksCts = null;

            _playing = false;
        }
        private async Task Init()
        {
            string apiKey = ConfigurationManager.AppSettings["ApiKey"];
            string apiSecret = ConfigurationManager.AppSettings["ApiSecret"];

            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logsDirectory))
                Directory.CreateDirectory(logsDirectory);

            BinanceClient.SetDefaultOptions(new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials(apiKey, apiSecret),
                LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Debug,
                LogWriters = new List<System.IO.TextWriter> { Console.Out, new StreamWriter(File.OpenWrite(Path.Combine(logsDirectory, $"{nameof(BinanceClient)}.log"))) }
            });

            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions
            {
                ApiCredentials = new ApiCredentials(apiKey, apiSecret),
                AutoReconnect = true,
                LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Debug,
                LogWriters = new List<System.IO.TextWriter> { Console.Out, new StreamWriter(File.OpenWrite(Path.Combine(logsDirectory, $"{nameof(BinanceSocketClient)}.log"))) }
            });

            _socketClient = new BinanceSocketClient();
            _apiClient = new BinanceClient();



            _inited = true;
            await Task.CompletedTask;
        }

        private void InitIntervalTasks()
        {
            _intervalTasksCts = new CancellationTokenSource();
            var token = _intervalTasksCts.Token;
            var idleFiveMins = TimeSpan.FromMinutes(3);

            // GetOpenInterest
            WrapIntervalTask(() =>
            {

                var response = _apiClient.FuturesUsdt.Market.GetOpenInterest(_asset, token);
                if (response.Success)
                {
                    if (OpenInterestEvent.Listened)
                        _eventAggregator.GetEvent<OpenInterestEvent>().Publish(response.Data);

                    OpenInterestData = response.Data;
                }
                else
                {
                    // TODO: notify data is old
                }
            }, idleFiveMins, token);

            // GetKlines
            WrapIntervalTask(() =>
            {
                var response = _apiClient.FuturesUsdt.Market.GetKlines(_asset, KlineInterval.FiveMinutes, limit: 500, ct: token);
                if (response.Success)
                {
                    if (KlinesEvent.Listened)
                        _eventAggregator.GetEvent<KlinesEvent>().Publish(response.Data);

                    KlinesData = response.Data;
                }
                else
                {
                    // TODO: notify data is old
                }
            }, idleFiveMins, token);


            // GetMyTrades
            WrapIntervalTask(RefreshRecentTrades, idleFiveMins, token);

        }

        static TimeSpan pause = TimeSpan.FromSeconds(1);
        private Task WrapIntervalTask(Action action, TimeSpan interval, CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    if (!_playing)
                    {
                        await Task.Delay(pause, token);
                        continue;
                    }

                    action();

                    await Task.Delay(interval, token);
                }
            }, token);
        }

        private BinanceFuturesStreamMarkPrice _markPriceData;
        public BinanceFuturesStreamMarkPrice MarkPriceData
        {
            get { return _markPriceData; }
            set
            {
                SetProperty(ref _markPriceData, value);
                LastDataUpdate = DateTime.UtcNow;
            }
        }

        private IBinanceTick _symbolTickerData;
        public IBinanceTick SymbolTickerData
        {
            get { return _symbolTickerData; }
            set
            {
                SetProperty(ref _symbolTickerData, value);
                LastDataUpdate = DateTime.UtcNow;
            }
        }

        private BinanceStreamAggregatedTrade _assetRecentTradeData;
        public BinanceStreamAggregatedTrade AssetRecentTradeData
        {
            get { return _assetRecentTradeData; }
            set
            {
                SetProperty(ref _assetRecentTradeData, value);
                LastDataUpdate = DateTime.UtcNow;
                LastTradePrice = value.Price;
            }
        }

        private BinanceFuturesOpenInterest _openInterestData;
        public BinanceFuturesOpenInterest OpenInterestData
        {
            get { return _openInterestData; }
            set
            {
                SetProperty(ref _openInterestData, value);
                LastDataUpdate = DateTime.UtcNow;
            }
        }

        private IEnumerable<IBinanceKline> _klinestData;
        public IEnumerable<IBinanceKline> KlinesData
        {
            get { return _klinestData; }
            set
            {
                SetProperty(ref _klinestData, value);
                LastDataUpdate = DateTime.UtcNow;
            }
        }

        private decimal _lastTradePrice;
        public decimal LastTradePrice
        {
            get { return _lastTradePrice; }
            set { SetProperty(ref _lastTradePrice, value); }
        }

        private DateTime _lastDataUpdate;
        public DateTime LastDataUpdate
        {
            get { return _lastDataUpdate; }
            set
            {
                SetProperty(ref _lastDataUpdate, value);

                if (AssetTickEvent.Listened)
                    _eventAggregator.GetEvent<AssetTickEvent>().Publish((_lastDataUpdate, (double)_lastTradePrice));
            }
        }

        private IEnumerable<BinanceFuturesUsdtTrade> _accountRecentTradesData;
        public IEnumerable<BinanceFuturesUsdtTrade> AccountRecentTradesData
        {
            get { return _accountRecentTradesData; }
            set { SetProperty(ref _accountRecentTradesData, value); }
        }

        public void RefreshRecentTrades()
        {
            var _24hours = DateTime.UtcNow.AddDays(-1);
            var response = _apiClient.FuturesUsdt.Order.GetMyTrades(_asset, startTime: _24hours, limit: 1000, ct: _intervalTasksCts.Token);
            if (response.Success)
            {
                if (GenericMarketEvent<IEnumerable<BinanceFuturesUsdtTrade>>.Listened)
                    _eventAggregator.GetEvent<GenericMarketEvent<IEnumerable<BinanceFuturesUsdtTrade>>>().Publish(response.Data);

                AccountRecentTradesData = response.Data;
            }
            else
            {
                // TODO: notify data is old
            }
        }



        public ObservableCollectionExtension<IBinanceTick> MarketTickers { get; } = new ObservableCollectionExtension<IBinanceTick>(ObservableCollectionExtension<IBinanceTick>.ObservableCollectionExtensionType.List);
        public ObservableCollectionExtension<IBinanceAggregatedTrade> AssetRecentTradesData { get; } = new ObservableCollectionExtension<IBinanceAggregatedTrade>(ObservableCollectionExtension<IBinanceAggregatedTrade>.ObservableCollectionExtensionType.Reversed, 20);
        public ObservableCollectionExtension<BinanceFuturesStreamLiquidation> AssetLiquidationsData { get; } = new ObservableCollectionExtension<BinanceFuturesStreamLiquidation>(ObservableCollectionExtension<BinanceFuturesStreamLiquidation>.ObservableCollectionExtensionType.Reversed, 100);
        public GroupedBinanceFuturesUsdtSymbolOrderBook OrderBook { get; private set; }
    }
}
