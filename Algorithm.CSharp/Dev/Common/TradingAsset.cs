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

        public bool IsTradable = true;

        public void Retrain(List<double[]> inputs, List<int> outputs, List<double> weights = null)
        {
            ((IRetrainable)EnterSignal).Retrain(inputs, outputs, weights);
        }

        public void Liquidate()
        {
            MarkStopTicketsFilled();

            foreach (var tradeProfile in _tradeProfiles.Where(x => !x.IsTradeFinished))
            {
                tradeProfile.ExitTicket = _orderMethods.MarketOrder(_symbol, -(int)tradeProfile.OpenTicket.QuantityFilled);
                tradeProfile.StopTicket.Cancel();
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
        }

        public void Scan(QuoteBar data, bool isWarmingUp)
        {
            foreach (var tradeProfile in _tradeProfiles)
            {
                tradeProfile.UpdateStopLoss(data);
                tradeProfile.CurrentPrice = data.Price;
            }
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

                if (profile.Quantity > 0 && _security.Exchange.ExchangeOpen)
                {
                    profile.OpenTicket = _orderMethods.MarketOrder(_symbol, (int)EnterSignal.Signal * profile.Quantity);
                    //profile.OpenTicket = _orderMethods.LimitOrder(_symbol, (int)EnterSignal.Signal * profile.Quantity, data.Price - (int)EnterSignal.Signal * (10m / 10000m));
                    profile.StopTicket = _orderMethods.StopMarketOrder(_symbol, -(int)EnterSignal.Signal * profile.Quantity,
                        OrderUtil.RoundOrderPrices(profile.OpenTicket.AverageFillPrice - (int)EnterSignal.Signal * profile.DeltaStopLoss));

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

                if (tradeProfile.ExitSignal.Signal == SignalType.Exit
                    || EnterSignal.Signal == SignalType.Exit
                    /*|| (tradeProfile.OpenTicket.QuantityFilled > 0 && data.Price <= tradeProfile.StopTicket.Get(OrderField.StopPrice)
	            		|| tradeProfile.OpenTicket.QuantityFilled < 0 && data.Price >= tradeProfile.StopTicket.Get(OrderField.StopPrice)))*/
                    && tradeProfile.StopTicket.Status != OrderStatus.Filled
                    && _security.Exchange.ExchangeOpen)
                {
                    tradeProfile.ExitTicket = _orderMethods.MarketOrder(_symbol, -(int)tradeProfile.OpenTicket.QuantityFilled);
                    //tradeProfile.ExitTicket = _orderMethods.LimitOrder(_symbol, -(int) tradeProfile.OpenTicket.QuantityFilled, data.Price);
                    tradeProfile.StopTicket.Cancel();
                    tradeProfile.IsTradeFinished = true;
                }
            }
        }

        public void MarkStopTicketsFilled()
        {
            foreach (var tradeProfile in _tradeProfiles)
            {
                if (tradeProfile.StopTicket.Status == OrderStatus.Filled)
                {
                    tradeProfile.IsTradeFinished = true;
                }
            }
        }

        public void RemoveAllFinishedTrades()
        {
            _tradeProfiles = _tradeProfiles.Where(x => !x.IsTradeFinished).ToList();
        }
    }
}