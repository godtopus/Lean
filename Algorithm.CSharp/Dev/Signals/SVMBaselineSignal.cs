﻿using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using System.Linq;
using System;
using Accord.Math;
using QuantConnect.Data.Consolidators;

namespace QuantConnect.Algorithm.CSharp
{
    public class SVMBaselineSignal : ISignal
    {
        private QuoteBarConsolidator _consolidator;
        private Stochastic _stoch;
        private ExponentialMovingAverage _stochMA;
        private LeastSquaresMovingAverage _stochEmaLSMA;
        private ExponentialMovingAverage _ema;
        private LeastSquaresMovingAverage _emaMA;

        private ExponentialMovingAverage _shortTermMA;

        private RollingWindow<IndicatorDataPoint> _rollingStochMA;
        private RollingWindow<IndicatorDataPoint> _rollingEmaSlope;

        private SecurityHolding _securityHolding;

        private SVMBaselineStrategy _qcAlgorithm;

        readonly bool _debug = false;

        public SVMBaselineSignal(
            QuoteBarConsolidator consolidator,
            Stochastic stoch,
            ExponentialMovingAverage stochMA,
            RollingWindow<IndicatorDataPoint> rollingStochMA,
            LeastSquaresMovingAverage stochEmaLSMA,
            ExponentialMovingAverage ema,
            LeastSquaresMovingAverage emaMA,
            RollingWindow<IndicatorDataPoint> rollingEmaSlope,
            ExponentialMovingAverage shortTermMA,
            SecurityHolding securityHolding,
            SVMBaselineStrategy qcAlgorithm)
        {
            _consolidator = consolidator;
            _stoch = stoch;
            _stochMA = stochMA;
            _rollingStochMA = rollingStochMA;
            _stochEmaLSMA = stochEmaLSMA;
            _ema = ema;
            _emaMA = emaMA;
            _rollingEmaSlope = rollingEmaSlope;
            _shortTermMA = shortTermMA;
            _securityHolding = securityHolding;
            _qcAlgorithm = qcAlgorithm;

            _stochMA.Updated += (sender, args) =>
            {
                try
                {
                    var currentQuote = (QuoteBar)_consolidator.Consolidated;

                    var aboveEma = currentQuote.Close - _ema.Current.Value > 3.5m / 10000m;
                    var belowEma = _ema.Current.Value - currentQuote.Close > 3.5m / 10000m;

                    var prediction = _rollingStochMA[0] > _rollingStochMA[1] && aboveEma && _rollingStochMA[0] > 45 && _rollingEmaSlope[0] > 0 && _rollingEmaSlope[0] > _rollingEmaSlope[1]
                        ? 1
                        : _rollingStochMA[0] < _rollingStochMA[1] && belowEma && _rollingStochMA[0] < 55 && _rollingEmaSlope[0].Value < 0 && _rollingEmaSlope[0] < _rollingEmaSlope[1]
                            ? -1
                            : 0;

                    /*var prediction = _rollingStochMA[0] > _rollingStochMA[1] && aboveEma && _stochEmaLSMA.Slope > 0.5 && _rollingEmaSlope[0] > 0
                        ? 1
                        : _rollingStochMA[0] < _rollingStochMA[1] && belowEma && _stochEmaLSMA.Slope < -0.5 && _rollingEmaSlope[0] < 0
                            ? -1
                            : 0;*/
                    var probability = 1d;
                    var logLikelihood = 1d;

                    _qcAlgorithm.PlotSignal((QuoteBar) _consolidator.Consolidated, prediction, logLikelihood);

                    var longExit = Signal == SignalType.Long && belowEma && _rollingStochMA[0] < 35 && _rollingStochMA[0] < _rollingStochMA[1];
                    var shortExit = Signal == SignalType.Short && aboveEma && _rollingStochMA[0] > 65 && _rollingStochMA[0] > _rollingStochMA[1];
                    /*var longExit = Signal == SignalType.Long && _stochEmaLSMA.Slope < -0.5;
                    var shortExit = Signal == SignalType.Short && _stochEmaLSMA.Slope > 0.5;*/

                    if (!_securityHolding.Invested && prediction == 1)
                    {
                        Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;

                        if (_debug)
                        {
                            Console.WriteLine("Long Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Long STO: {0} STO MA: {1}", _stoch.Current.Value, args.Value);
                            Console.WriteLine("Long Time: {0} Price: {1}", _consolidator.Consolidated.Time, _consolidator.Consolidated.Value);
                        }
                    }
                    else if (!_securityHolding.Invested && prediction == -1)
                    {
                        Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;

                        if (_debug)
                        {
                            Console.WriteLine("Short Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Short STO: {0} STO MA: {1}", _stoch.Current.Value, args.Value);
                            Console.WriteLine("Short Time: {0} Price: {1}", _consolidator.Consolidated.Time, _consolidator.Consolidated.Value);
                        }
                    }
                    else if ((_securityHolding.Invested && longExit) || (_securityHolding.Invested && shortExit))
                    {
                        if (_debug)
                        {
                            Console.WriteLine("Exit Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Exit STO: {0} STO MA: {1}", _stoch.Current.Value, args.Value);
                            Console.WriteLine("Exit Time: {0} Price: {1}", _consolidator.Consolidated.Time, _consolidator.Consolidated.Value);
                        }

                        Signal = SignalType.Exit;
                    }
                    else if (!_securityHolding.Invested && (Signal == SignalType.PendingLong || Signal == SignalType.PendingShort))
                    {
                        Signal = SignalType.NoSignal;
                    }
                    else
                    {
                        //Signal = SignalType.NoSignal;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Signal = SignalType.NoSignal;
                }
            };
        }

        public void Scan(QuoteBar data)
        {
            /*if (Signal == SignalType.PendingLong && data.Close > _shortTermMA)
            {
                Signal = SignalType.Long;
            }
            else if (Signal == SignalType.PendingShort && data.Close < _shortTermMA)
            {
                Signal = SignalType.Short;
            }*/
        }

        public SignalType Signal { get; private set; }
    }
}