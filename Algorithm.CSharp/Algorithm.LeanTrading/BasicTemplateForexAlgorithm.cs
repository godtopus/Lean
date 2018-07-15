using QuantConnect.Data;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public class BasicTemplateForexAlgorithm : QCAlgorithm
    {
        private string symbol = "EURUSD";

        private ArnaudLegouxMovingAverage _alma;
        private ParabolicStopAndReverse _psar;

        public override void Initialize()
        {
            SetStartDate(2016, 01, 01);
            SetEndDate(DateTime.Now);
            SetCash(10000);

            AddForex(symbol, Resolution.Minute);

            _alma = new ArnaudLegouxMovingAverage(50);
            _psar = new ParabolicStopAndReverse();

            var fiveConsolidator = new QuoteBarConsolidator(TimeSpan.FromMinutes(5));
            SubscriptionManager.AddConsolidator(symbol, fiveConsolidator);

            RegisterIndicator(symbol, _alma, fiveConsolidator);
            RegisterIndicator(symbol, _psar, fiveConsolidator);

            fiveConsolidator.DataConsolidated += OnFiveMinutes;

            var history = History(System.TimeSpan.FromMinutes(5 * 50), Resolution.Minute);

            foreach (var data in history.OrderBy(x => x.Time))
            {
                foreach (var key in data.Keys)
                {
                    //_alma.Update(key.Time, key.Value);
                }
            }
        }

        public override void OnData(Slice data)
        {
            foreach (var key in data.Keys)
            {
                Log(key.Value + ": " + data[key].Time + " > " + data[key].Value);
            }
        }

        private void OnFiveMinutes(object sender, QuoteBar consolidated)
        {
            if (!_alma.IsReady || !_psar.IsReady)
            {
                return;
            }

            if (!Portfolio[symbol].HoldStock)
            {
                if (consolidated.Close < _alma && _psar > consolidated.High)
                {
                    SetHoldings(symbol, -1m);
                } else if (consolidated.Close > _alma && _psar < consolidated.Low) {
                    SetHoldings(symbol, 1m);
                }
            } else
            {
                if (Portfolio[symbol].IsLong && consolidated.Close < _psar)
                {
                    SetHoldings(symbol, 0m);
                }
                else if (Portfolio[symbol].IsShort && consolidated.Close > _psar)
                {
                    SetHoldings(symbol, 0m);
                }
            }
        }
    }
}