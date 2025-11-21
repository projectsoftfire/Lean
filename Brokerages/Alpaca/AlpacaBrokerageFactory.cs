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
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Alpaca
{
    /// <summary>
    /// Factory for creating Alpaca brokerage instances
    /// </summary>
    public class AlpacaBrokerageFactory : BrokerageFactory
    {
        /// <summary>
        /// Creates a new instance of AlpacaBrokerageFactory
        /// </summary>
        public AlpacaBrokerageFactory() : base(typeof(AlpacaBrokerage))
        {
        }

        /// <summary>
        /// Gets the brokerage data required to run the brokerage from configuration
        /// </summary>
        public override Dictionary<string, string> BrokerageData
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "alpaca-api-key", Config.Get("alpaca-api-key") },
                    { "alpaca-api-secret", Config.Get("alpaca-api-secret") },
                    { "alpaca-paper-trading", Config.Get("alpaca-paper-trading", "true") }
                };
            }
        }

        /// <summary>
        /// Gets a brokerage model for Alpaca
        /// </summary>
        public override IBrokerageModel GetBrokerageModel(IOrderProvider orderProvider)
        {
            return new AlpacaBrokerageModel();
        }

        /// <summary>
        /// Creates a new IBrokerage instance and wrapper for Alpaca
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();

            var apiKey = Read<string>(job.BrokerageData, "alpaca-api-key", errors);
            var apiSecret = Read<string>(job.BrokerageData, "alpaca-api-secret", errors);
            var isPaperTrading = Read<bool>(job.BrokerageData, "alpaca-paper-trading", errors);

            if (errors.Count > 0)
            {
                throw new ArgumentException($"AlpacaBrokerageFactory.CreateBrokerage(): {string.Join(Environment.NewLine, errors)}");
            }

            var brokerage = new AlpacaBrokerage(apiKey, apiSecret, isPaperTrading, algorithm);

            // Set up data queue handler
            Composer.Instance.AddPart<IDataQueueHandler>(new AlpacaDataQueueHandler(apiKey, apiSecret));

            return brokerage;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
        /// </summary>
        public override void Dispose()
        {
            // Nothing to dispose
        }
    }
}
