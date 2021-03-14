using AutoMapper;
using AutoMapper.Configuration;

using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.MarketStream;

using DryIoc;

using Prism.DryIoc;
using Prism.Ioc;

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

using TradingTerminal.Models.Bindables;
using TradingTerminal.Services;
using TradingTerminal.Views;

namespace TradingTerminal
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

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
        }

        public static void RunUI(Action action)
        {
            App.Current?.Dispatcher.InvokeAsync(action);
        }

    }


}
