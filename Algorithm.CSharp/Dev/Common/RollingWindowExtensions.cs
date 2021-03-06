﻿using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System;
using System.Collections.Generic;
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

        public static bool CrossAbove(this RollingWindow<decimal> window, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window.Count - 1); i++)
            {
                predicate = window[i] > boundary * (1 + tolerance) && window[i + 1] < boundary * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossAbove(this RollingWindow<IndicatorDataPoint> window, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window.Count - 1); i++)
            {
                predicate = window[i] > boundary * (1 + tolerance) && window[i + 1] < boundary * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossAbove<T>(this RollingWindow<T> window, decimal boundary, int lookback = 1, decimal tolerance = 0m)
            where T : IBaseDataBar
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window.Count - 1); i++)
            {
                predicate = window[i].Close > boundary * (1 + tolerance) && window[i].Open < boundary * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow(this RollingWindow<decimal> window, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window.Count - 1); i++)
            {
                predicate = window[i] < boundary * (1 - tolerance) && window[i + 1] > boundary * (1 + tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow(this RollingWindow<IndicatorDataPoint> window, decimal boundary, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window.Count - 1); i++)
            {
                predicate = window[i] < boundary * (1 - tolerance) && window[i + 1] > boundary * (1 + tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool CrossBelow<T>(this RollingWindow<T> window, decimal boundary, int lookback = 1, decimal tolerance = 0m)
            where T : IBaseDataBar
        {
            var predicate = false;
            for (var i = 0; i < Math.Min(lookback, window.Count - 1); i++)
            {
                predicate = window[i].Open > boundary * (1 + tolerance) && window[i].Close < boundary * (1 - tolerance);
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool DoubleCrossAbove(this RollingWindow<IndicatorDataPoint> window1, RollingWindow<IndicatorDataPoint> window2, RollingWindow<IndicatorDataPoint> window3, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            var fastSlowCross = false;
            var mediumSlowCross = false;

            for (var i = 0; i < Math.Min(lookback, Math.Min(window1.Count - 1, window2.Count - 1)); i++)
            {
                if (!mediumSlowCross)
                {
                    mediumSlowCross = window2[i] > window3[i] * (1 + tolerance) && window2[i + 1] < window3[i + 1] * (1 - tolerance);
                }
                else if (mediumSlowCross)
                {
                    fastSlowCross = window1[i] > window3[i] * (1 + tolerance) && window1[i + 1] < window3[i + 1] * (1 - tolerance);
                }

                predicate = mediumSlowCross && fastSlowCross;
                if (predicate)
                {
                    break;
                }
            }

            return predicate;
        }

        public static bool DoubleCrossBelow(this RollingWindow<IndicatorDataPoint> window1, RollingWindow<IndicatorDataPoint> window2, RollingWindow<IndicatorDataPoint> window3, int lookback = 1, decimal tolerance = 0m)
        {
            var predicate = false;
            var fastSlowCross = false;
            var mediumSlowCross = false;

            for (var i = 0; i < Math.Min(lookback, Math.Min(window1.Count - 1, window2.Count - 1)); i++)
            {
                if (!mediumSlowCross)
                {
                    mediumSlowCross = window2[i] < window3[i] * (1 - tolerance) && window2[i + 1] > window3[i + 1] * (1 + tolerance);
                }
                else if (mediumSlowCross)
                {
                    fastSlowCross = window1[i] < window3[i] * (1 - tolerance) && window1[i + 1] > window3[i + 1] * (1 + tolerance);
                }

                predicate = mediumSlowCross && fastSlowCross;
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
            return InRangeInclusive(window[0], lowerBound, upperBound, tolerance);
        }

        public static bool InRangeInclusive(this RollingWindow<IndicatorDataPoint> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return InRangeInclusive(window[0].Value, lowerBound, upperBound, tolerance);
        }

        /*public static bool InRangeInclusive<T>(this RollingWindow<T> window, decimal lowerBound, decimal upperBound, Func<T, decimal> selector = null, decimal tolerance = 0m)
            where T : IBaseDataBar
        {
            selector = selector ?? (x => x.Value);
            return InRangeInclusive(selector(window[0]), lowerBound, upperBound, tolerance);
        }*/

        private static bool InRangeInclusive(decimal w, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return w >= lowerBound * (1 - tolerance) && w <= upperBound * (1 + tolerance);
        }

        public static bool InRangeExclusive(this RollingWindow<decimal> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return InRangeExclusive(window[0], lowerBound, upperBound, tolerance);
        }

        public static bool InRangeExclusive(this RollingWindow<IndicatorDataPoint> window, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return InRangeExclusive(window[0].Value, lowerBound, upperBound, tolerance);
        }

        /*public static bool InRangeExclusive<T>(this RollingWindow<T> window, decimal lowerBound, decimal upperBound, Func<T, decimal> selector = null, decimal tolerance = 0m)
            where T : IBaseDataBar
        {
            selector = selector ?? (x => x.Value);
            return InRangeExclusive(selector(window[0]), lowerBound, upperBound, tolerance);
        }*/

        private static bool InRangeExclusive(decimal w, decimal lowerBound, decimal upperBound, decimal tolerance = 0m)
        {
            return w > lowerBound * (1 - tolerance) && w < upperBound * (1 + tolerance);
        }

        public static IEnumerable<decimal> Diff(this RollingWindow<IndicatorDataPoint> window1, RollingWindow<IndicatorDataPoint> window2, int lookback = 1)
        {
            return window1.Take(lookback).Select((w1, index) => w1 - window2[index]);
        }

        public static IEnumerable<decimal> Diff<T>(this RollingWindow<T> window1, RollingWindow<IndicatorDataPoint> window2, Func<T, decimal> selector = null, int lookback = 1)
            where T : IBaseDataBar
        {
            selector = selector ?? (x => x.Value);

            return window1.Take(lookback).Select((w1, index) => selector(w1) - window2[index]);
        }
    }
}
