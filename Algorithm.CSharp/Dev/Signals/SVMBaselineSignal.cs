using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using System.Linq;
using System;
using Accord.Math;
using QuantConnect.Data.Consolidators;
using System.Collections.Generic;
using QuantConnect.Algorithm.CSharp.Dev.Common;
using System.IO;

namespace QuantConnect.Algorithm.CSharp
{
    public class SVMBaselineSignal : ISignal
    {
        private QuoteBarConsolidator _dailyConsolidator;
        private RollingWindow<IndicatorDataPoint> _rollingDailyHMA;
        private RollingWindow<IndicatorDataPoint> _rollingDailyHMASlope;
        private RollingWindow<IndicatorDataPoint> _rollingDailyFAMA;
        private RollingWindow<IndicatorDataPoint> _rollingDailyFAMASlope;

        private QuoteBarConsolidator _shortTermConsolidator;
        private RollingWindow<IndicatorDataPoint> _rollingSchaffTrendCycle;
        private RollingWindow<IndicatorDataPoint> _rollingStoch;
        private RollingWindow<IndicatorDataPoint> _rollingEMA;
        private RollingWindow<IndicatorDataPoint> _rollingEmaSlope;

        private SecurityHolding _securityHolding;
        private Security _security;
        private decimal _minimumPriceVariation;

        private SVMBaselineStrategy _qcAlgorithm;

        private SignalType _pendingSignal;
        private bool _waitingForScan;

        private QuoteBar _previousBar;
        private QuoteBar _triggerBar;
        private Trend.Direction _emaEntry;

        private readonly bool _debug = false;
        private readonly bool _store = false;

