using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp
{
    class OneShotTrigger : IRetrainable
    {
        private ISignal _signal;
        private SignalType _previousSignalType;
        public OneShotTrigger(ISignal signal)
        {
            _signal = signal;
            Signal = SignalType.NoSignal;
            _previousSignalType = SignalType.NoSignal;
        }

        public void Scan(QuoteBar data)
        {
            _signal.Scan(data);

            if (_signal.Signal != _previousSignalType)
            {
                Signal = _signal.Signal;
            }
            else
            {
                Signal = SignalType.NoSignal;
            }
            _previousSignalType = _signal.Signal;
        }

        public void Retrain(List<double[]> inputs, List<int> outputs, List<double> weights = null)
        {
            ((IRetrainable)_signal).Retrain(inputs, outputs, weights);
        }

        public SignalType Signal { get; private set; }
    }
}