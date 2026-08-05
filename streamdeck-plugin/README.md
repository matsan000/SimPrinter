# SimPrinter Stream Deck plugin

Adds three Stream Deck buttons that trigger prints in a running SimPrinter instance:

- **Print Flight Plan**
- **Print Preliminary Loadsheet**
- **Print Final Loadsheet**

## How it works

SimPrinter now runs a small localhost-only HTTP server (`127.0.0.1:47653`) while it's
open. The Stream Deck plugin is a background process that Stream Deck launches; on a key
press it sends a request to that local server, which triggers the exact same print logic
as clicking the button inside SimPrinter itself. Nothing is exposed outside your machine.

Because of this, **SimPrinter must be running** for the buttons to do anything, and a
flight plan must be **loaded** for the buttons to actually print (same requirement as the
in-app buttons). If either isn't true, the Stream Deck key will flash a red "X".

## Installing

1. Close Stream Deck if it's open.
2. Copy the whole `com.simprinter.actions.sdPlugin` folder (not just its contents) into:
   `%APPDATA%\Elgato\StreamDeck\Plugins\`
3. Start Stream Deck again. A "SimPrinter" category should appear in the actions list on
   the right, with the three actions above.
4. Drag whichever actions you want onto keys.
5. Launch SimPrinter, load a flight plan, and press the keys.

Stream Deck bundles its own Node.js runtime to run plugins like this one, so you don't
need Node installed separately.

## A note on testing

I built and unit-tested the plugin's logic (WebSocket registration, HTTP calls, success/
failure feedback) by executing it against a mocked Stream Deck connection and mocked
SimPrinter responses, and separately tested SimPrinter's HTTP server directly with curl -
both worked as expected before this was ever tested against real Stream Deck software.

**Confirmed working against real Stream Deck 7.5.0** (see fix below) - buttons load a
flight plan, print, and give the correct green check / red X feedback.

## Troubleshooting: key shows a red X and nothing happens, even though SimPrinter is open

If Stream Deck's own log (`%APPDATA%\Elgato\StreamDeck\logs\StreamDeck.log`) shows
`[com.simprinter.actions] The plugin has no attached client` and grepping the log for
`Plugin connected` never shows an entry for `com.simprinter.actions` (every other plugin
does), Stream Deck isn't launching the plugin process at all - check `node.exe` in Task
Manager; you won't find one for it.

The cause (already fixed in this manifest, documenting in case a future edit reintroduces
it): a Node.js-based Stream Deck plugin's `manifest.json` **must** declare a `"Nodejs"`
block, e.g.:

```json
"Nodejs": {
  "Version": "20"
}
```

Without it, Stream Deck doesn't recognize `CodePath: "plugin.js"` as something to run
through its bundled Node runtime, and silently never spawns it - no error dialog, no
useful log line, the plugin just never connects. A root-level `"UUID"` field (the
plugin's own identity, distinct from each action's UUID) is also required and is easy to
forget if you're hand-writing a manifest instead of generating one with Elgato's CLI.

If you ever need to reinstall: fully quit Stream Deck via its system tray icon (not just
the window) before copying files, since it can cache a broken plugin registration across
in-place file edits.
