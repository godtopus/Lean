using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp.Dev.Models
{
    public interface IModel
    {
        void Train(List<double[]> inputs, List<int> outputs, List<double> weights = null);

        double[][] NormalizedInput { get; }
    }
}
