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
using System.Windows.Threading;
using Trader.Models;
using Trader.Services;
using Media = System.Windows.Media;

namespace Trader.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        public MainWindowViewModel(ITerminal terminal) =>
            MainTerminal = terminal;
            //_ea = eventAggregator;

        #region Properties


        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ITerminal MainTerminal { get; }

        #endregion

        #region Commands

        public DelegateCommand LoadedCommand => new(() => MainTerminal.Playing = true);

        public DelegateCommand UnloadedCommand => new(() => MainTerminal.Playing = false);

        #endregion

    


    }
}
