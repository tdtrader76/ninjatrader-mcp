# NinjaTrader MCP Server - Setup Checklist

## 📋 What You Need to Get

### From NinjaTrader

**Option A: Local Mode (Desktop)**
- [ ] **NinjaTrader 8** installed on your computer
  - Website: [ninjatrader.com](https://ninjatrader.com)
  - Download: [ninjatrader.com/downloads](https://ninjatrader.com/downloads)

**Option B: Cloud Mode (Ecosystem API)**
- [ ] **NinjaTrader Ecosystem Account**
  - Website: [ninjatrader.com](https://ninjatrader.com)
- [ ] **API Key** from NinjaTrader Developer Portal
  - Website: [developer.ninjatrader.com](https://developer.ninjatrader.com)

### From Your Computer

- [ ] **Node.js 18+ installed**
  - Check: `node --version`
  - If missing: [nodejs.org](https://nodejs.org) or `brew install node`

---

## ✅ To-Do Checklist

### Step 1: Install Node.js
```bash
node --version
```
If version is below 18 or shows error, install Node.js from [nodejs.org](https://nodejs.org)

### Step 2: Clone/Copy the Project
```bash
# Already created at:
ls ~/ninjatrader-mcp/
```

### Step 3: Install Dependencies
```bash
cd ~/ninjatrader-mcp
npm install
```

### Step 4: Choose Your Connection Mode

#### Option A: Local Mode (Recommended for chart/indicator access)

**Requirements:**
- [ ] NinjaTrader 8 installed and running
- [ ] NinjaScript Add-On configured (or use built-in local server)

**Configure .env:**
```bash
open .env
```

Set:
```env
NINJATRADER_MODE=local
NINJATRADER_LOCAL_URL=http://localhost:7890
```

**Start NinjaTrader with local server:**
1. Open NinjaTrader 8
2. Go to Tools > Options > NinjaScript Add-On
3. Enable local HTTP server on port 7890
4. Restart NinjaTrader

#### Option B: Cloud Mode (No local software)

**Requirements:**
- [ ] NinjaTrader Ecosystem account
- [ ] API key from developer portal

**Configure .env:**
```bash
open .env
```

Set:
```env
NINJATRADER_MODE=cloud
NINJATRADER_API_KEY=your_api_key_here
```

### Step 5: Create Environment File
```bash
cd ~/ninjatrader-mcp
cp .env.example .env
```

### Step 6: Fill in Your Credentials
```bash
open .env
```

**For Local Mode:**
```env
NINJATRADER_MODE=local
NINJATRADER_LOCAL_URL=http://localhost:7890
# Leave API_KEY blank or commented out
```

**For Cloud Mode:**
```env
NINJATRADER_MODE=cloud
NINJATRADER_API_KEY=YOUR_API_KEY_HERE
```

### Step 7: Test Your Connection

**For Local Mode (NinjaTrader must be running):**
```bash
cd ~/ninjatrader-mcp
node -e "
const NinjaTraderAPI = require('./src/api');
const api = new NinjaTraderAPI();
api.healthCheck().then(console.log).catch(console.error);
"
```

**Expected output if successful:**
```
{ success: true, connected: true, mode: 'local', accounts: [...], accountCount: 1 }
```

**For Cloud Mode:**
```bash
cd ~/ninjatrader-mcp
node -e "
const NinjaTraderAPI = require('./src/api');
const api = new NinjaTraderAPI();
api.healthCheck().then(console.log).catch(console.error);
"
```

### Step 8: Get Your Username for MCP Config
```bash
echo $USER
```
This will output something like `ozm` - you'll need this for the next step.

### Step 9: Add to Claude Code MCP Config

**First, check if you have an existing MCP config:**
```bash
cat ~/.claude/.mcp.json 2>/dev/null || echo "File does not exist"
```

**If file exists**, add the ninjatrader server to it:
```bash
open ~/.claude/.mcp.json
```

**If file doesn't exist**, create it:
```bash
mkdir -p ~/.claude
cat > ~/.claude/.mcp.json << 'EOF'
{
  "mcpServers": {
    "ninjatrader": {
      "command": "node",
      "args": ["/Users/YOUR_USERNAME/ninjatrader-mcp/src/server.js"]
    }
  }
}
EOF
```

**Replace `YOUR_USERNAME`** with the output from Step 8.

### Step 10: Restart Claude Code

```bash
# Exit Claude Code completely, then reopen
# Or use Cmd+Q to quit and relaunch
```

### Step 11: Verify Connection

In Claude Code, ask:
```
Use nt_health_check to verify NinjaTrader is connected
```

---

## 🚀 How to Run

### Start the MCP Server Manually (for testing)
```bash
cd ~/ninjatrader-mcp
node src/server.js
```

### Run with Claude Code

1. Make sure NinjaTrader is running (for local mode)
2. Ask questions like:
   - "Use nt_health_check to verify connection"
   - "Show my positions with nt_positions"
   - "Get a quote for ES with nt_quote"
   - "Place a market order to buy 1 ES"

### Stop the Server
```
Ctrl+C
```

---

## 🔧 Troubleshooting

### "Connection refused" (Local Mode)
- Make sure NinjaTrader 8 is running
- Check Tools > Options > NinjaScript Add-On > HTTP Server is enabled
- Verify port 7890 is correct
- Try: `curl http://localhost:7890/api/health` in browser

### "Authentication failed" (Cloud Mode)
- Check API key is correct
- Verify API key has necessary permissions
- Make sure NinjaTrader Ecosystem account is active

### "Indicator values not available"
- This feature is ONLY available in local mode
- NinjaTrader Desktop must be running with chart open
- Check NinjaScript Add-On is enabled

### "node: command not found"
Install Node.js: [nodejs.org](https://nodejs.org) or `brew install node`

### MCP server not showing in Claude Code
- Double-check the path in `.mcp.json` matches your actual username
- Restart Claude Code completely
- Check `.mcp.json` has valid JSON syntax

---

## 📊 Local vs Cloud Mode Comparison

| Feature | Local Mode | Cloud Mode |
|---------|------------|------------|
| **Setup Complexity** | Medium | Easy |
| **Requires NinjaTrader Desktop** | Yes | No |
| **Indicator Access** | ✅ Yes | ❌ No |
| **Chart State** | ✅ Yes | ❌ No |
| **Strategy Execution** | ✅ Yes | ❌ No |
| **Market Data** | ✅ Yes | ✅ Yes |
| **Order Management** | ✅ Yes | ✅ Yes |
| **Works Offline** | Yes (if running) | No |

---

## 🎯 After Setup - What You Can Ask Claude

### Both Modes:
```
"Show my current positions"
"What's my account balance?"
"Get a quote for ES"
"Place a limit order to buy 1 ES at 5500"
"Cancel all my open orders"
"Get historical bars for ES (100 bars, 5 minute)"
```

### Local Mode Only:
```
"What's on my chart right now?" (nt_chart_state)
"What's the RSI value on my ES chart?" (nt_indicator_values)
"Execute my mean reversion strategy" (nt_execute_strategy)
```

---

## Quick Reference

| Command | Purpose |
|---------|---------|
| `npm install` | Install dependencies |
| `node src/server.js` | Start server manually |
| `node -e "..."` | Test API connection |
| `nt_health_check` | Verify connection |
| `nt_positions` | Show positions |
| `nt_orders` | Show orders |
| `nt_quote` | Get price quote |
| `nt_chart_state` | Get chart info (local only) |
| `nt_indicator_values` | Read indicators (local only) |

---

## 📞 Need Help?

### Resources:
- NinjaTrader Main: [ninjatrader.com](https://ninjatrader.com)
- Developer Portal: [developer.ninjatrader.com](https://developer.ninjatrader.com)
- NinjaTrader Forum: [discourse.ninjatrader.com](https://discourse.ninjatrader.com)
- Support: [support.ninjatrader.com](https://support.ninjatrader.com)

### Third-Party Tools (Optional):
- CrossTrade API: [crosstrade.io](https://crosstrade.io) - Adds REST API to NinjaTrader

---

*Created: 2026-04-05*
