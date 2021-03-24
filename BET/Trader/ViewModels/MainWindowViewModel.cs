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
using System.Windows.Data;
using Binance.Net.Objects.Futures.MarketData;

namespace Trader.ViewModels
{
    public class MainWindowViewModel : ExtendedBindableBase
    {

        private List<(EventBase @event, SubscriptionToken token)> _subscriptions = new();

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
            foreach (var subscription in _subscriptions)
            {
                subscription.@event.Unsubscribe(subscription.token);
            }
            MainTerminal.Playing = false;
        });

        #endregion

        #region Charts
        private void InitCharts()
        {

            /// PRICE CANDLES AND VOLUME

            var klinePriceMapper = Mappers.Financial<IBinanceKline>()
                 .X((value, index) => index)
                 .Open(value => (double)value.Open)
                 .High(value => (double)value.High)
                 .Low(value => (double)value.Low)
                 .Close(value => (double)value.Close);
            var klinePriceSerie = new OhlcSeries(klinePriceMapper)
            {
                Values = AssetKlinesData,
                ScalesYAt = 0
            };
            BindingOperations.SetBinding(klinePriceSerie, OhlcSeries.TitleProperty, new Binding("MainTerminal.Asset.Name") { Source = this });


            var klineBuyVolumeMapper = Mappers.Xy<IBinanceKline>()
                 .X((value, index) => index)
                 .Y(value => (double)value.TakerBuyBaseVolume);
            var klineBuyVolumeSerie = new StackedColumnSeries(klineBuyVolumeMapper)
            {
                Values = AssetKlinesData,
                ScalesYAt = 1,
                Fill = Brushes.Green
            };
            BindingOperations.SetBinding(klineBuyVolumeSerie, OhlcSeries.TitleProperty, new Binding("MainTerminal.Asset.BaseAsset") { Source = this, StringFormat = "{0} BAUGHT by MARKET" });


            var klineSellVolumeMapper = Mappers.Xy<IBinanceKline>()
                .X((value, index) => index)
                .Y(value => (double)(value.TakerBuyBaseVolume - value.BaseVolume));
            var klineSellVolumeSerie = new StackedColumnSeries(klineSellVolumeMapper)
            {
                Values = AssetKlinesData,
                ScalesYAt = 1,
                Fill = Brushes.Red
            };
            BindingOperations.SetBinding(klineSellVolumeSerie, OhlcSeries.TitleProperty, new Binding("MainTerminal.Asset.BaseAsset") { Source = this, StringFormat = "{0} SOLD by MARKET" });

            AssetKlinesChartSeries = new SeriesCollection
            {
                klineBuyVolumeSerie,
                klineSellVolumeSerie,
                klinePriceSerie ,
            };

            var klinesEvent = _eventAggregator.GetEvent<KlinesEvent>();
            var klinesEventToken = klinesEvent.Subscribe((klines) =>
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

                    AssetKlinesChartMaxVolume = klines.Select(i => i.BaseVolume).Max() * 5; // 20% of height
                }
            });
            _subscriptions.Add((klinesEvent, klinesEventToken));


            /// open intereset , long shorts
            var openInterestMapper = Mappers.Xy<BinanceFuturesOpenInterestHistory>()
                 .X((value, index) => index)
                 .Y(value => (double)value.SumOpenInterest);
            var openInterestSerie = new LineSeries(openInterestMapper)
            {
                Values = AssetOpenInterestHistoryData,
                ScalesYAt = 0
            };
            BindingOperations.SetBinding(openInterestSerie, LineSeries.TitleProperty, new Binding("MainTerminal.Asset.BaseAsset") { Source = this, StringFormat = "{0} Open Intereset" });

            var longShortRatioMapper = Mappers.Xy<BinanceFuturesLongShortRatio>()
                 .X((value, index) => index)
                 .Y(value => (double)value.LongShortRatio);
            var globalLongShortAccountRatioSerie = new LineSeries(longShortRatioMapper)
            {
                Values = GlobalLongShortAccountRatioData,
                ScalesYAt = 1
            };
            BindingOperations.SetBinding(globalLongShortAccountRatioSerie, LineSeries.TitleProperty, new Binding("MainTerminal.Asset.BaseAsset") { Source = this, StringFormat = "{0} Global Account Long/Short" });


            AssetRatiosChartSeries = new SeriesCollection
            {
                openInterestSerie,
                globalLongShortAccountRatioSerie
            };

            var openInterestHistoryEvent = _eventAggregator.GetEvent<OpenInterestHistoryEvent>();
            var openInterestHistoryEventToken = openInterestHistoryEvent.Subscribe((history) =>
            {
                // replace all
                AssetOpenInterestHistoryData.Clear();
                AssetOpenInterestHistoryData.AddRange(history);
            });
            _subscriptions.Add((openInterestHistoryEvent, openInterestHistoryEventToken));

            var globalLongShortAccountRatioEvent = _eventAggregator.GetEvent<GlobalLongShortAccountRatioEvent>();
            var globalLongShortAccountRatioEventToken = globalLongShortAccountRatioEvent.Subscribe((history) =>
            {
                // replace all
                GlobalLongShortAccountRatioData.Clear();
                GlobalLongShortAccountRatioData.AddRange(history);
            });
            _subscriptions.Add((globalLongShortAccountRatioEvent, globalLongShortAccountRatioEventToken));
        }


        public ChartValues<IBinanceKline> AssetKlinesData { get; } = new();
        public SeriesCollection AssetKlinesChartSeries { get => GetValue<SeriesCollection>(); private set => SetValue(value); }
        public decimal AssetKlinesChartMaxVolume { get => GetValue<decimal>(); private set => SetValue(value); }
        public Func<double, string> AssetKlinesChartTimeLabelFormatter => (index) => AssetKlinesData.Count > index ? AssetKlinesData[(int)index].OpenTime.ToShortTimeString() : string.Empty;
        public Func<double, string> AssetKlinesChartVolumeLabelFormatter => (value) => Math.Abs(value).ToString("N");


        public SeriesCollection AssetRatiosChartSeries { get => GetValue<SeriesCollection>(); private set => SetValue(value); }
        public Func<double, string> AssetRatiosChartTimeLabelFormatter => (index) => AssetKlinesData.Count > index ? AssetKlinesData[(int)index].OpenTime.ToShortTimeString() : string.Empty;
        public Func<double, string> AssetOpenInterestChartTimeLabelFormatter => (index) => AssetOpenInterestHistoryData.Count > index ? AssetOpenInterestHistoryData[(int)index].Timestamp?.ToShortTimeString() : string.Empty;
        public ChartValues<BinanceFuturesOpenInterestHistory> AssetOpenInterestHistoryData { get; } = new();
        public ChartValues<BinanceFuturesLongShortRatio> GlobalLongShortAccountRatioData { get; } = new();


        #endregion


    }
}
