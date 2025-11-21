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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Alpaca.Api
{
    /// <summary>
    /// Alpaca REST API client
    /// </summary>
    public class AlpacaApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _tradingBaseUrl;
        private readonly string _dataBaseUrl;

        private const string LiveTradingUrl = "https://api.alpaca.markets";
        private const string PaperTradingUrl = "https://paper-api.alpaca.markets";
        private const string DataUrl = "https://data.alpaca.markets";

        /// <summary>
        /// Creates a new Alpaca API client
        /// </summary>
        /// <param name="apiKey">Alpaca API key</param>
        /// <param name="apiSecret">Alpaca API secret</param>
        /// <param name="isPaperTrading">Whether to use paper trading endpoint</param>
        public AlpacaApiClient(string apiKey, string apiSecret, bool isPaperTrading)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _tradingBaseUrl = isPaperTrading ? PaperTradingUrl : LiveTradingUrl;
            _dataBaseUrl = DataUrl;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Add("APCA-API-KEY-ID", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", _apiSecret);
        }

        /// <summary>
        /// Gets account information
        /// </summary>
        public async Task<AlpacaAccount> GetAccountAsync(CancellationToken cancellationToken = default)
        {
            return await GetAsync<AlpacaAccount>($"{_tradingBaseUrl}/v2/account", cancellationToken);
        }

        /// <summary>
        /// Gets all open positions
        /// </summary>
        public async Task<List<AlpacaPosition>> GetPositionsAsync(CancellationToken cancellationToken = default)
        {
            return await GetAsync<List<AlpacaPosition>>($"{_tradingBaseUrl}/v2/positions", cancellationToken);
        }

        /// <summary>
        /// Gets all orders
        /// </summary>
        public async Task<List<AlpacaOrder>> GetOrdersAsync(string status = "open", CancellationToken cancellationToken = default)
        {
            var url = $"{_tradingBaseUrl}/v2/orders?status={status}&limit=500";
            return await GetAsync<List<AlpacaOrder>>(url, cancellationToken);
        }

        /// <summary>
        /// Gets a specific order by ID
        /// </summary>
        public async Task<AlpacaOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            return await GetAsync<AlpacaOrder>($"{_tradingBaseUrl}/v2/orders/{orderId}", cancellationToken);
        }

        /// <summary>
        /// Places a new order
        /// </summary>
        public async Task<AlpacaOrder> PlaceOrderAsync(AlpacaPlaceOrderRequest request, CancellationToken cancellationToken = default)
        {
            return await PostAsync<AlpacaOrder>($"{_tradingBaseUrl}/v2/orders", request, cancellationToken);
        }

        /// <summary>
        /// Updates an existing order
        /// </summary>
        public async Task<AlpacaOrder> UpdateOrderAsync(string orderId, decimal? quantity = null, decimal? limitPrice = null,
            decimal? stopPrice = null, string timeInForce = null, CancellationToken cancellationToken = default)
        {
            var request = new
            {
                qty = quantity,
                limit_price = limitPrice,
                stop_price = stopPrice,
                time_in_force = timeInForce
            };

            return await PatchAsync<AlpacaOrder>($"{_tradingBaseUrl}/v2/orders/{orderId}", request, cancellationToken);
        }

        /// <summary>
        /// Cancels an order
        /// </summary>
        public async Task<bool> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                await DeleteAsync($"{_tradingBaseUrl}/v2/orders/{orderId}", cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaApiClient.CancelOrderAsync(): Error canceling order {orderId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets historical bars
        /// </summary>
        public async Task<Dictionary<string, List<AlpacaBar>>> GetBarsAsync(string[] symbols, string timeframe,
            DateTime? start = null, DateTime? end = null, CancellationToken cancellationToken = default)
        {
            var symbolsParam = string.Join(",", symbols);
            var url = $"{_dataBaseUrl}/v2/stocks/bars?symbols={symbolsParam}&timeframe={timeframe}";

            if (start.HasValue)
                url += $"&start={start.Value:yyyy-MM-ddTHH:mm:ssZ}";
            if (end.HasValue)
                url += $"&end={end.Value:yyyy-MM-ddTHH:mm:ssZ}";

            var response = await GetAsync<Dictionary<string, object>>(url, cancellationToken);

            if (response.TryGetValue("bars", out var barsObj))
            {
                var barsJson = JsonConvert.SerializeObject(barsObj);
                return JsonConvert.DeserializeObject<Dictionary<string, List<AlpacaBar>>>(barsJson);
            }

            return new Dictionary<string, List<AlpacaBar>>();
        }

        private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response);
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(content);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaApiClient.GetAsync(): Error making GET request to {url}: {ex.Message}");
                throw;
            }
        }

        private async Task<T> PostAsync<T>(string url, object data, CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response);
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaApiClient.PostAsync(): Error making POST request to {url}: {ex.Message}");
                throw;
            }
        }

        private async Task<T> PatchAsync<T>(string url, object data, CancellationToken cancellationToken)
        {
            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
                var response = await _httpClient.SendAsync(request, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response);
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaApiClient.PatchAsync(): Error making PATCH request to {url}: {ex.Message}");
                throw;
            }
        }

        private async Task DeleteAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(url, cancellationToken);
                await EnsureSuccessStatusCodeAsync(response);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaApiClient.DeleteAsync(): Error making DELETE request to {url}: {ex.Message}");
                throw;
            }
        }

        private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Alpaca API request failed with status {response.StatusCode}: {content}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
