using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    static class EnumerableExtensions
    {
        public static IEnumerable<TResult> Rollup<TSource, TResult>(
            this IEnumerable<TSource> source,
            TResult seed,
            Func<TSource, TResult, TResult> projection)
        {
            TResult nextSeed = seed;
            foreach (TSource src in source)
            {
                TResult projectedValue = projection(src, nextSeed);
                nextSeed = projectedValue;
                yield return projectedValue;
            }
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int n)
        {
            return source.Skip(Math.Max(0, source.Count() - n));
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.ShuffleIterator();
        }

        private static IEnumerable<T> ShuffleIterator<T>(this IEnumerable<T> source)
        {
            var buffer = source.ToList();
            for (int i = 0; i < buffer.Count; i++)
            {
                int j = StaticRandom.Instance.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }
    }
}
