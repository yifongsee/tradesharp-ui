/***************************************************************************** 
* Copyright 2016 Aurora Solutions 
* 
*    http://www.aurorasolutions.io 
* 
* Aurora Solutions is an innovative services and product company at 
* the forefront of the software industry, with processes and practices 
* involving Domain Driven Design(DDD), Agile methodologies to build 
* scalable, secure, reliable and high performance products.
* 
* TradeSharp is a C# based data feed and broker neutral Algorithmic 
* Trading Platform that lets trading firms or individuals automate 
* any rules based trading strategies in stocks, forex and ETFs. 
* TradeSharp allows users to connect to providers like Tradier Brokerage, 
* IQFeed, FXCM, Blackwood, Forexware, Integral, HotSpot, Currenex, 
* Interactive Brokers and more. 
* Key features: Place and Manage Orders, Risk Management, 
* Generate Customized Reports etc 
* 
* Licensed under the Apache License, Version 2.0 (the "License"); 
* you may not use this file except in compliance with the License. 
* You may obtain a copy of the License at 
* 
*    http://www.apache.org/licenses/LICENSE-2.0 
* 
* Unless required by applicable law or agreed to in writing, software 
* distributed under the License is distributed on an "AS IS" BASIS, 
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
* See the License for the specific language governing permissions and 
* limitations under the License. 
*****************************************************************************/


﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AForge;
using AForge.Genetic;
using TraceSourceLogger;
using TradeHub.Common.Core.Constants;
using TradeHub.Optimization.Genetic;
using TradeHub.Optimization.Genetic.FitnessFunction;
using TradeHub.Optimization.Genetic.FitnessFunctionImplementation;
using TradeHubGui.Common;
using TradeHubGui.Common.Models;
using TradeHubGui.Common.ValueObjects;
using TradeHubGui.StrategyRunner.Executors;
using Parallel = System.Threading.Tasks.Parallel;

namespace TradeHubGui.StrategyRunner.Managers
{
    /// <summary>
    /// Manages the optimization process (Genetic Algorithm) for a given strategy 
    /// </summary>
    public class OptimizationManagerGeneticAlgorithm : IDisposable
    {
        private readonly Type _type = typeof(OptimizationManagerGeneticAlgorithm);

        private AsyncClassLogger _logger = new AsyncClassLogger("OptimizationManagerGeneticAlgorithm");

        private bool _disposed = false;

        /// <summary>
        /// Array size to be used for local arrays 
        /// Linked with objects required for optimization
        /// </summary>
        private readonly int _arrayLength = 2;

        private object[] _ctorArguments;
        private object[][] _ctorArgumentsArray;

        private Type _strategyType;
        private Population[] _populationArray;
        private GeneticOptimization[] _fitnessFunctionArray;
        private StrategyExecutorGeneticAlgorithm[] _strategyExecutorArray;
        private SortedDictionary<int, OptimizationParameterDetail> _optimizationParameters;

        /// <summary>
        /// GA Iterations
        /// </summary>
        private int _iterations;
        /// <summary>
        /// GA population size
        /// </summary>
        private int _populationSize;

        private bool _singleExecution = true;

        private Range[] _ranges;

        /// <summary>
        /// Default Constructor
        /// </summary>
        public OptimizationManagerGeneticAlgorithm()
        {
            _logger.SetLoggingLevel();
            _logger.LogDirectory(DirectoryStructure.CLIENT_LOGS_LOCATION);

            // Subscribe Event
            EventSystem.Subscribe<GeneticAlgorithmParameters>(OptimizeStrategy);
        }

