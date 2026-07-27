---
name: unity-mcp-reconnect
description: Diagnose and fix a stuck Unity-MCP (AI Game Developer) connection — device auth
  succeeded but MCP tool calls still fail with "requires re-authorization" or a connection error.
  Use whenever MCP tools error out even though the user says they already authorized/reauthorized
  in the Unity Editor.
---

# Fixing a stuck Unity-MCP connection (auth OK, tools still failing)

Source of truth for everything below: the plugin's own Editor source, readable straight out of
`Library/PackageCache/com.ivanmurzak.unity.mcp@<hash>/Editor/Scripts/UI/Window/` in any project
that has it installed (`MainWindowEditor.Connection.cs`, `MainWindowEditor.cs`). Read those files
directly if anything here seems off for a newer plugin version — don't guess.

## The architecture in one paragraph

The plugin has two connection modes, a segmented toggle inside its window: **Custom** (local -
the plugin starts its own local HTTP/SignalR server, default `http://localhost:<port>`, port is
a hash of the project directory via `UnityMcpPlugin.GeneratePortFromDirectory()`) and **Cloud**
(connects out to `https://ai-game.dev/mcp`, no local server). Most setups (check the project's
`.mcp.json`) use **Cloud mode**. In Cloud mode, "is MCP working" is really two independent
questions:
1. Is the device authorized (does a cloud token exist)? — `DeviceAuthFlow` / `btnCloudAuthorize`.
2. Is *this specific Unity Editor instance* currently connected to the hub over SignalR? —
   independent of #1, governed by a `KeepConnected` flag.

**Auth succeeding does NOT automatically mean Unity is connected.** These are separate state
machines. A completed device-auth flow (`DeviceTokenResponse` in the Editor log) only triggers
one reconnect *attempt*; if that attempt doesn't land (timing, a transient hub issue, whatever),
the connection sits `Disconnected` with `KeepConnected == false`, so the plugin's own
`ConnectionManager.EnsureConnection` will *not* auto-retry - the Editor log shows exactly this:

```
warn: [AI] McpManagerClientHub ConnectionManager[...] EnsureConnection Connection not available
and auto-reconnect disabled for endpoint: /hub/mcp-server
```

Nothing external is going to fix that. It needs a manual "Connect" click in the Editor - **there
is no CLI/log-only way to flip `KeepConnected` back to true**, by design of this plugin's UI-only
control surface.

## Diagnose first (don't guess)

From the Unity project directory (needs `unity-mcp-cli` on PATH):

```bash
unity-mcp-cli status /path/to/project --verbose
```

Read the output as three separate checks, not one:
- **Unity Editor Process**: is Unity even running for this project path?
- **Local MCP Server** (`http://localhost:<port>`): only meaningful in *Custom* mode. In Cloud
  mode this will always say "connection refused" - **that's expected, not a bug**. Don't chase it.
- **Config Server** (`https://ai-game.dev/mcp` in Cloud mode): a generic reachability ping to the
  cloud relay itself, NOT proof that *this Editor instance* has a live session with it. It can
  say `SUCCESS: MCP server is reachable` while your specific Unity Editor is still sitting
  disconnected - the relay being up and your Editor's session with it being up are different
  things.

To see the real state, grep the Editor's own log for what its ConnectionManager is actually
doing:

```bash
grep -n "EnsureConnection\|DeviceTokenResponse\|OnAuthorizationRejected" ~/Library/Logs/Unity/Editor.log | tail -40
```

(macOS path shown; on Windows/Linux use the Editor's log location for that platform.) Look for
the sequence: `DeviceTokenResponse` succeeding, followed by repeated `EnsureConnection
Connection not available and auto-reconnect disabled` - that combination is the exact symptom
this skill fixes.

## The fix

1. In the Unity Editor for that project, open **Window → AI Game Developer — MCP**
   (⌘⌥A on Mac / Ctrl+Alt+A on Windows/Linux - exact shortcut from `MenuItems.cs`).
2. Confirm the mode toggle is on **Cloud** (or **Custom**, matching what `.mcp.json`/the project
   expects).
3. Look for one of these, and click it:
   - An amber/red **"Connection Required"** alert panel: *"Cloud authorization is complete but
     Unity is not connected to the server."* with a **Connect** button - this is the exact panel
     for this exact symptom (only shown when: Cloud mode + a token exists + currently
     disconnected + `KeepConnected` is false).
   - If that panel isn't visible, use the main connection row's toggle button (top of the
     window) - it reads **"Connect"** when disconnected, **"Disconnect"** when connected. Click
     **Connect**.
   - Only if there's genuinely no token yet (an **"Authorization Required"** panel instead):
     click **Authorize**, complete the device-code flow in the browser, then come back for the
     Connect step above - authorizing does not automatically connect.
4. Either click sets `KeepConnected = true`, saves it, and calls into the plugin's
   `UnityBuildAndConnect()` → `ConnectIfNeeded()` - watch the connection status dot/text next to
   the toggle button flip to "Connected."
5. **Relaunch or resume the AI agent's CLI session.** A live Claude Code (or other MCP client)
   session that already marked the server "requires re-authorization" will NOT pick up a freshly
   reconnected server on its own - it cached the failure. Restart the session (or
   `claude --resume <session-id>` from the project directory) after step 4, not before.

## If it still won't connect

- Check `.claude/settings.local.json` (or the equivalent MCP client config) for
  `enabledMcpjsonServers` / `disabledMcpjsonServers` both listing the same server name at once -
  that contradictory state silently blocks the connection regardless of what the Editor UI says,
  and has caused exactly this confusion before.
- Confirm the project's `.mcp.json` URL/transport matches what the Editor window's mode toggle
  is actually set to (Cloud vs Custom) - a mismatch here means the CLI and the Editor are
  configured for two different servers and neither side is "wrong," they're just not talking to
  the same endpoint.
- `unity-mcp-cli wait-for-ready /path/to/project` will block and poll until the server actually
  answers tool calls, instead of guessing from a point-in-time `status` check.
