using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.SymbolOrderBooks;
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
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using TradingTerminal.Controls;
using TradingTerminal.Models;
using TradingTerminal.Services;
using Media = System.Windows.Media;

namespace TradingTerminal.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly IEventAggregator _ea;


        public MainWindowViewModel(ITerminal terminal, IEventAggregator eventAggregator)
        {
            _ea = eventAggregator;
            MainTerminal = terminal;
            Assets = new ObservableCollection<string>();
            MainChart = new TradingChart(eventAggregator);
            ChartHost = new WindowsFormsHost() { Child = MainChart };
        }

        #region Properties

        public string Title => $"{Asset} {MainTerminal.LastTradePrice:C2} {MainTerminal.LastDataUpdate:HH:mm:ss}";

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { SetProperty(ref _isBusy, value); }
        }

        public WindowsFormsHost ChartHost { get; }
        private TradingChart MainChart { get; }
        public ITerminal MainTerminal { get; }

        #endregion

        #region Commands

        public DelegateCommand LoadedCommand => new DelegateCommand(async () =>
        {
            Assets.Add("BTCUSDT");
            Assets.Add(Asset = "ETHUSDT");
            _ea.GetEvent<AssetTickEvent>().Subscribe(OnMarketTickEvent);
            await MainTerminal.Play(Asset);
        });

        public DelegateCommand UnloadedCommand => new DelegateCommand(async () =>
        {
            await MainTerminal.Stop();
            MainChart.Dispose();
            _ea.GetEvent<AssetTickEvent>().Unsubscribe(OnMarketTickEvent);
        });

        #endregion

        #region Events

        private void OnMarketTickEvent((DateTime updated, double price) data) => RaisePropertyChanged(nameof(Title));

        #endregion

        #region ASSET PANE

        public ObservableCollection<string> Assets { get; set; }

        private string _Asset;
        public string Asset
        {
            get { return _Asset; }
            set
            {

                if (value != _Asset)
                {
                    SetProperty(ref _Asset, value);
                    MainTerminal.Pause().GetAwaiter().GetResult();
                    MainTerminal.Play(value);
                }

            }
        }

        #endregion

    }
}
