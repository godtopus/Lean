using System;
using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using QuantConnect.Brokerages;
using System.Linq;
using QuantConnect.Data.Consolidators;
using System.Threading.Tasks;
using QuantConnect.Data.Custom;
using QuantConnect.Orders;
using System.Drawing;
using QuantConnect.Statistics;
using QuantConnect.Algorithm.CSharp.Dev.Common;

namespace QuantConnect.Algorithm.CSharp
{
    public class SVMBaselineStrategy : QCAlgorithm, IRequiredOrderMethods
    {
        /*
         * EURUSD 0.5
         * AUDUSD 1.0
         * EURGBP 0.3
         * USDCAD 0.3
         */
        /*
         * EURUSD 0.68
         * AUDUSD 0.63
         * EURGBP 0.1
         * USDCAD 0.72
         */
        public string[] Forex = {
            "EURUSD",
            "AUDUSD",
            //"GBPUSD",
            "EURGBP",
            "USDCAD",
            //"NZDUSD",
            //"USDCHF"
        };
        //public string[] Forex2 = { "AUDCAD", "AUDCHF", "AUDNZD", "CADCHF", "EURAUD", "EURCHF", "NZDCAD", "NZDCHF", "USDJPY" };

        public IEnumerable<string> Symbols => Forex;

        private decimal _maximumTradeSize = 200m;
        private decimal _targetProfitLoss = 1m;
        private decimal _maximumTradeRisk = 2000m;

        private Resolution _dataResolution = Resolution.Minute;
        private Dictionary<string, TradingAsset> _tradingAssets = new Dictionary<string, TradingAsset>();

        private bool _store = false;

