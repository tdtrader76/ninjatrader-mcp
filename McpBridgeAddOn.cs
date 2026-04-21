// McpBridgeAddOn v2.0.2 - 2026-04-21 - Fixed Order.State, AccountItem.Margin, CreateOrder signature
#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.AddOns
{
    public class McpBridgeAddOn : AddOnBase
    {
        private HttpListener listener;
        private CancellationTokenSource cts;
        private Task serverTask;

        private readonly object sync = new object();
        private bool serverRunning = false;

        // Cambia esto
        private string baseUrl = "http://localhost:7890/";
        private string apiToken = "1976060810082016";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "McpBridgeAddOn";
            }
            else if (State == State.Active)
            {
                StartServer();
            }
            else if (State == State.Terminated)
            {
                StopServer();
            }
        }

        private void StartServer()
        {
            lock (sync)
            {
                if (serverRunning)
                    return;

                try
                {
                    cts = new CancellationTokenSource();
                    listener = new HttpListener();
                    listener.Prefixes.Add(baseUrl);
                    listener.Start();

                    serverTask = Task.Run(() => ListenLoop(cts.Token), cts.Token);
                    serverRunning = true;

                    Print("[MCP] HTTP server iniciado en " + baseUrl);
                }
                catch (Exception ex)
                {
                    Print("[MCP] Error iniciando server: " + ex);
                }
            }
        }

        private void StopServer()
        {
            lock (sync)
            {
                if (!serverRunning)
                    return;

                try
                {
                    cts?.Cancel();

                    if (listener != null)
                    {
                        if (listener.IsListening)
                            listener.Stop();

                        listener.Close();
                        listener = null;
                    }

                    serverRunning = false;
                    Print("[MCP] HTTP server detenido");
                }
                catch (Exception ex)
                {
                    Print("[MCP] Error deteniendo server: " + ex);
                }
            }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && listener != null && listener.IsListening)
            {
                HttpListenerContext ctx = null;

                try
                {
                    ctx = await listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestSafe(ctx), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        Print("[MCP] Error en loop HTTP: " + ex);
                }
            }
        }

        private void ProcessRequestSafe(HttpListenerContext ctx)
        {
            try
            {
                ProcessRequest(ctx);
            }
            catch (Exception ex)
            {
                Print("[MCP] Error procesando request: " + ex);
                WriteJson(ctx.Response, 500, "{\"ok\":false,\"error\":\"internal_server_error\"}");
            }
            finally
            {
                try
                {
                    ctx.Response.OutputStream.Close();
                }
                catch {}
            }
        }

        private void ProcessRequest(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
            if (string.IsNullOrEmpty(path))
                path = "/";

            if (!IsAuthorized(ctx.Request))
            {
                WriteJson(ctx.Response, 401, "{\"ok\":false,\"error\":\"unauthorized\"}");
                return;
            }

            if (ctx.Request.HttpMethod == "GET" && path == "/health")
            {
                string json = "{"
                    + "\"ok\":true,"
                    + "\"service\":\"ninjatrader-mcp-bridge\","
                    + "\"running\":true,"
                    + "\"timestamp\":\"" + EscapeJson(DateTime.UtcNow.ToString("o")) + "\""
                    + "}";
                WriteJson(ctx.Response, 200, json);
                return;
            }

            // PRIORIDAD 1: GET /orders - CRÍTICO
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/orders"))
            {
                HandleGetOrders(ctx, path);
                return;
            }

            // PRIORIDAD 2: GET /accounts/{id}/balance
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/accounts/"))
            {
                HandleGetBalance(ctx, path);
                return;
            }

            // PRIORIDAD 3: GET /quote/{symbol}
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/quote/"))
            {
                HandleGetQuote(ctx, path);
                return;
            }

            // PRIORIDAD 4: GET /instruments/search
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/instruments/search"))
            {
                HandleSearchInstruments(ctx, ctx.Request.Url.Query);
                return;
            }

            // PRIORIDAD 5: POST /order/cancel
            if (ctx.Request.HttpMethod == "POST" && path == "/order/cancel")
            {
                string body = ReadBody(ctx.Request);
                WriteJson(ctx.Response, 200, HandleCancelOrder(body));
                return;
            }

            // PRIORIDAD 6: POST /order/modify
            if (ctx.Request.HttpMethod == "POST" && path == "/order/modify")
            {
                string body = ReadBody(ctx.Request);
                WriteJson(ctx.Response, 200, HandleModifyOrder(body));
                return;
            }

            // STUB: GET /accounts
            if (ctx.Request.HttpMethod == "GET" && path == "/accounts")
            {
                WriteJson(ctx.Response, 200, GetAccountsJson());
                return;
            }

            // STUB: GET /positions
            if (ctx.Request.HttpMethod == "GET" && path == "/positions")
            {
                WriteJson(ctx.Response, 200, GetPositionsJson());
                return;
            }

            // STUB: POST /order
            if (ctx.Request.HttpMethod == "POST" && path == "/order")
            {
                string body = ReadBody(ctx.Request);
                WriteJson(ctx.Response, 200, HandleOrder(body));
                return;
            }

            // STUB: POST /flatten
            if (ctx.Request.HttpMethod == "POST" && path == "/flatten")
            {
                string body = ReadBody(ctx.Request);
                WriteJson(ctx.Response, 200, HandleFlatten(body));
                return;
            }

            // STUB: GET /bars
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/bars"))
            {
                WriteJson(ctx.Response, 501, "{\"ok\":false,\"error\":\"not_implemented\",\"endpoint\":\"bars\"}");
                return;
            }

            // STUB: GET /depth
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/depth"))
            {
                WriteJson(ctx.Response, 501, "{\"ok\":false,\"error\":\"not_implemented\",\"endpoint\":\"depth\"}");
                return;
            }

            // STUB: GET /chart/state
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/chart/state"))
            {
                WriteJson(ctx.Response, 501, "{\"ok\":false,\"error\":\"not_implemented\",\"endpoint\":\"chart_state\"}");
                return;
            }

            // STUB: GET /indicator
            if (ctx.Request.HttpMethod == "GET" && path.StartsWith("/indicator"))
            {
                WriteJson(ctx.Response, 501, "{\"ok\":false,\"error\":\"not_implemented\",\"endpoint\":\"indicator\"}");
                return;
            }

            WriteJson(ctx.Response, 404, "{\"ok\":false,\"error\":\"not_found\"}");
        }

        // PRIORIDAD 1: GET /orders - CRÍTICO
        private void HandleGetOrders(HttpListenerContext ctx, string path)
        {
            try
            {
                // Parse optional accountId filter: /orders or /orders?account=Sim101
                string accountFilter = null;
                if (path.Length > "/orders".Length)
                {
                    // For /orders/{accountId} format
                    var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                        accountFilter = parts[1];
                }
                else
                {
                    // For ?account= query param
                    var query = ctx.Request.Url.Query;
                    if (!string.IsNullOrEmpty(query))
                    {
                        accountFilter = GetQueryParam(query, "account");
                    }
                }

                var sb = new StringBuilder();
                sb.Append("{\"ok\":true,\"orders\":[");

                bool first = true;

                foreach (var acct in Account.All)
                {
                    // Filter by account if specified
                    if (!string.IsNullOrWhiteSpace(accountFilter) &&
                        !acct.Name.Equals(accountFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (Order order in acct.Orders)
                    {
                        if (!first) sb.Append(",");
                        first = false;

                        sb.Append("{");
                        sb.Append("\"accountId\":\"").Append(EscapeJson(acct.Name)).Append("\",");
                        sb.Append("\"orderId\":\"").Append(EscapeJson(order.Name)).Append("\","); // CORREGIDO: order.OrderId → order.Name
                        sb.Append("\"instrument\":\"").Append(EscapeJson(order.Instrument != null ? order.Instrument.FullName : "")).Append("\",");
                        sb.Append("\"state\":\"").Append(EscapeJson(order.State.ToString())).Append("\","); // Modificado 2026-04-21: OrderState → State
                        sb.Append("\"orderAction\":\"").Append(EscapeJson(order.OrderAction.ToString())).Append("\",");
                        sb.Append("\"orderType\":\"").Append(EscapeJson(order.OrderType.ToString())).Append("\",");
                        sb.Append("\"quantity\":").Append(order.Quantity).Append(",");

                        // Limit price
                        if (order.LimitPrice > 0)
                            sb.Append("\"limitPrice\":").Append(Convert.ToString(order.LimitPrice, CultureInfo.InvariantCulture)).Append(",");
                        else
                            sb.Append("\"limitPrice\":null,");

                        // Stop price
                        if (order.StopPrice > 0)
                            sb.Append("\"stopPrice\":").Append(Convert.ToString(order.StopPrice, CultureInfo.InvariantCulture)).Append(",");
                        else
                            sb.Append("\"stopPrice\":null,");

                        // Filled quantity
                        sb.Append("\"filled\":").Append(order.Filled).Append(",");

                        // Timestamp
                        if (order.Time != Core.Globals.MinDate)
                            sb.Append("\"time\":\"").Append(EscapeJson(order.Time.ToString("o"))).Append("\"");
                        else
                            sb.Append("\"time\":null");

                        sb.Append("}");
                    }
                }

                sb.Append("]}");
                WriteJson(ctx.Response, 200, sb.ToString());
            }
            catch (Exception ex)
            {
                WriteJson(ctx.Response, 500, "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
            }
        }

        // PRIORIDAD 2: GET /accounts/{id}/balance
        private void HandleGetBalance(HttpListenerContext ctx, string path)
        {
            try
            {
                // Extract account ID from /accounts/{id}/balance
                var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    WriteJson(ctx.Response, 400, "{\"ok\":false,\"error\":\"invalid_path\",\"expected\":\"/accounts/{id}/balance\"}");
                    return;
                }

                string accountId = parts[1]; // accounts/{accountId}/balance

                Account account = Account.All.FirstOrDefault(a => a.Name.Equals(accountId, StringComparison.OrdinalIgnoreCase));
                if (account == null)
                {
                    WriteJson(ctx.Response, 404, "{\"ok\":false,\"error\":\"account_not_found\",\"accountId\":\"" + EscapeJson(accountId) + "\"}");
                    return;
                }

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"ok\":true,");
                sb.Append("\"accountId\":\"").Append(EscapeJson(account.Name)).Append("\",");
                sb.Append("\"balance\":{");
                sb.Append("\"cashValue\":").Append(Convert.ToString(account.Get(AccountItem.CashValue), CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"realizedProfitLoss\":").Append(Convert.ToString(account.Get(AccountItem.RealizedProfitLoss), CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"unrealizedProfitLoss\":").Append(Convert.ToString(account.Get(AccountItem.UnrealizedProfitLoss), CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"buyingPower\":").Append(Convert.ToString(account.Get(AccountItem.BuyingPower), CultureInfo.InvariantCulture)).Append(",");
                sb.Append("\"availableCash\":").Append(Convert.ToString(account.Get(AccountItem.AvailableCash), CultureInfo.InvariantCulture));
                sb.Append("},");
                sb.Append("\"timestamp\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\"");
                sb.Append("}");

                WriteJson(ctx.Response, 200, sb.ToString());
            }
            catch (Exception ex)
            {
                WriteJson(ctx.Response, 500, "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
            }
        }

        // PRIORIDAD 3: GET /quote/{symbol}
        private void HandleGetQuote(HttpListenerContext ctx, string path)
        {
            try
            {
                // Extract symbol from /quote/{symbol}
                string symbolPath = path.Substring("/quote/".Length);
                string symbol = Uri.UnescapeDataString(symbolPath);

                if (string.IsNullOrWhiteSpace(symbol))
                {
                    WriteJson(ctx.Response, 400, "{\"ok\":false,\"error\":\"missing_symbol\"}");
                    return;
                }

                // CORREGIDO: Instrument.GetInstrument no existe como método estático
                Instrument instrument = null;
                foreach (var acct in Account.All)
                {
                    foreach (var ord in acct.Orders)
                    {
                        if (ord.Instrument != null && ord.Instrument.FullName.Equals(symbol, StringComparison.OrdinalIgnoreCase))
                        {
                            instrument = ord.Instrument;
                            break;
                        }
                    }
                    if (instrument != null) break;
                }

                if (instrument == null)
                {
                    WriteJson(ctx.Response, 404, "{\"ok\":false,\"error\":\"instrument_not_found\",\"symbol\":\"" + EscapeJson(symbol) + "\"}");
                    return;
                } // Modificado 2026-04-21

                // NT8 MarketData: obtener desde MarketDepth si está disponible
                double lastPrice = 0, bidPrice = 0, askPrice = 0;
                long vol = 0;
                try
                {
                    // CORREGIDO: instrument.BarsSeries no existe
                    // Usar MarketDepth si está disponible
                    if (instrument.MarketDepth != null)
                    {
                        if (instrument.MarketDepth.Bids.Count > 0)
                            bidPrice = instrument.MarketDepth.Bids[0].Price;
                        if (instrument.MarketDepth.Asks.Count > 0)
                            askPrice = instrument.MarketDepth.Asks[0].Price;

                        // Último precio como promedio de bid/ask o el último disponible
                        if (bidPrice > 0 && askPrice > 0)
                            lastPrice = (bidPrice + askPrice) / 2.0;
                        else if (bidPrice > 0)
                            lastPrice = bidPrice;
                        else if (askPrice > 0)
                            lastPrice = askPrice;
                    }
                }
                catch { /* Market data may not be available */ }

                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append("\"ok\":true,");
                sb.Append("\"symbol\":\"").Append(EscapeJson(instrument.FullName)).Append("\",");
                sb.Append("\"description\":\"").Append(EscapeJson(instrument.FullName ?? "")).Append("\","); // Modificado 2026-04-21
                sb.Append("\"quote\":{");

                if (lastPrice > 0)
                    sb.Append("\"last\":").Append(Convert.ToString(lastPrice, CultureInfo.InvariantCulture)).Append(",");
                else
                    sb.Append("\"last\":null,");

                if (bidPrice > 0)
                    sb.Append("\"bid\":").Append(Convert.ToString(bidPrice, CultureInfo.InvariantCulture)).Append(",");
                else
                    sb.Append("\"bid\":null,");

                if (askPrice > 0)
                    sb.Append("\"ask\":").Append(Convert.ToString(askPrice, CultureInfo.InvariantCulture)).Append(",");
                else
                    sb.Append("\"ask\":null,");

                if (vol > 0)
                    sb.Append("\"volume\":").Append(vol);
                else
                    sb.Append("\"volume\":null");

                sb.Append("},");
                sb.Append("\"timestamp\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\"");
                sb.Append("}");

                WriteJson(ctx.Response, 200, sb.ToString());
            }
            catch (Exception ex)
            {
                WriteJson(ctx.Response, 500, "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
            }
        }

        // PRIORIDAD 4: GET /instruments/search?query=...
        private void HandleSearchInstruments(HttpListenerContext ctx, string query)
        {
            try
            {
                string searchQuery = null;
                if (!string.IsNullOrEmpty(query))
                {
                    searchQuery = GetQueryParam(query, "query");
                }

                var sb = new StringBuilder();
                sb.Append("{\"ok\":true,\"instruments\":[");

                bool first = true;

                // CORREGIDO: Instrument.GetInstrument no existe como método estático
                // Buscar en todas las cuentas
                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    // Return empty result - would need access to instrument list
                    sb.Append("]");
                }
                else
                {
                    // Try exact match first across all accounts
                    Instrument exactMatch = null;
                    foreach (var acct in Account.All)
                    {
                        foreach (var ord in acct.Orders)
                        {
                            if (ord.Instrument != null && ord.Instrument.FullName.Equals(searchQuery, StringComparison.OrdinalIgnoreCase))
                            {
                                exactMatch = ord.Instrument;
                                break;
                            }
                        }
                        if (exactMatch != null) break;
                    } // Modificado 2026-04-21

                    if (exactMatch != null)
                    {
                        sb.Append("{");
                        sb.Append("\"symbol\":\"").Append(EscapeJson(exactMatch.FullName)).Append("\",");
                        sb.Append("\"description\":\"").Append(EscapeJson(exactMatch.FullName ?? "")).Append("\","); // Modificado 2026-04-21
                        sb.Append("\"assetClass\":\"").Append(EscapeJson(exactMatch.MasterInstrument.InstrumentType.ToString())).Append("\"");
                        sb.Append("}");
                        first = false;
                    }

                    // TODO: Add fuzzy search if NT8 provides instrument enumeration
                    sb.Append("]");
                }

                sb.Append("}");
                WriteJson(ctx.Response, 200, sb.ToString());
            }
            catch (Exception ex)
            {
                WriteJson(ctx.Response, 500, "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
            }
        }

        // PRIORIDAD 5: POST /order/cancel
        private string HandleCancelOrder(string body)
        {
            try
            {
                string orderId = GetJsonValue(body, "orderId");
                string accountName = GetJsonValue(body, "account");

                if (string.IsNullOrWhiteSpace(orderId))
                    return "{\"ok\":false,\"error\":\"missing_order_id\"}";

                Account targetAccount = null;
                Order targetOrder = null;

                // Find the order
                foreach (var acct in Account.All)
                {
                    // Filter by account if specified
                    if (!string.IsNullOrWhiteSpace(accountName) &&
                        !acct.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // CORREGIDO: order.OrderId → order.Name
                    Order order = acct.Orders.FirstOrDefault(o => o.Name.Equals(orderId, StringComparison.OrdinalIgnoreCase));
                    if (order != null)
                    {
                        targetAccount = acct;
                        targetOrder = order;
                        break;
                    }
                }

                if (targetOrder == null)
                    return "{\"ok\":false,\"error\":\"order_not_found\",\"orderId\":\"" + EscapeJson(orderId) + "\"}";

                // Attempt to cancel
                try
                {
                    // NT8: order has no Cancel method, use account approach
                    // The correct NT8 pattern is to submit a cancel request
                    NinjaTrader.NinjaScript.AtmStrategy.CancelOrder(targetOrder.Name);
                    return "{"
                        + "\"ok\":true,"
                        + "\"message\":\"cancel_submitted\","
                        + "\"orderId\":\"" + EscapeJson(targetOrder.Name) + "\","
                        + "\"account\":\"" + EscapeJson(targetAccount.Name) + "\""
                        + "}"; // Modificado 2026-04-21
                }
                catch (Exception cancelEx)
                {
                    return "{\"ok\":false,\"error\":\"cancel_failed\",\"message\":\"" + EscapeJson(cancelEx.Message) + "\"}";
                }
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        // PRIORIDAD 6: POST /order/modify
        private string HandleModifyOrder(string body)
        {
            try
            {
                string orderId = GetJsonValue(body, "orderId");
                string accountName = GetJsonValue(body, "account");
                string quantityText = GetJsonValue(body, "quantity");
                string limitPriceText = GetJsonValue(body, "limitPrice");
                string stopPriceText = GetJsonValue(body, "stopPrice");

                if (string.IsNullOrWhiteSpace(orderId))
                    return "{\"ok\":false,\"error\":\"missing_order_id\"}";

                Account targetAccount = null;
                Order targetOrder = null;

                // Find the order
                foreach (var acct in Account.All)
                {
                    // Filter by account if specified
                    if (!string.IsNullOrWhiteSpace(accountName) &&
                        !acct.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // CORREGIDO: order.OrderId → order.Name
                    Order order = acct.Orders.FirstOrDefault(o => o.Name.Equals(orderId, StringComparison.OrdinalIgnoreCase));
                    if (order != null)
                    {
                        targetAccount = acct;
                        targetOrder = order;
                        break;
                    }
                }

                if (targetOrder == null)
                    return "{\"ok\":false,\"error\":\"order_not_found\",\"orderId\":\"" + EscapeJson(orderId) + "\"}";

                // Parse parameters
                int? newQuantity = null;
                double? newLimitPrice = null;
                double? newStopPrice = null;

                if (!string.IsNullOrWhiteSpace(quantityText))
                {
                    int qty;
                    if (!int.TryParse(quantityText, out qty) || qty <= 0)
                        return "{\"ok\":false,\"error\":\"invalid_quantity\"}";
                    newQuantity = qty;
                }

                if (!string.IsNullOrWhiteSpace(limitPriceText))
                {
                    double price;
                    if (!double.TryParse(limitPriceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                        return "{\"ok\":false,\"error\":\"invalid_limit_price\"}";
                    newLimitPrice = price;
                }

                if (!string.IsNullOrWhiteSpace(stopPriceText))
                {
                    double price;
                    if (!double.TryParse(stopPriceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                        return "{\"ok\":false,\"error\":\"invalid_stop_price\"}";
                    newStopPrice = price;
                }

                if (!newQuantity.HasValue && !newLimitPrice.HasValue && !newStopPrice.HasValue)
                    return "{\"ok\":false,\"error\":\"no_changes_specified\"}";

                // Attempt to modify
                try
                {
                    int qty = newQuantity ?? targetOrder.Quantity;
                    double limit = newLimitPrice ?? targetOrder.LimitPrice;
                    double stop = newStopPrice ?? targetOrder.StopPrice;

                    // CORREGIDO: account.ChangeOrder en NT8 - firma correcta
                    targetAccount.ChangeOrder(targetOrder, limit, stop, qty); // Modificado 2026-04-21: orden de parámetros corregido

                    return "{"
                        + "\"ok\":true,"
                        + "\"message\":\"modify_submitted\","
                        + "\"orderId\":\"" + EscapeJson(targetOrder.Name) + "\","
                        + "\"account\":\"" + EscapeJson(targetAccount.Name) + "\","
                        + "\"newQuantity\":" + qty + ","
                        + "\"newLimitPrice\":" + Convert.ToString(limit, CultureInfo.InvariantCulture) + ","
                        + "\"newStopPrice\":" + Convert.ToString(stop, CultureInfo.InvariantCulture)
                        + "}"; // Modificado 2026-04-21
                }
                catch (Exception modifyEx)
                {
                    return "{\"ok\":false,\"error\":\"modify_failed\",\"message\":\"" + EscapeJson(modifyEx.Message) + "\"}";
                }
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private bool IsAuthorized(HttpListenerRequest request)
        {
            string token = request.Headers["X-NTMCP-Token"];
            return !string.IsNullOrWhiteSpace(token) && token == apiToken;
        }

        private string GetAccountsJson()
        {
            try
            {
                var accounts = Account.All;
                var sb = new StringBuilder();

                sb.Append("{\"ok\":true,\"accounts\":[");
                bool first = true;

                foreach (var acct in accounts)
                {
                    if (!first) sb.Append(",");
                    first = false;

                    sb.Append("{");
                    sb.Append("\"name\":\"").Append(EscapeJson(acct.Name)).Append("\"");
                    sb.Append("}");
                }

                sb.Append("]}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private string GetPositionsJson()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"ok\":true,\"positions\":[");

                bool firstPos = true;

                foreach (var acct in Account.All)
                {
                    foreach (Position pos in acct.Positions)
                    {
                        if (!firstPos) sb.Append(",");
                        firstPos = false;

                        sb.Append("{");
                        sb.Append("\"account\":\"").Append(EscapeJson(acct.Name)).Append("\",");
                        sb.Append("\"instrument\":\"").Append(EscapeJson(pos.Instrument != null ? pos.Instrument.FullName : "")).Append("\",");
                        sb.Append("\"marketPosition\":\"").Append(EscapeJson(pos.MarketPosition.ToString())).Append("\",");
                        sb.Append("\"quantity\":").Append(pos.Quantity).Append(",");
                        sb.Append("\"averagePrice\":").Append(Convert.ToString(pos.AveragePrice, CultureInfo.InvariantCulture));
                        sb.Append("}");
                    }
                }

                sb.Append("]}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private string HandleOrder(string body)
        {
            try
            {
                // Parser simple, suficiente para pruebas rápidas.
                // Mejorable: usar Newtonsoft.Json si tu proyecto DLL la referencia.
                string accountName = GetJsonValue(body, "account");
                string instrumentName = GetJsonValue(body, "instrument");
                string actionText = GetJsonValue(body, "action");
                string typeText = GetJsonValue(body, "type");
                string quantityText = GetJsonValue(body, "quantity");

                if (string.IsNullOrWhiteSpace(accountName))
                    return "{\"ok\":false,\"error\":\"missing_account\"}";

                if (string.IsNullOrWhiteSpace(instrumentName))
                    return "{\"ok\":false,\"error\":\"missing_instrument\"}";

                if (string.IsNullOrWhiteSpace(actionText))
                    return "{\"ok\":false,\"error\":\"missing_action\"}";

                if (string.IsNullOrWhiteSpace(typeText))
                    return "{\"ok\":false,\"error\":\"missing_type\"}";

                int quantity;
                if (!int.TryParse(quantityText, out quantity) || quantity <= 0)
                    return "{\"ok\":false,\"error\":\"invalid_quantity\"}";

                Account account = Account.All.FirstOrDefault(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));
                if (account == null)
                    return "{\"ok\":false,\"error\":\"account_not_found\"}";

                // CORREGIDO: Instrument.GetInstrument no existe como método estático
                Instrument instrument = null;
                foreach (var acct in Account.All)
                {
                    foreach (var ord in acct.Orders)
                    {
                        if (ord.Instrument != null && ord.Instrument.FullName.Equals(instrumentName, StringComparison.OrdinalIgnoreCase))
                        {
                            instrument = ord.Instrument;
                            break;
                        }
                    }
                    if (instrument != null) break;
                } // Modificado 2026-04-21

                if (instrument == null)
                    return "{\"ok\":false,\"error\":\"instrument_not_found\"}";

                OrderAction action;
                if (!Enum.TryParse(actionText, true, out action))
                    return "{\"ok\":false,\"error\":\"invalid_action\"}";

                OrderType orderType;
                if (!Enum.TryParse(typeText, true, out orderType))
                    return "{\"ok\":false,\"error\":\"invalid_type\"}";

                // Importante:
                // La firma exacta de CreateOrder/Submit puede variar según versión/licencia/contexto.
                // Este bloque es la parte que más probablemente tendrás que ajustar en tu entorno.
                // CORREGIDO: Firma simplificada para NT8 - Modificado 2026-04-21
                Order order = account.CreateOrder(instrument, action, orderType, quantity, 0, 0, TimeInForce.Day, OrderEntry.Automated, string.Empty);

                order.Submit();

                return "{"
                    + "\"ok\":true,"
                    + "\"message\":\"order_submitted\","
                    + "\"account\":\"" + EscapeJson(account.Name) + "\","
                    + "\"instrument\":\"" + EscapeJson(instrument.FullName) + "\","
                    + "\"action\":\"" + EscapeJson(action.ToString()) + "\","
                    + "\"type\":\"" + EscapeJson(orderType.ToString()) + "\","
                    + "\"quantity\":" + quantity
                    + "}";
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private string HandleFlatten(string body)
        {
            try
            {
                string accountName = GetJsonValue(body, "account");
                string instrumentName = GetJsonValue(body, "instrument");

                if (string.IsNullOrWhiteSpace(accountName))
                    return "{\"ok\":false,\"error\":\"missing_account\"}";

                Account account = Account.All.FirstOrDefault(a => a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase));
                if (account == null)
                    return "{\"ok\":false,\"error\":\"account_not_found\"}";

                var positions = account.Positions.ToList();

                if (!string.IsNullOrWhiteSpace(instrumentName))
                    positions = positions.Where(p => p.Instrument != null && p.Instrument.FullName.Equals(instrumentName, StringComparison.OrdinalIgnoreCase)).ToList();

                int flattened = 0;

                foreach (var pos in positions)
                {
                    if (pos.Quantity == 0 || pos.MarketPosition == MarketPosition.Flat)
                        continue;

                    OrderAction action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                    // CORREGIDO: Firma simplificada para NT8 - Modificado 2026-04-21
                    Order order = account.CreateOrder(pos.Instrument, action, OrderType.Market, Math.Abs(pos.Quantity), 0, 0, TimeInForce.Day, OrderEntry.Automated, string.Empty);

                    order.Submit();
                    flattened++;
                }

                return "{"
                    + "\"ok\":true,"
                    + "\"message\":\"flatten_submitted\","
                    + "\"flattened\":" + flattened
                    + "}";
            }
            catch (Exception ex)
            {
                return "{\"ok\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}";
            }
        }

        private string ReadBody(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private void WriteJson(HttpListenerResponse response, int statusCode, string json)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private string EscapeJson(string value)
        {
            if (value == null)
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private string GetJsonValue(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
                return null;

            string pattern = "\"" + key + "\"";
            int keyIndex = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
                return null;

            int colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
            if (colonIndex < 0)
                return null;

            int start = colonIndex + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;

            if (start >= json.Length)
                return null;

            if (json[start] == '"')
            {
                start++;
                int end = start;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\')
                        break;
                    end++;
                }

                if (end >= json.Length)
                    return null;

                return json.Substring(start, end - start)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }
            else
            {
                int end = start;
                while (end < json.Length && ",}]".IndexOf(json[end]) == -1)
                    end++;

                return json.Substring(start, end - start).Trim();
            }
        }

        private string GetQueryParam(string query, string paramName)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(paramName))
                return null;

            string search = paramName + "=";
            int idx = query.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            int start = idx + search.Length;
            int end = query.IndexOf('&', start);
            if (end < 0)
                end = query.Length;

            return Uri.UnescapeDataString(query.Substring(start, end - start));
        }
    }
}
