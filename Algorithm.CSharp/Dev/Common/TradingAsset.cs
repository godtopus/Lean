﻿using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    class TradingAsset
    {
        public IExitSignal ExitSignal;
        public ISignal EnterSignal;

        private decimal _risk;
        private Symbol _symbol;
        private Security _security;
        private decimal _maximumTradeSize;
        private List<TradeProfile> _tradeProfiles;
        private IRequiredOrderMethods _orderMethods;

        private HiddenMarkovModelPositionSizing _hmmPositionSizing;

        public bool IsTradable = true;

        public void Retrain(List<double[]> inputs, List<int> outputs, List<double> weights = null)
        {
            ((IRetrainable)EnterSignal).Retrain(inputs, outputs, weights);
        }

        public void Liquidate()
        {
            RemovePendingLimitOrders();
            MarkStopTicketsFilled();

            foreach (var tradeProfile in _tradeProfiles.Where(x => !x.IsTradeFinished))
            {
                tradeProfile.ExitTicket = _orderMethods.MarketOrder(_symbol, -(int)tradeProfile.OpenTicket.QuantityFilled);
                tradeProfile.StopTicket.Cancel();
                tradeProfile.IsTradeFinished = true;
            }

            RemoveAllFinishedTrades();
        }

        public TradingAsset(Security security, ISignal enterSignal, IExitSignal exitSignal, decimal risk, decimal maximumTradeSize, IRequiredOrderMethods orderMethods)
        {
            _security = security;
            _symbol = _security.Symbol;
            EnterSignal = enterSignal;
            ExitSignal = exitSignal;
            _risk = risk;
            _maximumTradeSize = maximumTradeSize;
            _orderMethods = orderMethods;
            _tradeProfiles = new List<TradeProfile>();

            _hmmPositionSizing = new HiddenMarkovModelPositionSizing();
        }

        public void Scan(QuoteBar data, bool isWarmingUp)
        {
            foreach (var tradeProfile in _tradeProfiles)
            {
                tradeProfile.UpdateStopLoss(data);
                tradeProfile.CurrentPrice = data.Price;
            }

            RemovePendingLimitOrders(data);
            MarkStopTicketsFilled();
            EnterTradeSignal(data, isWarmingUp);
            ExitTradeSignal(data);
            RemoveAllFinishedTrades();
        }

        public void EnterTradeSignal(QuoteBar data, bool isWarmingUp)
        {
            EnterSignal.Scan(data);

            if (!isWarmingUp && IsTradable
                && (EnterSignal.Signal == SignalType.Long || EnterSignal.Signal == SignalType.Short)
                && _security.Exchange.ExchangeOpen)
            {
                //Creates a new trade profile once it enters a trade
                var profile = new TradeProfile(_symbol, _security.VolatilityModel.Volatility, _risk, data.Price, _maximumTradeSize);

                if (!profile.IsSpreadTradable(data))
                {
                    return;
                }

                profile.ExitSignal = ExitSignal.ExitSignalFactory(profile);

                if (profile.Quantity > 0 && _security.Exchange.ExchangeOpen && _tradeProfiles.Count == 0)
                {
                    var hmmPrediction = 1m;// _hmmPositionSizing.PredictionRisk();
                    var quantity = (int)((int)EnterSignal.Signal * profile.Quantity * hmmPrediction);

                    profile.OpenTicket = _orderMethods.MarketOrder(_symbol, quantity, false, ((int)EnterSignal.Signal).ToString());
                    var askLimit = data.Ask.Close - (2m / 10000m);
                    var bidLimit = data.Bid.Close + (2m / 10000m);
                    var limitPrice = EnterSignal.Signal == SignalType.Long ? askLimit : bidLimit;
                    //profile.OpenTicket = _orderMethods.LimitOrder(_symbol, (int)EnterSignal.Signal * profile.Quantity, OrderUtil.RoundOrderPrices(limitPrice));

                    //var stopPrice = profile.OpenTicket.AverageFillPrice - (int)EnterSignal.Signal * profile.DeltaStopLoss;
                    var stopPrice = data.Close - (int)EnterSignal.Signal * (20m / 10000m);
                    profile.StopTicket = _orderMethods.StopMarketOrder(_symbol, -quantity, OrderUtil.RoundOrderPrices(_security, stopPrice));

                    /*Console.WriteLine("{0} {1} {2} {3}",
                        profile.OpenTicket.OrderEvents.Select((oe) => oe.Direction).First() == OrderDirection.Buy ? "Buy " : "Sell",
                        profile.OpenTicket.AverageFillPrice,
                        profile.StopTicket.Get(OrderField.StopPrice),
                        Math.Abs(data.Ask.Close - data.Bid.Close) * 10000
                    );*/

                    /*profile.StopTicket = _orderMethods.StopLimitOrder(_symbol, -(int) EnterSignal.Signal * profile.Quantity,
                        profile.OpenTicket.AverageFillPrice - (int) EnterSignal.Signal * profile.DeltaStopLoss,
                        profile.OpenTicket.AverageFillPrice - (int) EnterSignal.Signal * profile.DeltaStopLoss);*/

                    _tradeProfiles.Add(profile);
                }
            }
        }

        public void ExitTradeSignal(QuoteBar data)
        {
            foreach (var tradeProfile in _tradeProfiles.Where(x => !x.IsTradeFinished))
            {
                tradeProfile.ExitSignal.Scan(data);

                var timedExit = false;

                try
                {
                    var orderEvent = tradeProfile.OpenTicket.OrderEvents.FirstOrDefault((oe) => oe.Status == OrderStatus.Filled);

                    if (orderEvent != null)
                    {
                        var endTime = data.EndTime.ToUniversalTime();
                        var orderEventTime = orderEvent.UtcTime.Subtract(TimeSpan.FromHours(6));
                        var span = endTime.Subtract(orderEventTime);
                        var timeComparison = span.CompareTo(TimeSpan.FromMinutes(270));
                        // TODO: Timed Exit
                        //timedExit = timeComparison > 0 && OrderUtil.IsUnprofitable(data.Close, orderEvent);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                if ((tradeProfile.ExitSignal.Signal == SignalType.Exit
                    || EnterSignal.Signal == SignalType.Exit || EnterSignal.Signal == SignalType.Reverse || timedExit)
                    /*|| (tradeProfile.OpenTicket.QuantityFilled > 0 && data.Price <= tradeProfile.StopTicket.Get(OrderField.StopPrice)
	            		|| tradeProfile.OpenTicket.QuantityFilled < 0 && data.Price >= tradeProfile.StopTicket.Get(OrderField.StopPrice)))*/
                    && tradeProfile.StopTicket.Status != OrderStatus.Filled
                    && _security.Exchange.ExchangeOpen)
                {
                    tradeProfile.ExitTicket = _orderMethods.MarketOrder(_symbol, -(int)tradeProfile.OpenTicket.QuantityFilled, false, ((int)EnterSignal.Signal).ToString());
                    //tradeProfile.ExitTicket = _orderMethods.LimitOrder(_symbol, -(int) tradeProfile.OpenTicket.QuantityFilled, data.Price);
                    tradeProfile.StopTicket.Cancel();
                    tradeProfile.IsTradeFinished = true;
                }
            }
        }

        public void MarkStopTicketsFilled()
        {
            foreach (var tradeProfile in _tradeProfiles.Where((oe) => oe.StopTicket.Status == OrderStatus.Filled))
            {
                tradeProfile.IsTradeFinished = true;
            }
        }

        public void RemovePendingLimitOrders(QuoteBar data = null)
        {
            foreach (var tradeProfile in _tradeProfiles.Where((oe) => oe.OpenTicket.OrderType == OrderType.Limit))
            {
                var timeComparison = data == null ? 1 : -1;

                if (data != null)
                {
                    var orderEvent = tradeProfile.OpenTicket.OrderEvents.First();
                    var endTime = data.EndTime.ToUniversalTime();
                    var orderEventTime = orderEvent.UtcTime.Subtract(TimeSpan.FromHours(6));
                    var span = endTime.Subtract(orderEventTime);
                    timeComparison = span.CompareTo(TimeSpan.FromMinutes(15));
                }

                if (tradeProfile.OpenTicket.Status != OrderStatus.Filled && timeComparison >= 0)
                {
                    tradeProfile.OpenTicket.Cancel();
                    tradeProfile.StopTicket.Cancel();
                    tradeProfile.IsTradeFinished = true;
                }
            }
        }

        public void RemoveAllFinishedTrades()
        {
            foreach (var tradeProfile in _tradeProfiles.Where(x => x.IsTradeFinished))
            {
                try
                {
                    var orderEvent = tradeProfile.OpenTicket.OrderEvents.Where((oe) => oe.Status == OrderStatus.Filled).First();
                    var exitPrice = tradeProfile.ExitTicket != null ? tradeProfile.ExitTicket.AverageFillPrice : tradeProfile.StopTicket.AverageFillPrice;
                    var profitLoss = (tradeProfile.OpenTicket.AverageFillPrice - exitPrice) / tradeProfile.OpenTicket.AverageFillPrice;
                    profitLoss = (orderEvent.Direction == OrderDirection.Sell ? Math.Abs(profitLoss) : profitLoss) * 100m;
                    _hmmPositionSizing.Update(profitLoss);
                }
                catch(Exception ex)
                {

                }
            }

            _tradeProfiles = _tradeProfiles.Where(x => !x.IsTradeFinished).ToList();
        }
    }
}