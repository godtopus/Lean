using QuantConnect.Data.Market;
using QuantConnect.Orders;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    public class TradeProfile
    {
        public OrderTicket OpenTicket, StopTicket, ExitTicket;
        public decimal CurrentPrice;
        public int TradeDirection => OpenTicket.Quantity > 0 ? 1 : OpenTicket.Quantity < 0 ? -1 : 0;
        public Symbol TradeSymbol;

        private decimal _risk;
        private int _maximumTradeQuantity;
        protected decimal _volatility;
        private decimal _trailingStop = 20m / 10000m;

        public int Quantity
        {
            get
            {
                if (_volatility == 0)
                {
                    return 0;
                }

                long quantity = (long)(_risk / _volatility);

                if (quantity > _maximumTradeQuantity)
                {
                    return _maximumTradeQuantity;
                }

                //return (int) quantity;
                return _maximumTradeQuantity;
            }
        }

        public decimal DeltaStopLoss
        {
            get
            {
                if (Quantity == 0)
                {
                    return 0m;
                }

                return _risk / 10000m;
            }
        }

        public decimal ProfitLossRatio
        {
            get
            {
                if (OpenTicket != null)
                {
                    return Math.Abs(CurrentPrice - OpenTicket.AverageFillPrice) / (_risk / 10000m);
                }

                return 0m;
            }
        }

        public bool IsSpreadTradable(QuoteBar latestQuote)
        {
            return Math.Abs(latestQuote.Ask.Close - latestQuote.Bid.Close) * 10000 <= 3;
        }

        public void UpdateStopLoss(QuoteBar latestQuote)
        {
            return;
            if ((latestQuote.Bid.Close - _trailingStop > StopTicket.Get(OrderField.StopPrice) && TradeDirection > 0) ||
                (latestQuote.Ask.Close + _trailingStop < StopTicket.Get(OrderField.StopPrice) && TradeDirection < 0))
            {
                StopTicket.Update(
                    new UpdateOrderFields
                    {
                        StopPrice = OrderUtil.RoundOrderPrices(null, (TradeDirection > 0 ? latestQuote.Bid.Close : latestQuote.Ask.Close) + TradeDirection * _trailingStop)
                    }
                );
            }
        }

        public ISignal ExitSignal
        {
            get;
            set;
        }

        public bool IsTradeFinished
        {
            get;
            set;
        }

        public TradeProfile(Symbol symbol, decimal volatility, decimal risk, decimal currentPrice, decimal maximumTradeSize)
        {
            TradeSymbol = symbol;
            _volatility = volatility;
            _risk = risk;
            CurrentPrice = currentPrice;
            _maximumTradeQuantity = (int)(maximumTradeSize / CurrentPrice);
        }
    }
}