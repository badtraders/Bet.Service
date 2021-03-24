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

using Trader.Extensions;
using Trader.Models;
using Trader.Models.Bindables;

namespace Trader.Services
{
    public partial class Terminal : ExtendedBindableBase, ITerminal
    {
        private bool _inited;
        private BinanceSocketClient _socketClient;
        private BinanceClient _apiClient;
        private bool _subscribed;
        private CancellationTokenSource _intervalTasksCts;
        private CancellationToken _intervalTasksCancellationToken;
        private static readonly TimeSpan oneMinute = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan fiveMinutes = TimeSpan.FromMinutes(5);

        public Terminal(IEventAggregator eventAggregator, IMapper mapper) : base(eventAggregator, mapper)
        {
            TimeFrame = TimeFramePreset.StandardTimeFramePresets[KlineInterval.FiveMinutes];
        }

        private bool _playing;
        public bool Playing
        {
            get => _playing;
            set => _ = value ? Play() : Stop();
        }

        private async Task Init()
        {
            if (_inited)
                return;

            string apiKey = ConfigurationManager.AppSettings["ApiKey"];
            string apiSecret = ConfigurationManager.AppSettings["ApiSecret"];

            BinanceClient.SetDefaultOptions(new BinanceClientOptions
            {
                ApiCredentials = new ApiCredentials(apiKey, apiSecret),
                LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Info,
                LogWriters = new List<System.IO.TextWriter> { Console.Out }
            });

            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions
            {
                ApiCredentials = new ApiCredentials(apiKey, apiSecret),
                AutoReconnect = true,
                LogVerbosity = CryptoExchange.Net.Logging.LogVerbosity.Info,
                LogWriters = new List<System.IO.TextWriter> { Console.Out }
            });

            _socketClient = new BinanceSocketClient();
            _apiClient = new BinanceClient();

            _inited = true;

            await Task.CompletedTask;
        }

        private async Task Play()
        {
            await Stop(); // Play twice resets

            if (_playing)
                return;

            await Init();

            _intervalTasksCts = new CancellationTokenSource();
            _intervalTasksCancellationToken = _intervalTasksCts.Token;


            await PlayInternalMarketArea();

            var symbol = Asset?.Name;

            if (symbol is null)
            {
                // TODO: raise terminal error
            }
            else
            {
                // updated assets
                var asset = MarketAssets.FirstOrDefault(i => i.Name == symbol);
                //if(asset.Status != SymbolStatus.Trading)
                //{
                //// TODO: some things
                //}
                SetProperty(ref _asset, asset, nameof(Asset));

            }

            await PlayInternalAssetArea(symbol);
            await PlayInternalAccountArea(symbol);

            _subscribed = true;

            SetProperty(ref _playing, true, nameof(Playing));
        }

        private async Task Stop()
        {
            if (!_playing)
                return;

            if (_subscribed)
            {
                await StopInternalAccountArea();
                await StopInternalAssetArea();
                await StopInternalMarketArea();
                await _socketClient.UnsubscribeAll();
                _subscribed = false;
            }

            if (!_intervalTasksCts.IsCancellationRequested)
                _intervalTasksCts.Cancel(true);
            _intervalTasksCts.Dispose();
            _intervalTasksCts = null;

            SetProperty(ref _playing, false, nameof(Playing));
        }

        private BinanceFuturesUsdtSymbol _asset;
        public BinanceFuturesUsdtSymbol Asset
        {
            get
            {
                if (_asset is null)
                {
                    _asset = MarketAssets.FirstOrDefault();
                    if (_asset is not null)
                        RaisePropertyChanged(nameof(Asset));
                }

                return _asset;
            }
            set
            {
                if (!_playing)
                    return;

                if (SetProperty(ref _asset, value))
                    _ = Play();
                //{
                //Task.Run(async () =>
                //{
                //    await Stop();
                //    await Play();
                //});
                //}
            }
        }


        public TimeFramePreset TimeFrame
        {
            get => GetValue<TimeFramePreset>();
            set => SetValue(value);
        }





    }
}
