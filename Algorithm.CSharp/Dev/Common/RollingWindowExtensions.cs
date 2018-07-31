using QuantConnect.Indicators;
using System;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    public static class RollingWindowExtensions
    {
        public static bool CrossAbove(this RollingWindow<decimal> window1, RollingWindow<decimal> window2, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, Math.Min(window1.Count - 1, window2.Count - 1)); i++)
            {
                predicate = window1[i] > window2[i] * (1 + tolerance) && window1[i + 1] < window2[i + 1] * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossAbove(this RollingWindow<IndicatorDataPoint> window1, RollingWindow<IndicatorDataPoint> window2, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, Math.Min(window1.Count - 1, window2.Count - 1)); i++)
            {
                predicate = window1[i] > window2[i] * (1 + tolerance) && window1[i + 1] < window2[i + 1] * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow(this RollingWindow<decimal> window1, RollingWindow<decimal> window2, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, Math.Min(window1.Count - 1, window2.Count - 1)); i++)
            {
                predicate = window1[i] < window2[i] * (1 - tolerance) && window1[i + 1] > window2[i + 1] * (1 + tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow(this RollingWindow<IndicatorDataPoint> window1, RollingWindow<IndicatorDataPoint> window2, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, Math.Min(window1.Count - 1, window2.Count - 1)); i++)
            {
                predicate = window1[i] < window2[i] * (1 - tolerance) && window1[i + 1] > window2[i + 1] * (1 + tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossAbove(this RollingWindow<decimal> window1, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window1.Count - 1); i++)
            {
                predicate = window1[i] > boundary * (1 + tolerance) && window1[i + 1] < boundary * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossAbove(this RollingWindow<IndicatorDataPoint> window1, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window1.Count - 1); i++)
            {
                predicate = window1[i] > boundary * (1 + tolerance) && window1[i + 1] < boundary * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow(this RollingWindow<decimal> window1, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window1.Count - 1); i++)
            {
                predicate = window1[i] < boundary * (1 - tolerance) && window1[i + 1] > boundary * (1 + tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow(this RollingWindow<IndicatorDataPoint> window1, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window1.Count - 1); i++)
            {
                predicate = window1[i] < boundary * (1 - tolerance) && window1[i + 1] > boundary * (1 + tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool Rising(this RollingWindow<decimal> window, int lookback = 1, decimal tolerance = 0m)
        {
            return window.Take(lookback).Zip(window.Skip(1).Take(lookback), (w1, w2) => Tuple.Create(w1, w2)).All((w) => w.Item1 > w.Item2 * (1 + tolerance));
        }

        public static bool Rising(this RollingWindow<IndicatorDataPoint> window, int lookback = 1, decimal tolerance = 0m)
        {
            return window.Take(lookback).Zip(window.Skip(1).Take(lookback), (w1, w2) => Tuple.Create(w1, w2)).All((w) => w.Item1 > w.Item2 * (1 + tolerance));
        }

        public static bool Falling(this RollingWindow<decimal> window, int lookback = 1, decimal tolerance = 0m)
        {
            return window.Take(lookback).Zip(window.Skip(1).Take(lookback), (w1, w2) => Tuple.Create(w1, w2)).All((w) => w.Item1 < w.Item2 * (1 - tolerance));
        }

        public static bool Falling(this RollingWindow<IndicatorDataPoint> window, int lookback = 1, decimal tolerance = 0m)
        {
            return window.Take(lookback).Zip(window.Skip(1).Take(lookback), (w1, w2) => Tuple.Create(w1, w2)).All((w) => w.Item1 < w.Item2 * (1 - tolerance));
        }

        public static bool InRangeInclusive(this RollingWindow<decimal> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return window[0] >= lowerBound * (1 - tolerance) && window[0] <= upperBound * (1 + tolerance);
        }

        public static bool InRangeInclusive(this RollingWindow<IndicatorDataPoint> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return window[0] >= lowerBound * (1 - tolerance) && window[0] <= upperBound * (1 + tolerance);
        }

        public static bool InRangeExclusive(this RollingWindow<decimal> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return window[0] > lowerBound * (1 - tolerance) && window[0] < upperBound * (1 + tolerance);
        }

        public static bool InRangeExclusive(this RollingWindow<IndicatorDataPoint> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return window[0] > lowerBound * (1 - tolerance) && window[0] < upperBound * (1 + tolerance);
        }
    }
}