        /// <summary>
        /// Handles incoming strategy optimization requests
        /// </summary>
        /// <param name="strategyInfo">Information to optimized specified strategy</param>
        private void OptimizeStrategy(GeneticAlgorithmParameters strategyInfo)
        {
            // Save constructor arguments
            _ctorArguments = strategyInfo.CtorArgs;
            
            // Save user strategy type
            _strategyType = strategyInfo.StrategyType;
            
            // Save optimization parameters info
            _optimizationParameters = strategyInfo.OptimzationParameters;
            
            //save iterations
            _iterations = strategyInfo.Iterations;
            
            //save population size
            _populationSize = strategyInfo.PopulationSize;

            int roundCount = 0;

            // Execute GA for the specified No. of times
            while (++roundCount<=strategyInfo.Rounds)
            {
                // Executes a single Round of Genetic Algorithm optimization
                ExecuteGeneticAlgorithmRound();
            }
        }

        /// <summary>
        /// Executes a single Round of Genetic Algorithm optimization
        /// </summary>
        private void ExecuteGeneticAlgorithmRound()
        {
            //initialize range
            if (_ranges == null)
            {
                InitializeRangeArray();
            }

            //Initialize Required parameters
            if (InitializeOptimizationParameters())
            {
                int index;
                // Start Optimization
                if ((index = StartOptimizationProcess()) >= 0)
                {
                    // Get Optimized parameters info
                    Dictionary<int, double> optimizedParameters = ProvideOptimizedParameterInfo(index);

                    // Create result to be displayed on UI
                    var result = new GeneticAlgorithmResult();

                    foreach (KeyValuePair<int, double> keyValuePair in optimizedParameters)
                    {
                        OptimizationParameterDetail optimizationParameter;
                        if (_optimizationParameters.TryGetValue(keyValuePair.Key, out optimizationParameter))
                        {
                            // Initialize new value
                            OptimizationParameterDetail optimizationDetail = new OptimizationParameterDetail();
                            
                            // Set optimized value
                            optimizationDetail.OptimizedValue = keyValuePair.Value;

                            // Save general information
                            optimizationDetail.Description = optimizationParameter.Description;

                            // Add to result 
                            result.ParameterList.Add(optimizationDetail);
                        }
                    }

                    // Update Fitness
                    var risk = Math.Round(_populationArray[index].FitnessMax, 5);

                    var tempParameterDetail = new OptimizationParameterDetail();
                    tempParameterDetail.OptimizedValue = risk;
                    tempParameterDetail.Description = "Risk";

                    // Add to result 
                    result.ParameterList.Add(tempParameterDetail);

                    // Publish event to UI
                    EventSystem.Publish<GeneticAlgorithmResult>(result);
                    ////Dispose();
                }
            }
        }

        /// <summary>
        /// Initialize Range Array
        /// </summary>
        public void InitializeRangeArray()
        {
            _ranges = new Range[_optimizationParameters.Count];
            for (int i = 0; i < _optimizationParameters.Count; i++)
            {
                _ranges[i] = new Range((float)_optimizationParameters.ElementAt(i).Value.StartValue, (float)_optimizationParameters.ElementAt(i).Value.EndValue);
            }
        }

        /// <summary>
        /// Execute single run of strategy.
        /// </summary>
        public void ExecuteSingleRun()
        {
            //var result = _algo.ExecuteStrategy(2.92, 0.0098, 8.1717, 0.0062);
        }