        public override void Initialize()
        {
            SetStartDate(2016, 1, 1);
            SetEndDate(2017, 1, 1);
            SetCash(3000);

            SetBrokerageMessageHandler(new CustomBrokerageMessageHandler(this));

            foreach (var symbol in Symbols)
            {
                AddForex(symbol, _dataResolution, Market.Oanda, false, 1m);

                Securities[symbol].TransactionModel = new OandaTransactionModel();
                //Securities[symbol].SlippageModel = new ConstantSlippageModel(0m);
                SetBrokerageModel(BrokerageName.OandaBrokerage);

                /******** LONG TERM TREND ********/
                var dailyConsolidator = new QuoteBarConsolidator(TimeSpan.FromHours(4));
                var alma = new ArnaudLegouxMovingAverage(symbol, 55);

                RegisterIndicator(symbol, alma, dailyConsolidator);
                SubscriptionManager.AddConsolidator(symbol, dailyConsolidator);

                /******** SHORT TERM TRADING ********/
                var consolidator = new QuoteBarConsolidator(TimeSpan.FromMinutes(15));
                var schaffTrendCycle = new SchaffTrendCycle(symbol);
                var adx = new AverageDirectionalIndex(symbol, 10);

                RegisterIndicator(symbol, schaffTrendCycle, consolidator);
                RegisterIndicator(symbol, adx, consolidator);

                /****** ALMA Fibonacci ******/
                var alma5 = new ArnaudLegouxMovingAverage(symbol, 5);
                var alma8 = new ArnaudLegouxMovingAverage(symbol, 8);
                var alma13 = new ArnaudLegouxMovingAverage(symbol, 13);
                var alma21 = new ArnaudLegouxMovingAverage(symbol, 21);
                var alma34 = new ArnaudLegouxMovingAverage(symbol, 34);
                var alma55 = new ArnaudLegouxMovingAverage(symbol, 55);
                var alma89 = new ArnaudLegouxMovingAverage(symbol, 89);
                var alma144 = new ArnaudLegouxMovingAverage(symbol, 144);

                RegisterIndicator(symbol, alma5, consolidator);
                RegisterIndicator(symbol, alma8, consolidator);
                RegisterIndicator(symbol, alma13, consolidator);
                RegisterIndicator(symbol, alma21, consolidator);
                RegisterIndicator(symbol, alma34, consolidator);
                RegisterIndicator(symbol, alma55, consolidator);
                RegisterIndicator(symbol, alma89, consolidator);
                RegisterIndicator(symbol, alma144, consolidator);

                SubscriptionManager.AddConsolidator(symbol, consolidator);

                var signal = new SVMBaselineSignal(
                    dailyConsolidator, alma,
                    consolidator, alma5, alma8, alma13, alma21, alma34, alma55, alma89, alma144, schaffTrendCycle, adx,
                    Portfolio[symbol], Securities[symbol], this
                );

                var std = ATR(symbol, 100, MovingAverageType.DoubleExponential, _dataResolution);

                /******** HISTORY ********/
                var history = History<QuoteBar>(symbol, TimeSpan.FromDays(100), _dataResolution);

                foreach (var bar in history)
                {
                    std.Update(bar);
                    consolidator.Update(bar);
                    dailyConsolidator.Update(bar);
                }

                if (_store)
                {
                    var header = new string[] { "Time", "ALMA_5", "ALMA_8", "ALMA_13", "ALMA_21", "ALMA_34", "ALMA_55", "ALMA_89", "ALMA_144" };
                    Storage.CreateFile($"C:\\Users\\M\\Desktop\\{symbol}_ALMA.csv", header);

                    consolidator.DataConsolidated += (sender, args) =>
                    {
                        var line = new object[] { Storage.ToUTCTimestamp(args.Time), alma5.Current.Value, alma8.Current.Value, alma13.Current.Value,
                        alma21.Current.Value, alma34.Current.Value, alma55.Current.Value, alma89.Current.Value, alma144.Current.Value };
                        Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{symbol}_ALMA.csv", line);
                    };
                }

                Securities[symbol].VolatilityModel = new AverageTrueRangeVolatilityModel(std);
                _tradingAssets.Add(symbol,
                    new TradingAsset(Securities[symbol],
                        new OneShotTrigger(signal),
                        new ProfitTargetSignalExit(null, _targetProfitLoss),
                        _maximumTradeRisk,
                        _maximumTradeSize,
                        this
                    ));

                //_tradingAssets[symbol].IsTradable = true;

                var h = new object[] { "Time", "Signal", "Price" };
                Storage.CreateFile($"C:\\Users\\M\\Desktop\\{symbol}_ALMA_Signal.csv", h);
            }

            Schedule.On(DateRules.Every(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday),
                TimeRules.At(7, 0, TimeZones.London), () =>
                {
                    var tradeableDay = TradingCalendar.GetTradingDay().BusinessDay;
                    if (tradeableDay)
                    {
                        foreach (var s in Symbols)
                        {
                            _tradingAssets[s].IsTradable = true;
                        }
                    }
                });

            Schedule.On(DateRules.Every(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday),
                TimeRules.At(20, 0, TimeZones.London), () =>
                {
                    foreach (var s in Symbols)
                    {
                        //_tradingAssets[s].IsTradable = false;
                    }
                });

            Schedule.On(DateRules.Every(DayOfWeek.Friday),
                TimeRules.BeforeMarketClose(Symbols.First(), 240), () =>
                {
                    foreach (var s in Symbols)
                    {
                        //_tradingAssets[s].IsTradable = false;
                    }
                });

            Schedule.On(DateRules.Every(DayOfWeek.Friday),
                TimeRules.BeforeMarketClose(Symbols.First(), 180), () =>
                {
                    foreach (var s in Symbols)
                    {
                        //_tradingAssets[s].Liquidate();
                        //_tradingAssets[s].IsTradable = false;
                    }
                });

            /******** CHARTING ********/
            /*Chart price = new Chart("Daily Price");
            price.AddSeries(new Series("Price", SeriesType.Candle, 0));
            price.AddSeries(new Series("MA", SeriesType.Line, 0));
            price.AddSeries(new Series("Slope", SeriesType.Line, 1));
            price.AddSeries(new Series("STC", SeriesType.Line, 2));
            AddChart(price);

            Chart ma = new Chart("MA");
            ma.AddSeries(new Series("HMA", SeriesType.Line, 0));
            ma.AddSeries(new Series("FAMA", SeriesType.Line, 1));
            AddChart(ma);

            Chart lsma = new Chart("LSMA");
            lsma.AddSeries(new Series("Slope", SeriesType.Line, 0));
            AddChart(lsma);*/

            Chart plotter = new Chart("Plotter");
            plotter.AddSeries(new Series("Close", SeriesType.Line, 0));
            plotter.AddSeries(new Series("EMA", SeriesType.Line, 1));
            plotter.AddSeries(new Series("Buy", SeriesType.Scatter, "", Color.Green, ScatterMarkerSymbol.Triangle));
            plotter.AddSeries(new Series("Sell", SeriesType.Scatter, "", Color.Red, ScatterMarkerSymbol.TriangleDown));
            //plotter.AddSeries(new Series("Stopped", SeriesType.Scatter, "", Color.Yellow, ScatterMarkerSymbol.Diamond));
            plotter.AddSeries(new Series("Diff", SeriesType.Bar, 2));
            plotter.AddSeries(new Series("Slope", SeriesType.Line, 3));
            plotter.AddSeries(new Series("STC", SeriesType.Line, 4));
            plotter.AddSeries(new Series("STOCH", SeriesType.Line, 5));
            plotter.AddSeries(new Series("Prediction", SeriesType.Bar, 6));
            plotter.AddSeries(new Series("Signal", SeriesType.Bar, 7));
            AddChart(plotter);
        }

