using QuantConnect.Data;
using QuantConnect.Data.Market;
using System;

namespace QuantConnect.Indicators
{
    public class EmpiricalModeDecomposition : BarIndicator
    {
        public enum Direction
        {
            Up = 1,
            Down = -1,
            Flat = 0
        }

        public override bool IsReady => _bars.IsReady && _bps.IsReady && _peak.IsReady && _valley.IsReady && _bpMA.IsReady && _peakMA.IsReady && _valleyMA.IsReady;

        public decimal Peak => _fraction * _peakMA;
        public decimal Valley => _fraction * _valleyMA;
        public int LeadDirection => (int)_direction;

        private RollingWindow<IBaseDataBar> _bars;
        private RollingWindow<double> _bps;
        private Identity _peak;
        private Identity _valley;
        private SimpleMovingAverage _bpMA;
        private SimpleMovingAverage _peakMA;
        private SimpleMovingAverage _valleyMA;

        private int _period;
        private decimal _fraction;
        private decimal _delta;

        private Direction _direction = Direction.Flat;

        public EmpiricalModeDecomposition(string name) : this(name, 20, 0.1m, 0.5m)
        {
        }

        public EmpiricalModeDecomposition(string name, int period, decimal fraction, decimal delta) : base(name)
        {
            _period = period;
            _fraction = fraction;
            _delta = delta;

            _bars = new RollingWindow<IBaseDataBar>(3);
            _bps = new RollingWindow<double>(3);
            _peak = new Identity(name + "_Peak");
            _valley = new Identity(name + "_Valley");
            _bpMA = new SimpleMovingAverage(2 * _period);
            _peakMA = new SimpleMovingAverage(50);
            _valleyMA = new SimpleMovingAverage(50);
        }

        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            _bars.Add(input);

            if (!_bars.IsReady)
            {
                return 0m;
            }

            double beta = Math.Cos(2 * Math.PI / _period);
            double gamma = (1 / Math.Cos(4 * Math.PI * (double)_delta / _period));
            double alpha = gamma - Math.Sqrt(Math.Pow(gamma, 2) - 1);
            double p0 = (double)(_bars[0].High + _bars[0].Low) / 2;
            double p2 = (double)(_bars[2].High + _bars[2].Low) / 2;
            double bp = _bps.IsReady ? 0.5 * (1 - alpha) * (p0 - p2) + beta * (1 + alpha) * _bps[0] - alpha * _bps[1] : 0.5 * (1 - alpha) * (p0 - p2);

            _bps.Add(bp);
            _bpMA.Update(input.Time, (decimal)bp);

            if (!_bps.IsReady)
            {
                return 0m;
            }

            double peak = _bps[1] > _bps[0] && _bps[1] > _bps[2] ? _bps[1] : (double)_peak.Current.Value;
            double valley = _bps[1] < _bps[0] && _bps[1] < _bps[2] ? _bps[1] : (double)_valley.Current.Value;

            _peak.Update(input.Time, (decimal)peak);
            _valley.Update(input.Time, (decimal)valley);
            _peakMA.Update(input.Time, (decimal)peak);
            _valleyMA.Update(input.Time, (decimal)valley);

            return _bpMA.IsReady ? _bpMA : 0m;
        }
    }
}