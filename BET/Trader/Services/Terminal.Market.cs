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


    partial class Terminal
    {
        private async Task PlayInternalMarketArea()
        {
            /// Initial Data
            var xInfo = await _apiClient.FuturesUsdt.System.GetExchangeInfoAsync();
            if (xInfo.Success)
            {
                ExchangeInfo = xInfo.Data;
            }
            else
            {
                // TODO: NOTIFY ERROR
            }

            /// Streams
            await _socketClient.FuturesUsdt.SubscribeToAllSymbolTickerUpdatesAsync((eventData) =>
            {
                if (!_playing)
                    return;

                if (AllSymbolsTickerEvent.Listened)
                    _eventAggregator.GetEvent<AllSymbolsTickerEvent>().Publish(eventData);

                App.RunUI(() => eventData.ForEach((ticker) => MarketTickers.ReplaceOrAdd(ticker, i => i.Symbol == ticker.Symbol)));
            });

            /// Interval Tasks
            RunTaskRegularly(() =>
            {
                var xTime = _apiClient.FuturesUsdt.System.GetServerTime();
                if (xTime.Success)
                {
                    ExchangeDelay = DateTime.UtcNow - xTime.Data;
                }
                else
                {
                    // TODO: notify data is old
                }
            }, oneMinute);
        }

        private async Task StopInternalMarketArea()
        {
            await Task.CompletedTask;
        }

        public TimeSpan ExchangeDelay { get => GetValue<TimeSpan>(); private set => SetValue(value); }
        public BinanceFuturesUsdtExchangeInfo ExchangeInfo
        {
            get => GetValue<BinanceFuturesUsdtExchangeInfo>();
            private set
            {
                if (SetValue(value))
                {
                    MarketAssets.Clear();
                    MarketAssets.AddRange(value.Symbols);
                }
            }
        }
        public ObservableCollectionExtension<BinanceFuturesUsdtSymbol> MarketAssets { get; } = new ObservableCollectionExtension<BinanceFuturesUsdtSymbol>();
        public ObservableCollectionExtension<IBinanceTick> MarketTickers { get; } = new ObservableCollectionExtension<IBinanceTick>(ObservableCollectionExtensionType.List);




    }
}
