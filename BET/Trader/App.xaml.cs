using System;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

using AutoMapper;

using Binance.Net.Interfaces;

using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;

using Prism.Ioc;

using Trader.Services;
using Trader.Views;

namespace Trader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {


        protected override Window CreateShell() => Container.Resolve<MainWindow>();

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<ITerminal, Terminal>();

            // Automapper
            containerRegistry.RegisterSingleton<IMapper>(GetMapperFactory);
        }

        private IMapper GetMapperFactory(IContainerProvider container)
        {
            var configuration = new MapperConfiguration(cfg =>
            {
            });
#if DEBUG
            configuration.AssertConfigurationIsValid();
#endif
            return configuration.CreateMapper();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var cultureKey = ConfigurationManager.AppSettings["Culture"];
            var culture = CultureInfo.GetCultureInfo(cultureKey);
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture =
                Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            InitCharts();
        }

        public static void RunUI(Action action, DispatcherPriority dispatcherPriority = DispatcherPriority.Background, CancellationToken cancellationToken = default)
            => Current?.Dispatcher.InvokeAsync(action, dispatcherPriority, cancellationToken);

        //public CancellationToken AllAsyncThreadsCancellationToken { get; } 
        private void InitCharts()
        {
            var mapper = Mappers.Financial<IBinanceKline>()
                .X((value, index) => value.OpenTime.Ticks)
                .Open(value => (double)value.Open)
                .High(value => (double)value.High)
                .Low(value => (double)value.Low)
                .Close(value => (double)value.Close);

            LiveCharts.Charting.For<IBinanceKline>(mapper, SeriesOrientation.Horizontal);
            
        }
    }
}
