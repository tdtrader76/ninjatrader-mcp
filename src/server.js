#!/usr/bin/env node

/**
 * NinjaTrader MCP Server
 * Provides Claude Code with tools to interact with NinjaTrader trading platform
 * 
 * Supports two modes:
 * - local: Connects to NinjaTrader Desktop running locally with Add-On
 * - cloud: Uses NinjaTrader Ecosystem API
 */

const readline = require('readline');
const NinjaTraderAPI = require('./api');

const api = new NinjaTraderAPI();

// MCP Protocol helpers
const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false
});

let messageBuffer = '';

rl.on('line', (line) => {
    if (line.trim() === '') return;
    messageBuffer += line;
    
    try {
        const message = JSON.parse(messageBuffer);
        messageBuffer = '';
        handleMessage(message);
    } catch (e) {
        // Continue buffering
    }
});

async function handleMessage(message) {
    const { method, id, params } = message;

    switch (method) {
        case 'initialize':
            sendResponse(id, {
                protocolVersion: '2024-11-05',
                capabilities: { tools: {} },
                serverInfo: { name: 'ninjatrader-mcp', version: '1.0.0' }
            });
            break;

        case 'tools/list':
            sendResponse(id, { tools: TOOL_DEFINITIONS });
            break;

        case 'tools/call':
            await handleToolCall(id, params);
            break;

        case 'notifications/initialized':
            break;

        default:
            console.error('Unknown method:', method);
    }
}

async function handleToolCall(id, params) {
    const { name, arguments: args } = params;
    
    try {
        let result;
        
        switch (name) {
            // ========== ACCOUNT TOOLS ==========
            case 'nt_accounts':
                result = await api.getAccounts();
                break;

            case 'nt_account_info':
                result = await api.getAccountInfo(args?.accountId);
                break;

            case 'nt_account_balance':
                result = await api.getAccountBalance(args?.accountId);
                break;

            // ========== POSITION TOOLS ==========
            case 'nt_positions':
                result = await api.getPositions(args?.accountId);
                break;

            case 'nt_position':
                result = await api.getPosition(args?.accountId, args?.instrument);
                break;

            // ========== ORDER TOOLS ==========
            case 'nt_orders':
                result = await api.getOrders(args?.accountId, args?.status);
                break;

            case 'nt_place_order':
                result = await api.placeOrder({
                    accountId: args?.accountId,
                    instrument: args?.instrument,
                    quantity: args?.quantity,
                    orderType: args?.orderType || 'Market',
                    action: args?.action || 'Buy',
                    limitPrice: args?.limitPrice,
                    stopPrice: args?.stopPrice
                });
                break;

            case 'nt_modify_order':
                result = await api.modifyOrder(args?.orderId, {
                    quantity: args?.quantity,
                    limitPrice: args?.limitPrice,
                    stopPrice: args?.stopPrice
                });
                break;

            case 'nt_cancel_order':
                result = await api.cancelOrder(args?.orderId);
                break;

            case 'nt_cancel_all_orders':
                result = await api.cancelAllOrders(args?.accountId);
                break;

            // ========== MARKET DATA TOOLS ==========
            case 'nt_quote':
                result = await api.getQuote(args?.symbol);
                break;

            case 'nt_historical_bars':
                result = await api.getHistoricalBars(args?.symbol, args?.interval, args?.bars);
                break;

            case 'nt_market_depth':
                result = await api.getMarketDepth(args?.symbol);
                break;

            case 'nt_search_instruments':
                result = await api.searchInstruments(args?.query);
                break;

            // ========== NINJASCRIPT TOOLS (Local Only) ==========
            case 'nt_indicator_values':
                result = await api.getIndicatorValues(args?.symbol, args?.indicatorName);
                break;

            case 'nt_chart_state':
                result = await api.getChartState();
                break;

            case 'nt_execute_strategy':
                result = await api.executeNinjaScript(args?.strategy, args?.parameters);
                break;

            // ========== HEALTH CHECK ==========
            case 'nt_health_check':
                result = await api.healthCheck();
                break;

            default:
                result = { success: false, error: `Unknown tool: ${name}` };
        }

        sendResponse(id, {
            content: [{
                type: 'text',
                text: JSON.stringify(result, null, 2)
            }]
        });

    } catch (error) {
        sendResponse(id, {
            content: [{
                type: 'text',
                text: JSON.stringify({ success: false, error: error.message })
            }],
            isError: true
        });
    }
}

function sendResponse(id, result) {
    const msg = { jsonrpc: '2.0', id };
    if (result.error) {
        msg.error = result.error;
    } else {
        msg.result = result;
    }
    console.log(JSON.stringify(msg));
}

