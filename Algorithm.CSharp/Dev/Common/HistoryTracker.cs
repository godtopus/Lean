using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    public static class HistoryTracker
    {
        public static RollingWindow<IndicatorDataPoint> Track<T>(IndicatorBase<T> indicator, int historyLength = 8)
            where T : BaseData
        {
            var window = new RollingWindow<IndicatorDataPoint>(historyLength);
            indicator.Updated += (sender, args) => window.Add(indicator.Current);
            return window;
        }

        public static RollingWindow<T> Track<T>(DataConsolidator<T> consolidator, int historyLength = 8)
            where T : BaseData
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
