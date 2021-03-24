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
        private void RunTaskRegularly(Action action, TimeSpan interval) => Task.Run(async () =>
                                                                             {
                                                                                 while (!_intervalTasksCancellationToken.IsCancellationRequested)
                                                                                 {
                                                                                     action();
                                                                                     await Task.Delay(interval, _intervalTasksCancellationToken);
                                                                                 }
                                                                             }, _intervalTasksCancellationToken);

        private void RunTasksRegularly(TimeSpan interval, Action onLoop, Action onFail, params Func<bool>[] actions) => Task.Run(async () =>
        {
            while (!_intervalTasksCancellationToken.IsCancellationRequested)
            {
                foreach (var action in actions)
                {
                    if (action?.Invoke() == false)
                        onFail?.Invoke();
                }
                onLoop?.Invoke();
                await Task.Delay(interval, _intervalTasksCancellationToken);
            }
        }, _intervalTasksCancellationToken);

        public class TimeFramePreset
        {
            public static readonly IDictionary<KlineInterval, TimeFramePreset> StandardTimeFramePresets;
            static TimeFramePreset()
            {
                StandardTimeFramePresets = Enum
                    .GetValues<KlineInterval>()
                    .Select(klineInterval => new TimeFramePreset(klineInterval))
                    .ToDictionary(tf => tf.KlineInterval);
            }
            public TimeFramePreset(KlineInterval klineInterval) => KlineInterval = klineInterval;
            public KlineInterval KlineInterval { get; }
            public KlineInterval HigherKlineInterval
                 => KlineInterval switch
                 {
                     KlineInterval.OneMinute => KlineInterval.FiveMinutes,
                     KlineInterval.ThreeMinutes => KlineInterval.FifteenMinutes,
                     KlineInterval.FiveMinutes => KlineInterval.ThirtyMinutes,
                     KlineInterval.FifteenMinutes => KlineInterval.OneHour,
                     KlineInterval.ThirtyMinutes => KlineInterval.TwoHour,
                     KlineInterval.OneHour => KlineInterval.FourHour,
                     KlineInterval.TwoHour => KlineInterval.EightHour,
                     KlineInterval.FourHour => KlineInterval.TwelveHour,
                     KlineInterval.SixHour => KlineInterval.OneDay,
                     KlineInterval.EightHour => KlineInterval.OneDay,
                     KlineInterval.TwelveHour => KlineInterval.ThreeDay,
                     KlineInterval.OneDay => KlineInterval.ThreeDay,
                     KlineInterval.ThreeDay => KlineInterval.OneWeek,
                     KlineInterval.OneWeek => KlineInterval.OneMonth,
                     KlineInterval.OneMonth => KlineInterval.OneDay,
                     _ => throw new ArgumentOutOfRangeException(nameof(KlineInterval)),
                 };

            public PeriodInterval AsPeriodInterval
                => KlineInterval switch
                {
                    <= KlineInterval.FiveMinutes => PeriodInterval.FiveMinutes,
                    KlineInterval.FifteenMinutes => PeriodInterval.FifteenMinutes,
                    KlineInterval.ThirtyMinutes => PeriodInterval.ThirtyMinutes,
                    KlineInterval.OneHour => PeriodInterval.OneHour,
                    KlineInterval.TwoHour => PeriodInterval.TwoHour,
                    KlineInterval.FourHour => PeriodInterval.FourHour,
                    <= KlineInterval.EightHour => PeriodInterval.SixHour,
                    KlineInterval.TwelveHour => PeriodInterval.TwelveHour,
                    <= KlineInterval.OneMonth => PeriodInterval.OneDay,
                    _ => throw new ArgumentOutOfRangeException(nameof(KlineInterval)),
                };
            public TimeSpan AsTimeSpan
                => KlineInterval switch
                {
                    KlineInterval.OneMinute => TimeSpan.FromMinutes(1),
                    KlineInterval.ThreeMinutes => TimeSpan.FromMinutes(3),
                    KlineInterval.FiveMinutes => TimeSpan.FromMinutes(5),
                    KlineInterval.FifteenMinutes => TimeSpan.FromMinutes(15),
                    KlineInterval.ThirtyMinutes => TimeSpan.FromMinutes(30),
                    KlineInterval.OneHour => TimeSpan.FromHours(1),
                    KlineInterval.TwoHour => TimeSpan.FromHours(2),
                    KlineInterval.FourHour => TimeSpan.FromHours(4),
                    KlineInterval.SixHour => TimeSpan.FromHours(6),
                    KlineInterval.EightHour => TimeSpan.FromHours(8),
                    KlineInterval.TwelveHour => TimeSpan.FromHours(12),
                    KlineInterval.OneDay => TimeSpan.FromDays(1),
                    KlineInterval.ThreeDay => TimeSpan.FromDays(3),
                    KlineInterval.OneWeek => TimeSpan.FromDays(7),
                    KlineInterval.OneMonth => TimeSpan.FromDays(30),
                    _ => throw new ArgumentOutOfRangeException(nameof(KlineInterval)),
                };
        }

        //private static TimeSpan PeriodIntervalToTimeSpan(PeriodInterval periodInterval)
        //    => periodInterval switch
        //    {
        //        PeriodInterval.FiveMinutes => TimeSpan.FromMinutes(5),
        //        PeriodInterval.FifteenMinutes => TimeSpan.FromMinutes(15),
        //        PeriodInterval.ThirtyMinutes => TimeSpan.FromMinutes(30),
        //        PeriodInterval.OneHour => TimeSpan.FromHours(1),
        //        PeriodInterval.TwoHour => TimeSpan.FromHours(2),
        //        PeriodInterval.FourHour => TimeSpan.FromHours(4),
        //        PeriodInterval.SixHour => TimeSpan.FromHours(6),
        //        PeriodInterval.TwelveHour => TimeSpan.FromHours(12),
        //        PeriodInterval.OneDay => TimeSpan.FromDays(1),
        //        _ => throw new ArgumentOutOfRangeException(nameof(periodInterval)),
        //    };




    }
}
