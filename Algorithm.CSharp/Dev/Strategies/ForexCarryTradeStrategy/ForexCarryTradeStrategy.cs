using QuantConnect.Data.Custom;
using QuantConnect.Data.Market;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp
{
    class ForexCarryTradeStrategy : QCAlgorithm
    {
        public string[] Forex = {
            "USDEUR", "USDCAD", "USDAUD",
            "USDJPY", "USDTRY", "USDINR",
            "USDCNY", "USDMXN", "USDZAR"
        };

        public IEnumerable<string> Symbols => Forex;

        private Resolution _dataResolution = Resolution.Daily;

        private Dictionary<string, string> _rateSymbols = new Dictionary<string, string>
        {
            { "USDEUR", "BCB/17900" },
            { "USDCAD", "BCB/17881" },
            { "USDAUD", "BCB/17880" },
            { "USDJPY", "BCB/17903" },

            { "USDTRY", "BCB/17907" },
            { "USDINR", "BCB/17901" },
            { "USDCNY", "BCB/17899" },
            { "USDMXN", "BCB/17904" },
            { "USDZAR", "BCB/17906" },

			//{ "GBPUSD", "BCB/17908" },
			//{ "USD", "BCB/18152" },
        };

        private int _positionCount = 3;

        private decimal _leverage = 10m;

        public override void Initialize()
        {
            SetStartDate(2010, 1, 1);
            SetEndDate(2018, 1, 1);
            SetCash(3000);

            foreach (var symbol in Symbols)
            {
                AddForex(symbol, _dataResolution, Market.Oanda, false, _leverage);
                AddData<QuandlRate>(_rateSymbols[symbol], _dataResolution, TimeZones.Utc, false);
            }

            Schedule.On(DateRules.MonthStart("USDEUR"),
                TimeRules.AfterMarketOpen("USDEUR"), () =>
                {
                    var orderByRateDecreasing = Symbols.Select((s) =>
                    {
                        var kv = new KeyValuePair<string, string>(s, _rateSymbols[s]);
                        return Tuple.Create(kv, Securities[kv.Value].Price);
                    }).OrderByDescending((kvr) => kvr.Item2);

                    foreach (var kvr in orderByRateDecreasing.Take(_positionCount))
                    {
                        SetHoldings(kvr.Item1.Key, _leverage * 1m / (_positionCount * 2));
                    }

                    foreach (var kvr in orderByRateDecreasing.Skip(Math.Max(0, orderByRateDecreasing.Count() - _positionCount)))
                    {
                        SetHoldings(kvr.Item1.Key, _leverage * -1m / (_positionCount * 2));
                    }

                    foreach (var kvr in orderByRateDecreasing.Skip(_positionCount).Take(orderByRateDecreasing.Count() - (2 * _positionCount)))
                    {
                        Liquidate(kvr.Item1.Key);
                    }

                    foreach (var kvr in orderByRateDecreasing)
                    {
                        //Console.WriteLine("Symbol: {0} Rate: {1}", kvr.Item1.Key, kvr.Item2);
                    }
                });

            //AddData<DailyFx>("DFX", Resolution.Minute, TimeZones.Utc);
        }

        public void OnData(QuoteBars data)
        {

        }

        public void OnData(QuandlRate data)
        {

        }

        public void OnData(DailyFx calendar)
        {
            if (!Portfolio.Invested || calendar.Importance != FxDailyImportance.High)
            {
                return;
            }

            foreach (var symbol in Symbols)
            {
                if (calendar.Meaning == FxDailyMeaning.Better && Portfolio[symbol].IsShort)
                {

                }
                else if (calendar.Meaning == FxDailyMeaning.Worse && Portfolio[symbol].IsLong)
                {

                }
            }
        }
    }

    public class QuandlRate : Quandl
    {
        public QuandlRate() : base(valueColumnName: "Value")
        {
        }
    }
}