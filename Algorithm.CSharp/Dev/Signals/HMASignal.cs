using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class HMASignal : ISignal
    {
        private SecurityHolding _securityHolding;

        private HullMovingAverage _fast;
        private HullMovingAverage _medium;
        private HullMovingAverage _slow;
        private InstantTrend _trend;

        private decimal previousFast;
        private decimal previousMedium;
        private decimal previousSlow;
        private decimal previousPrice;

        public HMASignal(HullMovingAverage fast,
            HullMovingAverage medium,
            HullMovingAverage slow,
            InstantTrend trend,
            SecurityHolding securityHolding)
        {
            _fast = fast;
            _medium = medium;
            _slow = slow;
            _trend = trend;
            _securityHolding = securityHolding;
        }

        public void Scan(QuoteBar data)
        {
            var filter = !_securityHolding.Invested;

            bool enterLongSignal, enterShortSignal, exitLongSignal, exitShortSignal;

            enterLongSignal = filter && _slow > previousSlow && data.Price > _slow && data.Price > previousPrice;
            enterShortSignal = filter && _slow < previousSlow && data.Price < _slow && data.Price < previousPrice;

            exitLongSignal = _slow < previousSlow && data.Price < previousPrice;
            exitShortSignal = _slow > previousSlow && data.Price > previousPrice;

            if (enterShortSignal)
            {
                Signal = SignalType.Short;
            }
            else if (enterLongSignal)
            {
                Signal = SignalType.Long;
            }
            else if ((exitLongSignal) && _securityHolding.IsLong)
            {
                // exit long due to switching
                Signal = SignalType.Exit;
            }
            else if ((exitShortSignal) && _securityHolding.IsShort)
            {
                // exit short due to switching
                Signal = SignalType.Exit;
            }
            else
            {
                Signal = SignalType.NoSignal;
            }

            previousFast = _fast;
            previousMedium = _medium;
            previousSlow = _slow;
            previousPrice = data.Price;
        }

        public SignalType Signal { get; private set; }
    }
}