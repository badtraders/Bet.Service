using Binance.Net.Interfaces;
using Binance.Net.Objects.Futures.MarketStream;
using Binance.Net.Objects.Spot.MarketStream;

using Prism.Events;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Threading;

using TradingTerminal.Models;

namespace TradingTerminal.Controls
{
    public class TradingChart : Chart
    {
        private readonly IEventAggregator _ea;
        private readonly CancellationTokenSource _cts;
        private readonly ChartArea PrimaryChartArea;
        private readonly Series PrimaryKlineSerie;

        private readonly ChartArea PrimaryVolumeChartArea;
        private readonly Series PrimaryBuyVolumeSerie;
        private readonly StripLine PrimaryAreaPriceLine;
        private readonly CustomLabel PrimaryAreaPriceLabel;
        private readonly Series PrimarySellVolumeSerie;

        public TradingChart(IEventAggregator eventAggregator)
        {
            _ea = eventAggregator;
            _cts = new CancellationTokenSource();

            /// KLINES AREA
            PrimaryChartArea = new ChartArea(nameof(PrimaryChartArea));
            ChartAreas.Add(PrimaryChartArea);
            PrimaryChartArea.Position = new ElementPosition(2, 0, 98, 80);
            //PrimaryChartArea.AxisX.LabelStyle = new LabelStyle { Format = "MM/d, H:MM" };
            //PrimaryChartArea.AxisY.Enabled = AxisEnabled.False;
            //PrimaryChartArea.AxisY2.Enabled = AxisEnabled.True;

            
            PrimaryChartArea.AxisX.Enabled = AxisEnabled.False;
            PrimaryChartArea.AxisY.Enabled = AxisEnabled.False;
            PrimaryChartArea.AxisY2.Enabled = AxisEnabled.False;

            //PrimaryChartArea.CursorX.IsUserSelectionEnabled = true;
            //PrimaryChartArea.CursorX.IsUserEnabled = true;
            //PrimaryChartArea.CursorX.AxisType = AxisType.Primary;
            //PrimaryChartArea.CursorX.AutoScroll = true;


            //PrimaryChartArea.AxisX.ScrollBar.Enabled = true;
            //PrimaryChartArea.AxisX.ScaleView.Zoomable = true;

            //PrimaryChartArea.CursorX.AutoScroll = true;
            //PrimaryChartArea.AxisX.ScrollBar.IsPositionedInside = true;

            // TODO: customization
            //BackColor = ColorTranslator.FromHtml("#191b20");

            /// KLINES SERIE
            PrimaryKlineSerie = Series.Add(nameof(PrimaryKlineSerie));
            PrimaryKlineSerie.ChartArea = nameof(PrimaryChartArea);
            PrimaryKlineSerie.ChartType = SeriesChartType.Candlestick;
            PrimaryKlineSerie.XValueType = ChartValueType.DateTime;
            // TODO: customization
            //GeneralKlineSerie.Color = System.Drawing.Color.Gray;// System.Drawing.ColorTranslator.FromHtml("#0ecb81");
            //                                         //series.BackSecondaryColor = System.Drawing.ColorTranslator.FromHtml("#f6465d");
            PrimaryKlineSerie.SetCustomProperty("PriceUpColor", "#0ecb81");
            PrimaryKlineSerie.SetCustomProperty("PriceDownColor", "#f6465d");

            /// VOLUME AREA & SERIES
            PrimaryVolumeChartArea = new ChartArea(nameof(PrimaryVolumeChartArea));
            PrimaryVolumeChartArea.BackColor = Color.Transparent;
            PrimaryVolumeChartArea.AxisX.Enabled = AxisEnabled.False;
            PrimaryVolumeChartArea.AxisY.Enabled = AxisEnabled.False;
            PrimaryVolumeChartArea.AxisY2.Enabled = AxisEnabled.False;
            ChartAreas.Add(PrimaryVolumeChartArea);
            PrimaryVolumeChartArea.Position = new ElementPosition(2, 40, 98, 40);
            //PrimaryVolumeChartArea.Position = new ElementPosition(2, 80, 98, 20);

            //PrimaryVolumeChartArea.AxisX.LabelStyle = new LabelStyle { Format = "MM/d, H:MM" };
            //PrimaryVolumeChartArea.AxisY.Enabled = AxisEnabled.False;
            //PrimaryVolumeChartArea.AxisY2.Enabled = AxisEnabled.True;

            PrimarySellVolumeSerie = Series.Add(nameof(PrimarySellVolumeSerie));
            PrimarySellVolumeSerie.ChartArea = nameof(PrimaryVolumeChartArea);
            PrimarySellVolumeSerie.ChartType = SeriesChartType.StackedColumn;
            PrimarySellVolumeSerie.XValueType = ChartValueType.DateTime;
            PrimarySellVolumeSerie.Color = ColorTranslator.FromHtml("#f6465d");

            PrimaryBuyVolumeSerie = Series.Add(nameof(PrimaryBuyVolumeSerie));
            PrimaryBuyVolumeSerie.ChartArea = nameof(PrimaryVolumeChartArea);
            PrimaryBuyVolumeSerie.ChartType = SeriesChartType.StackedColumn;
            PrimaryBuyVolumeSerie.XValueType = ChartValueType.DateTime;
            PrimaryBuyVolumeSerie.Color = ColorTranslator.FromHtml("#0ecb81");






            /// PRICE STRIPLINE
            PrimaryAreaPriceLine = new StripLine
            {
                StripWidth = 0.1,
                Interval = 0,
                Tag = nameof(PrimaryAreaPriceLine),
                BackColor = System.Drawing.Color.DarkBlue,
                ForeColor = System.Drawing.Color.Black
            };
            PrimaryChartArea.AxisY2.StripLines.Add(PrimaryAreaPriceLine);

            _ea.GetEvent<SymbolTickerEvent>().Subscribe(OnSymbolTickerEvent, ThreadOption.BackgroundThread);
            _ea.GetEvent<KlinesEvent>().Subscribe(OnKlinesEvent, ThreadOption.BackgroundThread);
            //_ea.GetEvent<MarkPriceEvent>().Subscribe(OnMarkPriceEvent, ThreadOption.BackgroundThread);
            _ea.GetEvent<AssetTickEvent>().Subscribe(OnMarketTickEvent, ThreadOption.BackgroundThread);

        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _ea.GetEvent<SymbolTickerEvent>().Unsubscribe(OnSymbolTickerEvent);
                //_ea.GetEvent<MarkPriceEvent>().Unsubscribe(OnMarkPriceEvent);
                _ea.GetEvent<KlinesEvent>().Unsubscribe(OnKlinesEvent);
                _ea.GetEvent<AssetTickEvent>().Unsubscribe(OnMarketTickEvent);
            }

