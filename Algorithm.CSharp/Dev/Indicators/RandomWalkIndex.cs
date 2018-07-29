using QuantConnect.Data.Market;
using System;

namespace QuantConnect.Indicators
{
    public class RandomWalkIndexHigh : BarIndicator
    {
        private Delay _low;
        private AverageTrueRange _atr;

        public override bool IsReady => _low.IsReady && _atr.IsReady;

        private int _period;
        private decimal _sqrtPeriod;

        public RandomWalkIndexHigh(string name, int period) : base(name)
        {
            _period = period;
            _sqrtPeriod = (decimal)Math.Sqrt(_period);

            _low = new Delay(name + "_Low", _period);
            _atr = new AverageTrueRange(name + "_ATR", _period);
        }

        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            _low.Update(input.EndTime, input.Low);
            _atr.Update(input);

            return (input.High - _low) / (_atr * _sqrtPeriod);
        }
    }
}