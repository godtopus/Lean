using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    public class InternalBarStrength : TradeBarIndicator
    {
        public override bool IsReady => true;

        public InternalBarStrength(string name) : this(name, 2)
        {
        }

        public InternalBarStrength(string name, int period) : base(name)
        {
        }

        protected override decimal ComputeNextValue(TradeBar input)
        {
            return (input.Close - input.Low) / (input.High - input.Low);
        }
    }
}