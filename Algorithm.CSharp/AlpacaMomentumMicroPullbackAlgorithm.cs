/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Momentum-style micro pullback strategy for Alpaca broker with WebSocket data feed.
    ///
    /// Strategy Logic:
    /// 1. Identifies stocks with strong upward momentum using EMA crossover
    /// 2. Waits for small pullbacks (micro retracements) using RSI
    /// 3. Enters long positions when pullback shows signs of reversal
    /// 4. Uses tight stop-loss to manage risk
    /// 5. Takes profit when momentum weakens
    ///
    /// This algorithm is designed to work with Alpaca's WebSocket SIP data feed for real-time execution.
    /// </summary>
    public class AlpacaMomentumMicroPullbackAlgorithm : QCAlgorithm
    {
        // Trading parameters
        private const decimal _targetPortfolioPercent = 0.2m; // 20% per position
        private const decimal _stopLossPercent = 0.02m; // 2% stop loss
        private const decimal _takeProfitPercent = 0.05m; // 5% take profit

        // Momentum parameters
        private const int _fastEmaPeriod = 8;
        private const int _slowEmaPeriod = 21;
        private const int _rsiPeriod = 14;
        private const decimal _rsiOversoldLevel = 35m; // RSI level indicating pullback
        private const decimal _rsiRecoveryLevel = 45m; // RSI level indicating pullback ending

        // Symbol tracking
        private readonly List<string> _universe = new List<string>
        {
            "SPY",  // S&P 500 ETF
            "QQQ",  // Nasdaq 100 ETF
            "IWM",  // Russell 2000 ETF
            "AAPL", // Apple
            "MSFT", // Microsoft
            "GOOGL",// Google
            "AMZN", // Amazon
            "TSLA", // Tesla
            "NVDA", // NVIDIA
            "AMD"   // AMD
        };

        // Indicators dictionary
        private readonly Dictionary<Symbol, SymbolData> _symbolData = new Dictionary<Symbol, SymbolData>();

        /// <summary>
        /// Initializes the algorithm
        /// </summary>
        public override void Initialize()
        {
            // Set start and end dates
            SetStartDate(2024, 1, 1);
            SetEndDate(2024, 12, 31);
            SetCash(100000);

            // Set brokerage model to Alpaca
            SetBrokerageModel(Brokerages.BrokerageName.Alpaca);

            // Add securities with minute resolution for real-time WebSocket data
            foreach (var ticker in _universe)
            {
                var symbol = AddEquity(ticker, Resolution.Minute).Symbol;

                // Initialize indicators
                var symbolData = new SymbolData
                {
                    Symbol = symbol,
                    FastEma = EMA(symbol, _fastEmaPeriod, Resolution.Minute),
                    SlowEma = EMA(symbol, _slowEmaPeriod, Resolution.Minute),
                    Rsi = RSI(symbol, _rsiPeriod, MovingAverageType.Wilders, Resolution.Minute),
                    WasInPullback = false
                };

                _symbolData[symbol] = symbolData;
            }

            // Warm up indicators
            SetWarmUp(Math.Max(_slowEmaPeriod, _rsiPeriod));

            // Schedule function to check positions every minute
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(1)), CheckPositions);
        }

        /// <summary>
        /// OnData event handler - processes real-time market data from Alpaca WebSocket
        /// </summary>
        public override void OnData(Slice data)
        {
            if (IsWarmingUp)
                return;

            // Process each symbol
            foreach (var kvp in _symbolData)
            {
                var symbol = kvp.Key;
                var symbolData = kvp.Value;

                // Skip if indicators not ready
                if (!symbolData.FastEma.IsReady || !symbolData.SlowEma.IsReady || !symbolData.Rsi.IsReady)
                    continue;

                // Check if we have data for this symbol
                if (!data.ContainsKey(symbol))
                    continue;

                var price = data[symbol].Close;

                // Check momentum condition: Fast EMA > Slow EMA (uptrend)
                var isInUptrend = symbolData.FastEma > symbolData.SlowEma;

                // Check for pullback: RSI dropped below oversold level
                var isInPullback = symbolData.Rsi < _rsiOversoldLevel;

                // Check for pullback recovery: RSI recovering above recovery level
                var isPullbackEnding = symbolData.WasInPullback && symbolData.Rsi > _rsiRecoveryLevel;

                // Update pullback state
                if (isInPullback)
                {
                    symbolData.WasInPullback = true;
                }

                // Entry condition: Uptrend + Pullback ending
                if (isInUptrend && isPullbackEnding && !Portfolio[symbol].Invested)
                {
                    EnterPosition(symbol, price);
                    symbolData.WasInPullback = false;
                }

                // Exit condition: Momentum weakening (Fast EMA crosses below Slow EMA)
                if (Portfolio[symbol].Invested && symbolData.FastEma < symbolData.SlowEma)
                {
                    ExitPosition(symbol, "Momentum weakening");
                }
            }
        }

        /// <summary>
        /// Enters a long position with stop-loss and take-profit
        /// </summary>
        private void EnterPosition(Symbol symbol, decimal price)
        {
            // Calculate position size
            var quantity = CalculateOrderQuantity(symbol, _targetPortfolioPercent);

            if (quantity == 0)
                return;

            // Place market order
            var ticket = MarketOrder(symbol, quantity, tag: "Pullback Entry");

            if (ticket.Status == OrderStatus.Filled || ticket.Status == OrderStatus.PartiallyFilled)
            {
                // Calculate stop loss and take profit prices
                var stopLossPrice = price * (1 - _stopLossPercent);
                var takeProfitPrice = price * (1 + _takeProfitPercent);

                // Place stop loss order
                StopMarketOrder(symbol, -quantity, stopLossPrice, tag: "Stop Loss");

                // Place take profit order
                LimitOrder(symbol, -quantity, takeProfitPrice, tag: "Take Profit");

                Debug($"Entered position: {symbol} at {price}, Stop: {stopLossPrice}, Target: {takeProfitPrice}");
            }
        }

        /// <summary>
        /// Exits a position by liquidating
        /// </summary>
        private void ExitPosition(Symbol symbol, string reason)
        {
            // Cancel all open orders for this symbol
            var openOrders = Transactions.GetOpenOrders(symbol);
            foreach (var order in openOrders)
            {
                Transactions.CancelOrder(order.Id);
            }

            // Liquidate position
            Liquidate(symbol, tag: reason);

            Debug($"Exited position: {symbol}, Reason: {reason}");
        }

        /// <summary>
        /// Checks all positions and manages risk
        /// </summary>
        private void CheckPositions()
        {
            if (IsWarmingUp)
                return;

            foreach (var holding in Portfolio.Values.Where(x => x.Invested))
            {
                var symbol = holding.Symbol;

                if (!_symbolData.ContainsKey(symbol))
                    continue;

                var symbolData = _symbolData[symbol];
                var currentPrice = holding.Price;
                var entryPrice = holding.AveragePrice;
                var profitPercent = (currentPrice - entryPrice) / entryPrice;

                // Check if we've hit stop loss manually (in case order didn't fill)
                if (profitPercent <= -_stopLossPercent)
                {
                    ExitPosition(symbol, "Manual Stop Loss");
                }

                // Check if we've hit take profit manually (in case order didn't fill)
                if (profitPercent >= _takeProfitPercent)
                {
                    ExitPosition(symbol, "Manual Take Profit");
                }
            }
        }

        /// <summary>
        /// Order event handler
        /// </summary>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status == OrderStatus.Filled)
            {
                Debug($"Order filled: {orderEvent.Symbol} {orderEvent.Direction} {orderEvent.FillQuantity} @ {orderEvent.FillPrice}");
            }
            else if (orderEvent.Status == OrderStatus.Invalid)
            {
                Debug($"Order invalid: {orderEvent.Symbol} - {orderEvent.Message}");
            }
        }

        /// <summary>
        /// End of day summary
        /// </summary>
        public override void OnEndOfDay(Symbol symbol)
        {
            if (_symbolData.ContainsKey(symbol))
            {
                var data = _symbolData[symbol];
                Plot("Indicators", $"{symbol} Fast EMA", data.FastEma);
                Plot("Indicators", $"{symbol} Slow EMA", data.SlowEma);
                Plot("Indicators", $"{symbol} RSI", data.Rsi);
            }
        }

        /// <summary>
        /// Container for symbol-specific data and indicators
        /// </summary>
        private class SymbolData
        {
            public Symbol Symbol { get; set; }
            public ExponentialMovingAverage FastEma { get; set; }
            public ExponentialMovingAverage SlowEma { get; set; }
            public RelativeStrengthIndex Rsi { get; set; }
            public bool WasInPullback { get; set; }
        }
    }
}
