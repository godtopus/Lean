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
        private QuoteBarConsolidator _shortTermConsolidator;

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

        private SecurityHolding _securityHolding;
        private Security _security;
        private decimal _minimumPriceVariation;

        private SVMBaselineStrategy _qcAlgorithm;

        private SignalType _pendingSignal;
        private bool _waitingForScan;

        private QuoteBar _previousBar;
        private QuoteBar _triggerBar;
        private IEnumerable<IndicatorDataPoint> _maEntry;

        private readonly bool _debug = false;
        private readonly bool _store = false;

        public SVMBaselineSignal(
            QuoteBarConsolidator shortTermConsolidator,
            ArnaudLegouxMovingAverage alma5,
            ArnaudLegouxMovingAverage alma8,
            ArnaudLegouxMovingAverage alma13,
            ArnaudLegouxMovingAverage alma21,
            ArnaudLegouxMovingAverage alma34,
            ArnaudLegouxMovingAverage alma55,
            ArnaudLegouxMovingAverage alma89,
            ArnaudLegouxMovingAverage alma144,
            SecurityHolding securityHolding,
            Security security,
            SVMBaselineStrategy qcAlgorithm)
        {
            _shortTermConsolidator = shortTermConsolidator;

            _alma5 = alma5;
            _alma8 = alma8;
            _alma13 = alma13;
            _alma21 = alma21;
            _alma34 = alma34;
            _alma55 = alma55;
            _alma89 = alma89;
            _alma144 = alma144;

            _rollingAlma5 = HistoryTracker.Track(_alma5);
            _rollingAlma8 = HistoryTracker.Track(_alma8);
            _rollingAlma13 = HistoryTracker.Track(_alma13);
            _rollingAlma21 = HistoryTracker.Track(_alma21);
            _rollingAlma34 = HistoryTracker.Track(_alma34);
            _rollingAlma55 = HistoryTracker.Track(_alma55);
            _rollingAlma89 = HistoryTracker.Track(_alma89);
            _rollingAlma144 = HistoryTracker.Track(_alma144);

            _windows = new List<RollingWindow<IndicatorDataPoint>> { _rollingAlma5, _rollingAlma8, _rollingAlma13, _rollingAlma21, _rollingAlma34, _rollingAlma55, _rollingAlma89, _rollingAlma144 };
            _windowCombinations = _windows.Combinations(3);

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

                var maSignals = _windowCombinations.Select((wc) =>
                {
                    var buySignal = wc[0].DoubleCrossAbove(wc[1], wc[2], 5, 0.05m / 10000m) && wc[0].Rising(3) && wc[1].Rising(3) && wc[2].Rising(3);
                    var sellSignal = wc[0].DoubleCrossBelow(wc[1], wc[2], 5, 0.05m / 10000m) && wc[0].Falling(3) && wc[1].Falling(3) && wc[2].Falling(3);
                    return buySignal ? SignalType.Long : sellSignal ? SignalType.Short : SignalType.NoSignal;
                });

                var longCondition = maSignals.Where((s) => s == SignalType.Long).Count() > 9 && args.Close > _alma144;
                var shortCondition = maSignals.Where((s) => s == SignalType.Short).Count() > 9 && args.Close < _alma144;

                var longExit = Signal == SignalType.Long
                                && (maSignals.Where((s) => s == SignalType.Short).Count() > 4);
                var shortExit = Signal == SignalType.Short
                                && (maSignals.Where((s) => s == SignalType.Long).Count() > 4);

                if (!_securityHolding.Invested && longCondition)
                {
                    Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;
                    _triggerBar = args;
                    _maEntry = _windows.Select((w) => w[0]);
                }
                else if (!_securityHolding.Invested && shortCondition)
                {
                    Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;
                    _triggerBar = args;
                    _maEntry = _windows.Select((w) => w[0]);
                }
                else if ((_securityHolding.Invested && longExit) || (_securityHolding.Invested && shortExit))
                {
                    Signal = (Signal == SignalType.Long && shortCondition) || (Signal == SignalType.Short && longCondition) ? SignalType.Reverse : SignalType.Exit;
                    _pendingSignal = Signal == SignalType.Reverse && shortCondition ? SignalType.Short : Signal == SignalType.Reverse && longCondition ? SignalType.Long : SignalType.NoSignal;
                    _waitingForScan = true;
                    _triggerBar = args;
                    _maEntry = _windows.Select((w) => w[0]);
                }
                else if (!_securityHolding.Invested)
                {
                    Signal = SignalType.NoSignal;
                    _triggerBar = null;
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