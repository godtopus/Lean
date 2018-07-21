using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Securities;
using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Statistics.Kernels;
using Accord.Math.Optimization.Losses;
using System.Linq;
using System;
using Accord.Statistics;
using Accord.Math;
using Accord.Statistics.Analysis;
using System.Collections.Generic;
using QuantConnect.Data.Consolidators;
using Accord.MachineLearning;
using Accord.Neuro;
using Accord.Neuro.Networks;
using Accord.Neuro.Learning;
using Accord.Neuro.ActivationFunctions;

namespace QuantConnect.Algorithm.CSharp
{
    public class SVMSignal : IRetrainable
    {
        private MulticlassSupportVectorMachine<Gaussian<Polynomial>> _svm;
        private ActivationNetwork _dbn;
        private PrincipalComponentAnalysis _pca;

        private HullMovingAverage _slow;
        private InstantTrend _slowSlope;
        private LogReturn _logReturns;
        private LeastSquaresMovingAverage _returnSlope;
        private RelativeStrengthIndex _rsi;

        private QuoteBarConsolidator _consolidator;
        private Stochastic _stoch;
        private HullMovingAverage _StochMA;
        private RollingWindow<double> _rolling;

        private SecurityHolding _securityHolding;
        private RollingWindow<int> _previousPredictions = new RollingWindow<int>(15);

        private GeneralConfusionMatrix _cm;

        private SVMStrategy _qcAlgorithm;

        private List<double[]> _inputs = new List<double[]>();

        readonly bool _pcaTransform = false;
        readonly bool _debug = false;

