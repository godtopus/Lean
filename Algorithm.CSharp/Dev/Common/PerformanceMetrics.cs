using Accord.Statistics;
using QuantConnect.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp.Dev.Common
{
    class PerformanceMetrics
    {
        // See http://qusma.com/2013/01/22/doing-the-jaffray-woodriff-thing/
        public static decimal TRASYCODRAVOPFACOM(List<Trade> trades, TimeSpan backTestPeriod)
        {
            var tradeStatistics = new TradeStatistics(trades);
            var equityChangePerDay = EquityChangePerDay(trades).Select((ec) => (double)ec.Value).ToArray();
            var equityCurve = equityChangePerDay.Rollup(0d, (acc, val) => acc + val);
            var expectedCurve = Enumerable.Repeat(tradeStatistics.AverageProfitLoss, equityCurve.Count()).Rollup(0m, (acc, val) => acc + val);

            var rSquared = RSquared(trades);
            var dailyReturnsStdDev = Measures.StandardDeviation(equityChangePerDay);
            var dailyReturnsSkewness = Measures.Skewness(equityChangePerDay);
            var cagr = CompoundAnnualGrowthRate(trades, backTestPeriod);

            var consistency = (1m - 2m * (1m - rSquared));
            var drawdown = (cagr / tradeStatistics.MaximumClosedTradeDrawdown) - 1;
            var returnAsymmetry = (decimal)Math.Min(1, dailyReturnsSkewness) / 10m;
            var volatility = (cagr - 0.05m) / (decimal)(dailyReturnsStdDev * Math.Sqrt(252));
            var profitFactor = ((tradeStatistics.ProfitLossRatio - 1m) / 2m);

            return consistency + drawdown + returnAsymmetry + volatility + profitFactor;
        }

        public static decimal CompoundAnnualGrowthRate(List<Trade> trades, TimeSpan backTestPeriod)
        {
            var equityChangePerDay = EquityChangePerDay(trades);
            var equityCurve = equityChangePerDay.Select((ec) => ec.Value).Rollup(0m, (acc, val) => acc + val);
            var years = backTestPeriod.TotalDays / 365d;

            return (decimal)(Math.Pow((double)(equityCurve.Last() / equityCurve.First()), 1d / years) - 1d);
        }

        public static decimal RSquared(List<Trade> trades)
        {
            var tradeStatistics = new TradeStatistics(trades);
            var equityChangePerDay = EquityChangePerDay(trades).Select((ec) => (double)ec.Value);
            var equityCurve = equityChangePerDay.Rollup(0d, (acc, val) => acc + val);
            var expectedCurve = Enumerable.Repeat((double)tradeStatistics.AverageProfitLoss, equityCurve.Count()).Rollup(0d, (acc, val) => acc + val);

            var sumOfSquares = expectedCurve.Sum((e) => Math.Pow(e - 1, 2));
            var residualSumOfSquares = equityCurve.Zip(expectedCurve, (a, b) => (a - b)).Sum((r) => Math.Pow(r, 2));

            return (decimal)(1 - (residualSumOfSquares / sumOfSquares));
        }

        public static decimal BlissFunction(List<Trade> trades)
        {
            var tradeStatistics = new TradeStatistics(trades);
            return tradeStatistics.SharpeRatio + 2 * (tradeStatistics.ProfitLossRatio - 1) + 50 * (tradeStatistics.AverageProfit / Math.Abs(tradeStatistics.AverageLoss));

        }

        public static decimal LakeRatio(List<Trade> trades)
        {
            var equityChangePerDay = EquityChangePerDay(trades);
            var equityCurve = equityChangePerDay.Select((ec) => ec.Value).Rollup(0m, (acc, val) => acc + val);

            var mountainsAndLakes = equityCurve.Select((ec, index) =>
            {
                var max = index == 0 ? (ec > 0m ? ec : 0m) : equityCurve.Take(index + 1).Max();
                var min = index == 0 ? (ec < 0m ? ec : 0m) : equityCurve.Take(index + 1).Min();

                var mountain = ec >= max ? ec : max - Math.Abs(max - ec);
                var lake = ec >= max ? 0m : Math.Abs(max - ec);

                return Tuple.Create(mountain, lake);
            });

            return mountainsAndLakes.Select((ml) => ml.Item2).Sum() / mountainsAndLakes.Select((ml) => ml.Item1).Sum();
        }

        public static SortedDictionary<DateTime, decimal> EquityChangePerDay(List<Trade> trades)
        {
            var sortedEquity = new SortedDictionary<DateTime, decimal>();
            trades.ForEach((t) =>
            {
                var boxedTime = t.ExitTime.Date;
                decimal value = 0m;
                var exists = sortedEquity.TryGetValue(boxedTime, out value);
                sortedEquity[boxedTime] = value + t.ProfitLoss;
            });

            return sortedEquity;
        }
    }
}
