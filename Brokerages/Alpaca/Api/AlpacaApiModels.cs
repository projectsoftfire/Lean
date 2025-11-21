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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Brokerages.Alpaca.Api
{
    /// <summary>
    /// Represents an Alpaca account
    /// </summary>
    public class AlpacaAccount
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("account_number")]
        public string AccountNumber { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("cash")]
        public decimal Cash { get; set; }

        [JsonProperty("buying_power")]
        public decimal BuyingPower { get; set; }

        [JsonProperty("portfolio_value")]
        public decimal PortfolioValue { get; set; }

        [JsonProperty("equity")]
        public decimal Equity { get; set; }

        [JsonProperty("initial_margin")]
        public decimal InitialMargin { get; set; }

        [JsonProperty("maintenance_margin")]
        public decimal MaintenanceMargin { get; set; }
    }

    /// <summary>
    /// Represents an Alpaca position
    /// </summary>
    public class AlpacaPosition
    {
        [JsonProperty("asset_id")]
        public string AssetId { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("exchange")]
        public string Exchange { get; set; }

        [JsonProperty("asset_class")]
        public string AssetClass { get; set; }

        [JsonProperty("avg_entry_price")]
        public decimal AverageEntryPrice { get; set; }

        [JsonProperty("qty")]
        public decimal Quantity { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("market_value")]
        public decimal MarketValue { get; set; }

        [JsonProperty("cost_basis")]
        public decimal CostBasis { get; set; }

        [JsonProperty("unrealized_pl")]
        public decimal UnrealizedProfitLoss { get; set; }

        [JsonProperty("unrealized_plpc")]
        public decimal UnrealizedProfitLossPercent { get; set; }

        [JsonProperty("current_price")]
        public decimal CurrentPrice { get; set; }
    }

    /// <summary>
    /// Represents an Alpaca order
    /// </summary>
    public class AlpacaOrder
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("client_order_id")]
        public string ClientOrderId { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [JsonProperty("submitted_at")]
        public DateTime? SubmittedAt { get; set; }

        [JsonProperty("filled_at")]
        public DateTime? FilledAt { get; set; }

        [JsonProperty("expired_at")]
        public DateTime? ExpiredAt { get; set; }

        [JsonProperty("canceled_at")]
        public DateTime? CanceledAt { get; set; }

        [JsonProperty("failed_at")]
        public DateTime? FailedAt { get; set; }

        [JsonProperty("asset_id")]
        public string AssetId { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("asset_class")]
        public string AssetClass { get; set; }

        [JsonProperty("qty")]
        public decimal? Quantity { get; set; }

        [JsonProperty("notional")]
        public decimal? Notional { get; set; }

        [JsonProperty("filled_qty")]
        public decimal FilledQuantity { get; set; }

        [JsonProperty("filled_avg_price")]
        public decimal? FilledAveragePrice { get; set; }

        [JsonProperty("order_type")]
        public string OrderType { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("time_in_force")]
        public string TimeInForce { get; set; }

        [JsonProperty("limit_price")]
        public decimal? LimitPrice { get; set; }

        [JsonProperty("stop_price")]
        public decimal? StopPrice { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("extended_hours")]
        public bool ExtendedHours { get; set; }

        [JsonProperty("trail_percent")]
        public decimal? TrailPercent { get; set; }

        [JsonProperty("trail_price")]
        public decimal? TrailPrice { get; set; }
    }

    /// <summary>
    /// Request to place an order with Alpaca
    /// </summary>
    public class AlpacaPlaceOrderRequest
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("qty")]
        public decimal? Quantity { get; set; }

        [JsonProperty("notional")]
        public decimal? Notional { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("time_in_force")]
        public string TimeInForce { get; set; }

        [JsonProperty("limit_price")]
        public decimal? LimitPrice { get; set; }

        [JsonProperty("stop_price")]
        public decimal? StopPrice { get; set; }

        [JsonProperty("trail_price")]
        public decimal? TrailPrice { get; set; }

        [JsonProperty("trail_percent")]
        public decimal? TrailPercent { get; set; }

        [JsonProperty("extended_hours")]
        public bool? ExtendedHours { get; set; }

        [JsonProperty("client_order_id")]
        public string ClientOrderId { get; set; }

        [JsonProperty("order_class")]
        public string OrderClass { get; set; }
    }

    /// <summary>
    /// Alpaca market data trade
    /// </summary>
    public class AlpacaTrade
    {
        [JsonProperty("t")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("p")]
        public decimal Price { get; set; }

        [JsonProperty("s")]
        public long Size { get; set; }

        [JsonProperty("x")]
        public string Exchange { get; set; }

        [JsonProperty("c")]
        public List<string> Conditions { get; set; }
    }

    /// <summary>
    /// Alpaca market data quote
    /// </summary>
    public class AlpacaQuote
    {
        [JsonProperty("t")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("bp")]
        public decimal BidPrice { get; set; }

        [JsonProperty("bs")]
        public long BidSize { get; set; }

        [JsonProperty("ap")]
        public decimal AskPrice { get; set; }

        [JsonProperty("as")]
        public long AskSize { get; set; }

        [JsonProperty("bx")]
        public string BidExchange { get; set; }

        [JsonProperty("ax")]
        public string AskExchange { get; set; }

        [JsonProperty("c")]
        public List<string> Conditions { get; set; }
    }

    /// <summary>
    /// Alpaca market data bar (candle)
    /// </summary>
    public class AlpacaBar
    {
        [JsonProperty("t")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("o")]
        public decimal Open { get; set; }

        [JsonProperty("h")]
        public decimal High { get; set; }

        [JsonProperty("l")]
        public decimal Low { get; set; }

        [JsonProperty("c")]
        public decimal Close { get; set; }

        [JsonProperty("v")]
        public long Volume { get; set; }

        [JsonProperty("n")]
        public long? TradeCount { get; set; }

        [JsonProperty("vw")]
        public decimal? VolumeWeightedAverage { get; set; }
    }
}