// ========== TOOL DEFINITIONS ==========
const TOOL_DEFINITIONS = [
    // Account Tools
    {
        name: 'nt_accounts',
        description: 'List all trading accounts from NinjaTrader',
        inputSchema: { type: 'object', properties: {} }
    },
    {
        name: 'nt_account_info',
        description: 'Get information about a specific account',
        inputSchema: {
            type: 'object',
            properties: { accountId: { type: 'string', description: 'Account ID (optional)' } }
        }
    },
    {
        name: 'nt_account_balance',
        description: 'Get balance and margin information for an account',
        inputSchema: {
            type: 'object',
            properties: { accountId: { type: 'string', description: 'Account ID (optional)' } }
        }
    },

    // Position Tools
    {
        name: 'nt_positions',
        description: 'Get all open positions for an account',
        inputSchema: {
            type: 'object',
            properties: { accountId: { type: 'string' } }
        }
    },
    {
        name: 'nt_position',
        description: 'Get a specific position by account and instrument',
        inputSchema: {
            type: 'object',
            properties: {
                accountId: { type: 'string' },
                instrument: { type: 'string', description: 'Symbol (e.g., "ES 06-24")' }
            }
        }
    },

    // Order Tools
    {
        name: 'nt_orders',
        description: 'List all orders, optionally filtered',
        inputSchema: {
            type: 'object',
            properties: {
                accountId: { type: 'string' },
                status: { type: 'string', description: 'Filter by status' }
            }
        }
    },
    {
        name: 'nt_place_order',
        description: 'Place a new trading order',
        inputSchema: {
            type: 'object',
            properties: {
                accountId: { type: 'string' },
                instrument: { type: 'string', description: 'Symbol (e.g., "ES 06-24")' },
                quantity: { type: 'number' },
                orderType: { type: 'string', enum: ['Market', 'Limit', 'Stop', 'StopLimit'] },
                action: { type: 'string', enum: ['Buy', 'Sell'] },
                limitPrice: { type: 'number' },
                stopPrice: { type: 'number' }
            },
            required: ['instrument', 'quantity', 'action']
        }
    },
    {
        name: 'nt_modify_order',
        description: 'Modify an existing order',
        inputSchema: {
            type: 'object',
            properties: {
                orderId: { type: 'string' },
                quantity: { type: 'number' },
                limitPrice: { type: 'number' },
                stopPrice: { type: 'number' }
            },
            required: ['orderId']
        }
    },
    {
        name: 'nt_cancel_order',
        description: 'Cancel a specific order',
        inputSchema: {
            type: 'object',
            properties: { orderId: { type: 'string' } },
            required: ['orderId']
        }
    },
    {
        name: 'nt_cancel_all_orders',
        description: 'Cancel all open orders',
        inputSchema: {
            type: 'object',
            properties: { accountId: { type: 'string' } }
        }
    },

    // Market Data Tools
    {
        name: 'nt_quote',
        description: 'Get current quote for a symbol',
        inputSchema: {
            type: 'object',
            properties: { symbol: { type: 'string' } },
            required: ['symbol']
        }
    },
    {
        name: 'nt_historical_bars',
        description: 'Get historical OHLCV bars',
        inputSchema: {
            type: 'object',
            properties: {
                symbol: { type: 'string' },
                interval: { type: 'string', default: '1', description: 'Bar interval (1, 5, 15, 60, D)' },
                bars: { type: 'number', default: 100, description: 'Number of bars' }
            },
            required: ['symbol']
        }
    },
    {
        name: 'nt_market_depth',
        description: 'Get order book / market depth',
        inputSchema: {
            type: 'object',
            properties: { symbol: { type: 'string' } },
            required: ['symbol']
        }
    },
    {
        name: 'nt_search_instruments',
        description: 'Search for instruments by name or symbol',
        inputSchema: {
            type: 'object',
            properties: { query: { type: 'string' } },
            required: ['query']
        }
    },

    // NinjaScript Tools (Local Mode Only)
    {
        name: 'nt_indicator_values',
        description: 'Get current indicator values from chart (local mode only)',
        inputSchema: {
            type: 'object',
            properties: {
                symbol: { type: 'string' },
                indicatorName: { type: 'string' }
            },
            required: ['symbol', 'indicatorName']
        }
    },
    {
        name: 'nt_chart_state',
        description: 'Get current chart state - symbol, timeframe, indicators (local mode only)',
        inputSchema: { type: 'object', properties: {} }
    },
    {
        name: 'nt_execute_strategy',
        description: 'Execute a NinjaScript strategy (local mode only)',
        inputSchema: {
            type: 'object',
            properties: {
                strategy: { type: 'string' },
                parameters: { type: 'object' }
            },
            required: ['strategy']
        }
    },

    // Health Check
    {
        name: 'nt_health_check',
        description: 'Verify connection to NinjaTrader',
        inputSchema: { type: 'object', properties: {} }
    }
];

console.error('🚀 NinjaTrader MCP Server starting...');
console.error(`📡 Mode: ${api.mode}`);
console.error('📡 Ready for MCP connections');
