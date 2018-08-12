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
using QuantConnect.Data;

namespace QuantConnect.Algorithm.CSharp
{
    public class SVMBaselineSignal : ISignal
    {
        private QuoteBarConsolidator _dailyConsolidator;
        private ExponentialMovingAverage _dailyALMA;
        private RollingWindow<IndicatorDataPoint> _rollingDailyAlma;

        private QuoteBarConsolidator _shortTermConsolidator;

        private RollingWindow<QuoteBar> _rollingConsolidator;

        private SchaffTrendCycle _schaffTrendCycle;
        private RollingWindow<IndicatorDataPoint> _rollingSchaffTrendCycle;

        private AverageTrueRange _atr;
        private AverageDirectionalIndex _adx;

        private ArnaudLegouxMovingAverage _alma5;
        private ArnaudLegouxMovingAverage _alma8;
        private ArnaudLegouxMovingAverage _alma13;
        private ArnaudLegouxMovingAverage _alma21;
        private ArnaudLegouxMovingAverage _alma34;
        private ArnaudLegouxMovingAverage _alma55;
        private ArnaudLegouxMovingAverage _alma89;
        private ArnaudLegouxMovingAverage _alma144;

        private RollingWindow<IndicatorDataPoint> _rollingAlma5;
        private RollingWindow<IndicatorDataPoint> _rollingAlma8;
        private RollingWindow<IndicatorDataPoint> _rollingAlma13;
        private RollingWindow<IndicatorDataPoint> _rollingAlma21;
        private RollingWindow<IndicatorDataPoint> _rollingAlma34;
        private RollingWindow<IndicatorDataPoint> _rollingAlma55;
        private RollingWindow<IndicatorDataPoint> _rollingAlma89;
        private RollingWindow<IndicatorDataPoint> _rollingAlma144;

        private List<RollingWindow<IndicatorDataPoint>> _windows;
        private IEnumerable<RollingWindow<IndicatorDataPoint>[]> _windowCombinations;

        private MovingAverageCross _maCross;

        private SecurityHolding _securityHolding;
        private Security _security;
        private decimal _minimumPriceVariation;

        private SVMBaselineStrategy _qcAlgorithm;

        private SignalType _pendingSignal;
        private bool _waitingForScan;

        private QuoteBar _previousBar;
        private QuoteBar _triggerBar;
        private IEnumerable<IndicatorDataPoint> _maEntry;
        private TradeType.Direction _lastTradeType = TradeType.Direction.Flat;

        private readonly bool _debug = false;
        private readonly bool _store = false;

