using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Binance.Net.Objects.Spot;
using Binance.Net.SymbolOrderBooks;

using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;

using Prism.Events;
using Prism.Ioc;

using Trader.Models;

namespace Trader.Extensions
{
    public class GroupedBinanceFuturesUsdtSymbolOrderBook : BinanceFuturesUsdtSymbolOrderBook
    {
        private static readonly Lazy<IEventAggregator> _lazyEventAggregator = new Lazy<IEventAggregator>(() => ContainerLocator.Container.Resolve<IEventAggregator>());
        private IEventAggregator _eventAggregator => _lazyEventAggregator.Value;

        public GroupedBinanceFuturesUsdtSymbolOrderBook(string symbol) : base(symbol, new BinanceOrderBookOptions(limit: null))
        {
            OnStatusChange += (_, newStatus) =>
            {
                _eventAggregator.GetEvent<ConnectionStatusEvent>().Publish(newStatus == OrderBookStatus.Synced || newStatus == OrderBookStatus.Syncing);
                OnChanged();
            };
            OnOrderBookUpdate += GroupedBinanceFuturesUsdtSymbolOrderBook_OnOrderBookUpdate;
            OnBestOffersChanged += (offer) =>
            {
                OnChanged();
            };
        }

        private void GroupedBinanceFuturesUsdtSymbolOrderBook_OnOrderBookUpdate((IEnumerable<ISymbolOrderBookEntry> Bids, IEnumerable<ISymbolOrderBookEntry> Asks) obj)
        {
            App.RunUI(() =>
            {

                GroupedOrderBookEntry.MaxSum = 0;
                var asks = Asks
                    .GroupBy(ask => GroupKey(ask.Price, bid: false))
                     //.OrderByDescending(entry => entry.Key)
                     .SelectWithPreviousResult<IGrouping<decimal, ISymbolOrderBookEntry>, GroupedOrderBookEntry>((prevGroupedOrderBookEntry, groupedAsks) =>
                     {
                         var prevIsNull = prevGroupedOrderBookEntry is null;
                         var quantity = groupedAsks.Select(ask => ask.Quantity).DefaultIfEmpty().Sum();
                         var sum = prevIsNull ? quantity : quantity + prevGroupedOrderBookEntry.Sum;
                         if (sum > GroupedOrderBookEntry.MaxSum)
                             GroupedOrderBookEntry.MaxSum = sum;

                         return new GroupedOrderBookEntry
                         {
                             Price = groupedAsks.Key,
                             Quantity = quantity,
                             Sum = sum
                         };
                     }).Take(25);

                GroupedAsks.Except(asks).ForEachMutable(i => GroupedAsks.Remove(i));
                asks.ForEach(entry => GroupedAsks.ReplaceOrAdd(entry, i => i.Price == entry.Price));



                var bids = Bids
                    .GroupBy(bid => GroupKey(bid.Price, bid: true))
                    .SelectWithPreviousResult<IGrouping<decimal, ISymbolOrderBookEntry>, GroupedOrderBookEntry>((prevGroupedOrderBookEntry, groupedBids) =>
                    {
                        var prevIsNull = prevGroupedOrderBookEntry is null;
                        var quantity = groupedBids.Select(bid => bid.Quantity).DefaultIfEmpty().Sum();
                        var sum = prevIsNull ? quantity : quantity + prevGroupedOrderBookEntry.Sum;
                        if (sum > GroupedOrderBookEntry.MaxSum)
                            GroupedOrderBookEntry.MaxSum = sum;
                        return new GroupedOrderBookEntry
                        {
                            Price = groupedBids.Key,
                            Quantity = quantity,
                            Sum = sum
                        };
                    })
                    .Take(25);

                GroupedBids.Except(bids).ForEachMutable(i => GroupedBids.Remove(i));

                bids.ForEach(entry => GroupedBids.ReplaceOrAdd(entry, i => i.Price == entry.Price));

            });

            OnChanged();
        }

        private void OnChanged()
        {
            OnChange?.Invoke();
        }

        public static readonly decimal[] ValidValues = new[] { 0.01m, 0.1m, 1m, 10m, 50m, 100m };
        public event Action OnChange;
        private decimal _groupingInterval = 10m;
        public decimal GroupingInterval
        {
            get { return _groupingInterval; }
            set
            {
                if (ValidValues.Contains(value))
                    _groupingInterval = value;
            }
        }

        public ObservableCollectionExtension<GroupedOrderBookEntry> GroupedAsks { get; private set; } = new ObservableCollectionExtension<GroupedOrderBookEntry>(ObservableCollectionExtensionType.Reversed);
        public ObservableCollectionExtension<GroupedOrderBookEntry> GroupedBids { get; private set; } = new ObservableCollectionExtension<GroupedOrderBookEntry>(ObservableCollectionExtensionType.List);

        public decimal GroupKey(decimal number, bool bid) => bid ? ((int)Math.Floor(number / GroupingInterval)) * GroupingInterval : ((int)Math.Ceiling(number / GroupingInterval)) * GroupingInterval;

    }

    public class GroupedOrderBookEntry : ISymbolOrderBookEntry, IEquatable<GroupedOrderBookEntry>
    {
        public static decimal MaxSum { get; set; }
        public static decimal MinSum { get; set; }
        public decimal Depth => Math.Round((Sum- MinSum) / MaxSum, 4);
        public decimal DepthVideModel => Depth * 100;
        public decimal Sum { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }

        public override string ToString()
        {
            return $"{Price:C2} {Quantity,20:N2} {Sum,20:N2} {Depth,6:P2} ";
        }

        public bool Equals(GroupedOrderBookEntry other)
        {
            return Price == other.Price;
        }
    }
}
