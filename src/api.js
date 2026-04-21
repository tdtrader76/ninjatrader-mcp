/**
 * NinjaTrader API Wrapper
 * Handles communication with NinjaTrader Desktop via local server
 * 
 * Note: NinjaTrader Desktop must be running with the NinjaScript Add-On
 * or you can use the Ecosystem API for cloud-based access
 */

const axios = require('axios');
const WebSocket = require('ws');
require('dotenv').config();

class NinjaTraderAPI {
    constructor() {
        this.accessToken = null;
        this.accountId = null;
        this.ws = null;
        this.messageHandlers = new Map();

        // Configuration from environment
        this.localUrl = process.env.NINJATRADER_LOCAL_URL || 'http://localhost:7890';
        this.apiKey = process.env.NINJATRADER_API_KEY;
        this.apiToken = process.env.NINJATRADER_API_TOKEN || '1976060810082016';
        this.ecosystemUrl = 'https://api.ninjatrader.com';

        // Connection mode: 'local' (desktop) or 'cloud' (ecosystem)
        this.mode = process.env.NINJATRADER_MODE || 'local';
    }

    // ============ LOCAL CONNECTION (Desktop Add-On) ============
    
    async localRequest(method, endpoint, data = null) {
        try {
            const config = {
                method,
                url: `${this.localUrl}${endpoint}`,
                headers: {
                    'Content-Type': 'application/json',
                    'X-NTMCP-Token': this.apiToken
                },
                timeout: 10000
            };

            if (data) {
                config.data = data;
            }

            const response = await axios(config);
            return { success: true, data: response.data };
        } catch (error) {
            return {
                success: false,
                error: error.message,
                code: error.code
            };
        }
    }

    // ============ CLOUD CONNECTION (Ecosystem API) ============
    
    async authenticate() {
        if (!this.apiKey) {
            return { success: false, error: 'No API key configured' };
        }

        try {
            // Note: Actual auth flow depends on NinjaTrader's Ecosystem API
            this.accessToken = this.apiKey;
            return { success: true, accessToken: this.accessToken };
        } catch (error) {
            return { success: false, error: error.message };
        }
    }

    async cloudRequest(method, endpoint, data = null) {
        try {
            const config = {
                method,
                url: `${this.ecosystemUrl}${endpoint}`,
                headers: {
                    'Authorization': `Bearer ${this.accessToken}`,
                    'Content-Type': 'application/json'
                }
            };

            if (data) {
                config.data = data;
            }

            const response = await axios(config);
            return { success: true, data: response.data };
        } catch (error) {
            return {
                success: false,
                error: error.response?.data?.message || error.message
            };
        }
    }

    // ============ ACCOUNT METHODS ============
    
    async getAccounts() {
        if (this.mode === 'local') {
            return await this.localRequest('GET', '/accounts');
        }
        return await this.cloudRequest('GET', '/v1/accounts');
    }