        public SVMBaselineSignal(
            QuoteBarConsolidator dailyConsolidator,
            ExponentialMovingAverage dailyALMA,
            QuoteBarConsolidator shortTermConsolidator,
            ArnaudLegouxMovingAverage alma5,
            ArnaudLegouxMovingAverage alma8,
            ArnaudLegouxMovingAverage alma13,
            ArnaudLegouxMovingAverage alma21,
            ArnaudLegouxMovingAverage alma34,
            ArnaudLegouxMovingAverage alma55,
            ArnaudLegouxMovingAverage alma89,
            ArnaudLegouxMovingAverage alma144,
            SchaffTrendCycle schaffTrendCycle,
            AverageTrueRange atr,
            AverageDirectionalIndex adx,
            SecurityHolding securityHolding,
            Security security,
            SVMBaselineStrategy qcAlgorithm)
        {
            _dailyConsolidator = dailyConsolidator;
            _dailyALMA = dailyALMA;
            _rollingDailyAlma = HistoryTracker.Track(_dailyALMA);

            _shortTermConsolidator = shortTermConsolidator;
            _rollingConsolidator = HistoryTracker.Track(_shortTermConsolidator);

            _schaffTrendCycle = schaffTrendCycle;
            _rollingSchaffTrendCycle = HistoryTracker.Track(_schaffTrendCycle);

            _atr = atr;
            _adx = adx;

            _alma5 = alma5;
            _alma8 = alma8;
            _alma13 = alma13;
            _alma21 = alma21;
            _alma34 = alma34;
            _alma55 = alma55;
            _alma89 = alma89;
            _alma144 = alma144;

            _rollingAlma5 = HistoryTracker.Track(_alma5, 100);
            _rollingAlma8 = HistoryTracker.Track(_alma8, 100);
            _rollingAlma13 = HistoryTracker.Track(_alma13, 100);
            _rollingAlma21 = HistoryTracker.Track(_alma21, 100);
            _rollingAlma34 = HistoryTracker.Track(_alma34, 100);
            _rollingAlma55 = HistoryTracker.Track(_alma55, 100);
            _rollingAlma89 = HistoryTracker.Track(_alma89, 100);
            _rollingAlma144 = HistoryTracker.Track(_alma144, 100);

            _windows = new List<RollingWindow<IndicatorDataPoint>> { _rollingAlma5, _rollingAlma8, _rollingAlma13, _rollingAlma21, _rollingAlma34, _rollingAlma55, _rollingAlma89, _rollingAlma144 };
            _windowCombinations = _windows.Combinations(3);

            _maCross = new MovingAverageCross(_rollingAlma8, _rollingAlma21, _rollingAlma144);

            _securityHolding = securityHolding;
            _security = security;
            _minimumPriceVariation = 10000m;

            _qcAlgorithm = qcAlgorithm;

            var eader = new string[] { "Time", "Signal" };
            Storage.CreateFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_ALMA_Signal.csv", eader);

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

            shortTermConsolidator.DataConsolidated += (sender, args) =>
            {
                if (_previousBar == null)
                {
                    _previousBar = args;
                    return;
                }

                if (_dailyConsolidator.Consolidated == null)
                {
                    return;
                }

                /*var maSignals = _windowCombinations.Select((wc) =>
                {
                    var buySignal = wc[0].DoubleCrossAbove(wc[1], wc[2], 5, 0.05m / 10000m) && wc[0].Rising(3, 0.05m / 10000m) && wc[1].Rising(3, 0.05m / 10000m) && wc[2].Rising(3, 0.05m / 10000m);
                    var sellSignal = wc[0].DoubleCrossBelow(wc[1], wc[2], 5, 0.05m / 10000m) && wc[0].Falling(3, 0.05m / 10000m) && wc[1].Falling(3, 0.05m / 10000m) && wc[2].Falling(3, 0.05m / 10000m);
                    return buySignal ? SignalType.Long : sellSignal ? SignalType.Short : SignalType.NoSignal;
                });

                var longCondition = maSignals.Where((s) => s == SignalType.Long).Count() > 9 && args.Close > _alma144 + (10m / 10000m) && args.Close > _alma21 && args.Close > _alma8;
                var shortCondition = maSignals.Where((s) => s == SignalType.Short).Count() > 9 && args.Close < _alma144 - (10m / 10000m) && args.Close < _alma21 && args.Close < _alma8;

                var longExit = Signal == SignalType.Long
                                && (maSignals.Where((s) => s == SignalType.Short).Count() > 4);
                var shortExit = Signal == SignalType.Short
                                && (maSignals.Where((s) => s == SignalType.Long).Count() > 4);*/
                var dailyQuote = (QuoteBar)_dailyConsolidator.Consolidated;

                // If mean reverting and no slow cross within ?, close

                var longMeanReversion = _maCross.CrossAbove(6, 0.7m / 10000m)
                                        && _rollingAlma8.InRangeExclusive(_alma21, _alma144)
                                        && _adx >= 23
                                        //&& _atr > 7m / 10000m
                                        //&& _rollingAlma144.Diff(_rollingAlma21, 10).Average() * 10000m >= 15m
                                        && _rollingAlma8.Rising(1, 1m / 10000m)
                                        && _rollingAlma21.Rising(1, 0.1m / 10000m)
                                        ? TradeType.Direction.MeanRevertingUp
                                        : TradeType.Direction.Flat;

                var longTrend = _maCross.DoubleCrossAbove(6, 0.4m / 10000m)
                                //&& _adx < 20
                                //&& _rollingAlma144.Diff(_rollingAlma21, 10).Sum() * 10000m > 5m
                                && _rollingAlma8.Rising(2, 1m / 10000m)
                                && _rollingAlma21.Rising(2, 0.1m / 10000m)
                                && _rollingAlma55.Rising(2)
                                && args.Close > _alma144
                                ? TradeType.Direction.TrendUp
                                : TradeType.Direction.Flat;

                var longCondition = args.Close > _dailyALMA && (longMeanReversion == TradeType.Direction.MeanRevertingUp || longTrend == TradeType.Direction.TrendUp);

                var shortMeanReversion = _maCross.CrossBelow(6, 0.6m / 10000m)
                                            && _rollingAlma8.InRangeExclusive(_alma144, _alma21)
                                            && _adx >= 23
                                            //&& _atr > 7m / 10000m
                                            //&& _rollingAlma21.Diff(_rollingAlma144, 10).Average() * 10000m >= 15m
                                            && _rollingAlma8.Falling(1, 1m / 10000m)
                                            && _rollingAlma21.Falling(1, 0.1m / 10000m)
                                            ? TradeType.Direction.MeanRevertingDown
                                            : TradeType.Direction.Flat;

                var shortTrend = _maCross.DoubleCrossBelow(6, 0.4m / 10000m)
                                    //&& _adx < 20
                                    //&& _rollingAlma21.Diff(_rollingAlma144, 10).Sum() * 10000m > 5m
                                    && _rollingAlma8.Falling(2, 1m / 10000m)
                                    && _rollingAlma21.Falling(2, 0.1m / 10000m)
                                    && _rollingAlma55.Falling(2)
                                    && args.Close < _alma144
                                    ? TradeType.Direction.TrendDown
                                    : TradeType.Direction.Flat;

                var shortCondition = args.Close < _dailyALMA && (shortMeanReversion == TradeType.Direction.MeanRevertingDown || shortTrend == TradeType.Direction.TrendDown);

                var longExit = Signal == SignalType.Long &&
                    ((shortTrend == TradeType.Direction.TrendDown
                        || _rollingAlma34.CrossBelow(_rollingAlma144, 7, 0.1m / 10000m))
                    || (_lastTradeType == TradeType.Direction.MeanRevertingUp
                        && args.Time.Subtract(_triggerBar.Time).CompareTo(TimeSpan.FromMinutes(15 * 6)) > 0
                        && _maCross.CrossBelow(6, 0.3m / 10000m)
                        && _alma144 > _alma21));
                //var longExit = Signal == SignalType.Long && (_rollingAlma21.CrossBelow(_rollingAlma144, 7, 0.1m / 10000m)); NZDUSD
                    //_rollingConsolidator.Diff(_rollingAlma144, Field.Close, 8).All((d) => d < 5m)
                                /*&& (((_rollingAlma8.CrossBelow(_rollingAlma21, 5, 0.1m / 10000m) && _rollingAlma21.Falling())
                                    || (_rollingConsolidator.CrossBelow(_alma144.Current.Value) && _rollingAlma21.Falling())));*/
                var shortExit = Signal == SignalType.Short &&
                    ((longTrend == TradeType.Direction.TrendUp
                        || _rollingAlma34.CrossAbove(_rollingAlma144, 7, 0.1m / 10000m))
                    || (_lastTradeType == TradeType.Direction.MeanRevertingDown
                        && args.Time.Subtract(_triggerBar.Time).CompareTo(TimeSpan.FromMinutes(15 * 6)) > 0
                        && _maCross.CrossAbove(6, 0.3m / 10000m)
                        && _alma144 < _alma21));
                //var shortExit = Signal == SignalType.Short && ( _rollingAlma21.CrossAbove(_rollingAlma144, 7, 0.1m / 10000m)); NZDUSD
                //_rollingConsolidator.Diff(_rollingAlma144, Field.Close, 8).All((d) => d > 5m)
                /*&& (((_rollingAlma8.CrossAbove(_rollingAlma21, 5, 0.1m / 10000m) && _rollingAlma21.Rising())
                    || (_rollingConsolidator.CrossAbove(_alma144.Current.Value) && _rollingAlma21.Rising())));*/

                if (!_securityHolding.Invested && longCondition && !shortCondition && (_triggerBar == null || args.Time.Subtract(_triggerBar.Time).CompareTo(TimeSpan.FromMinutes(15 * 6)) > 0))
                {
                    Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;
                    _triggerBar = args;
                    _maEntry = _windows.Select((w) => w[0]);
                    _lastTradeType = longTrend != TradeType.Direction.Flat ? longTrend : longMeanReversion;
                }
                else if (!_securityHolding.Invested && shortCondition && !longCondition && (_triggerBar == null || args.Time.Subtract(_triggerBar.Time).CompareTo(TimeSpan.FromMinutes(15 * 6)) > 0))
                {
                    Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;
                    _triggerBar = args;
                    _maEntry = _windows.Select((w) => w[0]);
                    _lastTradeType = shortTrend != TradeType.Direction.Flat ? shortTrend : shortMeanReversion;
                }
                else if ((_securityHolding.Invested && longExit) || (_securityHolding.Invested && shortExit))
                {
                    Signal = (Signal == SignalType.Long && shortCondition) || (Signal == SignalType.Short && longCondition) ? SignalType.Reverse : SignalType.Exit;
                    _pendingSignal = Signal == SignalType.Reverse && shortCondition ? SignalType.Short : Signal == SignalType.Reverse && longCondition ? SignalType.Long : SignalType.NoSignal;
                    _waitingForScan = true;
                    _triggerBar = args;
                    _maEntry = _windows.Select((w) => w[0]);
                    _lastTradeType = _pendingSignal == SignalType.Long
                                        ? longTrend != TradeType.Direction.Flat ? longTrend : longMeanReversion
                                        : shortTrend != TradeType.Direction.Flat ? shortTrend : shortMeanReversion;
                }
                else if (!_securityHolding.Invested)
                {
                    Signal = SignalType.NoSignal;
                    //_triggerBar = null;
                    _maEntry = null;
                }

                _previousBar = args;

                //_qcAlgorithm.PlotSignal(args, _rollingEMA[0], _rollingEmaSlope[0], _rollingSchaffTrendCycle[0], _rollingStoch[0], (int)shortTermTrend, (int) Signal);

                if (_store)
                {
                    /*var line = new object[] { Storage.ToUTCTimestamp(args.Time), Storage.ToUTCTimestamp(args.EndTime), args.Open, args.High, args.Low, args.Close, _rollingSchaffTrendCycle[0].Value, _rollingSchaffTrendCycle[1].Value,
                                            _rollingEMA[0].Value, _rollingEmaSlope[0].Value, (args.Close - _rollingEMA[0]) * _minimumPriceVariation, (int)shortTermTrend, (int) Signal };
                    Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}.csv", line);

                    var ohlcLine = new object[] { Storage.ToUTCTimestamp(args.Time), args.Open, args.High, args.Low, args.Close };
                    Storage.AppendToFile($"C:\\Users\\M\\Desktop\\{_security.Symbol.Value}_OHLC_15M.csv", ohlcLine);*/
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

            var longExit = Signal == SignalType.Long && (_triggerBar.Close - data.Close) * _minimumPriceVariation < -20;
            var shortExit = Signal == SignalType.Short && (data.Close - _triggerBar.Close) * _minimumPriceVariation < -20;

            if (longExit || shortExit)
            {
                //Signal = SignalType.Exit;
            }

            _waitingForScan = false;
        }

        public SignalType Signal { get; private set; }
    }
}