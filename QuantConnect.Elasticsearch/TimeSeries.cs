using Nest;
using System;
using System.Collections.Generic;

namespace QuantConnect.Elasticsearch
{
    public class TimeSeries
    {
        public string Name { get; set; }
        public List<IBaseDataPoint> Series { get; set; }

        public static IReadOnlyCollection<T> FromSearch<T>(string name, DateTime time, DateTime endTime)
            where T : class, IBaseDataPoint
        {
            return Query.Search(name, time, endTime).Documents;
        }
    }
}
