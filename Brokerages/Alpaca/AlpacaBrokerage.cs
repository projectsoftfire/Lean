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
using System.Threading;
using QuantConnect.Brokerages.Alpaca.Api;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Alpaca
{
    /// <summary>
    /// Alpaca brokerage implementation
    /// </summary>
    [BrokerageFactory(typeof(AlpacaBrokerageFactory))]
    public class AlpacaBrokerage : Brokerage
    {
        private readonly AlpacaApiClient _apiClient;
        private readonly AlpacaSymbolMapper _symbolMapper;
        private readonly ISecurityProvider _securityProvider;
        private readonly IAlgorithm _algorithm;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;

        /// <summary>
        /// Creates a new AlpacaBrokerage instance
        /// </summary>
        /// <param name="apiKey">Alpaca API key</param>
        /// <param name="apiSecret">Alpaca API secret</param>
        /// <param name="isPaperTrading">Whether to use paper trading</param>
        /// <param name="algorithm">The algorithm instance</param>
        public AlpacaBrokerage(string apiKey, string apiSecret, bool isPaperTrading, IAlgorithm algorithm)
            : base("Alpaca")
        {
            _apiClient = new AlpacaApiClient(apiKey, apiSecret, isPaperTrading);
            _symbolMapper = new AlpacaSymbolMapper();
            _algorithm = algorithm;
            _securityProvider = algorithm?.Portfolio;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Returns true if connected to the brokerage
        /// </summary>
        public override bool IsConnected => _isConnected;

        /// <summary>
        /// Connects to the brokerage
        /// </summary>
        public override void Connect()
        {
            if (IsConnected)
                return;

            try
            {
                // Test connection by fetching account
                var account = _apiClient.GetAccountAsync(_cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();
                _isConnected = true;
                Log.Trace($"AlpacaBrokerage.Connect(): Connected successfully. Account: {account.AccountNumber}");
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.Connect(): Error connecting to Alpaca: {ex.Message}");
                _isConnected = false;
                throw;
            }
        }

        /// <summary>
        /// Disconnects from the brokerage
        /// </summary>
        public override void Disconnect()
        {
            _isConnected = false;
            _cancellationTokenSource.Cancel();
            Log.Trace("AlpacaBrokerage.Disconnect(): Disconnected");
        }

        /// <summary>
        /// Gets all open orders
        /// </summary>
        public override List<Order> GetOpenOrders()
        {
            var openOrders = new List<Order>();

            try
            {
                var alpacaOrders = _apiClient.GetOrdersAsync("open", _cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();

                foreach (var alpacaOrder in alpacaOrders)
                {
                    var order = ConvertAlpacaOrderToLeanOrder(alpacaOrder);
                    if (order != null)
                    {
                        openOrders.Add(order);
                    }
                }

                Log.Trace($"AlpacaBrokerage.GetOpenOrders(): Retrieved {openOrders.Count} open orders");
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.GetOpenOrders(): Error retrieving open orders: {ex.Message}");
            }

            return openOrders;
        }

        /// <summary>
        /// Gets all account holdings
        /// </summary>
        public override List<Holding> GetAccountHoldings()
        {
            var holdings = new List<Holding>();

            try
            {
                var positions = _apiClient.GetPositionsAsync(_cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();

                foreach (var position in positions)
                {
                    var securityType = position.AssetClass.Equals("us_equity", StringComparison.OrdinalIgnoreCase)
                        ? SecurityType.Equity
                        : position.AssetClass.Equals("crypto", StringComparison.OrdinalIgnoreCase)
                            ? SecurityType.Crypto
                            : SecurityType.Equity;

                    var symbol = _symbolMapper.GetLeanSymbol(position.Symbol, securityType, Market.USA);

                    holdings.Add(new Holding
                    {
                        Symbol = symbol,
                        Type = securityType,
                        AveragePrice = position.AverageEntryPrice,
                        Quantity = position.Quantity,
                        MarketPrice = position.CurrentPrice,
                        CurrencySymbol = Currencies.USD
                    });
                }

                Log.Trace($"AlpacaBrokerage.GetAccountHoldings(): Retrieved {holdings.Count} holdings");
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.GetAccountHoldings(): Error retrieving holdings: {ex.Message}");
            }

            return holdings;
        }

        /// <summary>
        /// Gets current cash balance
        /// </summary>
        public override List<CashAmount> GetCashBalance()
        {
            try
            {
                var account = _apiClient.GetAccountAsync(_cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();

                return new List<CashAmount>
                {
                    new CashAmount(account.Cash, account.Currency ?? Currencies.USD)
                };
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.GetCashBalance(): Error retrieving cash balance: {ex.Message}");
                return new List<CashAmount>();
            }
        }

        /// <summary>
        /// Places an order
        /// </summary>
        public override bool PlaceOrder(Order order)
        {
            try
            {
                var request = ConvertLeanOrderToAlpacaRequest(order);
                var alpacaOrder = _apiClient.PlaceOrderAsync(request, _cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();

                // Update the brokerage order ID
                order.BrokerId.Add(alpacaOrder.Id);

                // Fire order submitted event
                OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                {
                    Status = OrderStatus.Submitted
                });

                Log.Trace($"AlpacaBrokerage.PlaceOrder(): Order placed successfully. Lean ID: {order.Id}, Alpaca ID: {alpacaOrder.Id}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.PlaceOrder(): Error placing order {order.Id}: {ex.Message}");
                OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                {
                    Status = OrderStatus.Invalid,
                    Message = ex.Message
                });
                return false;
            }
        }

        /// <summary>
        /// Updates an order
        /// </summary>
        public override bool UpdateOrder(Order order)
        {
            try
            {
                if (!order.BrokerId.Any())
                {
                    Log.Error($"AlpacaBrokerage.UpdateOrder(): Order {order.Id} has no brokerage ID");
                    return false;
                }

                var brokerId = order.BrokerId.First();
                decimal? limitPrice = null;
                decimal? stopPrice = null;

                if (order is LimitOrder limitOrder)
                {
                    limitPrice = limitOrder.LimitPrice;
                }
                else if (order is StopMarketOrder stopMarketOrder)
                {
                    stopPrice = stopMarketOrder.StopPrice;
                }
                else if (order is StopLimitOrder stopLimitOrder)
                {
                    limitPrice = stopLimitOrder.LimitPrice;
                    stopPrice = stopLimitOrder.StopPrice;
                }

                var alpacaOrder = _apiClient.UpdateOrderAsync(brokerId, order.Quantity, limitPrice, stopPrice,
                    null, _cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();

                Log.Trace($"AlpacaBrokerage.UpdateOrder(): Order updated successfully. Lean ID: {order.Id}, Alpaca ID: {brokerId}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.UpdateOrder(): Error updating order {order.Id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cancels an order
        /// </summary>
        public override bool CancelOrder(Order order)
        {
            try
            {
                if (!order.BrokerId.Any())
                {
                    Log.Error($"AlpacaBrokerage.CancelOrder(): Order {order.Id} has no brokerage ID");
                    return false;
                }

                var brokerId = order.BrokerId.First();
                var success = _apiClient.CancelOrderAsync(brokerId, _cancellationTokenSource.Token).SynchronouslyAwaitTaskResult();

                if (success)
                {
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero)
                    {
                        Status = OrderStatus.Canceled
                    });

                    Log.Trace($"AlpacaBrokerage.CancelOrder(): Order canceled successfully. Lean ID: {order.Id}, Alpaca ID: {brokerId}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.CancelOrder(): Error canceling order {order.Id}: {ex.Message}");
                return false;
            }
        }

        private AlpacaPlaceOrderRequest ConvertLeanOrderToAlpacaRequest(Order order)
        {
            var symbol = _symbolMapper.GetBrokerageSymbol(order.Symbol);
            var request = new AlpacaPlaceOrderRequest
            {
                Symbol = symbol,
                Quantity = Math.Abs(order.Quantity),
                Side = order.Quantity > 0 ? "buy" : "sell",
                TimeInForce = ConvertTimeInForce(order.TimeInForce),
                ClientOrderId = order.Id.ToStringInvariant(),
                OrderClass = "simple"
            };

            // Set order type and prices
            switch (order.Type)
            {
                case OrderType.Market:
                    request.Type = "market";
                    break;

                case OrderType.Limit:
                    request.Type = "limit";
                    request.LimitPrice = ((LimitOrder)order).LimitPrice;
                    break;

                case OrderType.StopMarket:
                    request.Type = "stop";
                    request.StopPrice = ((StopMarketOrder)order).StopPrice;
                    break;

                case OrderType.StopLimit:
                    request.Type = "stop_limit";
                    var stopLimitOrder = (StopLimitOrder)order;
                    request.LimitPrice = stopLimitOrder.LimitPrice;
                    request.StopPrice = stopLimitOrder.StopPrice;
                    break;

                case OrderType.TrailingStop:
                    request.Type = "trailing_stop";
                    var trailingStopOrder = (TrailingStopOrder)order;
                    if (trailingStopOrder.TrailingAsPercentage)
                    {
                        request.TrailPercent = trailingStopOrder.TrailingAmount * 100;
                    }
                    else
                    {
                        request.TrailPrice = trailingStopOrder.TrailingAmount;
                    }
                    break;

                default:
                    throw new NotSupportedException($"Order type {order.Type} is not supported by Alpaca");
            }

            // Check for extended hours
            if (order.Properties is AlpacaOrderProperties alpacaProperties)
            {
                request.ExtendedHours = alpacaProperties.OutsideRegularTradingHours;
            }

            return request;
        }

        private string ConvertTimeInForce(Orders.TimeInForces.TimeInForce timeInForce)
        {
            if (timeInForce is Orders.TimeInForces.DayTimeInForce)
                return "day";
            if (timeInForce is Orders.TimeInForces.GoodTilCanceledTimeInForce)
                return "gtc";
            if (timeInForce is Orders.TimeInForces.ImmediateOrCancelTimeInForce)
                return "ioc";
            if (timeInForce is Orders.TimeInForces.FillOrKillTimeInForce)
                return "fok";

            return "day"; // default
        }

        private Order ConvertAlpacaOrderToLeanOrder(AlpacaOrder alpacaOrder)
        {
            try
            {
                var securityType = alpacaOrder.AssetClass.Equals("us_equity", StringComparison.OrdinalIgnoreCase)
                    ? SecurityType.Equity
                    : alpacaOrder.AssetClass.Equals("crypto", StringComparison.OrdinalIgnoreCase)
                        ? SecurityType.Crypto
                        : SecurityType.Equity;

                var symbol = _symbolMapper.GetLeanSymbol(alpacaOrder.Symbol, securityType, Market.USA);
                var quantity = alpacaOrder.Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
                    ? alpacaOrder.Quantity ?? 0
                    : -(alpacaOrder.Quantity ?? 0);

                Order order = null;

                switch (alpacaOrder.OrderType.ToLowerInvariant())
                {
                    case "market":
                        order = new MarketOrder(symbol, quantity, DateTime.UtcNow);
                        break;

                    case "limit":
                        order = new LimitOrder(symbol, quantity, alpacaOrder.LimitPrice ?? 0, DateTime.UtcNow);
                        break;

                    case "stop":
                        order = new StopMarketOrder(symbol, quantity, alpacaOrder.StopPrice ?? 0, DateTime.UtcNow);
                        break;

                    case "stop_limit":
                        order = new StopLimitOrder(symbol, quantity, alpacaOrder.StopPrice ?? 0,
                            alpacaOrder.LimitPrice ?? 0, DateTime.UtcNow);
                        break;
                }

                if (order != null)
                {
                    order.BrokerId.Add(alpacaOrder.Id);
                    order.Status = ConvertAlpacaOrderStatus(alpacaOrder.Status);
                }

                return order;
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaBrokerage.ConvertAlpacaOrderToLeanOrder(): Error converting order: {ex.Message}");
                return null;
            }
        }

        private OrderStatus ConvertAlpacaOrderStatus(string status)
        {
            switch (status.ToLowerInvariant())
            {
                case "new":
                case "accepted":
                case "pending_new":
                    return OrderStatus.Submitted;

                case "partially_filled":
                    return OrderStatus.PartiallyFilled;

                case "filled":
                    return OrderStatus.Filled;

                case "canceled":
                case "pending_cancel":
                    return OrderStatus.Canceled;

                case "expired":
                    return OrderStatus.Canceled;

                case "rejected":
                case "suspended":
                    return OrderStatus.Invalid;

                default:
                    return OrderStatus.None;
            }
        }

        /// <summary>
        /// Dispose of the brokerage instance
        /// </summary>
        public override void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _apiClient?.Dispose();
        }
    }
}
