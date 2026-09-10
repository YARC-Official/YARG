# YARG Event Mode (YAQ)

This fork of [YARG](https://github.com/YARC-Official/YARG) adds **YAQ Event Mode**: the game is driven by the local [YAQ](../yaq) queue app. Guests browse songs and join from their phones; YARG only shows the ready (difficulty select) and score screens.

## Launch

1. Start YAQ (`cd ~/Projects/yaq && npm start`).
2. Open Unity and enable **Settings → Experimental → YAQ stream**, **or** launch a built binary with event mode:

```bash
# Recommended
./YARG -event-mode -yaq-url "ws://127.0.0.1:3000/ws?role=yarg"

# Aliases / shortcuts
./YARG -yaq-event
./YARG -yaq-url "ws://127.0.0.1:3000/ws?role=yarg"   # URL alone also enables event mode
```

| Flag | Effect |
|------|--------|
| `-event-mode` / `-yaq-event` | Force Event Mode on at launch (connects to YAQ) |
| `-yaq-url <url>` | Override bridge URL (default `ws://127.0.0.1:3000/ws?role=yarg`); also enables Event Mode |

The Experimental settings toggle connects/disconnects without restarting. Launch flags still force the stream on.

## Behavior

- Main menu is skipped when event flag `skipMainMenu` is on (default).
- Idle HUD shows **READY/SCORE** (current set) and **UP NEXT** from `queue.preview`.
- Admin launches the on-deck set in YAQ → YARG applies players and opens Difficulty Select.
- After the score screen Continue → idle again for the next group.
- **Hot mic** (flag `hotMic`) keeps vocal monitoring up for host announcements.
- Song library is pushed to YAQ via `library.sync` after scan completes (authoritative hashes).

## Event flags (YAQ → YARG)

Pushed as `{ type: "settings.update", flags }` on connect and when admin saves. YARG replies with `settings.ack`. Defaults:

| Flag | Default | Effect |
|------|---------|--------|
| `hotMic` | `true` | Force vocal monitoring for host talkback |
| `showUpNextHud` | `true` | Show idle OnGUI up-next / ready HUD |
| `skipMainMenu` | `true` | Hide main menu while stream is active |
| `openDifficultySelect` | `true` | Open Difficulty Select on `set.prepare` / `set.launch` |

Toggle these under **YAQ Admin → YARG event flags**.

## Enter / exit Event Mode from YAQ

While YARG is connected, Admin → **Enter Event Mode** / **Exit Event Mode** (or `POST /api/admin/yarg/event-mode` with `{ "enabled": true|false }`).

- **Exit** suspends Event Mode (menus/HUD/hot mic/set launch off) but **keeps the WebSocket** so YAQ can re-enter later.
- **Enter** resumes Event Mode behaviors and re-syncs the library.
- Fully disconnecting still uses YARG’s Experimental **YAQ stream** toggle (or quitting the game).

## Message catalog

| Type | Direction | Purpose |
|------|-----------|---------|
| `hello` | both | Handshake |
| `library.sync` | YARG → YAQ | Authoritative song list |
| `library.request` | YAQ → YARG | Ask YARG to re-push library |
| `queue.preview` | YAQ → YARG | On-deck song + players for HUD |
| `set.prepare` | YAQ → YARG | Apply players, set current song, ready |
| `set.launch` | YAQ → YARG | Ensure Difficulty Select is open |
| `state` | YARG → YAQ | `idle` / `ready` / `playing` / `score` |
| `ready` | YARG → YAQ | Prepare acknowledged |
| `song.ended` | YARG → YAQ | Set complete + score payload |
| `settings.update` | YAQ → YARG | Event flags |
| `settings.ack` / `settings.report` | YARG → YAQ | Flag confirmation |
| `eventmode.enter` / `eventmode.exit` | YAQ → YARG | Resume / suspend Event Mode (bridge stays connected) |
| `eventmode.state` | YARG → YAQ | `{ enabled, suspended }` after enter/exit |
| `error` | YARG → YAQ | e.g. `song_not_found`, `eventmode_suspended` |

## Files

- `Assets/Script/YAQ/EventMode.cs` — flags + `IsActive`
- `Assets/Script/YAQ/YaqBridgeClient.cs` — WebSocket client
- `Assets/Script/YAQ/EventModeController.cs` — HUD, bridge handlers, hot mic

## Notes

- Bind controllers/profiles once in a normal YARG session before the event if possible.
- YAQ folder-scan hashes are provisional until YARG syncs; prefer launching YARG Event before guests queue when possible.
- LGPL-3.0 (same as upstream YARG). Keep this fork clearly marked as modified for events.
