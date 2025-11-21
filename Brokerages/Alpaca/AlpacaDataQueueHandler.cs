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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Packets;

namespace QuantConnect.Brokerages.Alpaca
{
    /// <summary>
    /// Alpaca data queue handler for WebSocket streaming market data
    /// </summary>
    public class AlpacaDataQueueHandler : IDataQueueHandler
    {
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly AlpacaSymbolMapper _symbolMapper;
        private readonly ConcurrentDictionary<Symbol, SubscriptionDataConfig> _subscriptions;
        private readonly ConcurrentQueue<BaseData> _dataQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private ClientWebSocket _webSocket;
        private Task _connectionTask;
        private bool _isConnected;

        // Alpaca WebSocket URLs
        private const string LiveDataStreamUrl = "wss://stream.data.alpaca.markets/v2/sip";
        private const string PaperDataStreamUrl = "wss://stream.data.alpaca.markets/v2/sip"; // Same for paper

        /// <summary>
        /// Creates a new instance of AlpacaDataQueueHandler
        /// </summary>
        public AlpacaDataQueueHandler(string apiKey, string apiSecret)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _symbolMapper = new AlpacaSymbolMapper();
            _subscriptions = new ConcurrentDictionary<Symbol, SubscriptionDataConfig>();
            _dataQueue = new ConcurrentQueue<BaseData>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Returns whether the data provider is connected
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!_subscriptions.ContainsKey(dataConfig.Symbol))
            {
                _subscriptions[dataConfig.Symbol] = dataConfig;

                // Send subscription message if connected
                if (_isConnected)
                {
                    SubscribeSymbol(dataConfig.Symbol);
                }

                Log.Trace($"AlpacaDataQueueHandler.Subscribe(): Subscribed to {dataConfig.Symbol}");
            }

