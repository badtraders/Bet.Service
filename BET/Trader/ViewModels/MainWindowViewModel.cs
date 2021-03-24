using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.SymbolOrderBooks;

using LiveCharts.Configurations;
using LiveCharts;

using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

using Trader.Models;
using Trader.Services;

using Media = System.Windows.Media;
using LiveCharts.Wpf;
using CryptoExchange.Net.Interfaces;
using AutoMapper;
using System.Windows.Media;

namespace Trader.ViewModels
{
    public class MainWindowViewModel : ExtendedBindableBase
    {

        private SubscriptionToken _klinesEventSubscriptionToken;
        public ITerminal MainTerminal { get; }

        public MainWindowViewModel(ITerminal terminal, IEventAggregator eventAggregator, IMapper mapper) : base(eventAggregator, mapper)
        {
            MainTerminal = terminal;
        }


        #region Properties


        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }



        #endregion

        #region Commands

        public DelegateCommand LoadedCommand => new(() =>
        {
            InitCharts();
            MainTerminal.Playing = true;
        });

        public DelegateCommand UnloadedCommand => new(() =>
        {
            _eventAggregator.GetEvent<KlinesEvent>().Unsubscribe(_klinesEventSubscriptionToken);
            MainTerminal.Playing = false;
        });

        #endregion

        #region Charts
        private void InitCharts()
        {
            var klinePriceMapper = Mappers.Financial<IBinanceKline>()
                 .X((value, index) => value.OpenTime.Ticks)
                 .Open(value => (double)value.Open)
                 .High(value => (double)value.High)
                 .Low(value => (double)value.Low)
                 .Close(value => (double)value.Close);

            var klineBuyVolumeMapper = Mappers.Xy<IBinanceKline>()
                 .X((value, index) => value.OpenTime.Ticks)
                 .Y(value => (double)value.TakerBuyBaseVolume);
            var klineSellVolumeMapper = Mappers.Xy<IBinanceKline>()
                .X((value, index) => value.OpenTime.Ticks)
                .Y(value => (double)(value.BaseVolume - value.TakerBuyBaseVolume));

            AssetKlinesChartSeries = new SeriesCollection
            {
                new StackedColumnSeries(klineSellVolumeMapper)
                {
                    Values = AssetKlinesData,
                    ScalesYAt = 1,
                    Fill = Brushes.Red
                },
                new StackedColumnSeries(klineBuyVolumeMapper)
                {
                    Values = AssetKlinesData,
                    ScalesYAt = 1,
                    Fill = Brushes.Green
                },
                new OhlcSeries(klinePriceMapper)
                {
                    Values = AssetKlinesData,
                    ScalesYAt = 0
                },
            };

            _klinesEventSubscriptionToken = _eventAggregator.GetEvent<KlinesEvent>().Subscribe((klines) =>
            {
                if (klines.Count() == 1)
                {
                    // update last
                    var kline = klines.FirstOrDefault();

                    if (AssetKlinesData.Count > 0 && AssetKlinesData[^1].OpenTime == kline.OpenTime)
                        AssetKlinesData[^1] = kline;
                    else
                        AssetKlinesData.Add(kline);
                }
                else
                {
                    // replace all
                    AssetKlinesData.Clear();
                    AssetKlinesData.AddRange(klines);

                    AssetKlinesChartMaxVolume = klines.Select(i => i.BaseVolume).Max() * 5;
                    AssetKlinesChartMinTime = klines.Select(i => i.OpenTime).Min();
                    AssetKlinesChartMaxTime = klines.Select(i => i.OpenTime).Max().AddMinutes(5);
                }

             

            });
        }

        public ChartValues<IBinanceKline> AssetKlinesData { get; } = new();
        public SeriesCollection AssetKlinesChartSeries { get => GetValue<SeriesCollection>(); private set => SetValue(value); }
        public DateTime AssetKlinesChartMinTime { get => GetValue<DateTime>(); private set => SetValue(value); }
        public DateTime AssetKlinesChartMaxTime { get => GetValue<DateTime>(); private set => SetValue(value); }
        public decimal AssetKlinesChartMaxVolume { get => GetValue<decimal>(); private set => SetValue(value); }

        public Func<double, string> AssetKlinesChartTimeLabelFormatter => (time) => new DateTime((long)time).ToShortTimeString();

        #endregion


    }
}
