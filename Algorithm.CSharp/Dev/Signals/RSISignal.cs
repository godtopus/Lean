using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class RSISignal : ISignal
    {
        private SecurityHolding _securityHolding;

        private RelativeStrengthIndex _rsi;
        private ExponentialMovingAverage _ema;
        private decimal previousRsi;
        private decimal previousEma;

        private bool above;
        private bool below;

        public RSISignal(RelativeStrengthIndex rsi,
            ExponentialMovingAverage ema,
            SecurityHolding securityHolding)
        {
            _rsi = rsi;
            _ema = ema;
            _securityHolding = securityHolding;
        }

        public void Scan(QuoteBar data)
        {
            if (!_rsi.IsReady || !_ema.IsReady) return;

            var filter = !_securityHolding.Invested;

            bool enterLongSignal = false, enterShortSignal = false, exitLongSignal = false, exitShortSignal = false;

            enterLongSignal = (filter && _rsi < previousRsi && _rsi < 20m);
            enterShortSignal = (filter && _rsi > previousRsi && _rsi > 80m);

            /*enterLongSignal = (filter && _ema > _rsi && previousEma < previousRsi);
	        enterShortSignal = (filter && _ema < _rsi && previousEma > previousRsi);

	        exitLongSignal = (_ema < _rsi && previousEma > previousRsi) || _rsi > 65m;
	        exitShortSignal = (_ema > _rsi && previousEma < previousRsi) || _rsi < 35m;*/

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
                // exit long due to bb switching
                Signal = SignalType.Exit;
            }
            else if ((exitShortSignal) && _securityHolding.IsShort)
            {
                // exit short due to bb switching
                Signal = SignalType.Exit;
            }
            else
            {
                Signal = SignalType.NoSignal;
            }

            previousRsi = _rsi;
            previousEma = _ema;
        }

        public SignalType Signal { get; private set; }
    }
}