            // Return enumerator that yields data from the queue
            return GetNextTicksEnumerator(dataConfig.Symbol, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            if (_subscriptions.TryRemove(dataConfig.Symbol, out _))
            {
                if (_isConnected)
                {
                    UnsubscribeSymbol(dataConfig.Symbol);
                }

                Log.Trace($"AlpacaDataQueueHandler.Unsubscribe(): Unsubscribed from {dataConfig.Symbol}");
            }
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        public void SetJob(LiveNodePacket job)
        {
            // Start WebSocket connection
            _connectionTask = Task.Run(async () => await ConnectWebSocketAsync(), _cancellationTokenSource.Token);
        }

        private async Task ConnectWebSocketAsync()
        {
            try
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(LiveDataStreamUrl), _cancellationTokenSource.Token);

                _isConnected = true;
                Log.Trace("AlpacaDataQueueHandler.ConnectWebSocketAsync(): WebSocket connected");

                // Authenticate
                var authMessage = new
                {
                    action = "auth",
                    key = _apiKey,
                    secret = _apiSecret
                };

                await SendMessageAsync(authMessage);

                // Subscribe to existing symbols
                foreach (var symbol in _subscriptions.Keys)
                {
                    SubscribeSymbol(symbol);
                }

                // Start receiving messages
                await ReceiveMessagesAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.ConnectWebSocketAsync(): Error: {ex.Message}");
                _isConnected = false;
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            var buffer = new byte[8192];

            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        _isConnected = false;
                        Log.Trace("AlpacaDataQueueHandler.ReceiveMessagesAsync(): WebSocket closed");
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(message);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.ReceiveMessagesAsync(): Error: {ex.Message}");
                _isConnected = false;
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                var messages = JsonConvert.DeserializeObject<List<JObject>>(message);

                foreach (var msg in messages)
                {
                    var messageType = msg["T"]?.ToString();

                    switch (messageType)
                    {
                        case "t": // Trade
                            ProcessTrade(msg);
                            break;

                        case "q": // Quote
                            ProcessQuote(msg);
                            break;

                        case "b": // Bar (minute)
                            ProcessBar(msg);
                            break;

                        case "success":
                            Log.Trace($"AlpacaDataQueueHandler: {msg["msg"]}");
                            break;

                        case "error":
                            Log.Error($"AlpacaDataQueueHandler: {msg["msg"]}");
                            break;

                        case "subscription":
                            Log.Trace($"AlpacaDataQueueHandler: Subscription confirmed");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.ProcessMessage(): Error processing message: {ex.Message}");
            }
        }

        private void ProcessTrade(JObject msg)
        {
            try
            {
                var ticker = msg["S"]?.ToString();
                var symbol = _symbolMapper.GetLeanSymbol(ticker, SecurityType.Equity, Market.USA);

                var trade = new Tick
                {
                    Symbol = symbol,
                    Time = msg["t"].Value<DateTime>(),
                    Value = msg["p"].Value<decimal>(),
                    Quantity = msg["s"].Value<decimal>(),
                    TickType = TickType.Trade,
                    Exchange = msg["x"]?.ToString() ?? string.Empty
                };

                _dataQueue.Enqueue(trade);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.ProcessTrade(): Error: {ex.Message}");
            }
        }

        private void ProcessQuote(JObject msg)
        {
            try
            {
                var ticker = msg["S"]?.ToString();
                var symbol = _symbolMapper.GetLeanSymbol(ticker, SecurityType.Equity, Market.USA);

                var bidTick = new Tick
                {
                    Symbol = symbol,
                    Time = msg["t"].Value<DateTime>(),
                    BidPrice = msg["bp"].Value<decimal>(),
                    BidSize = msg["bs"].Value<decimal>(),
                    TickType = TickType.Quote
                };

                var askTick = new Tick
                {
                    Symbol = symbol,
                    Time = msg["t"].Value<DateTime>(),
                    AskPrice = msg["ap"].Value<decimal>(),
                    AskSize = msg["as"].Value<decimal>(),
                    TickType = TickType.Quote
                };

                _dataQueue.Enqueue(bidTick);
                _dataQueue.Enqueue(askTick);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.ProcessQuote(): Error: {ex.Message}");
            }
        }

        private void ProcessBar(JObject msg)
        {
            try
            {
                var ticker = msg["S"]?.ToString();
                var symbol = _symbolMapper.GetLeanSymbol(ticker, SecurityType.Equity, Market.USA);

                var bar = new TradeBar
                {
                    Symbol = symbol,
                    Time = msg["t"].Value<DateTime>(),
                    Open = msg["o"].Value<decimal>(),
                    High = msg["h"].Value<decimal>(),
                    Low = msg["l"].Value<decimal>(),
                    Close = msg["c"].Value<decimal>(),
                    Volume = msg["v"].Value<decimal>()
                };

                _dataQueue.Enqueue(bar);
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.ProcessBar(): Error: {ex.Message}");
            }
        }

        private void SubscribeSymbol(Symbol symbol)
        {
            var ticker = _symbolMapper.GetBrokerageSymbol(symbol);

            var subscribeMessage = new
            {
                action = "subscribe",
                trades = new[] { ticker },
                quotes = new[] { ticker },
                bars = new[] { ticker }
            };

            Task.Run(async () => await SendMessageAsync(subscribeMessage));
        }

        private void UnsubscribeSymbol(Symbol symbol)
        {
            var ticker = _symbolMapper.GetBrokerageSymbol(symbol);

            var unsubscribeMessage = new
            {
                action = "unsubscribe",
                trades = new[] { ticker },
                quotes = new[] { ticker },
                bars = new[] { ticker }
            };

            Task.Run(async () => await SendMessageAsync(unsubscribeMessage));
        }

        private async Task SendMessageAsync(object message)
        {
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    var json = JsonConvert.SerializeObject(message);
                    var buffer = Encoding.UTF8.GetBytes(json);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AlpacaDataQueueHandler.SendMessageAsync(): Error: {ex.Message}");
            }
        }

        private IEnumerator<BaseData> GetNextTicksEnumerator(Symbol symbol, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_dataQueue.TryDequeue(out var data))
                {
                    if (data.Symbol == symbol)
                    {
                        yield return data;
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Dispose of the data queue handler
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
        }
    }
}
