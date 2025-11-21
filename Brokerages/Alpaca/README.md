# Alpaca Brokerage Integration

This directory contains the implementation of the Alpaca brokerage integration for QuantConnect LEAN.

## Features

- **Full Brokerage Integration**: Support for order execution, account management, and position tracking
- **WebSocket Market Data**: Real-time market data streaming via Alpaca's SIP (Securities Information Processor) WebSocket feed
- **Supported Order Types**:
  - Market Orders
  - Limit Orders
  - Stop Market Orders
  - Stop Limit Orders
  - Trailing Stop Orders
- **Supported Securities**: Equities, Options, and Crypto

## Components

### AlpacaBrokerage.cs
Main brokerage implementation that handles:
- Order placement, updates, and cancellations
- Account holdings and cash balance retrieval
- Connection management
- Order status tracking

### AlpacaDataQueueHandler.cs
Real-time data feed implementation using WebSocket:
- Subscribes to trades, quotes, and bars
- Streams real-time market data from Alpaca SIP
- Converts Alpaca data to LEAN format

### AlpacaBrokerageFactory.cs
Factory class for creating brokerage instances:
- Reads configuration from config.json
- Creates and initializes brokerage and data handler
- Returns appropriate brokerage model

### AlpacaSymbolMapper.cs
Maps between LEAN symbols and Alpaca symbols:
- Converts equity, crypto, and option symbols
- Handles proper formatting for Alpaca API

### Api/AlpacaApiClient.cs
REST API client for Alpaca:
- Account information
- Order management
- Position tracking
- Historical data retrieval

### Api/AlpacaApiModels.cs
Data models for Alpaca API responses and requests

## Configuration

Add the following to your `config.json` file:

```json
{
  "alpaca-api-key": "YOUR_API_KEY",
  "alpaca-api-secret": "YOUR_API_SECRET",
  "alpaca-paper-trading": true,

  "live-alpaca": {
    "live-mode": true,
    "live-mode-brokerage": "AlpacaBrokerage",
    "data-queue-handler": ["AlpacaBrokerage"]
  }
}
```

## Usage

### In Your Algorithm

```csharp
public class MyAlgorithm : QCAlgorithm
{
    public override void Initialize()
    {
        // Set brokerage model to Alpaca
        SetBrokerageModel(BrokerageName.Alpaca);

        // Rest of your algorithm initialization
        AddEquity("SPY", Resolution.Minute);
    }
}
```

### Running with Alpaca

1. Set your Alpaca API credentials in `config.json`
2. Set `alpaca-paper-trading` to `true` for paper trading or `false` for live trading
3. Run your algorithm with the live-alpaca configuration

## Example Algorithm

See `/Algorithm.CSharp/AlpacaMomentumMicroPullbackAlgorithm.cs` for a complete example of a momentum-style micro pullback strategy that uses Alpaca's WebSocket data feed.

## WebSocket Data Feed

The Alpaca data queue handler connects to Alpaca's SIP WebSocket feed and streams:
- **Trades**: Real-time trade executions
- **Quotes**: Real-time bid/ask quotes
- **Bars**: Real-time minute bars

All data is automatically converted to LEAN's `Tick` and `TradeBar` format for seamless integration.

## Notes

- Alpaca accounts are set up as margin accounts by default
- The brokerage model enforces Alpaca's order type restrictions per security type
- Extended hours trading is supported for limit orders with day time-in-force
- WebSocket reconnection is not yet implemented (future enhancement)

## API Documentation

For more information about Alpaca's API:
- REST API: https://docs.alpaca.markets/docs/trading-api
- WebSocket Data: https://docs.alpaca.markets/docs/real-time-stock-pricing-data
- Market Data SIP: https://docs.alpaca.markets/docs/about-market-data-api

## License

Licensed under the Apache License, Version 2.0. See LICENSE file for details.
