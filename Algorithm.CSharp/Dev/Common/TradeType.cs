namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    public class TradeType
    {
        public enum Direction
        {
            TrendUp = 1,
            TrendDown = -1,
            Flat = 0,
            MeanRevertingUp = 2,
            MeanRevertingDown = -2
        }
    }
}
