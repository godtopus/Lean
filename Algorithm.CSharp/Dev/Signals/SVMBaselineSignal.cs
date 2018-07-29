using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using System.Linq;
using System;
using Accord.Math;
using QuantConnect.Data.Consolidators;
using System.Collections.Generic;
using QuantConnect.Algorithm.CSharp.Dev.Common;

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

        readonly bool _debug = false;

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

            shortTermConsolidator.DataConsolidated += (sender, args) =>
            {
                var dailyQuote = (QuoteBar)dailyConsolidator.Consolidated;
                var longTermTrend = dailyQuote.Close > _rollingDailyHMA[0] && dailyQuote.Close > _rollingDailyFAMA[0] && _rollingDailyHMASlope[0] > 0
                                    ? Trend.Direction.Up
                                    : dailyQuote.Close < _rollingDailyHMA[0] && dailyQuote.Close < _rollingDailyFAMA[0] && _rollingDailyHMASlope[0] < 0
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;

                var emaCondition = ((args.Close - _rollingEMA[0] > (2m / _minimumPriceVariation) && args.Close - _rollingEMA[0] < (10m / _minimumPriceVariation))
                                        || _rollingEMA[0] - args.Close > (25m / _minimumPriceVariation)) && _rollingEmaSlope[0] > _rollingEmaSlope[1]
                                    ? Trend.Direction.Up
                                    : ((_rollingEMA[0] - args.Close > (2m / _minimumPriceVariation) && _rollingEMA[0] - args.Close < (10m / _minimumPriceVariation))
                                        || args.Close - _rollingEMA[0] > (25m / _minimumPriceVariation)) && _rollingEmaSlope[0] < _rollingEmaSlope[1]
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;
                var schaffTrendCycleCondition = _rollingSchaffTrendCycle[0] > 25 && _rollingSchaffTrendCycle[0] < 55 && _rollingSchaffTrendCycle[1] < 5 && _rollingSchaffTrendCycle[0] > _rollingSchaffTrendCycle[1]
                                    ? Trend.Direction.Up
                                    : _rollingSchaffTrendCycle[0] < 75 && _rollingSchaffTrendCycle[0] > 45 && _rollingSchaffTrendCycle[1] > 95 && _rollingSchaffTrendCycle[0] < _rollingSchaffTrendCycle[1]
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;

                var shortTermTrend = emaCondition == Trend.Direction.Up && schaffTrendCycleCondition == Trend.Direction.Up
                                    ? Trend.Direction.Up
                                    : emaCondition == Trend.Direction.Down && schaffTrendCycleCondition == Trend.Direction.Down
                                    ? Trend.Direction.Down
                                    : Trend.Direction.Flat;

                var longCondition = shortTermTrend == Trend.Direction.Up
                                    && (_previousBar == null || Math.Abs(args.Close - _previousBar.Close) * _minimumPriceVariation < 10m);
                var shortCondition = shortTermTrend == Trend.Direction.Down
                                    && (_previousBar == null || Math.Abs(args.Close - _previousBar.Close) * _minimumPriceVariation < 10m);

                var longExit = Signal == SignalType.Long &&
                                ((_rollingSchaffTrendCycle[0] < 90 && _rollingSchaffTrendCycle[1] > 90)
                                    || _rollingSchaffTrendCycle[0] > 99.99m
                                    || schaffTrendCycleCondition == Trend.Direction.Down
                                    || _rollingEmaSlope[0] < _rollingEmaSlope[1] && _rollingSchaffTrendCycle[0] < 10);
                var shortExit = Signal == SignalType.Short &&
                                ((_rollingSchaffTrendCycle[0] > 10 && _rollingSchaffTrendCycle[1] < 10)
                                    || _rollingSchaffTrendCycle[0] < 0.01m
                                    || schaffTrendCycleCondition == Trend.Direction.Up
                                    || (_rollingEmaSlope[0] > _rollingEmaSlope[1] && _rollingSchaffTrendCycle[0] > 90));

                if (!_securityHolding.Invested && longCondition)
                {
                    Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;
                }
                else if (!_securityHolding.Invested && shortCondition)
                {
                    Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;
                }
                else if ((_securityHolding.Invested && longExit) || (_securityHolding.Invested && shortExit))
                {
                    Signal = (Signal == SignalType.Long && shortCondition) || (Signal == SignalType.Short && longCondition) ? SignalType.Reverse : SignalType.Exit;
                    _pendingSignal = Signal == SignalType.Reverse && shortCondition ? SignalType.Short : Signal == SignalType.Reverse && longCondition ? SignalType.Long : SignalType.NoSignal;
                    _waitingForScan = true;
                }
                else if (!_securityHolding.Invested)
                {
                    Signal = SignalType.NoSignal;
                }

                _previousBar = args;

                _qcAlgorithm.PlotSignal(args, _rollingEMA[0], _rollingEmaSlope[0], _rollingSchaffTrendCycle[0], _rollingStoch[0], (int)shortTermTrend, (int) Signal);
            };
        }

        public void Scan(QuoteBar data)
        {
            if (Signal == SignalType.Reverse && !_waitingForScan)
            {
                Signal = _pendingSignal;
            }

            _waitingForScan = false;
        }

        public SignalType Signal { get; private set; }
    }
}