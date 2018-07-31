using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    public class SchaffTrendCycle : BarIndicator
    {
        private MovingAverageConvergenceDivergence _macd;
        private Stochastic _frac1;
        private Stochastic _frac2;
        private Identity _pf;
        private Identity _pff;
        private decimal _factor;

        public override bool IsReady => _macd.IsReady && _frac1.IsReady && _frac2.IsReady;

        public SchaffTrendCycle(string name) : this(name, 23, 50, 10, 0.5m, MovingAverageType.Exponential)
        {
        }

        public SchaffTrendCycle(string name, int fastPeriod, int slowPeriod, int signalPeriod, decimal factor, MovingAverageType movingAverageType) : base(name)
        {
            _macd = new MovingAverageConvergenceDivergence(name + "_MACD", fastPeriod, slowPeriod, signalPeriod, movingAverageType);
            _frac1 = new Stochastic(name + "Frac1", 10, 10, 10);
            _frac2 = new Stochastic(name + "Frac2", 10, 10, 10);
            _pf = new Identity(name + "_PF");
            _pff = new Identity(name + "_PFF");
            _factor = factor;
        }

        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            _macd.Update(input.EndTime, input.Close);

            if (!_macd.IsReady) return 0m;

            var macdBar = new TradeBar
            {
                Time = input.Time,
                EndTime = input.EndTime,
                Open = _macd,
                High = _macd,
                Low = _macd,
                Close = _macd
            };
            _frac1.Update(macdBar);

            if (!_frac1.IsReady) return 0m;

            var pf = _pf.IsReady ? _pf + (_factor * (_frac1.FastStoch - _pf)) : _frac1.FastStoch;
            _pf.Update(input.Time, pf);

            var pfBar = new TradeBar
            {
                Time = input.Time,
                EndTime = input.EndTime,
                Open = pf,
                High = pf,
                Low = pf,
                Close = pf
            };
            _frac2.Update(pfBar);

            if (!_frac2.IsReady) return 0m;

            var pff = _pff.IsReady ? _pff + (_factor * (_frac2.FastStoch - _pff)) : _frac2.FastStoch;
            _pff.Update(input.Time, pff);

            return pff;
        }
    }
}