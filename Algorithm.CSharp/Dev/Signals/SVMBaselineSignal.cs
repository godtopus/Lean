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
    public class SVMBaselineSignal : ISignal
    {
        private SecurityHolding _securityHolding;
        private Security _security;
        private decimal _minimumPriceVariation;

        private SVMBaselineStrategy _qcAlgorithm;

        readonly bool _debug = false;

        public SVMBaselineSignal()
        {
        }

        public void Scan(QuoteBar data)
        {
        }

        public SignalType Signal { get; private set; }
    }
}