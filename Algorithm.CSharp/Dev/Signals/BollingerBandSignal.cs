using System;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class BollingerBandSignal : ISignal
    {
        public BollingerBands _bb;
        private MovingAverageConvergenceDivergence _macd;
        private SecurityHolding _securityHolding;

        private decimal _previousClose;
        private decimal _lastTradePrice;
        private bool _previousShortSignal;
        private bool _previousLongSignal;

        private LeastSquaresMovingAverage _lsmaUpperBand;
        private LeastSquaresMovingAverage _lsmaMiddleBand;
        private LeastSquaresMovingAverage _lsmaLowerBand;

        private TickConsolidator _ticks;

        public BollingerBandSignal(BollingerBands bb,
            LeastSquaresMovingAverage lsmaUpperBand,
            LeastSquaresMovingAverage lsmaMiddleBand,
            LeastSquaresMovingAverage lsmaLowerBand,
            MovingAverageConvergenceDivergence macd,
            TickConsolidator ticks,
            SecurityHolding securityHolding)
        {
            _bb = bb;
            _lsmaUpperBand = lsmaUpperBand;
            _lsmaMiddleBand = lsmaMiddleBand;
            _lsmaLowerBand = lsmaLowerBand;
            _macd = macd;
            _ticks = ticks;
            _securityHolding = securityHolding;
        }

        public void Scan(QuoteBar data)
        {
            var filter = !_securityHolding.Invested;

            bool enterLongSignal, enterShortSignal, exitLongSignal, exitShortSignal;

            enterLongSignal = ((_lsmaUpperBand > _bb.UpperBand
                        && _lsmaMiddleBand > _bb.MiddleBand
                        && _lsmaLowerBand > _bb.LowerBand))
                /*|| (_lsmaUpperBand > _bb.UpperBand
                    && _lsmaMiddleBand > _bb.MiddleBand
                    && _lsmaLowerBand < _bb.LowerBand))*/
                && (_macd - _macd.Signal) / _macd.Fast > 0m
                //&& _ticks.WorkingBar.Price > _bb.MiddleBand;
                && _ticks.WorkingBar.Price > _bb.UpperBand - (_bb.UpperBand - _bb.MiddleBand) / 2m;

            enterShortSignal = ((_lsmaUpperBand < _bb.UpperBand
                        && _lsmaMiddleBand < _bb.MiddleBand
                        && _lsmaLowerBand < _bb.LowerBand))
                /*|| (_lsmaUpperBand > _bb.UpperBand
                    && _lsmaMiddleBand < _bb.MiddleBand
                    && _lsmaLowerBand < _bb.LowerBand))*/
                && (_macd - _macd.Signal) / _macd.Fast < 0m
                //&& _ticks.WorkingBar.Price < _bb.MiddleBand;
                && _ticks.WorkingBar.Price < _bb.LowerBand + (_bb.MiddleBand - _bb.LowerBand) / 2m;

            exitLongSignal = _ticks.WorkingBar.Price < _bb.LowerBand + (_bb.MiddleBand - _bb.LowerBand) / 2m
                || Math.Abs(data.Price - _lastTradePrice) > 20m / 20000m;
            exitShortSignal = _ticks.WorkingBar.Price > _bb.UpperBand - (_bb.UpperBand - _bb.MiddleBand) / 2m
                || Math.Abs(_lastTradePrice - data.Price) > 20m / 20000m;

            if (enterShortSignal && /*shortSignal != _previousShortSignal &&*/ filter)
            {
                Signal = SignalType.Short;
                _lastTradePrice = data.Price;
            }
            else if (enterLongSignal && /*shortSignal != _previousLongSignal &&*/ filter)
            {
                Signal = SignalType.Long;
                _lastTradePrice = data.Price;
            }
            else if (exitLongSignal && _securityHolding.IsLong)
            {
                // exit long due to bb switching
                Signal = SignalType.Exit;
            }
            else if (exitShortSignal && _securityHolding.IsShort)
            {
                // exit short due to bb switching
                Signal = SignalType.Exit;
            }
            else
            {
                Signal = SignalType.NoSignal;
            }

            _previousClose = data.Price;
            //_previousLongSignal = longSignal;
            //_previousShortSignal = shortSignal;
        }

        public SignalType Signal { get; private set; }
    }
}