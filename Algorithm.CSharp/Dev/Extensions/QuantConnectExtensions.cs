using QuantConnect.Statistics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuantConnect
{
    public static class QuantConnectExtensions
    {
        public static Dictionary<string, string> GetSummary(this TradeStatistics source)
        {
            return new Dictionary<string, string>
            {
                { "Average End Trade Drawdown", source.AverageEndTradeDrawdown.ToString(CultureInfo.InvariantCulture) },
                { "Average Losing Trade Duration", source.AverageLosingTradeDuration.ToString() },
                { "Average Loss", source.AverageLoss.ToString(CultureInfo.InvariantCulture) },
                { "Average MAE", source.AverageMAE.ToString(CultureInfo.InvariantCulture) },
                { "Average MFE", source.AverageMFE.ToString(CultureInfo.InvariantCulture) },
                { "Average Profit", source.AverageProfit.ToString(CultureInfo.InvariantCulture) },
                { "Average Profit-Loss", source.AverageProfitLoss.ToString(CultureInfo.InvariantCulture) },
                { "Average Trade Duration", source.AverageTradeDuration.ToString() },
                { "Average Winning Trade Duration", source.AverageWinningTradeDuration.ToString() },
                { "Largest Loss", source.LargestLoss.ToString(CultureInfo.InvariantCulture) },
                { "Largest MAE", source.LargestMAE.ToString(CultureInfo.InvariantCulture) },
                { "Largest MFE", source.LargestMFE.ToString(CultureInfo.InvariantCulture) },
                { "Largest Profit", source.LargestProfit.ToString(CultureInfo.InvariantCulture) },
                { "Loss Rate", source.LossRate.ToString(CultureInfo.InvariantCulture) },
                { "Max Consecutive Losing Trades", source.MaxConsecutiveLosingTrades.ToString(CultureInfo.InvariantCulture) },
                { "Max Consecutive Winning Trades", source.MaxConsecutiveWinningTrades.ToString(CultureInfo.InvariantCulture) },
                { "Maximum Closed Trade Drawdown", source.MaximumClosedTradeDrawdown.ToString(CultureInfo.InvariantCulture) },
                { "Maximum Drawdown Duration", source.MaximumDrawdownDuration.ToString() },
                { "Maximum End Trade Drawdown", source.MaximumEndTradeDrawdown.ToString(CultureInfo.InvariantCulture) },
                { "Maximum Intra Trade Drawdown", source.MaximumIntraTradeDrawdown.ToString(CultureInfo.InvariantCulture) },
                { "Number Of Losing Trades", source.NumberOfLosingTrades.ToString(CultureInfo.InvariantCulture) },
                { "Number Of Winning Trades", source.NumberOfWinningTrades.ToString(CultureInfo.InvariantCulture) },
                { "Profit Factor", source.ProfitFactor.ToString(CultureInfo.InvariantCulture) },
                { "Profit-Loss Downside Deviation", source.ProfitLossDownsideDeviation.ToString(CultureInfo.InvariantCulture) },
                { "Profit-Loss Ratio", source.ProfitLossRatio.ToString(CultureInfo.InvariantCulture) },
                { "Profit-Loss Standard Deviation", source.ProfitLossStandardDeviation.ToString(CultureInfo.InvariantCulture) },
                { "Profit To Max Drawdown Ratio", source.ProfitToMaxDrawdownRatio.ToString(CultureInfo.InvariantCulture) },
                { "Total Loss", source.TotalLoss.ToString(CultureInfo.InvariantCulture) },
                { "Total Profit", source.TotalProfit.ToString(CultureInfo.InvariantCulture) },
                { "Total Profit-Loss", source.TotalProfitLoss.ToString(CultureInfo.InvariantCulture) },
                { "Win Rate", source.WinRate.ToString(CultureInfo.InvariantCulture) },
            };
        }
    }
}
