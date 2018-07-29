using QuantConnect.Data.Market;
using System;

namespace QuantConnect.Indicators
{
    public class ZeroLagExponentialMovingAverage : BarIndicator
    {
        private ExponentialMovingAverage _ema;
        private Delay _delayedEMA;
        private Delay _delayedZLEMA;

        public override bool IsReady => _delayedEMA.IsReady && _delayedEMA.IsReady && _delayedZLEMA.IsReady;

        private int _period;
        private decimal _k;
        private int _lag;

        public ZeroLagExponentialMovingAverage(string name, int period) : base(name)
        {
            _period = period;
            _k = 2m / (_period + 1);
            _lag = (int)Math.Round((_period - 1) / 2m);

            _ema = new ExponentialMovingAverage(name + "_EMA", period);
            _delayedEMA = new Delay(name + "_DelayedEMA", _lag);
            _delayedZLEMA = new Delay(name + "_DelayedZLEMA", 1);
        }

        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            _ema.Update(input.EndTime, input.Close);
            _delayedEMA.Update(_ema.Current);

            var zlema = (_k * ((2 * _ema) - _delayedEMA)) + ((1 - _k) * _delayedZLEMA);
            _delayedZLEMA.Update(input.EndTime, zlema);

            return zlema;
        }
    }
}