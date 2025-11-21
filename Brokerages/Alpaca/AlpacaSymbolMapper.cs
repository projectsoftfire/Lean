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
using QuantConnect.Securities;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Alpaca
{
    /// <summary>
    /// Provides the mapping between Lean symbols and Alpaca symbols
    /// </summary>
    public class AlpacaSymbolMapper : ISymbolMapper
    {
        /// <summary>
        /// Converts a Lean symbol instance to an Alpaca symbol
        /// </summary>
        /// <param name="symbol">A Lean symbol instance</param>
        /// <returns>The Alpaca symbol</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value))
                throw new ArgumentException("Invalid symbol");

            if (symbol.SecurityType == SecurityType.Equity)
            {
                return symbol.Value.ToUpperInvariant();
            }
            else if (symbol.SecurityType == SecurityType.Crypto)
            {
                // Alpaca uses format like "BTCUSD" for crypto
                var ticker = symbol.Value.Replace("/", "").ToUpperInvariant();
                return ticker;
            }
            else if (symbol.SecurityType == SecurityType.Option)
            {
                // Alpaca option format: Underlying symbol + expiration (YYMMDD) + Option Type (C/P) + Strike
                // Example: SPY230120C00400000 (SPY Call expiring Jan 20, 2023, strike 400)
                var underlying = symbol.Underlying.Value.ToUpperInvariant();
                var expiration = symbol.ID.Date.ToString("yyMMdd");
                var optionType = symbol.ID.OptionRight == OptionRight.Call ? "C" : "P";
                var strike = ((int)(symbol.ID.StrikePrice * 1000)).ToString("D8");

                return $"{underlying}{expiration}{optionType}{strike}";
            }

            throw new NotSupportedException($"Alpaca does not support security type: {symbol.SecurityType}");
        }

        /// <summary>
        /// Converts an Alpaca symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The Alpaca symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market the security belongs to</param>
        /// <param name="expirationDate">Expiration date of the security (if applicable)</param>
        /// <param name="strike">The strike of the option (if applicable)</param>
        /// <param name="optionRight">The option right of the option (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market,
            DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException("Invalid Alpaca symbol");

            if (securityType == SecurityType.Equity)
            {
                return Symbol.Create(brokerageSymbol, SecurityType.Equity, Market.USA);
            }
            else if (securityType == SecurityType.Crypto)
            {
                // Convert BTCUSD to BTC/USD
                if (brokerageSymbol.EndsWith("USD"))
                {
                    var baseCurrency = brokerageSymbol.Substring(0, brokerageSymbol.Length - 3);
                    return Symbol.Create($"{baseCurrency}/USD", SecurityType.Crypto, Market.USA);
                }
                return Symbol.Create(brokerageSymbol, SecurityType.Crypto, Market.USA);
            }

            throw new NotSupportedException($"Alpaca symbol mapper does not support security type: {securityType}");
        }
    }
}
