# NinjaTrader Local Mode - To-Do List

## ✅ Completed

- [x] Node.js installed (v24.14.1)
- [x] Dependencies installed (npm install)
- [x] .env configured for local mode
- [x] MCP server files ready

---

## ⏳ Waiting For You

### Step 1: Enable HTTP Server in NinjaTrader 8

**Do this in NinjaTrader 8:**
1. Open **NinjaTrader 8**
2. Go to **Tools** → **Options**
3. Find **NinjaScript Add-On** section
4. Check **"Enable HTTP Server"**
5. Set **Port** to `7890`
6. Click **OK**
7. **Restart NinjaTrader 8** (important!)

---

### Step 2: Verify Server is Running

After restarting NinjaTrader, run:
```bash
curl http://localhost:7890/api/health
```

**Expected response:**
```json
{"status": "ok"}
```

If you see "Connection refused", the server isn't enabled yet.

---

### Step 3: Test MCP Connection

Once server is responding:
```bash
cd ~/ninjatrader-mcp && node -e "const api=require('./src/api'); new api().healthCheck().then(console.log)"
```

---

### Step 4: Add to Claude Code MCP Config

**Get your username:**
```bash
echo $USER
```

**Create/Edit MCP config:**
```bash
cat ~/.claude/.mcp.json 2>/dev/null || echo "Need to create"
```

**Add ninjatrader to config:**
```json
{
  "mcpServers": {
    "ninjatrader": {
      "command": "node",
      "args": ["/Users/YOUR_USERNAME/ninjatrader-mcp/src/server.js"]
    }
  }
}
```

Replace `YOUR_USERNAME` with the output from `echo $USER`.

---

### Step 5: Restart Claude Code

```bash
# Quit Claude Code completely (Cmd+Q)
# Reopen Claude Code
```

---

### Step 6: Verify MCP Connection

In Claude Code, ask:
```
Use nt_health_check to verify NinjaTrader is connected
```

---

## Quick Summary

| Task | Status |
|------|--------|
| Node.js installed | ✅ Done |
| Dependencies installed | ✅ Done |
| Configured for local | ✅ Done |
| Enable HTTP Server | ⏳ Pending |
| Test connection | ⏳ Pending |
| Add to Claude Code | ⏳ Pending |
| Verify MCP | ⏳ Pending |

---

## Need Help Enabling HTTP Server?

**In NinjaTrader 8:**

1. **Tools** menu → **Options**
2. Look for **NinjaScript** in the left panel
3. Click **Add-On** or **NinjaScript Add-On**
4. Look for **"Enable HTTP Server"** checkbox
5. Check it
6. Set port to **7890**
7. Click **OK**
8. **Restart NinjaTrader completely**

**If you can't find it:**
- Search NinjaTrader help for "HTTP Server"
- Check: [support.ninjatrader.com](https://support.ninjatrader.com)
- NinjaTrader forum: [discourse.ninjatrader.com](https://discourse.ninjatrader.com)

---

*Last updated: 2026-04-05*
