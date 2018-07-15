using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class LogReturnSignal : ISignal
    {
        public BollingerBands _bb;
        private MovingAverageConvergenceDivergence _macd;
        private SecurityHolding _securityHolding;

        private decimal _previousClose;
        private decimal _lastTradePrice;
        private bool _previousShortSignal;
        private bool _previousLongSignal;

        private decimal previousLogrSlow;
        private decimal previousLogr_Slow;

        private decimal previousLogrFast;
        private decimal previousLogr_Fast;

        private LogReturn _logr_Fast;
        private LogReturn _logr_Slow;

        private LeastSquaresMovingAverage _logrFast;
        private LeastSquaresMovingAverage _logrSlow;

        private TickConsolidator _ticks;

        public LogReturnSignal(LogReturn logr_Fast,
            LogReturn logr_Slow,
            LeastSquaresMovingAverage logrFast,
            LeastSquaresMovingAverage logrSlow,
            TickConsolidator ticks,
            SecurityHolding securityHolding)
        {
            _logr_Fast = logr_Fast;
            _logr_Slow = logr_Slow;
            _logrFast = logrFast;
            _logrSlow = logrSlow;
            _ticks = ticks;
            _securityHolding = securityHolding;
        }

        public void Scan(QuoteBar data)
        {
            var filter = !_securityHolding.Invested;

            bool enterLongSignal, enterShortSignal, exitLongSignal, exitShortSignal;

            enterLongSignal = (!_securityHolding.Invested && _logrSlow > _logr_Slow && previousLogrSlow < previousLogr_Slow);

            enterShortSignal = (!_securityHolding.Invested && _logrSlow < _logr_Slow && previousLogrSlow > previousLogr_Slow);

            exitLongSignal = _logrSlow < _logr_Slow;//_logrFast > _logr_Fast && previousLogrFast < previousLogr_Fast;
            exitShortSignal = _logrSlow > _logr_Slow;//_logrFast < _logr_Fast && previousLogrFast > previousLogr_Fast;

            if (enterShortSignal)
            {
                Signal = SignalType.Short;
                _lastTradePrice = data.Price;
            }
            else if (enterLongSignal)
            {
                Signal = SignalType.Long;
                _lastTradePrice = data.Price;
            }
            else if (exitLongSignal && _securityHolding.IsLong)
            {
                // exit long due to bb switching
                Signal = SignalType.Exit;
            }
            else if (exitShortSignal && _securityHolding.IsShort)
            {
                // exit short due to bb switching
                Signal = SignalType.Exit;
            }
            else
            {
                Signal = SignalType.NoSignal;
            }

            _previousClose = data.Price;
            previousLogrSlow = _logrSlow;
            previousLogr_Slow = _logr_Slow;
            previousLogrFast = _logrFast;
            previousLogr_Fast = _logr_Fast;
            //_previousLongSignal = longSignal;
            //_previousShortSignal = shortSignal;
        }

        public SignalType Signal { get; private set; }
    }
}