        /// <summary>
        /// Initializes the parameters required to initiate the optimization process
        /// </summary>
        /// <returns></returns>
        private bool InitializeOptimizationParameters()
        {
            try
            {
                Stopwatch sc = new Stopwatch();
                sc.Start();
                // Initialize Ctor Arguments array to use ctor arguments with multiple strategies
                if (_ctorArgumentsArray == null)
                {
                    _ctorArgumentsArray = new object[_arrayLength][];
                    if (!InitializeCtorArgumentsArray())
                        return false;
                }
                sc.Stop();
                _logger.Info("Ctor time=" + sc.ElapsedMilliseconds + " ms", _type.FullName, "InitializeOptimizationParameters");
                sc.Reset();
                sc.Start();

                // Initialize Strategy Executor array to allow multiple strategies to be executed simultaneously
                if (_strategyExecutorArray == null)
                {
                    _strategyExecutorArray = new StrategyExecutorGeneticAlgorithm[_arrayLength];
                    if (!InitializeStrategyExecutor())
                        return false;
                }
                sc.Stop();
                _logger.Info("Strategy Executor time=" + sc.ElapsedMilliseconds + " ms", _type.FullName, "InitializeOptimizationParameters");
                sc.Reset();
                sc.Start();
                if (_fitnessFunctionArray == null)
                {
                    // Initialize Fitness Function array to allow multiple fitness functions to be called simultaneously
                    _fitnessFunctionArray = new GeneticOptimization[_arrayLength];
                    if (!InitializeFitnessFunction())
                        return false;
                }
                sc.Stop();
                sc.Reset();
                sc.Start();

                //if(!InitializeCtorArgumentsArray()) 
                //    return false;

                //if (!InitializeStrategyExecutor()) 
                //    return false;

                //if (!InitializeFitnessFunction()) 
                //    return false;
                if (_populationArray == null)
                {
                    // Initialize Population array to allow multiple populations to be executed simultaneously
                    _populationArray = new Population[_arrayLength];
                    if (!InitializeGeneticPopulation())
                        return false;
                }
                else
                {
                    RegenratePopulations();
                }
                sc.Stop();
                _logger.Info("Genetic Population time=" + sc.ElapsedMilliseconds + " ms", _type.FullName, "InitializeOptimizationParameters");
                sc.Reset();
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "InitializeOptimizationParameters");
                return false;
            }
        }

