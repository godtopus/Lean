namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    public class Trend
    {
        public enum Direction
        {
            Up = 1,
            Down = -1,
            Flat = 0,
            MeanRevertingUp = 2,
            MeanRevertingDown = -2
        }
    }
}
