using System;
using System.Collections.Generic;
using System.Security.Permissions;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class BollingerBandSignal : ISignal
    {
        private BollingerBands _bb;
        private AverageDirectionalIndex _adx;
        private SecurityHolding _securityHolding;

        private decimal _previousClose;
        private bool _previousShortSignal;
        private bool _previousLongSignal;

        private LeastSquaresMovingAverage _lsmaUpperBand;
        private LeastSquaresMovingAverage _lsmaMiddleBand;
        private LeastSquaresMovingAverage _lsmaLowerBand;

        public BollingerBandSignal(BollingerBands bb,
            LeastSquaresMovingAverage lsmaUpperBand,
            LeastSquaresMovingAverage lsmaMiddleBand,
            LeastSquaresMovingAverage lsmaLowerBand,
            AverageDirectionalIndex adx,
            SecurityHolding securityHolding)
        {
            _bb = bb;
            _lsmaUpperBand = lsmaUpperBand;
            _lsmaMiddleBand = lsmaMiddleBand;
            _lsmaLowerBand = lsmaLowerBand;
            _adx = adx;
            _securityHolding = securityHolding;
        }

        public void Scan(QuoteBar data)
        {
            //_bb.Update(data.EndTime, data.Close);

            var filter = !_securityHolding.Invested;

            bool enterLongSignal, enterShortSignal, exitLongSignal, exitShortSignal;

            if (_adx > 20)
            {
                enterLongSignal = _lsmaUpperBand > _bb.UpperBand
                    && _lsmaMiddleBand > _bb.MiddleBand
                    && _lsmaLowerBand > _bb.LowerBand;

                enterShortSignal = _lsmaUpperBand < _bb.UpperBand
                   && _lsmaMiddleBand < _bb.MiddleBand
                   && _lsmaLowerBand < _bb.LowerBand;

                exitLongSignal = _lsmaUpperBand < _bb.UpperBand
                    && data.Price < _bb.LowerBand;
                exitShortSignal = _lsmaLowerBand > _bb.LowerBand
                    && data.Price > _bb.UpperBand;
            }
            else
            {
                enterLongSignal = data.Close > _bb.LowerBand
                    && _previousClose < _bb.LowerBand;
                enterShortSignal = data.Close < _bb.UpperBand
                    && _previousClose > _bb.UpperBand;
                exitLongSignal = data.Close > _bb.UpperBand;
                exitShortSignal = data.Close < _bb.LowerBand;
            }

            if (enterShortSignal && /*shortSignal != _previousShortSignal &&*/ filter)
            {
                Signal = SignalType.Short;
            }
            else if (enterLongSignal && /*shortSignal != _previousLongSignal &&*/ filter)
            {
                Signal = SignalType.Long;
            }
            else if (enterShortSignal && _securityHolding.IsLong && _securityHolding.UnrealizedProfitPercent > 0.0015m)
            {
                // exit long due to bb switching
                Signal = SignalType.Exit;
            }
            else if (enterLongSignal && _securityHolding.IsShort && _securityHolding.UnrealizedProfitPercent > 0.0015m)
            {
                // exit short due to bb switching
                Signal = SignalType.Exit;
            }
            /*if (data.Close < _bb.UpperBand && _previousClose > _bb.UpperBand && filter)
            {
                Signal = SignalType.Short;
            }
            else if (data.Close > _bb.LowerBand && _previousClose < _bb.LowerBand && filter)
            {
            	Signal = SignalType.Long;
            }
            else if (data.Close < _bb.UpperBand && _previousClose > _bb.UpperBand && _securityHolding.IsLong)
            {
                // exit long due to bb switching
                Signal = SignalType.Exit;
            }
            else if (data.Close > _bb.LowerBand && _previousClose < _bb.LowerBand && _securityHolding.IsShort)
            {
            	// exit short due to bb switching
            	Signal = SignalType.Exit;
            }*/
            else
            {
                Signal = SignalType.NoSignal;
            }

            _previousClose = data.Close;
            //_previousLongSignal = longSignal;
            //_previousShortSignal = shortSignal;
        }

        public SignalType Signal { get; private set; }
    }
}