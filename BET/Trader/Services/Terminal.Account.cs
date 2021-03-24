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

        private async Task PlayInternalAccountArea(string symbol)
        {
            /// Initial Data

            /// Streams

            /// Interval Tasks

            // GetMyTrades
            RunTaskRegularly(() =>
            {
                var _24hours = DateTime.UtcNow.AddDays(-1);
                var response = _apiClient.FuturesUsdt.Order.GetMyTrades(symbol, startTime: _24hours, limit: 1000, ct: _intervalTasksCts.Token);
                if (response.Success)
                {
                    if (GenericMarketEvent<IEnumerable<BinanceFuturesUsdtTrade>>.Listened)
                        _eventAggregator.GetEvent<GenericMarketEvent<IEnumerable<BinanceFuturesUsdtTrade>>>().Publish(response.Data);

                    AccountLatestTradesData = response.Data;
                }
                else
                {
                    // TODO: notify data is old
                }
            }, fiveMinutes);


            await Task.CompletedTask;
        }

        private async Task StopInternalAccountArea()
        {
            await Task.CompletedTask;
        }

        private IEnumerable<BinanceFuturesUsdtTrade> _accountLatestTradesData;
        public IEnumerable<BinanceFuturesUsdtTrade> AccountLatestTradesData
        {
            get => _accountLatestTradesData;
            set => SetProperty(ref _accountLatestTradesData, value);
        }





    }
}