        /// <summary>
        /// Initializes Ctor Arguments array by creating deep copies uing the given ctor arguments
        /// </summary>
        private bool InitializeCtorArgumentsArray()
        {
            try
            {
                if (_logger.IsInfoEnabled)
                {
                    _logger.Info("Initializing Ctor Arguments array.", _type.FullName, "InitializeCtorArgumentsArray");
                }

                if (_ctorArguments.Length > 0)
                {

                    for (int i = 0; i < _arrayLength; i++)
                    {
                        // Initialize current instance to be used for deep copy
                        _ctorArgumentsArray[i] = new object[_ctorArguments.Length];

                        // Create deep copy
                        _ctorArguments.CopyTo(_ctorArgumentsArray[i], 0);
                    }
                }

                if (_logger.IsInfoEnabled)
                {
                    _logger.Info("Initialization complete.", _type.FullName, "InitializeCtorArgumentsArray");
                }

                // Indicates initialization was successful
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "InitializeCtorArgumentsArray");
                return false;
            }
        }

        /// <summary>
        /// Initializes Strategy Executor to manages user strategy for Genetic Algo Optimization
        /// </summary>
        private bool InitializeStrategyExecutor()
        {
            try
            {
                if (_logger.IsInfoEnabled)
                {
                    _logger.Info("Initializing Strategy Executor array.", _type.FullName, "InitializeStrategyExecutor");
                }

                if (_singleExecution)
                {
                    _strategyExecutorArray[0] =
                        new StrategyExecutorGeneticAlgorithm(_strategyType, _ctorArgumentsArray[0]);
                }
                else
                {
                    // Initialize executor array for parallel execution
                    for (int i = 0; i < _arrayLength; i++)
                    {
                        _strategyExecutorArray[i] =
                            new StrategyExecutorGeneticAlgorithm(_strategyType, _ctorArgumentsArray[i]);
                    }
                }
                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("Initialization complete.", _type.FullName, "InitializeStrategyExecutor");
                }

                // Indicates initialization was successful
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "InitializeStrategyExecutor");
                return false;
            }
        }

        /// <summary>
        /// Initializes Range variable required to perform optimization
        /// </summary>
        private bool InitializeRangeVariables(Range[] firstRange, Range[] secondRange, Range[] thirdRange, Range[] fourthRange)
        {
            try
            {
                if (_logger.IsInfoEnabled)
                {
                    _logger.Info("Initializing Range variable.", _type.FullName, "InitializeRangeVariables");
                }
                for (int i = 0; i < _arrayLength; i++)
                {
                    firstRange[i] =
                        new Range(
                            float.Parse(_optimizationParameters.ElementAt(0).Value.StartValue.ToString()),
                            float.Parse(_optimizationParameters.ElementAt(0).Value.EndValue.ToString()));
                    secondRange[i] =
                        new Range(
                            float.Parse(_optimizationParameters.ElementAt(1).Value.StartValue.ToString()),
                            float.Parse(_optimizationParameters.ElementAt(1).Value.EndValue.ToString()));
                    thirdRange[i] =
                        new Range(
                            float.Parse(_optimizationParameters.ElementAt(2).Value.StartValue.ToString()),
                            float.Parse(_optimizationParameters.ElementAt(2).Value.EndValue.ToString()));
                    fourthRange[i] =
                        new Range(
                            float.Parse(_optimizationParameters.ElementAt(3).Value.StartValue.ToString()),
                            float.Parse(_optimizationParameters.ElementAt(3).Value.EndValue.ToString()));
                }
                if (_logger.IsInfoEnabled)
                {
                    _logger.Info("Initialization complete.", _type.FullName, "InitializeRangeVariables");
                }

                // Indicates Range initialization was successful
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "InitializeRangeVariables");
                return false;
            }
        }

        /// <summary>
        /// Initializes the required fitness function to optimize the given user strategy
        /// </summary>
        private bool InitializeFitnessFunction()
        {
            try
            {

                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("Initializing Fitness Function array.", _type.FullName, "InitializeFitnessFucntion");
                }

                #region Define Range

                // Create Range arrays for all optimization parameters(Four supported)
                Range[] firstVar = new Range[_arrayLength];
                Range[] secondVar = new Range[_arrayLength];
                Range[] thirdVar = new Range[_arrayLength];
                Range[] fourthVar = new Range[_arrayLength];

                #endregion

                // Initialize Range variable arrays
                if (!InitializeRangeVariables(firstVar, secondVar, thirdVar, fourthVar))
                    return false;
                Stopwatch sc = new Stopwatch();
                sc.Start();
                if (_singleExecution)
                {
                    _fitnessFunctionArray[0] = new StockTraderFitnessFunction(_strategyExecutorArray[0]);
                }
                else
                {

                    Task[] taskArrayFitnessFunction = new Task[_arrayLength];

                    for (int taskNumber = 0; taskNumber < _arrayLength; taskNumber++)
                    {
                        // capturing taskNumber in lambda wouldn't work correctly
                        int taskNumberCopy = taskNumber;

                        taskArrayFitnessFunction[taskNumber] = Task.Factory.StartNew(
                            () =>
                            {
                                _fitnessFunctionArray[taskNumberCopy] =
                                    new StockTraderFitnessFunction(_strategyExecutorArray[taskNumberCopy]);
                            });
                    }

                    // Wait for Task array to complete its operation
                    Task.WaitAll(taskArrayFitnessFunction);
                }
                sc.Stop();
                _logger.Info("Fitness Function time=" + sc.ElapsedMilliseconds + " ms", _type.FullName, "InitializeOptimizationParameters");
                //Array.Clear(taskArrayFitnessFunction, 0, 10);

                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("Initialization complete.", _type.FullName, "InitializeFitnessFucntion");
                }

                // Indicates initialization was successful.
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "InitializeFitnessFunction");
                return false;
            }
        }

        /// <summary>
        /// Initializes the Genetic Population required to execute Genetic Algo Optimization
        /// </summary>
        private bool InitializeGeneticPopulation()
        {
            try
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("Initializing Genetic Population array.", _type.FullName, "InitializeGeneticPopulation");
                }

                if (_singleExecution)
                {
                    _populationArray[0] =
                        new Population(_populationSize, new SimpleStockTraderChromosome(_ranges), _fitnessFunctionArray[0],
                            new EliteSelection());
                    _populationArray[0].CrossoverRate = 0.5;
                }
                else
                {
                    Task[] taskArray = new Task[_arrayLength];

                    for (int taskNumber = 0; taskNumber < _arrayLength; taskNumber++)
                    {
                        // capturing taskNumber in lambda wouldn't work correctly
                        int taskNumberCopy = taskNumber;

                        taskArray[taskNumber] = Task.Factory.StartNew(
                            () =>
                            {
                                _populationArray[taskNumberCopy] =
                                    new Population(_populationSize, new BinaryChromosome(64),
                                        _fitnessFunctionArray[taskNumberCopy],
                                        new EliteSelection());
                            });
                    }


                    // Wait for Task array to complete its operation
                    Task.WaitAll(taskArray);
                }
                //Array.Clear(taskArray, 0, 10);
                if (_logger.IsDebugEnabled)
                {
                    _logger.Debug("Initialization complete.", _type.FullName, "InitializeGeneticPopulation");
                }

                // Indicates initialization was successful.
                return true;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "InitializeGeneticPopulation");
                return false;
            }
        }

        /// <summary>
        /// Starts the optimization process for the given User Strategy
        /// </summary>
        private int StartOptimizationProcess()
        {
            try
            {
                _logger.Info("Start Iterations:", _type.FullName, "StartOptimizationProcess");
                Stopwatch sc = new Stopwatch();
                sc.Start();
                if (_singleExecution)
                {
                    //for (int i = 0; i < 10; i++)
                    //{
                    for (int mainIteration = 0; mainIteration < _iterations; mainIteration++)
                    {
                        _populationArray[0].RunEpoch();
                    }
                    // PublishParameters(0);
                    // }
                }
                else
                {
                    Task[] taskArray = new Task[_arrayLength];
                    // Parallel Executions
                    for (int mainIteration = 0; mainIteration < _iterations / _arrayLength; mainIteration++)
                    {
                        // _populationArray[0].RunEpoch();
                        for (int taskNumber = 0; taskNumber < _arrayLength; taskNumber++)
                        {
                            //_populationArray[0].RunEpoch();
                            // capturing taskNumber in lambda wouldn't work correctly
                            int taskNumberCopy = taskNumber;

                            taskArray[taskNumber] = Task.Factory.StartNew(
                                () => _populationArray[taskNumberCopy].RunEpoch());
                        }
                        //ManualResetEvent resetEvent = new ManualResetEvent(false);
                        //resetEvent.WaitOne(5000);
                        //resetEvent.WaitOne(5000);
                        //resetEvent.WaitOne(5000);
                        //resetEvent.WaitOne(5000);

                        Task.WaitAll(taskArray);
                        //Action[] actions=new Action[10];
                        //for (int i = 0; i < 10; i++)
                        //{
                        //    int taskNumberCopy =i;
                        //    actions[i] = () => _populationArray[taskNumberCopy].RunEpoch();
                        //}
                        //Parallel.Invoke(actions);

                        // Migrate values between populations
                        // MigratePopulation(mainIteration);


                        _logger.Info("Iteration Count: " + mainIteration, _type.FullName, "StartOptimizationProcess");
                    }
                    //Array.Clear(taskArray,0,10);
                }

                _logger.Info("Iterations completed", _type.FullName, "StartOptimizationProcess");

                // Find best population
                int bestPopulationIndex = SelectBestPopulation();
                sc.Stop();
                _logger.Info("Time Taken=" + sc.ElapsedMilliseconds + " ms", _type.FullName, "StartOptimizationProcess");

                // Return best population
                return bestPopulationIndex;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "StartOptimizationProcess");
                return -1;
            }
        }

        /// <summary>
        /// Responsible for population migration
        /// </summary>
        /// <param name="iteration">Current iteration count</param>
        private void MigratePopulation(int iteration)
        {
            switch (1)
            {
                case 0:
                    // Migrate values between populations
                    Parallel.For(0, 5,
                                 migrationIteration =>
                                 _populationArray[migrationIteration].Migrate(_populationArray[5 + migrationIteration],
                                                                              15, new EliteSelection()));
                    break;
                case 1:
                    // Migrate values between populations
                    for (int i = 0; i < _arrayLength - 1; i += 2)
                    {
                        _populationArray[i].Migrate(_populationArray[i + 1], 15, new EliteSelection());
                    }
                    break;
                case 2:
                    // Migrate values between populations
                    Parallel.For(0, 5,
                                 migrationIteration =>
                                 _populationArray[migrationIteration].Migrate(_populationArray[9 - migrationIteration],
                                                                              15, new EliteSelection()));
                    break;
            }
        }

        /// <summary>
        /// Selects the Population with best fitness from the population array
        /// </summary>
        /// <returns>Best population index</returns>
        private int SelectBestPopulation()
        {
            try
            {
                int bestPopulationIndex = 0;
                var bestFitness = _populationArray[0].FitnessMax;

                if (!_singleExecution)
                {
                    // Traverse all populations
                    for (int i = 1; i < _arrayLength; i++)
                    {
                        if (bestFitness < _populationArray[i].FitnessMax)
                        {
                            bestFitness = _populationArray[i].FitnessMax;
                            bestPopulationIndex = i;
                        }
                    }
                }

                // Return best population index
                return bestPopulationIndex;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, _type.FullName, "SelectBestPopulation");
                return -1;
            }
        }

        /// <summary>
        /// Provides Optimized parameter values after GA Optimization Process is complete
        /// </summary>
        /// <param name="bestPopulationIndex">Index of the best population</param>
        private Dictionary<int, double> ProvideOptimizedParameterInfo(int bestPopulationIndex)
        {
            try
            {
                var optimizedParameters = new Dictionary<int, double>();
                for (int i = 0; i < _ranges.Length; i++)
                {
                    optimizedParameters.Add(_optimizationParameters[i + 1].Index,
                                            _fitnessFunctionArray[bestPopulationIndex].Translate(_populationArray[bestPopulationIndex].BestChromosome)[i]);
                }

                return optimizedParameters;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, _type.FullName, "ProvideOptimizedParameterInfo");
                return null;
            }
        }

        /// <summary>
        /// Regenrate populations for next round
        /// </summary>
        private void RegenratePopulations()
        {
            if (_singleExecution)
            {
                _populationArray[0] =
                       new Population(_populationSize, new SimpleStockTraderChromosome(_ranges), _fitnessFunctionArray[0],
                           new EliteSelection());
                _populationArray[0].CrossoverRate = 0.5;
            }
            else
            {
                for (int i = 0; i < _arrayLength; i++)
                {
                    _populationArray[i].Regenerate();
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Make sure event is only subscribed once
                    EventSystem.Unsubscribe<GeneticAlgorithmParameters>(OptimizeStrategy);

                    if (_populationArray != null)
                        Array.Clear(_populationArray, 0, _populationArray.Length);

                    if (_ctorArgumentsArray != null) 
                        Array.Clear(_ctorArgumentsArray, 0, _ctorArgumentsArray.Length);

                    if (_fitnessFunctionArray != null)
                        Array.Clear(_fitnessFunctionArray, 0, _fitnessFunctionArray.Length);

                    if (_strategyExecutorArray != null)
                    {
                        for (int i = 0; i < _strategyExecutorArray.Length; i++)
                        {
                            if (_strategyExecutorArray[i] != null)
                            {
                                _strategyExecutorArray[i].StopStrategy();
                            }
                        }
                        Array.Clear(_strategyExecutorArray, 0, _strategyExecutorArray.Length);
                    }
                }
                // Release unmanaged resources.
                _disposed = true;
            }
        }
    }
}
