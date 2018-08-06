using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Elasticsearch
{
    public interface IBaseDataPoint
    {
        DateTime Time { get; set; }
        DateTime EndTime { get; set; }
        decimal Value { get; set; }
    }
}
