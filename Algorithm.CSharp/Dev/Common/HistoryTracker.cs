using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    public static class HistoryTracker
    {
        public static RollingWindow<IndicatorDataPoint> Track<T>(IndicatorBase<T> indicator, int historyLength = 8)
            where T : IBaseData
        {
            var window = new RollingWindow<IndicatorDataPoint>(historyLength);
            indicator.Updated += (sender, args) => window.Add(indicator.Current);
            return window;
        }

        public static RollingWindow<decimal> Track<T, R>(IndicatorBase<T> indicator, Func<IndicatorBase<T>, decimal> projection, int historyLength = 8)
            where T : IBaseData
        {
            var window = new RollingWindow<decimal>(historyLength);
            indicator.Updated += (sender, args) => window.Add(projection(indicator));
            return window;
        }

        public static RollingWindow<T> Track<T>(DataConsolidator<T> consolidator, int historyLength = 8)
            where T : IBaseDataBar
        {
            var window = new RollingWindow<T>(historyLength);
            consolidator.DataConsolidated += (sendar, args) => window.Add((T)args);
            return window;
        }

        public static RollingWindow<T> Track<T>(IDataConsolidator consolidator, int historyLength = 8) where T : class
        {
            var window = new RollingWindow<T>(historyLength);
            consolidator.DataConsolidated += (sendar, args) => window.Add(args as T);
            return window;
        }
    }
}
