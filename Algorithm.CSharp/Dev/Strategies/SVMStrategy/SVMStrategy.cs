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
    public class SVMStrategy : QCAlgorithm, IRequiredOrderMethods
    {
        public string[] Forex = { /*"NZDUSD",*/ "EURUSD"/*, "AUDUSD",*/ /*"GBPUSD", "EURGBP", "EURCHF", "AUDUSD", "AUDCAD", "AUDCHF", "AUDNZD", "CADCHF", "NZDCAD", "NZDCHF", "EURAUD"*/ };

        public IEnumerable<string> Symbols => Forex;

        private decimal _maximumTradeSize = 200m;
        private decimal _targetProfitLoss = 2m;
        private decimal _maximumTradeRisk = 2000m;

        private Resolution _dataResolution = Resolution.Minute;
        private Dictionary<string, TradingAsset> _tradingAssets = new Dictionary<string, TradingAsset>();

        /******** INDICATORS ********/
        private Dictionary<string, Stochastic> _stoch = new Dictionary<string, Stochastic>();

        // TODO: check volatilitymodel https://github.com/QuantConnect/Lean/blob/master/Common/Securities/RelativeStandardDeviationVolatilityModel.cs
        public override void Initialize()
        {
            SetStartDate(2016, 1, 1);
            SetEndDate(2016, 4, 1);
            SetCash(3000);

            SetBrokerageMessageHandler(new CustomBrokerageMessageHandler(this));

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
                TimeRules.At(18, 0, TimeZones.London), () =>
                {
                    foreach (var s in Symbols)
                    {
                        _tradingAssets[s].IsTradable = false;
                    }
                });

            var allInputs = new List<double[]>();
            var allOutputs = new List<int>();
            var allWeights = new List<double>();

            foreach (var symbol in Symbols)
            {
                AddForex(symbol, _dataResolution, Market.Oanda, false, 1m);

                Securities[symbol].TransactionModel = new OandaTransactionModel();
                //Securities[symbol].SlippageModel = new ConstantSlippageModel(0m);
                SetBrokerageModel(BrokerageName.OandaBrokerage);

                /************ TRAINING ************/
                var trainingResolution = Resolution.Minute;
                /*var slowT = HMA(symbol, 28, trainingResolution, Field.Close);
                var slowSlopeT = new InstantTrend(symbol, 5).Of(slowT);
                var returnT = LOGR(symbol, 3, trainingResolution, Field.Close);
                var returnSlopeT = LSMA(symbol, 5, trainingResolution).Of(returnT);*/

                var consolidatorT = new QuoteBarConsolidator(TimeSpan.FromMinutes(15));
                var stochT = new Stochastic(symbol, 14, 3, 3);
                var stochTMA = new HullMovingAverage(symbol, 3).Of(stochT);
                var emaT = new ExponentialMovingAverage(symbol, 40);
                RegisterIndicator(symbol, stochT, consolidatorT);
                RegisterIndicator(symbol, emaT, consolidatorT);
                SubscriptionManager.AddConsolidator(symbol, consolidatorT);

                var historyT = History<QuoteBar>(symbol, TimeSpan.FromDays(500), trainingResolution);

                var quoteBars = new List<QuoteBar>();
                var stochs = new List<double>();
                var rollingStochs = new RollingWindow<double>(1000);
                var stochsMA = new List<double>();
                var emas = new List<double>();
                var stochCount = new List<double>();
                var stochAverage = new List<double>();

                //consolidatorT.DataConsolidated += (sender, args) => quoteBars.Add(args);
                stochTMA.Updated += (sender, args) =>
                {
                    if (!stochTMA.IsReady || !emaT.IsReady)
                    {
                        return;
                    }

                    quoteBars.Add((QuoteBar)consolidatorT.Consolidated);
                    stochs.Add((double)stochT.Current.Value);
                    rollingStochs.Add((double)args.Value);
                    stochsMA.Add((double)args.Value);
                    emas.Add((double)emaT.Current.Value);

                    var filtered = rollingStochs.TakeWhile((s) => args.Value > 50 ? s > 50 : args.Value < 50 ? s < 50 : false);
                    stochCount.Add(filtered.Count());

                    try
                    {
                        stochAverage.Add(filtered.Average());
                    }
                    catch(Exception ex)
                    {
                        stochAverage.Add(0);
                    }
                };

                foreach (var bar in historyT)
                {
                    consolidatorT.Update(bar);
                }

                Console.WriteLine("{0} {1} {2} {3} {4}", quoteBars.Count, stochs.Count, stochCount.Count, stochAverage.Count, emas.Count);

                var inputs = new List<double[]>();
                var outputs = new List<int>();
                var weights = new List<double>();

                for (var i = 1; i < quoteBars.Count; i++)
                {
                    var longTarget = quoteBars[i].Close + (30m / 10000m);
                    var longStop = quoteBars[i].Close - (10m / 10000m);
                    var shortTarget = quoteBars[i].Close - (30m / 10000m);
                    var shortStop = quoteBars[i].Close + (10m / 10000m);

                    var longSetup = stochs[i] >= 35 && stochsMA[i] > stochsMA[i - 1] && (double)quoteBars[i].Close > emas[i];
                    var shortSetup = stochs[i] <= 65 && stochs[i] > 0 && stochsMA[i] < stochsMA[i - 1] && (double)quoteBars[i].Close < emas[i];

                    if (!longSetup && !shortSetup)
                    {
                        continue;
                    }

                    for (var j = i + 1; j < quoteBars.Count; j++)
                    {
                        var current = quoteBars[j];
                        if (current.High >= longTarget && current.Low > longStop && longSetup)
                        {
                            inputs.Add(new double[] { stochAverage[i], stochCount[i], (double)quoteBars[i].Close / emas[i] });
                            outputs.Add(1);

                            var profit = current.High - quoteBars[i].Close;
                            /*for (var k = j + 1; k < quoteBars.Count; k++)
                            {

                            }*/
                            weights.Add((double) (1m - (50m / 10000m) / profit));

                            //i = j;
                            break;
                        }
                        else if (current.Low <= shortTarget && current.High < shortStop && shortSetup)
                        {
                            inputs.Add(new double[] { stochAverage[i], stochCount[i], (double)quoteBars[i].Close / emas[i] });
                            outputs.Add(0);

                            var profit = quoteBars[i].Close - current.Low;
                            /*for (var k = j + 1; k < quoteBars.Count; k++)
                            {

                            }*/
                            weights.Add((double) (1m - (50m / 10000m) / profit));
                            //i = j;
                            break;
                        }
                        else if ((current.Low <= longStop && longSetup) || (current.High >= shortStop && shortSetup))
                        {
                            //inputs.Add(new double[] { stochAverage[i] / stochs[i], stochCount[i], stochAverage[i] });
                            //outputs.Add(2);
                            //i = j;
                            break;
                        }
                        /*else if (j - i > 4 * 8)
                        {
                            inputs.Add(new double[] { stochs[i], stochCount[i], stochAverage[i] });
                            outputs.Add(0);
                            //i = j;
                            break;
                        }*/
                    }
                }

                allInputs.AddRange(inputs);
                allOutputs.AddRange(outputs);
                allWeights.AddRange(weights);

                for (var i = 0; i < inputs.Count; i++)
                {
                    //Console.WriteLine("Input: " + inputs[i][0] + " " + inputs[i][1] + " " + inputs[i][2] + " Output: " + outputs[i]);
                }

                var none = outputs.Where((o) => o == 2).Count();
                var sell = outputs.Where((o) => o == 0).Count();
                var buy = outputs.Where((o) => o == 1).Count();

                Console.WriteLine("Total: {0} None: {1} Short: {2} Long: {3}", outputs.Count, none, sell, buy);

                /************ HMA ************/
                /*var slow = HMA(symbol, 28, trainingResolution, Field.Close);
                var slowSlope = new InstantTrend(symbol, 5).Of(slow);
                var logReturns = LOGR(symbol, 3, trainingResolution, Field.Close);
                var returnSlope = LSMA(symbol, 5, trainingResolution).Of(logReturns);*/

                var consolidator = new QuoteBarConsolidator(TimeSpan.FromMinutes(15));
                var stoch = new Stochastic(symbol, 14, 3, 3);
                var stochMA = new HullMovingAverage(symbol, 2).Of(stoch);
                var ema = new ExponentialMovingAverage(symbol, 40);
                var rolling = new RollingWindow<double>(1000);
                RegisterIndicator(symbol, stoch, consolidator);
                RegisterIndicator(symbol, ema, consolidator);
                SubscriptionManager.AddConsolidator(symbol, consolidator);

                _stoch[symbol] = stoch;

                stochMA.Updated += (sender, args) =>
                {
                    rolling.Add((double)args.Value);

                    if (Securities[symbol].Price > 0)
                    {
                        //Plot("Plotter", "Price", Securities["EURUSD"].Price);
                        Plot("Indicator", "STO", rolling[0]);
                    }
                };

                var std = ATR(symbol, 100, MovingAverageType.DoubleExponential, _dataResolution);

                var history = History<QuoteBar>(symbol, TimeSpan.FromDays(20), trainingResolution);

                foreach (var bar in history)
                {
                    //slow.Update(bar.EndTime, bar.Close);
                    //logReturns.Update(bar.EndTime, bar.Close);
                    std.Update(bar);
                    consolidator.Update(bar);
                }

                var signal = new SVMSignal(consolidator, stoch, stochMA, rolling, ema, Portfolio[symbol], this);
                signal.TrainSVM(inputs, outputs, weights);
                //signal.TrainNN(inputs, outputs, weights);

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

            foreach (var symbol in Symbols)
            {
                //_tradingAssets[symbol].Retrain(allInputs, allOutputs, allWeights);
            }

            //AddData<DailyFx>("DFX", Resolution.Minute, TimeZones.Utc);

            Chart plotter = new Chart("Plotter");
            plotter.AddSeries(new Series("Price", SeriesType.Line, 0));
            plotter.AddSeries(new Series("Buy", SeriesType.Scatter, "", Color.Green, ScatterMarkerSymbol.Triangle));
            plotter.AddSeries(new Series("Sell", SeriesType.Scatter, "", Color.Red, ScatterMarkerSymbol.TriangleDown));
            plotter.AddSeries(new Series("Stopped", SeriesType.Scatter, "", Color.Yellow, ScatterMarkerSymbol.Diamond));
            plotter.AddSeries(new Series("Prediction", SeriesType.Bar, 1));
            plotter.AddSeries(new Series("Probability", SeriesType.Bar, 2));
            AddChart(plotter);

            Chart indicator = new Chart("Indicator");
            indicator.AddSeries(new Series("STO", SeriesType.Line, 1));
            AddChart(indicator);

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

                    if (ticket.OrderType == OrderType.Market && orderEvent.Direction == OrderDirection.Buy)
                    {
                        Plot("Plotter", "Buy", ticket.AverageFillPrice);
                    }
                    else if (ticket.OrderType == OrderType.Market && orderEvent.Direction == OrderDirection.Sell)
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