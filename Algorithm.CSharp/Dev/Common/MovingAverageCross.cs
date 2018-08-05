using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    public class MovingAverageCross
    {
        private RollingWindow<IndicatorDataPoint> _fast;
        private RollingWindow<IndicatorDataPoint> _medium;
        private RollingWindow<IndicatorDataPoint> _slow;

        public bool CrossAbove(int lookback = 5, decimal tolerance = 0.7m / 10000m) => _fast.CrossAbove(_medium, lookback, tolerance);

        public bool CrossBelow(int lookback = 5, decimal tolerance = 0.7m / 10000m) => _fast.CrossBelow(_medium, lookback, tolerance);

        public bool DoubleCrossAbove(int lookback = 5, decimal tolerance = 0.7m / 10000m) => _fast.DoubleCrossAbove(_medium, _slow, lookback, tolerance);

        public bool DoubleCrossBelow(int lookback = 5, decimal tolerance = 0.7m / 10000m) => _fast.DoubleCrossBelow(_medium, _slow, lookback, tolerance);

        public MovingAverageCross(RollingWindow<IndicatorDataPoint> fast, RollingWindow<IndicatorDataPoint> medium, RollingWindow<IndicatorDataPoint> slow)
        {
            _fast = fast;
            _medium = medium;
            _slow = slow;
        }
    }
}
