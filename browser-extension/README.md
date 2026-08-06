# SimPrinter - SimBrief Performance Print

Firefox extension that adds a **🖨 Print** button to SimBrief's Takeoff/Landing
Performance calculator. Click it and the calculation prints straight to whatever printer
SimPrinter is configured to use - no more copy/paste into SimPrinter's Print Text dialog.

<p>
  <img src="../assets/screenshots/extension-print-button.png" width="420" alt="SimPrinter print button on SimBrief's Takeoff Performance calculator">
</p>

## How it works

SimPrinter can run a small localhost-only server (`127.0.0.1:39901`) when "Allow the
SimPrinter browser extension to print" is enabled in Settings -> Printer & General
Settings. This extension watches SimBrief's performance calculator for a result, adds the
Print button, and on click sends the calculation text to that local server, which reflows
it to fit your printer's width and prints it - the same pipeline as SimPrinter's own Print
Text feature.

The server only listens on `127.0.0.1` - nothing outside your own machine can reach it,
and no data is collected, stored, or sent anywhere else by the extension itself.

## Installing (recommended)

1. In SimPrinter, open **Settings -> Printer & General Settings** and turn on **"Allow the
   SimPrinter browser extension to print"**, then Save.
2. Download `simprinter-performance-print-signed.xpi` from this repo's
   [Releases](https://github.com/matsan000/SimPrinter/releases).
3. In Firefox, go to `about:addons`, click the gear icon (top right) -> **"Install Add-on
   From File..."** -> select the `.xpi`. Dragging the file into a Firefox window works too.

This build is signed by Mozilla (submitted unlisted/self-distributed, not published to the
public store), so it installs normally and stays installed - no developer mode, no
reloading it every time Firefox restarts.

Once installed, the Print button shows up in two places:

- The Takeoff/Landing Performance popup on a flight's briefing page - next to
  "Information" in the Calculation Output panel.
- The standalone [Performance & Tools](https://dispatch.simbrief.com/tools) page - in the
  "Raw Output" view's header, next to the Formatted/Raw Output toggle.

## Building and signing your own copy

If you'd rather not trust a prebuilt binary, or you've made changes to the source:

1. Zip up `manifest.json`, `background.js`, and `content.js` (root of the archive, not in
   a subfolder) and rename it to `.xpi` - or just load the unzipped files directly for
   testing via `about:debugging#/runtime/this-firefox` -> **Load Temporary Add-on...**
   (lasts until Firefox restarts, no signing needed).
2. For a permanent install, create a free account at
   [addons.mozilla.org/developers](https://addons.mozilla.org/developers/) and submit the
   `.xpi` as **unlisted**. Mozilla's automated validation and signing for a small extension
   like this normally takes seconds to minutes. You'll get back a signed `.xpi` - that's
   the one to install via `about:addons` as described above.

Note: as of November 2025, Mozilla requires every extension to declare what user data it
collects in `manifest.json` (`browser_specific_settings.gecko.data_collection_permissions`)
- already set to `["none"]` here, since nothing is collected.

## Files

- `manifest.json` - extension manifest (Firefox, Manifest V2)
- `content.js` - finds the calculation result on the SimBrief page and adds the Print button
- `background.js` - relays the print request to SimPrinter's local server (content scripts
  are subject to the page's CSP, which can block a direct fetch to localhost; the background
  script isn't)