        public void PlotSignal(QuoteBar current, decimal ema, decimal slope, decimal stc, decimal stoch,  int prediction, int signal)
        {
            Plot("Plotter", "Close", current.Close);
            Plot("Plotter", "EMA", ema);
            Plot("Plotter", "Diff", (current.Close - ema) * 10000m);
            Plot("Plotter", "Slope", slope);
            Plot("Plotter", "STC", stc);
            Plot("Plotter", "STOCH", stoch);
            Plot("Plotter", "Prediction", prediction);
            Plot("Plotter", "Signal", signal);
        }

        public void OnData(QuoteBars data)
        {
            try
            {
                foreach (var symbol in Symbols.Where(s => data.ContainsKey(s)))
                {
                    //Plot("Daily Price", "Price", data[symbol].Close);
                    //Plot("Plotter", "Close", data[symbol].Close);
                    _tradingAssets[symbol].Scan(data[symbol], ((AverageTrueRangeVolatilityModel)Securities[symbol].VolatilityModel).IsWarmingUp);
                }
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
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
                    _tradingAssets[symbol].IsTradable = false;
                    _tradingAssets[symbol].Liquidate();
                    Task.Delay(1000 * 60 * 60).ContinueWith(t => _tradingAssets[symbol].IsTradable = true);
                }
                else if (calendar.Meaning == FxDailyMeaning.Worse && Portfolio[symbol].IsLong)
                {
                    _tradingAssets[symbol].IsTradable = false;
                    _tradingAssets[symbol].Liquidate();
                    Task.Delay(1000 * 60 * 60).ContinueWith(t => _tradingAssets[symbol].IsTradable = true);
                }
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            var ticket = Transactions.GetOrderTickets(x => x.OrderId == orderEvent.OrderId).Single();

            if (orderEvent.Status == OrderStatus.Filled)
            {
                foreach (var symbol in Symbols.Where(s => ticket.Symbol.Value == s))
                {
                    //Plot("Plotter", "Price", Securities[symbol].Price);
                    //Plot("Plotter", "STO", _stoch[symbol]);

                    if ((ticket.OrderType == OrderType.Market || ticket.OrderType == OrderType.Limit) && orderEvent.Direction == OrderDirection.Buy)
                    {
                        Plot("Plotter", "Buy", ticket.AverageFillPrice);
                    }
                    else if ((ticket.OrderType == OrderType.Market || ticket.OrderType == OrderType.Limit) && orderEvent.Direction == OrderDirection.Sell)
                    {
                        Plot("Plotter", "Sell", ticket.AverageFillPrice);
                    }
                    else if (ticket.OrderType == OrderType.StopMarket)
                    {
                        Plot("Plotter", "Stopped", ticket.AverageFillPrice);
                    }

                    var line = new object[] { Storage.ToUTCTimestamp(orderEvent.UtcTime.Subtract(TimeSpan.FromHours(6))), ticket.Tag, ticket.AverageFillPrice };
                    Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{ticket.Symbol.Value}_ALMA_Signal.csv", line);
                }
            }
        }

        public override void OnEndOfDay()
        {
            foreach (var symbol in Symbols)
            {
                //Plot("Plotter", "Price", Securities[symbol].Price);
                //Plot("Plotter", "STO", _stoch[symbol]);
            }
        }

        private void GenerateTradeSummary(IEnumerable<Trade> trades, string header = "")
        {
            var tradeStatistics = new TradeStatistics(trades);
            var tradeSummary = tradeStatistics.GetSummary();

            Console.WriteLine(header);

            foreach (KeyValuePair<string, string> kvp in tradeSummary)
            {
                Console.WriteLine("{0} {1}", kvp.Key, kvp.Value);
            }
        }

        public override void OnEndOfAlgorithm()
        {
            try
            {
                GenerateTradeSummary(TradeBuilder.ClosedTrades, "Trade Summary (All Trades)");
                GenerateTradeSummary(TradeBuilder.ClosedTrades.Where((t) => t.Direction == TradeDirection.Long), "Trade Summary (Long Trades)");
                GenerateTradeSummary(TradeBuilder.ClosedTrades.Where((t) => t.Direction == TradeDirection.Short), "Trade Summary (Short Trades)");

                var equityChangePerDay = PerformanceMetrics.EquityChangePerDay(TradeBuilder.ClosedTrades);

                var backtestPeriod = EndDate - StartDate;

                var TRASYCODRAVOPFACOM = PerformanceMetrics.TRASYCODRAVOPFACOM(TradeBuilder.ClosedTrades, backtestPeriod);
                var lakeRatio = PerformanceMetrics.LakeRatio(TradeBuilder.ClosedTrades);
                var blissFunction = PerformanceMetrics.BlissFunction(TradeBuilder.ClosedTrades);

                Console.WriteLine("TRASYCODRAVOPFACOM: {0} Lake Ratio: {1} Bliss Function: {2}", TRASYCODRAVOPFACOM, lakeRatio, blissFunction);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}