        public SVMBaselineSignal(
            QuoteBarConsolidator dailyConsolidator,
            RollingWindow<IndicatorDataPoint> rollingDailyHMA,
            RollingWindow<IndicatorDataPoint> rollingDailyHMASlope,
            RollingWindow<IndicatorDataPoint> rollingDailyFAMA,
            RollingWindow<IndicatorDataPoint> rollingDailyFAMASlope,
            QuoteBarConsolidator shortTermConsolidator,
            RollingWindow<IndicatorDataPoint> rollingSchaffTrendCycle,
            RollingWindow<IndicatorDataPoint> rollingStoch,
            RollingWindow<IndicatorDataPoint> rollingEMA,
            RollingWindow<IndicatorDataPoint> rollingEmaSlope,
            SecurityHolding securityHolding,
            Security security,
            SVMBaselineStrategy qcAlgorithm)
        {
            _dailyConsolidator = dailyConsolidator;
            _rollingDailyHMA = rollingDailyHMA;
            _rollingDailyHMASlope = rollingDailyHMASlope;
            _rollingDailyFAMA = rollingDailyFAMA;
            _rollingDailyFAMASlope = rollingDailyFAMASlope;

            _shortTermConsolidator = shortTermConsolidator;
            _rollingSchaffTrendCycle = rollingSchaffTrendCycle;
            _rollingStoch = rollingStoch;
            _rollingEMA = rollingEMA;
            _rollingEmaSlope = rollingEmaSlope;

            _securityHolding = securityHolding;
            _security = security;
            _minimumPriceVariation = (1m / _security.SymbolProperties.MinimumPriceVariation) / 10m;

            _qcAlgorithm = qcAlgorithm;

            if (_store)
            {
                var path = @"C:\Users\M\Desktop\EURUSD.csv";
                var header = new string[] { "Time", "End Time", "Open", "High", "Low", "Close", "STC", "STC Previous", "EMA", "Slope", "Diff", "Prediction", "Signal" };
                File.WriteAllText(path, string.Join(";", header) + Environment.NewLine);

                var ohlcHeader = new string[] { "Time", "Open", "High", "Low", "Close" };
                Storage.CreateFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_1D.csv", ohlcHeader);
                Storage.CreateFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_15M.csv", ohlcHeader);
                //Storage.CreateFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_1M.csv", ohlcHeader);
            }

            dailyConsolidator.DataConsolidated += (sender, args) =>
            {
                if (_store)
                {
                    var ohlcLine = new object[] { Storage.ToUTCTimestamp(args.Time), args.Open, args.High, args.Low, args.Close };
                    Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_1D.csv", ohlcLine);
                }
            };

            shortTermConsolidator.DataConsolidated += (sender, args) =>
            {
                if (_previousBar == null)
                {
                    _previousBar = args;
                    return;
                }

                var dailyQuote = (QuoteBar)dailyConsolidator.Consolidated;
                var longTermTrend = dailyQuote.Close > _rollingDailyHMA[0]
                                    ? Trend.Direction.Up
                                    : dailyQuote.Close < _rollingDailyHMA[0]
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;

                var ema = _rollingEMA[0];
                var previousEMA = _rollingEMA[1];
                var emaSlope = _rollingEmaSlope[0];
                var previousEMASlope = _rollingEmaSlope[1];
                var stc = _rollingSchaffTrendCycle[0];
                var previousSTC = _rollingSchaffTrendCycle[1];

                var emaCondition = args.Close - ema > (2m / _minimumPriceVariation) && args.Close - ema < (10m / _minimumPriceVariation) && _rollingEMA.Rising(5)
                                    ? Trend.Direction.Up
                                    : ema - args.Close > (25m / _minimumPriceVariation) && _rollingEMA.Falling()
                                    ? Trend.Direction.MeanRevertingUp
                                    : ema - args.Close > (2m / _minimumPriceVariation) && ema - args.Close < (10m / _minimumPriceVariation) && _rollingEMA.Falling(5)
                                    ? Trend.Direction.Down
                                    : args.Close - ema > (25m / _minimumPriceVariation) && _rollingEMA.Rising()
                                    ? Trend.Direction.MeanRevertingDown
                                    : Trend.Direction.Flat;
                var schaffTrendCycleCondition = _rollingSchaffTrendCycle.InRangeExclusive(50m, 75m) && _rollingSchaffTrendCycle.CrossAbove(1m, 1) && _rollingSchaffTrendCycle.Rising()
                                    ? Trend.Direction.Up
                                    : _rollingSchaffTrendCycle.InRangeExclusive(25m, 50m) && _rollingSchaffTrendCycle.CrossBelow(99m, 1) && _rollingSchaffTrendCycle.Falling()
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;

                var shortTermTrend = emaCondition > 0 && schaffTrendCycleCondition == Trend.Direction.Up
                                    ? Trend.Direction.Up
                                    : emaCondition < 0 && schaffTrendCycleCondition == Trend.Direction.Down
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;

                var longCondition = shortTermTrend == Trend.Direction.Up;
                                    //&& longTermTrend != Trend.Direction.Down
                                    //&& args.Close > _previousBar.High
                                    //&& (Math.Abs(args.Close - _previousBar.Close) * _minimumPriceVariation < 10m);
                var shortCondition = shortTermTrend == Trend.Direction.Down;
                                    //&& longTermTrend != Trend.Direction.Up
                                    //&& args.Close < _previousBar.Low
                                    //&& (Math.Abs(args.Close - _previousBar.Close) * _minimumPriceVariation < 10m);

                var longExit = Signal == SignalType.Long &&
                                (/*_rollingSchaffTrendCycle.CrossBelow(90m, 1)
                                    || */_emaEntry == Trend.Direction.MeanRevertingUp && (ema - args.Close) * _minimumPriceVariation < 5m
                                    || _emaEntry == Trend.Direction.Up && (args.Close - ema) * _minimumPriceVariation > 35m
                                    //|| _emaEntry == Trend.Direction.Up && (args.Close - _triggerBar.Close) * _minimumPriceVariation < -5m
                                    || schaffTrendCycleCondition == Trend.Direction.Down);
                var shortExit = Signal == SignalType.Short &&
                                (/*_rollingSchaffTrendCycle.CrossAbove(10m, 1)
                                    || */_emaEntry == Trend.Direction.MeanRevertingDown && (args.Close - ema) * _minimumPriceVariation < 5m
                                    || _emaEntry == Trend.Direction.Down && (ema - args.Close) * _minimumPriceVariation > 35m
                                    //|| _emaEntry == Trend.Direction.Down && (args.Close - _triggerBar.Close) * _minimumPriceVariation > 5m
                                    || schaffTrendCycleCondition == Trend.Direction.Up);

                if (!_securityHolding.Invested && longCondition)
                {
                    Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;
                    _triggerBar = args;
                    _emaEntry = emaCondition;
                }
                else if (!_securityHolding.Invested && shortCondition)
                {
                    Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;
                    _triggerBar = args;
                    _emaEntry = emaCondition;
                }
                else if ((_securityHolding.Invested && longExit) || (_securityHolding.Invested && shortExit))
                {
                    Signal = (Signal == SignalType.Long && shortCondition) || (Signal == SignalType.Short && longCondition) ? SignalType.Reverse : SignalType.Exit;
                    _pendingSignal = Signal == SignalType.Reverse && shortCondition ? SignalType.Short : Signal == SignalType.Reverse && longCondition ? SignalType.Long : SignalType.NoSignal;
                    _waitingForScan = true;
                    _triggerBar = args;
                }
                else if (!_securityHolding.Invested)
                {
                    Signal = SignalType.NoSignal;
                    _triggerBar = null;
                }

                _previousBar = args;

                _qcAlgorithm.PlotSignal(args, _rollingEMA[0], _rollingEmaSlope[0], _rollingSchaffTrendCycle[0], _rollingStoch[0], (int)shortTermTrend, (int) Signal);

                if (_store)
                {
                    var line = new object[] { Storage.ToUTCTimestamp(args.Time), Storage.ToUTCTimestamp(args.EndTime), args.Open, args.High, args.Low, args.Close, _rollingSchaffTrendCycle[0].Value, _rollingSchaffTrendCycle[1].Value,
                                            _rollingEMA[0].Value, _rollingEmaSlope[0].Value, (args.Close - _rollingEMA[0]) * _minimumPriceVariation, (int)shortTermTrend, (int) Signal };
                    Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}.csv", line);

                    var ohlcLine = new object[] { Storage.ToUTCTimestamp(args.Time), args.Open, args.High, args.Low, args.Close };
                    Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_15M.csv", ohlcLine);
                }
            };
        }

        public void Scan(QuoteBar data)
        {
            if (_store)
            {
                //var ohlcLine = new object[] { Storage.ToUTCTimestamp(data.Time), data.Open, data.High, data.Low, data.Close };
                //Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_1M.csv", ohlcLine);
            }

            if (Signal == SignalType.Reverse && !_waitingForScan)
            {
                Signal = _pendingSignal;
            }

            _waitingForScan = false;
        }

        public SignalType Signal { get; private set; }
    }
}