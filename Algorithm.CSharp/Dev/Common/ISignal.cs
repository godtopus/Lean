using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp
{
    public interface ISignal
    {
        void Scan(QuoteBar data);

        SignalType Signal { get; }
    }

    public interface IExitSignal : ISignal
    {
        ISignal ExitSignalFactory(TradeProfile tradeProfile);
    }

    public interface IRetrainable : ISignal
    {
        void Retrain(List<double[]> inputs, List<int> outputs, List<double> weights = null);
    }

    public enum SignalType
    {
        Long = 1,
        Short = -1,
        Exit = 2,
        NoSignal = 0,
        LongToShort = 3,
        ShortToLong = 4,
        PendingLong = 5,
        PendingShort = 6
    }
}