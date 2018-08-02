using System;
using System.Linq;
using Accord.Statistics.Models.Markov.Learning;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    public enum Tradetype
    {
        Losing,
        Neutral,
        Winning
    };

    public class HiddenMarkovModelPositionSizing
    {
        private readonly RollingWindow<decimal> _tradeReturns;
        private const int MaxReturns = 4;

        public HiddenMarkovModelPositionSizing()
        {
            _tradeReturns = new RollingWindow<decimal>(MaxReturns);

            for (var i = 0; i < MaxReturns; i++)
            {
                _tradeReturns.Add(0);
            }
        }

        public void Update(decimal lastTradeProfitPercent)
        {
            _tradeReturns.Add(lastTradeProfitPercent);
        }

        public decimal PredictionRisk()
        {
            var direction = PredictNextTrade();
            var predictionRisk = 1.0m;

            if (direction == Tradetype.Losing)
            {
                predictionRisk = 0.5m;
            }
            else if (direction == Tradetype.Winning)
            {
                predictionRisk = 1.5m;
            }

            return predictionRisk;
        }

        public decimal NextPositionSize(decimal portfolioCash, decimal currentPrice)
        {
            var direction = PredictNextTrade();
            var predictionRisk = 1.0m;

            if (direction == Tradetype.Losing)
            {
                predictionRisk = 0.5m;
            }
            else if (direction == Tradetype.Winning)
            {
                predictionRisk = 1.5m;
            }

            return (int)Math.Round(predictionRisk * portfolioCash / currentPrice);
        }

        private Tradetype PredictNextTrade()
        {
            var res = Tradetype.Winning;
            var observationSequence = GetSequence();

            if (observationSequence.All((o) => o == 0))
            {
                res = Tradetype.Losing;
            }
            else if (observationSequence.All((o) => o == 1))
            {
                res = Tradetype.Neutral;
            }
            else if (observationSequence.All((o) => o == 2))
            {
                res = Tradetype.Winning;
            }
            else if (observationSequence.Distinct().Count() < 3)
            {
                res = Tradetype.Neutral;
            }
            else if (_tradeReturns.Count == 4)
            {
                var teacher = new BaumWelchLearning()
                {
                    NumberOfStates = 3,
                    NumberOfSymbols = observationSequence.Max() + 1
                };

                // and call its Run method to start learning
                var hmm = teacher.Learn(new int[][] { observationSequence });
                var predict = hmm.Predict(observationSequence, 1);

                if (predict[0] == 0)
                {
                    res = Tradetype.Losing;
                }
                else if (predict[0] == 1)
                {
                    res = Tradetype.Neutral;
                }
                else if (predict[0] == 2)
                {
                    res = Tradetype.Winning;
                }
            }

            return res;
        }

        private int[] GetSequence()
        {
            var observationSequence = new int[MaxReturns];
            for (var i = 0; i < _tradeReturns.Count; i++)
            {
                if (_tradeReturns[i] < 0m)
                {
                    observationSequence[i] = 0; // loss
                }
                else if (_tradeReturns[i] >= 0m && _tradeReturns[i] < 0.01m)
                {
                    observationSequence[i] = 1; //neutral, small win
                }
                else
                {
                    observationSequence[i] = 2; //big win
                }
            }

            return observationSequence;
        }
    }
}