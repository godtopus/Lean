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
    }
}