    async getAccountInfo(accountId = null) {
        const id = accountId || this.accountId;
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/accounts${id ? `/${id}` : ''}`);
        }
        return await this.cloudRequest('GET', `/v1/accounts/${id}`);
    }

    async getAccountBalance(accountId = null) {
        const id = accountId || this.accountId;
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/accounts/${id}/balance`);
        }
        return await this.cloudRequest('GET', `/v1/accounts/${id}/balance`);
    }

    // ============ POSITION METHODS ============
    
    async getPositions(accountId = null) {
        const id = accountId || this.accountId;
        if (this.mode === 'local') {
            return await this.localRequest('GET', '/positions');
        }
        return await this.cloudRequest('GET', `/v1/accounts/${id}/positions`);
    }

    async getPosition(accountId, instrument = null) {
        if (this.mode === 'local') {
            return await this.localRequest('GET', '/positions');
        }
        return await this.cloudRequest('GET', `/v1/accounts/${accountId}/positions?instrument=${instrument}`);
    }

    // ============ ORDER METHODS ============
    
    async getOrders(accountId = null, status = null) {
        let endpoint = this.mode === 'local' ? '/orders' : '/v1/orders';
        const params = [];
        if (accountId) params.push(`accountId=${accountId}`);
        if (status) params.push(`status=${status}`);
        if (params.length) endpoint += `?${params.join('&')}`;

        if (this.mode === 'local') {
            return await this.localRequest('GET', endpoint);
        }
        return await this.cloudRequest('GET', endpoint);
    }

    async placeOrder({ accountId, instrument, quantity, orderType = 'Market', action = 'Buy', limitPrice = null, stopPrice = null }) {
        const orderData = {
            accountId: accountId || this.accountId,
            instrument,
            quantity,
            orderType,
            action
        };

        if (limitPrice) orderData.limitPrice = limitPrice;
        if (stopPrice) orderData.stopPrice = stopPrice;

        if (this.mode === 'local') {
            return await this.localRequest('POST', '/order', orderData);
        }
        return await this.cloudRequest('POST', '/v1/orders', orderData);
    }

    async modifyOrder(orderId, { quantity, limitPrice, stopPrice }) {
        const data = { orderId };
        if (quantity) data.quantity = quantity;
        if (limitPrice) data.limitPrice = limitPrice;
        if (stopPrice) data.stopPrice = stopPrice;

        if (this.mode === 'local') {
            return await this.localRequest('POST', '/order/modify', data);
        }
        return await this.cloudRequest('PUT', '/v1/orders', data);
    }

    async cancelOrder(orderId) {
        if (this.mode === 'local') {
            return await this.localRequest('POST', '/order/cancel', { orderId });
        }
        return await this.cloudRequest('DELETE', `/v1/orders/${orderId}`);
    }

    async cancelAllOrders(accountId = null) {
        const data = {};
        if (accountId) data.accountId = accountId;

        if (this.mode === 'local') {
            return await this.localRequest('POST', '/flatten', data);
        }
        return await this.cloudRequest('POST', '/v1/orders/cancelall', data);
    }

    // ============ INSTRUMENT METHODS ============
    
    async searchInstruments(query) {
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/instruments/search?q=${encodeURIComponent(query)}`);
        }
        return await this.cloudRequest('GET', `/v1/instruments/search?q=${encodeURIComponent(query)}`);
    }

    async getInstrument(symbol) {
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/instruments/${encodeURIComponent(symbol)}`);
        }
        return await this.cloudRequest('GET', `/v1/instruments/${encodeURIComponent(symbol)}`);
    }

    // ============ MARKET DATA METHODS ============
    
    async getQuote(symbol) {
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/quote/${encodeURIComponent(symbol)}`);
        }
        return await this.cloudRequest('GET', `/v1/marketdata/${encodeURIComponent(symbol)}/quote`);
    }

    async getHistoricalBars(symbol, interval = '1', bars = 100) {
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/bars/${encodeURIComponent(symbol)}?interval=${interval}&bars=${bars}`);
        }
        return await this.cloudRequest('GET', `/v1/marketdata/${encodeURIComponent(symbol)}/bars?interval=${interval}&bars=${bars}`);
    }

    async getMarketDepth(symbol) {
        if (this.mode === 'local') {
            return await this.localRequest('GET', `/depth/${encodeURIComponent(symbol)}`);
        }
        return await this.cloudRequest('GET', `/v1/marketdata/${encodeURIComponent(symbol)}/depth`);
    }

    // ============ NINJASCRIPT METHODS (Local Only) ============
    
    async executeNinjaScript(strategy, parameters = {}) {
        if (this.mode !== 'local') {
            return { success: false, error: 'NinjaScript execution only available in local mode' };
        }
        
        return await this.localRequest('POST', '/ninjascript/execute', {
            strategy,
            parameters
        });
    }

    async getIndicatorValues(symbol, indicatorName) {
        if (this.mode !== 'local') {
            return { success: false, error: 'Indicator values only available in local mode' };
        }

        return await this.localRequest('GET', `/indicator/${encodeURIComponent(symbol)}/${encodeURIComponent(indicatorName)}`);
    }

    async getChartState() {
        if (this.mode !== 'local') {
            return { success: false, error: 'Chart state only available in local mode' };
        }

        return await this.localRequest('GET', '/chart/state');
    }

    // ============ WEBSOCKET METHODS ============
    
    async connectWebSocket() {
        return new Promise((resolve, reject) => {
            const wsUrl = this.mode === 'local' 
                ? this.localUrl.replace('http', 'ws') + '/ws'
                : `${this.ecosystemUrl.replace('http', 'ws')}/v1/ws`;

            this.ws = new WebSocket(wsUrl);

            this.ws.on('open', () => {
                console.log('✅ NinjaTrader WebSocket connected');
                if (this.accessToken) {
                    this.ws.send(JSON.stringify({ type: 'auth', token: this.accessToken }));
                }
            });

            this.ws.on('message', (data) => {
                const message = JSON.parse(data);
                this.handleMessage(message);
            });

            this.ws.on('error', (error) => {
                console.error('❌ WebSocket error:', error.message);
                reject(error);
            });

            this.ws.on('close', () => {
                console.log('⚠️ NinjaTrader WebSocket disconnected');
                this.ws = null;
            });

            // Timeout for connection
            setTimeout(() => {
                if (this.ws?.readyState !== WebSocket.OPEN) {
                    reject(new Error('WebSocket connection timeout'));
                }
            }, 5000);
        });
    }

    handleMessage(message) {
        if (message.type && this.messageHandlers.has(message.type)) {
            this.messageHandlers.get(message.type).forEach(handler => handler(message.data));
        }
    }

    onMessage(type, handler) {
        if (!this.messageHandlers.has(type)) {
            this.messageHandlers.set(type, []);
        }
        this.messageHandlers.get(type).push(handler);
    }

    subscribeToChannel(channel) {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify({ type: 'subscribe', channel }));
        }
    }

    disconnectWebSocket() {
        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }
    }

    // ============ HEALTH CHECK ============
    
    async healthCheck() {
        if (this.mode === 'local') {
            const result = await this.localRequest('GET', '/health');
            if (result.success) {
                const accounts = await this.getAccounts();
                return {
                    success: true,
                    connected: true,
                    mode: 'local',
                    serverInfo: result.data,
                    accounts: accounts.data || [],
                    accountCount: (accounts.data || []).length
                };
            }
            return { success: false, connected: false, mode: 'local', error: result.error };
        }

        const auth = await this.authenticate();
        if (auth.success) {
            const accounts = await this.getAccounts();
            return {
                success: true,
                connected: true,
                mode: 'cloud',
                accounts: accounts.data || []
            };
        }
        return { success: false, connected: false };
    }
}

module.exports = NinjaTraderAPI;
