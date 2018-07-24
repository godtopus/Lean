using QuantConnect.Orders;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    class OrderUtil
    {
        public static decimal RoundOrderPrices(decimal stopPrice)
        {
            // An increment is a tenth of a pip (pipette).
            var increment = 0.00001m;
            var stopRound = Math.Round(stopPrice / increment) * increment;

            return stopRound;
        }

        public static bool IsUnprofitable(decimal currentPrice, OrderEvent orderEvent)
        {
            return orderEvent.Status == OrderStatus.Filled && (orderEvent.Direction == OrderDirection.Buy ? currentPrice < orderEvent.FillPrice : currentPrice > orderEvent.FillPrice);
        }
    }
}
