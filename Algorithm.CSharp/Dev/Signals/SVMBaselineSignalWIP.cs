using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using System.Linq;
using System;
using Accord.Math;
using QuantConnect.Data.Consolidators;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp
{
    public class SVMBaselineSignalWIP : ISignal
    {
        private QuoteBarConsolidator _consolidator;
        private Stochastic _stoch;
        private ExponentialMovingAverage _stochMA;
        private LeastSquaresMovingAverage _stochEmaLSMA;
        private ExponentialMovingAverage _ema;
        private LeastSquaresMovingAverage _emaMA;

        private LeastSquaresMovingAverage _dailyEmaLSMA;

        private ExponentialMovingAverage _shortTermMA;

        private RollingWindow<IndicatorDataPoint> _rollingStochMA;
        private RollingWindow<IndicatorDataPoint> _rollingStochEmaSlope;
        private RollingWindow<IndicatorDataPoint> _rollingEmaSlope;
        private RollingWindow<IndicatorDataPoint> _rollingDailyEmaSlope;

        private SecurityHolding _securityHolding;
        private Security _security;
        private decimal _minimumPriceVariation;

        private SVMBaselineStrategyWIP _qcAlgorithm;

        readonly bool _debug = false;

        private List<string> _shortable => new List<string> { "EURGBP", "USDCAD", "USDCHF" };
        private List<string> _shortOnly => new List<string> { "USDCHF" };

        public SVMBaselineSignalWIP(
            QuoteBarConsolidator consolidator,
            Stochastic stoch,
            ExponentialMovingAverage stochMA,
            RollingWindow<IndicatorDataPoint> rollingStochMA,
            LeastSquaresMovingAverage stochEmaLSMA,
            RollingWindow<IndicatorDataPoint> rollingStochEmaSlope,
            ExponentialMovingAverage ema,
            LeastSquaresMovingAverage emaMA,
            RollingWindow<IndicatorDataPoint> rollingEmaSlope,
            ExponentialMovingAverage shortTermMA,
            LeastSquaresMovingAverage dailyEmaLSMA,
            RollingWindow<IndicatorDataPoint> rollingDailyEmaSlope,
            SecurityHolding securityHolding,
            Security security,
            SVMBaselineStrategyWIP qcAlgorithm)
        {
            _consolidator = consolidator;
            _stoch = stoch;
            _stochMA = stochMA;
            _rollingStochMA = rollingStochMA;
            _stochEmaLSMA = stochEmaLSMA;
            _rollingStochEmaSlope = rollingStochEmaSlope;
            _ema = ema;
            _emaMA = emaMA;
            _rollingEmaSlope = rollingEmaSlope;
            _shortTermMA = shortTermMA;
            _dailyEmaLSMA = dailyEmaLSMA;
            _rollingDailyEmaSlope = rollingDailyEmaSlope;

            _securityHolding = securityHolding;
            _security = security;
            _minimumPriceVariation = (1m / _security.SymbolProperties.MinimumPriceVariation) / 10m;
            _qcAlgorithm = qcAlgorithm;

            _stochMA.Updated += (sender, args) =>
            {
                try
                {
                    var currentQuote = (QuoteBar)_consolidator.Consolidated;

                    var aboveEma = currentQuote.Close - _ema.Current.Value > 4m / _minimumPriceVariation;
                    var belowEma = _ema.Current.Value - currentQuote.Close > 4m / _minimumPriceVariation;

                    var aboveEmaExit = (currentQuote.Close - _ema.Current.Value > 10m / _minimumPriceVariation) || _rollingDailyEmaSlope[0] > 0.0005m;
                    var belowEmaExit = (_ema.Current.Value - currentQuote.Close > 10m / _minimumPriceVariation) || _rollingDailyEmaSlope[0] < -0.0005m;

                    var longCondition = _rollingStochMA[0] > _rollingStochMA[1] &&
                                        _stochMA > 55 &&
                                        aboveEma &&
                                        //_rollingDailyEmaSlope[0] > _rollingDailyEmaSlope[1] &&
                                        _dailyEmaLSMA.Slope > 0 &&
                                        //_rollingStochEmaSlope[0] < 2 &&
                                        //_rollingStochEmaSlope[0] > _rollingStochEmaSlope[1] &&
                                        //_rollingStochMA[0] > 45 &&
                                        _rollingEmaSlope[0] > 0.00001m;
                    var shortCondition = _rollingStochMA[0] < _rollingStochMA[1] &&
                                        _stochMA < 45 &&
                                        belowEma &&
                                        //_rollingDailyEmaSlope[0] < _rollingDailyEmaSlope[1] &&
                                        _dailyEmaLSMA.Slope < 0 &&
                                        //_rollingStochEmaSlope[0] > -2 &&
                                        //_rollingStochEmaSlope[0] < _rollingStochEmaSlope[1] &&
                                        //_rollingStochMA[0] < 55 &&
                                        _rollingEmaSlope[0] < -0.00001m;

                    var prediction = longCondition ? 1 : shortCondition ? -1 : 0;

                    /*var prediction = _rollingStochMA[0] > _rollingStochMA[1] && aboveEma && _stochEmaLSMA.Slope > 0.5 && _rollingEmaSlope[0] > 0
                        ? 1
                        : _rollingStochMA[0] < _rollingStochMA[1] && belowEma && _stochEmaLSMA.Slope < -0.5 && _rollingEmaSlope[0] < 0
                            ? -1
                            : 0;*/
                    var probability = 1d;
                    var logLikelihood = 1d;

                    _qcAlgorithm.PlotSignal((QuoteBar) _consolidator.Consolidated, prediction, logLikelihood);

                    var longExit = Signal == SignalType.Long && belowEmaExit && _rollingEmaSlope[0] < 0;
                    var shortExit = Signal == SignalType.Short && aboveEmaExit && _rollingEmaSlope[0] > 0;
                    /*var longExit = Signal == SignalType.Long && _stochEmaLSMA.Slope < -0.5;
                    var shortExit = Signal == SignalType.Short && _stochEmaLSMA.Slope > 0.5;*/

                    if (!_securityHolding.Invested && prediction == 1)
                    {
                        if (true)//if (!_shortOnly.Contains(_securityHolding.Symbol))
                        {
                            Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;
                        }
                        else
                        {
                            Signal = SignalType.NoSignal;
                        }
                        //Signal = Signal != SignalType.PendingLong ? SignalType.Long : SignalType.Long;
                        //Signal = SignalType.NoSignal;

                        if (_debug)
                        {
                            Console.WriteLine("Long Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Long STO: {0} STO MA: {1}", _stoch.Current.Value, args.Value);
                            Console.WriteLine("Long Time: {0} Price: {1}", _consolidator.Consolidated.Time, _consolidator.Consolidated.Value);
                        }
                    }
                    else if (!_securityHolding.Invested && prediction == -1)
                    {
                        if (true) //if (_shortable.Contains(_securityHolding.Symbol))
                        {
                            Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;
                        }
                        else
                        {
                            Signal = SignalType.NoSignal;
                        }
                        //Signal = Signal != SignalType.PendingShort ? SignalType.Short : SignalType.Short;
                        //Signal = SignalType.NoSignal;

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