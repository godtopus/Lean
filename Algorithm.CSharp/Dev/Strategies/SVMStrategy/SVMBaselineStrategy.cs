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
        public string[] Forex = { "EURUSD"/*, "AUDUSD", "GBPUSD", "EURGBP", "USDCAD", "NZDUSD", "USDCHF"*/ };

        public IEnumerable<string> Symbols => Forex;

        private decimal _maximumTradeSize = 200m;
        private decimal _targetProfitLoss = 20m;
        private decimal _maximumTradeRisk = 3000m;

        private Resolution _dataResolution = Resolution.Minute;
        private Dictionary<string, TradingAsset> _tradingAssets = new Dictionary<string, TradingAsset>();

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

                /******** DAILY TREND ********/
                var consolidatorDaily = new QuoteBarConsolidator(TimeSpan.FromDays(1));
                var dailyHMA = new HullMovingAverage(symbol, 5);
                var dailyHmaLSMA = new LeastSquaresMovingAverage(symbol, 4).Of(dailyHMA);
                var dailyFAMA = new FractalAdaptiveMovingAverage(symbol, 2, 2);
                var dailyFamaLSMA = new LeastSquaresMovingAverage(symbol, 3).Of(dailyFAMA);
                var mesa = new MesaSineWave(symbol);

                var rollingDailyHMA = HistoryTracker.Track(dailyHMA);
                var rollingDailyHMASlope = HistoryTracker.Track(dailyHmaLSMA.Slope);
                var rollingDailyFAMA = HistoryTracker.Track(dailyFAMA);
                var rollingDailyFAMASlope = HistoryTracker.Track(dailyFamaLSMA.Slope);

                RegisterIndicator(symbol, dailyHMA, consolidatorDaily);
                RegisterIndicator(symbol, dailyFAMA, consolidatorDaily);
                RegisterIndicator(symbol, mesa, consolidatorDaily);
                SubscriptionManager.AddConsolidator(symbol, consolidatorDaily);

                dailyHmaLSMA.Updated += (sender, args) =>
                {
                    var currentQuote = (QuoteBar)consolidatorDaily.Consolidated;
                    var longTermTrend = currentQuote.Close > dailyHMA && dailyHmaLSMA > 0.001
                                        ? Trend.Direction.Up
                                        : currentQuote.Close < dailyHMA && dailyHmaLSMA < -0.001
                                        ? Trend.Direction.Down
                                        : Trend.Direction.Flat;

                    if (Securities[symbol].Price > 0)
                    {
                        Plot("MA", "HMA", dailyHMA);
                        Plot("MA", "FAMA", dailyFAMA);
                        Plot("Daily Price", "Direction", (int) longTermTrend);
                        Plot("LSMA", "Slope", dailyHmaLSMA.Slope);
                    }
                };

                /******** SHORT TERM TRADING ********/
                var consolidator = new QuoteBarConsolidator(TimeSpan.FromMinutes(15));
                var schaffTrendCycle = new SchaffTrendCycle(symbol);
                var stoch = new Stochastic(symbol, 21, 9, 9);
                var ema = new ExponentialMovingAverage(symbol, 100);
                var emaMA = new LeastSquaresMovingAverage(symbol, 5).Of(ema);

                var rollingSchaffTrendCycle = HistoryTracker.Track(schaffTrendCycle);
                var rollingStoch = HistoryTracker.Track(stoch);
                var rollingEMA = HistoryTracker.Track(ema);
                var rollingEmaSlope = HistoryTracker.Track(emaMA.Slope);

                RegisterIndicator(symbol, schaffTrendCycle, consolidator);
                RegisterIndicator(symbol, stoch, consolidator);
                RegisterIndicator(symbol, ema, consolidator);
                SubscriptionManager.AddConsolidator(symbol, consolidator);

                var std = ATR(symbol, 100, MovingAverageType.DoubleExponential, _dataResolution);

                /******** HISTORY ********/
                var history = History<QuoteBar>(symbol, TimeSpan.FromDays(40), _dataResolution);

                foreach (var bar in history)
                {
                    std.Update(bar);
                    consolidatorDaily.Update(bar);
                    consolidator.Update(bar);
                }

                var signal = new SVMBaselineSignal(
                    consolidatorDaily, rollingDailyHMA, rollingDailyHMASlope, rollingDailyFAMA, rollingDailyFAMASlope,
                    consolidator, rollingSchaffTrendCycle, rollingStoch, rollingEMA, rollingEmaSlope,
                    Portfolio[symbol], Securities[symbol], this
                );

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

            /******** CHARTING ********/
            Chart price = new Chart("Daily Price");
            price.AddSeries(new Series("Price", SeriesType.Candle, 0));
            price.AddSeries(new Series("Direction", SeriesType.Bar, 1));
            AddChart(price);

            Chart ma = new Chart("MA");
            ma.AddSeries(new Series("HMA", SeriesType.Line, 0));
            ma.AddSeries(new Series("FAMA", SeriesType.Line, 1));
            AddChart(ma);

            Chart lsma = new Chart("LSMA");
            lsma.AddSeries(new Series("Slope", SeriesType.Line, 0));
            AddChart(lsma);

            Chart plotter = new Chart("Plotter");
            plotter.AddSeries(new Series("Close", SeriesType.Line, 0));
            plotter.AddSeries(new Series("EMA", SeriesType.Line, 0));
            plotter.AddSeries(new Series("Diff", SeriesType.Bar, 1));
            AddChart(plotter);

            Chart indicator = new Chart("Indicator");
            indicator.AddSeries(new Series("STO", SeriesType.Line, 0));
            AddChart(indicator);
        }

        public void PlotSignal(QuoteBar current, ExponentialMovingAverage ema, int prediction, double logLikelihood)
        {
            Plot("Plotter", "Close", current.Close);
            Plot("Plotter", "EMA", ema);
            Plot("Plotter", "Diff", current.Close - ema);
            //Plot("Plotter", "Prediction", prediction);
            //Plot("Plotter", "Probability", logLikelihood);
        }

        public void OnData(QuoteBars data)
        {
            try
            {
                foreach (var symbol in Symbols.Where(s => data.ContainsKey(s)))
                {
                    Plot("Daily Price", "Price", data[symbol].Close);
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

            /*if (orderEvent.Status == OrderStatus.Filled)
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
            }*/
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