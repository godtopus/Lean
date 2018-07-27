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
        // Shortable: EURGBP, USDCAD

        public string[] Forex = { "EURUSD", "AUDUSD", "GBPUSD", "EURGBP", "USDCAD", "NZDUSD", "USDCHF" /*"EURCHF", "AUDCAD", "AUDCHF", "AUDNZD", "CADCHF", "NZDCAD", "NZDCHF", "EURAUD"*/ };

        public IEnumerable<string> Symbols => Forex;

        private decimal _maximumTradeSize = 200m;
        private decimal _targetProfitLoss = 20m;
        private decimal _maximumTradeRisk = 3000m;

        private Resolution _dataResolution = Resolution.Minute;
        private Dictionary<string, TradingAsset> _tradingAssets = new Dictionary<string, TradingAsset>();

        /******** INDICATORS ********/
        private Dictionary<string, Stochastic> _stoch = new Dictionary<string, Stochastic>();
        private Dictionary<string, ExponentialMovingAverage> _ema = new Dictionary<string, ExponentialMovingAverage>();

        // TODO: check volatilitymodel https://github.com/QuantConnect/Lean/blob/master/Common/Securities/RelativeStandardDeviationVolatilityModel.cs
        public override void Initialize()
        {
            SetStartDate(2016, 1, 1);
            SetEndDate(2016, 4, 1);
            SetCash(3000);

            SetBrokerageMessageHandler(new CustomBrokerageMessageHandler(this));

            foreach (var symbol in Symbols)
            {
                AddForex(symbol, _dataResolution, Market.Oanda, false, 1m);

                Securities[symbol].TransactionModel = new OandaTransactionModel();
                //Securities[symbol].SlippageModel = new ConstantSlippageModel(0m);
                SetBrokerageModel(BrokerageName.OandaBrokerage);

                var consolidator = new QuoteBarConsolidator(TimeSpan.FromMinutes(20));
                var stoch = new Stochastic(symbol, 10, 3, 3);
                var stochMA = new ExponentialMovingAverage(symbol, 25).Of(stoch);
                var stochEmaLSMA = new LeastSquaresMovingAverage(symbol, 15).Of(stochMA);
                var ema = new ExponentialMovingAverage(symbol, 30);
                var emaMA = new LeastSquaresMovingAverage(symbol, 5).Of(ema);

                var rollingStochMA = HistoryTracker.Track(stochMA);
                var rollingEmaSlope = HistoryTracker.Track(emaMA.Slope);
                var rollingStochEmaSlope = HistoryTracker.Track(stochEmaLSMA.Slope);

                var shortTermMA = EMA(symbol, 30, Resolution.Minute, Field.Close);

                RegisterIndicator(symbol, stoch, consolidator);
                RegisterIndicator(symbol, ema, consolidator);
                SubscriptionManager.AddConsolidator(symbol, consolidator);

                _stoch[symbol] = stoch;
                _ema[symbol] = ema;

                var consolidatorDaily = new QuoteBarConsolidator(TimeSpan.FromHours(6));
                var dailyMA = new ExponentialMovingAverage(symbol, 5);
                var dailyEmaLSMA = new LeastSquaresMovingAverage(symbol, 3).Of(dailyMA);

                RegisterIndicator(symbol, dailyMA, consolidatorDaily);
                SubscriptionManager.AddConsolidator(symbol, consolidatorDaily);

                stochMA.Updated += (sender, args) =>
                {
                    if (Securities[symbol].Price > 0)
                    {
                        Plot("Indicator", "STO", rollingStochMA[0]);
                    }
                };

                stochEmaLSMA.Updated += (sender, args) =>
                {
                    if (Securities[symbol].Price > 0)
                    {
                        Plot("IndicatorTrend", "STO", stochEmaLSMA.Slope);
                    }
                };

                /*emaMA.Updated += (sender, args) =>
                {
                    if (Securities[symbol].Price > 0)
                    {
                        Plot("Trend", "LSMA", emaMA.Slope);
                    }
                };*/

                dailyEmaLSMA.Updated += (sender, args) =>
                {
                    if (Securities[symbol].Price > 0)
                    {
                        Plot("Trend", "LSMA", dailyEmaLSMA.Slope);
                        Plot("Trend", "EMA", dailyMA);
                    }
                };

                var std = ATR(symbol, 100, MovingAverageType.DoubleExponential, _dataResolution);

                var history = History<QuoteBar>(symbol, TimeSpan.FromDays(20), _dataResolution);

                foreach (var bar in history)
                {
                    std.Update(bar);
                    consolidator.Update(bar);
                    shortTermMA.Update(bar.EndTime, bar.Close);
                    consolidatorDaily.Update(bar);
                }

                var signal = new SVMBaselineSignal(consolidator, stoch, stochMA, rollingStochMA, stochEmaLSMA, rollingStochEmaSlope, ema, emaMA, rollingEmaSlope, shortTermMA, dailyEmaLSMA, Portfolio[symbol], Securities[symbol], this);

                Securities[symbol].VolatilityModel = new AverageTrueRangeVolatilityModel(std);
                _tradingAssets.Add(symbol,
                    new TradingAsset(Securities[symbol],
                        new OneShotTrigger(signal),
                        new ProfitTargetSignalExit(null, _targetProfitLoss),
                        _maximumTradeRisk,
                        _maximumTradeSize,
                        this
                    ));
            }

            //AddData<DailyFx>("DFX", Resolution.Second, TimeZones.Utc);

            Schedule.On(DateRules.Every(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday),
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
                        _tradingAssets[s].IsTradable = false;
                    }
                });

            Schedule.On(DateRules.Every(DayOfWeek.Friday),
                TimeRules.BeforeMarketClose(Symbols.First(), 60), () =>
                {
                    foreach (var s in Symbols)
                    {
                        _tradingAssets[s].Liquidate();
                    }
                });

            Chart plotter = new Chart("Plotter");
            plotter.AddSeries(new Series("Price", SeriesType.Line, 0));
            plotter.AddSeries(new Series("EMA", SeriesType.Line, 0));
            plotter.AddSeries(new Series("Buy", SeriesType.Scatter, "", Color.Green, ScatterMarkerSymbol.Triangle));
            plotter.AddSeries(new Series("Sell", SeriesType.Scatter, "", Color.Red, ScatterMarkerSymbol.TriangleDown));
            plotter.AddSeries(new Series("Stopped", SeriesType.Scatter, "", Color.Yellow, ScatterMarkerSymbol.Diamond));
            plotter.AddSeries(new Series("Prediction", SeriesType.Bar, 1));
            plotter.AddSeries(new Series("Probability", SeriesType.Bar, 2));
            AddChart(plotter);

            Chart indicator = new Chart("Indicator");
            indicator.AddSeries(new Series("STO", SeriesType.Line, 0));
            AddChart(indicator);

            Chart indicatorTrend = new Chart("IndicatorTrend");
            indicatorTrend.AddSeries(new Series("STO", SeriesType.Line, 0));
            AddChart(indicatorTrend);

            Chart trend = new Chart("Trend");
            trend.AddSeries(new Series("LSMA", SeriesType.Line, 0));
            trend.AddSeries(new Series("EMA", SeriesType.Line, 1));
            AddChart(trend);

            Chart prediction = new Chart("Prediction");
            prediction.AddSeries(new Series("Pred", SeriesType.Bar, 0));
            AddChart(prediction);

            Chart probability = new Chart("Probability");
            probability.AddSeries(new Series("Prob", SeriesType.Bar, 0));
            AddChart(probability);
        }

        public void PlotSignal(QuoteBar current, int prediction, double logLikelihood)
        {
            Plot("Prediction", "Pred", prediction);
            Plot("Probability", "Prob", logLikelihood);

            Plot("Plotter", "Price", current.Value);
            Plot("Plotter", "EMA", _ema[current.Symbol]);
            Plot("Plotter", "Prediction", prediction);
            Plot("Plotter", "Probability", logLikelihood);
        }

        public void OnData(QuoteBars data)
        {
            try
            {
                foreach (var symbol in Symbols.Where(s => data.ContainsKey(s)))
                {
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

        /*public override void OnMarginCallWarning()
        {
        	Liquidate();
        	Quit();
        }*/

        /*public override void OnOrderEvent(OrderEvent orderEvent)
        {
			if (orderEvent.Status == OrderStatus.Filled && orderEvent.FillQuantity < 0)
			{
				Symbol security = orderEvent.Symbol;
				SecurityHolding s = Securities[security].Holdings;	
				var profit_pct = s.LastTradeProfit / Portfolio.TotalPortfolioValue;
				_tradingAssets[security].Update(profit_pct);
			}
            
            base.OnOrderEvent(orderEvent);
        }*/
    }
}