            base.Dispose(disposing);
        }


        #region Market Events

        private void OnSymbolTickerEvent(IBinanceTick obj)
        {
            //App.Current.Dispatcher.Invoke(() =>
            //{
            //    PrimaryChartArea.AxisY.Minimum = PrimaryChartArea.AxisY2.Minimum = (double)obj.LowPrice;// Math.Floor((double)obj.LowPrice * 0.99);
            //    PrimaryChartArea.AxisY.Maximum = PrimaryChartArea.AxisY2.Maximum = (double)obj.HighPrice;//Math.Floor((double)obj.HighPrice * 1.01);
            //}, DispatcherPriority.Render, _cts.Token);
        }

        private void OnKlinesEvent(IEnumerable<IBinanceKline> data)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                this.Series.SuspendUpdates();

                if (data.Count() == 1)
                {
                    var latestKlineData = data.First();
                    var klineId = latestKlineData.OpenTime;
                    var klineIdAsNumber = klineId.ToOADate();

                    var chartKline = PrimaryKlineSerie.Points.LastOrDefault(i => i.XValue == klineIdAsNumber);
                    if (chartKline == null)
                    {
                        chartKline = new DataPoint(PrimaryKlineSerie);
                    }
                    chartKline.SetValueXY(klineId, (double)latestKlineData.High, (double)latestKlineData.Low, (double)latestKlineData.Open, (double)latestKlineData.Close);

                    var chartPoint = PrimaryBuyVolumeSerie.Points.LastOrDefault(i => i.XValue == klineIdAsNumber);
                    if (chartPoint == null)
                        chartPoint = new DataPoint(PrimaryBuyVolumeSerie);
                    chartPoint.SetValueXY(klineId, (double)latestKlineData.TakerBuyBaseVolume);

                    chartPoint = PrimarySellVolumeSerie.Points.LastOrDefault(i => i.XValue == klineIdAsNumber);
                    if (chartPoint == null)
                        chartPoint = new DataPoint(PrimarySellVolumeSerie);
                    chartPoint.SetValueXY(klineId, (double)(latestKlineData.BaseVolume - latestKlineData.TakerBuyBaseVolume));


                }
                else
                {

                    PrimaryKlineSerie.Points.Clear();
                    PrimaryKlineSerie.Points.AddRange(data.Select(kline =>
                    {
                        var chartKline = new DataPoint(PrimaryKlineSerie);
                        var klineId = kline.OpenTime;
                        chartKline.SetValueXY(klineId, (double)kline.High, (double)kline.Low, (double)kline.Open, (double)kline.Close);
                        return chartKline;
                    }));

                    var ma = this.Series.FirstOrDefault(i => i.Name == nameof(FinancialFormula.MovingAverage));
                    ma?.Points.Clear();
                    this.DataManipulator.FinancialFormula(FinancialFormula.MovingAverage, "15", nameof(PrimaryKlineSerie), nameof(FinancialFormula.MovingAverage));
                    this.Series[nameof(FinancialFormula.MovingAverage)].ChartType = SeriesChartType.Line;

                    PrimaryBuyVolumeSerie.Points.Clear();
                    PrimaryBuyVolumeSerie.Points.AddRange(data.Select(kline =>
                    {
                        var klineId = kline.OpenTime;

                        var chartPoint = new DataPoint(PrimaryBuyVolumeSerie);
                        chartPoint.SetValueXY(klineId, (double)kline.TakerBuyBaseVolume);

                        return chartPoint;
                    }));

                    PrimarySellVolumeSerie.Points.Clear();
                    PrimarySellVolumeSerie.Points.AddRange(data.Select(kline =>
                    {
                        var klineId = kline.OpenTime;

                        var chartPoint = new DataPoint(PrimarySellVolumeSerie);
                        chartPoint.SetValueXY(klineId, (double)(kline.BaseVolume - kline.TakerBuyBaseVolume));

                        return chartPoint;
                    }));

                    var minPrice = data.Min(x => x.Close);
                    var maxPrice = data.Max(x => x.Close);

                    PrimaryChartArea.AxisY.Minimum = PrimaryChartArea.AxisY2.Minimum = (double)minPrice;// Math.Floor((double)obj.LowPrice * 0.99);
                    PrimaryChartArea.AxisY.Maximum = PrimaryChartArea.AxisY2.Maximum = (double)maxPrice;//Math.Floor((double)obj.HighPrice * 1.01);
                }



