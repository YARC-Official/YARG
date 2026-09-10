# YARG Event Mode (YAQ)

This fork of [YARG](https://github.com/YARC-Official/YARG) adds **YAQ Event Mode**: the game is driven by the local [YAQ](../yaq) queue app. Guests browse songs and join from their phones; YARG only shows the ready (difficulty select) and score screens.

## Launch

1. Start YAQ (`cd ~/Projects/yaq && npm start`).
2. Open Unity and build/run this project, **or** launch a built binary with:

```bash
./YARG -yaq-event -yaq-url "ws://127.0.0.1:3000/ws?role=yarg"
```

## Behavior

- Main menu / music library are skipped while `-yaq-event` is set.
- Idle screen shows **up next** player names + song from YAQ `queue.preview`.
- Admin launches the on-deck set in YAQ → YARG opens Difficulty Select for that song.
- After the score screen Continue → idle again for the next group.
- **Mics stay hot** (vocal monitoring forced on) so the host can announce between songs.
- Song library is pushed to YAQ via `library.sync` after scan completes (authoritative hashes).

## Files added

- `Assets/Script/YAQ/EventMode.cs`
- `Assets/Script/YAQ/YaqBridgeClient.cs`
- `Assets/Script/YAQ/EventModeController.cs`

## Notes

- Bind controllers/profiles once in a normal YARG session before the event if possible.
- YAQ folder-scan hashes are provisional until YARG syncs; prefer launching YARG Event before guests queue when possible.
- LGPL-3.0 (same as upstream YARG). Keep this fork clearly marked as modified for events.
