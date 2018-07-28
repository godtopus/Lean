using System;

namespace QuantConnect.Indicators
{
    public class MesaSineWave : IndicatorBase<IndicatorDataPoint>
    {
        public enum Direction
        {
            Up = 1,
            Down = -1,
            Flat = 0
        }

        public override bool IsReady => _prices.IsReady;

        public decimal Sine { get; private set; }

        public decimal Lead { get; private set; }

        public int LeadDirection => (int)_direction;

        private RollingWindow<IndicatorDataPoint> _prices;
        private int _period;
        private Direction _direction = Direction.Flat;

        public MesaSineWave(string name) : this(name, 9)
        {
        }

        public MesaSineWave(string name, int period) : base(name)
        {
            _period = period;

            _prices = new RollingWindow<IndicatorDataPoint>(period);
        }

        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            double realPart = 0;
            double imagPart = 0;

            _prices.Add(input);

            if (!_prices.IsReady)
            {
                return LeadDirection;
            }

            for (int i = 0; i < _period; i++)
            {
                double temp = (double)_prices[i].Value;
                realPart = realPart + temp * Math.Cos(2 * Math.PI * i / _period);
                imagPart = imagPart + temp * Math.Sin(2 * Math.PI * i / _period);
            }

            double phase1 = Math.Abs(realPart) > 0.001 ? Math.Atan(imagPart / realPart) : Math.PI / 2 * Math.Sign(imagPart);
            double phase2 = realPart < 0 ? phase1 + Math.PI : phase1;
            double phase = phase2 < 0 ? phase2 + 2 * Math.PI : phase2 > 2 * Math.PI ? phase2 - 2 * Math.PI : phase2;

            Sine = (decimal)Math.Cos(phase);
            Lead = (decimal)Math.Cos(phase + Math.PI / 4);

            _direction = Lead > Sine ? Direction.Up : Lead < Sine ? Direction.Down : _direction;

            return LeadDirection;
        }
    }
}