                this.Series.ResumeUpdates();


            }, DispatcherPriority.Render, _cts.Token);





        }

        private void OnMarketTickEvent((DateTime updated, double price) data)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                PrimaryAreaPriceLine.IntervalOffset = data.price;
                PrimaryAreaPriceLine.Text = data.price.ToString("C2");

                //PrimaryChartArea.AxisY.CustomLabels.Add(data.price, data.price+1, "PRICE");
                //if (PrimaryAreaPriceLabel == null)
                //{
                //    this.PrimaryAreaPriceLabel = new CustomLabel(0, 0, "PRICE", 0, LabelMarkStyle.None);
                //    this.PrimaryChartArea.AxisY2.CustomLabels.Add(this.PrimaryAreaPriceLabel);
                //}

                //PrimaryAreaPriceLabel.FromPosition = data.price;
                //PrimaryAreaPriceLabel.ToPosition = data.price +1;
                //PrimaryAreaPriceLabel.LabelMark = LabelMarkStyle.Box;
                //PrimaryAreaPriceLabel.Text = PrimaryAreaPriceLine.Text;

            }, DispatcherPriority.Render, _cts.Token);
        }

        //private void OnMarkPriceEvent(BinanceFuturesStreamMarkPrice data)
        //{
        //    App.Current.Dispatcher.Invoke(() =>
        //    {
        //        PrimaryAreaPriceLine.IntervalOffset = (double)data.MarkPrice;
        //        PrimaryAreaPriceLine.Text = data.MarkPrice.ToString("C2");
        //    }, DispatcherPriority.Render, _cts.Token);
        //}

        #endregion

    }
}
