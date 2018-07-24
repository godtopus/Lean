using System;

namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    class MathUtil
    {
        public static double DivisionToRealRange(double numerator, double denominator)
        {
            var result = numerator / denominator;

            return Double.IsPositiveInfinity(result)
                    ? Double.MaxValue
                    : Double.IsNegativeInfinity(result)
                        ? Double.MinValue
                        : result;
        }
    }
}