        public SVMSignal(QuoteBarConsolidator consolidator, Stochastic stoch, HullMovingAverage stochMA, RollingWindow<double> rolling, SecurityHolding securityHolding, SVMStrategy qcAlgorithm)
        {
            _consolidator = consolidator;
            _stoch = stoch;
            _StochMA = stochMA;
            _rolling = rolling;
            _securityHolding = securityHolding;
            _qcAlgorithm = qcAlgorithm;

            stochMA.Updated += (sender, args) =>
            {
                try
                {
                    var filtered = _rolling.TakeWhile((s) => args.Value > 50 ? s > 50 : args.Value < 50 ? s < 50 : false);

                    //Console.WriteLine("{0}, {1}, {2}", filtered.Count(), _rolling.Count(), _stoch.Current.Value);

                    var inputs = new double[] { (double) (args.Value / _stoch.Current.Value), filtered.Count(), (double) _stoch.Current.Value };
                    _inputs.Add(inputs);
                    inputs = Accord.Statistics.Tools.ZScores(_inputs.ToArray()).Last();
                    _inputs.RemoveAt(_inputs.Count - 1);

                    if (_pcaTransform)
                    {
                        inputs = _pca.Transform(inputs);
                    }

                    int prediction = _svm.Decide(inputs);
                    var probability = _svm.Probability(inputs);
                    var logLikelihood = _svm.LogLikelihood(inputs);

                    _qcAlgorithm.PlotSignal((QuoteBar) _consolidator.Consolidated, prediction == 0 ? -1 : prediction, logLikelihood);

                    /*var dbnPredictions = _dbn.Compute(inputs);
                    var shortPrediction = dbnPredictions.First();
                    var longPrediction = dbnPredictions.Last();*/

                    if (_securityHolding.Invested && Signal == SignalType.Long && prediction == 0)
                    {
                        //Console.WriteLine("Long Exit Time: {0} Prediction: {1} Probability: {2}", args.EndTime, prediction, probability);
                    }
                    else if (_securityHolding.Invested && Signal == SignalType.Short && prediction == 1) {
                        //Console.WriteLine("Short Exit Time: {0} Prediction: {1} Probability: {2}", args.EndTime, prediction, probability);
                    }

                    if (prediction != 2)
                    {
                        //Console.WriteLine("Time: {0} Prediction: {1} Probability: {2}, Log Likelihood: {3} Score: {4}", args.EndTime, prediction, probability, logLikelihood);
                    }

                    // EURUSD 0.9999
                    var probabilityFilter = logLikelihood >= 4;//probability >= 0.999999;// && _previousPredictions.IsReady && _previousPredictions.All((p) => p == prediction);

                    var longExit = Signal == SignalType.Long && prediction == 0;
                    var shortExit = Signal == SignalType.Short && prediction == 1;

                    if (!_securityHolding.Invested && probabilityFilter && prediction == 1)
                    {
                        Signal = Signal != SignalType.PendingLong ? SignalType.PendingLong : SignalType.Long;

                        if (_debug)
                        {
                            Console.WriteLine("Long Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Long STO: {0} STO MA: {1} Count: {2}", _stoch.Current.Value, args.Value, filtered.Count());
                            Console.WriteLine("Long Time: {0} Price: {1}", _consolidator.Consolidated.Time, _consolidator.Consolidated.Value);
                        }
                    }
                    else if (!_securityHolding.Invested && probabilityFilter && prediction == 0)
                    {
                        Signal = Signal != SignalType.PendingShort ? SignalType.PendingShort : SignalType.Short;

                        if (_debug)
                        {
                            Console.WriteLine("Short Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Short STO: {0} STO MA: {1} Count: {2}", _stoch.Current.Value, args.Value, filtered.Count());
                            Console.WriteLine("Short Time: {0} Price: {1}", _consolidator.Consolidated.Time, _consolidator.Consolidated.Value);
                        }
                    }
                    else if ((_securityHolding.Invested && longExit) || (_securityHolding.Invested && shortExit))
                    {
                        if (_debug)
                        {
                            Console.WriteLine("Exit Signal: {0} Probability: {1} Log Likelihood: {2}", Signal, probability, logLikelihood);
                            Console.WriteLine("Exit STO: {0} STO MA: {1} Count: {2}", _stoch.Current.Value, args.Value, filtered.Count());
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
        }

        public void Retrain(List<double[]> inputs, List<int> outputs, List<double> weights = null)
        {
            TrainSVM(inputs, outputs, weights);
        }

        public void PCA(List<double[]> inputs)
        {
            _pca = new PrincipalComponentAnalysis()
            {
                Method = PrincipalComponentMethod.Center,
                Whiten = true,
            };

            var transform = _pca.Learn(inputs.ToArray());

            var componentProportions = _pca.ComponentProportions;
            var cumulativeProportions = _pca.CumulativeProportions;
            var means = _pca.Means;

            Console.WriteLine("Component Proportions: {0}", string.Join(" ", componentProportions));
            Console.WriteLine("Cumulative Proportions: {0}", string.Join(" ", cumulativeProportions));
            Console.WriteLine("Means: {0}", string.Join(" ", means));
        }

        public void TrainSVM(List<double[]> inputs, List<int> outputs, List<double> weights = null)
        {
            var sell = outputs.Where((o) => o == 0).Count();
            var buy = outputs.Where((o) => o == 1).Count();
            var samples = Math.Min(sell, buy);
            var trainingSamples = (int) (Math.Floor(samples * 0.8) % 2 == 0 ? Math.Floor(samples * 0.8) : Math.Floor(samples * 0.8) - 1);
            var calibrationSamples = (int)(Math.Floor(samples * 0.2) % 2 == 0 ? Math.Floor(samples * 0.2) : Math.Floor(samples * 0.2) - 1);

            Console.WriteLine("Training SVM samples: {0} Calibration samples: {1}", trainingSamples, calibrationSamples);

            var zipped = inputs.Zip(outputs, (i, o) => Tuple.Create(i, o)).Zip(weights, (io, w) => Tuple.Create(io.Item1, io.Item2, w));//.Shuffle();

            var sellSamples = zipped.Where((o) => o.Item2 == 0);
            var buySamples = zipped.Where((o) => o.Item2 == 1);
            //var ignoreSamples = zipped.Where((o) => o.Item2 == 2);

            var trainingInputs = sellSamples.Take(trainingSamples / 2).Select((s) => s.Item1)
                                            .Concat(buySamples.Take(trainingSamples / 2).Select((s) => s.Item1))
                                            //.Concat(ignoreSamples.Take(trainingSamples / 3).Select((s) => s.Item1))
                                            .ToArray();
            var trainingOutputs = sellSamples.Take(trainingSamples / 2).Select((s) => s.Item2)
                                            .Concat(buySamples.Take(trainingSamples / 2).Select((s) => s.Item2))
                                            //.Concat(ignoreSamples.Take(trainingSamples / 3).Select((s) => s.Item2))
                                            .ToArray();
            var trainingWeights = sellSamples.Take(trainingSamples / 2).Select((s) => s.Item3)
                                            .Concat(buySamples.Take(trainingSamples / 2).Select((s) => s.Item3))
                                            //.Concat(ignoreSamples.Take(trainingSamples / 3).Select((s) => s.Item3))
                                            .ToArray();

            Console.WriteLine("Training SVM inputs: {0} Outputs: {1}, Weights: {2}", trainingInputs.Length, trainingOutputs.Length, trainingWeights.Length);

            _inputs.Clear();
            _inputs.AddRange(trainingInputs);

            trainingInputs = Accord.Statistics.Tools.ZScores(trainingInputs);

            PCA(trainingInputs.ToList());

            if (_pcaTransform)
            {
                trainingInputs = _pca.Transform(trainingInputs);
            }

            //inputs = Tools.Center(inputs);
            //inputs = Tools.Whitening(inputs);

            var teacher = new MulticlassSupportVectorLearning<Gaussian<Polynomial>>()
            {
                Learner = (param) => new SequentialMinimalOptimization<Gaussian<Polynomial>>()
                {
                    Complexity = 1,
                    Tolerance = 0.001,
                    Epsilon = 0.000001,
                    Kernel = new Gaussian<Polynomial>(new Polynomial(degree: 3), sigma: 4.2), //new Additive(new Gaussian(0.01)),
                    Strategy = SelectionStrategy.WorstPair,
                    //UseKernelEstimation = true,
                    //UseComplexityHeuristic = true,
                    //CacheSize = 0
                }
            };

            //teacher.ParallelOptions.MaxDegreeOfParallelism = 4;

            _svm = teacher.Learn(trainingInputs, trainingOutputs, trainingWeights);

            var calibrationInputs = sellSamples.Skip(trainingSamples / 2)
                                               .Take(calibrationSamples / 2)
                                               .Select((s) => s.Item1)
                                               .Concat(buySamples.Skip(trainingSamples / 2).Take(calibrationSamples / 2).Select((s) => s.Item1))
                                               //.Concat(ignoreSamples.Skip(trainingSamples / 3).Take(calibrationSamples / 3).Select((s) => s.Item1))
                                               .ToArray();
            var calibrationOutputs = sellSamples.Skip(trainingSamples / 2)
                                                .Take(calibrationSamples / 2)
                                                .Select((s) => s.Item2)
                                                .Concat(buySamples.Skip(trainingSamples / 2).Take(calibrationSamples / 2).Select((s) => s.Item2))
                                                //.Concat(ignoreSamples.Skip(trainingSamples / 3).Take(calibrationSamples / 3).Select((s) => s.Item2))
                                                .ToArray();
            var calibrationWeights = sellSamples.Skip(trainingSamples / 2)
                                                .Take(calibrationSamples / 2)
                                                .Select((s) => s.Item3)
                                                .Concat(buySamples.Skip(trainingSamples / 2).Take(calibrationSamples / 2).Select((s) => s.Item3))
                                                //.Concat(ignoreSamples.Skip(trainingSamples / 3).Take(calibrationSamples / 3).Select((s) => s.Item3))
                                                .ToArray();

            Console.WriteLine("Calibration SVM inputs: {0} Outputs: {1}, Weights: {2}", calibrationInputs.Length, calibrationOutputs.Length, calibrationWeights.Length);

            CalibrateSVM(calibrationInputs, calibrationOutputs, calibrationWeights);
        }

        public void CalibrateSVM(double[][] inputs, int[] outputs, double[] weights)
        {
            var tempInputs = _inputs.Take(_inputs.Count).Concat(inputs).ToArray();
            tempInputs = Accord.Statistics.Tools.ZScores(tempInputs);
            inputs = tempInputs.Skip(_inputs.Count).Take(inputs.Length).ToArray();

            if (_pcaTransform)
            {
                inputs = _pca.Transform(inputs);
            }

            var calibration = new MulticlassSupportVectorLearning<Gaussian<Polynomial>>()
            {
                Model = _svm,
                Learner = (param) => new ProbabilisticOutputCalibration<Gaussian<Polynomial>>()
                {
                    Model = param.Model
                }
            };

            //calibration.ParallelOptions.MaxDegreeOfParallelism = 4;

            calibration.Learn(inputs, outputs);

            _svm.Method = MulticlassComputeMethod.Elimination;

            var predicted = _svm.Decide(inputs);
            var error = new ZeroOneLoss(outputs).Loss(predicted);
            var cm = new GeneralConfusionMatrix(3, outputs, predicted);
            Console.WriteLine("Accuracy: {0} Variance: {1} Kappa: {2} Error: {3}", cm.Accuracy, cm.Variance, cm.Kappa, error);

            _cm = cm;

            /*int[] predicted = _svm.Decide(inputs);
            double error = new ZeroOneLoss(outputs).Loss(predicted);

            double[][] probabilities = _svm.Probabilities(inputs);
            Console.WriteLine("P: {0} Prob: {1} E: {2}", predicted.Length, probabilities.Length, error);
            double loss = new CategoryCrossEntropyLoss(outputs).Loss(probabilities);
            
            Console.WriteLine("Number of classes: {0} Error: {1} Loss: {2}", _svm.NumberOfClasses, error, loss);*/
        }

        public void TrainNN(List<double[]> inputs, List<int> outputs, List<double> weights = null)
        {
            var tempInputs = _inputs.Take(_inputs.Count).Concat(inputs).ToArray();
            tempInputs = Accord.Statistics.Tools.ZScores(tempInputs);
            var trainingInputs = tempInputs.Skip(_inputs.Count).Take(inputs.Count).ToArray();
            var trainingOutputs = Jagged.OneHot(outputs.ToArray());

            var network = new ActivationNetwork(new GaussianFunction(), trainingInputs.First().Length, 5, 2);
            _dbn = network;

            // Initialize the network with Gaussian weights
            new GaussianWeights(network, 0.1).Randomize();

            // Setup the learning algorithm.
            var teacher = new ParallelResilientBackpropagationLearning(network);

            double error = Double.MaxValue;
            for (int i = 0; i < 5000; i++)
            {
                error = teacher.RunEpoch(trainingInputs, trainingOutputs);
            }

            // Test the resulting accuracy.
            int correct = 0;
            for (int i = 0; i < trainingInputs.Length; i++)
            {
                double[] outputValues = network.Compute(inputs[i]);
                double outputResult = outputValues.First() >= 0.5 ? 1 : 0;

                if (outputResult == trainingOutputs[i].First())
                {
                    correct++;
                }
            }

            Console.WriteLine("DBN Correct: {0} Total: {1} Accuracy: {2}, Training Error: {3}", correct, trainingOutputs.Length, (double)correct / (double)trainingOutputs.Length, error);
        }

        public SignalType Signal { get; private set; }
    }
}