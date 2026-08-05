# SimPrinter

A companion app for Microsoft Flight Simulator that pulls your SimBrief flight plan and
prints flight-plan tickets, loadsheets, weather, and gate assignments to a real 58mm
thermal receipt printer (or any Windows printer) - so your virtual dispatch paperwork
feels like an actual paper OFP instead of a browser tab.

## Features

- **SimBrief integration** - fetches your latest OFP by username or pilot ID and prints a
  formatted flight-plan ticket, with a fully user-editable template
  (`Settings -> Edit Ticket Template`)
- **Preliminary and final loadsheets** - generated from the SimBrief plan, with optional
  randomized last-minute pax/cargo changes for the final loadsheet
- **Live weather and gate assignment** via [SayIntentions](https://sayintentions.ai/) -
  print METAR/ATIS or a requested gate as an ACARS-style ticket
- **Off-block countdown** - reads the sim's own Zulu clock via SimConnect and shows a live
  countdown to your scheduled off-block time in the footer, so it stays correct even with
  time acceleration or a non-real-time sim clock
- **Print anything else** - a "Print Text" dialog for pasting arbitrary text (e.g. a
  SimBrief takeoff/landing performance report copied from its web calculator) straight to
  the printer
- **Serial or Windows printer output** - talk directly to a COM-port thermal printer, or
  use a printer already installed via a Windows driver
- **Dark mode**, a resizable UI that scales down for small/low-resolution screens, and a
  companion [Stream Deck plugin](streamdeck-plugin/) for triggering prints from physical
  keys

## Installing

Grab the latest `SimPrinter-x.y.z.msi` from the [Releases](../../releases) page and run
it - it's a self-contained installer with no other dependencies to install first.

## Building from source

- Windows 10/11 (64-bit)
- [Visual Studio 2022 Community](https://visualstudio.microsoft.com/) (free) with the
  **.NET desktop development** workload, or the .NET 8 SDK standalone
- A 58mm thermal printer that speaks ESC/POS (optional - the app works without one, you
  just won't be able to print), connected either:
  - via USB or paired Bluetooth (SPP profile) - shows up as a **COM port** in Device
    Manager, or
  - installed as a normal Windows printer via its bundled driver

Open `SimPrinter.csproj` in Visual Studio and press **F5**, or from the command line:

```
dotnet build -c Release
```

### About `lib/SimConnect/`

This repo vendors Microsoft's SimConnect client (`Microsoft.FlightSimulator.SimConnect.dll`
+ `SimConnect.dll`, plus the VC++ redistributable DLLs `SimConnect.dll` itself needs) so the
app can read the sim's Zulu clock. These are Microsoft's own redistributable SDK files, not
something built in this repo, and they aren't covered by this project's MIT license - see
[LICENSE](LICENSE). This is standard practice for third-party MSFS add-ons; if you'd rather
pull them yourself, they come with the free [MSFS SDK](https://docs.flightsimulator.com/).

## Usage

1. **Settings**: enter your SimBrief username or numeric pilot ID, and (optionally) a
   [SayIntentions](https://sayintentions.ai/) API key for weather/gate lookups. Choose your
   printer connection - a COM port + baud rate, or an installed Windows printer.
2. Back on the main screen, click **Load Flight Plan** to pull your latest SimBrief OFP.
3. Print whichever tickets you need from the **Print** card, look up weather/gate under
   **Get Weather**, or paste arbitrary text via **Print Text**.

### Troubleshooting printing

- **Serial mode not printing**: confirm the COM port in Device Manager under "Ports (COM &
  LPT)". Bluetooth printers need to be paired first in Windows Bluetooth settings, then
  show up as an "Outgoing" SPP COM port.
- **Windows Printer mode fails**: not every driver supports passing raw ESC/POS bytes
  through (the `RAW` datatype). If it fails, try Serial/COM instead - it bypasses the
  Windows print spooler entirely, which is more reliable for generic thermal printers.
- **Garbled characters**: output is encoded as CP437, the most common ESC/POS default.
  If your printer uses a different code page, that's a one-line change in
  `EscPosBuilder.cs`.

## Project structure

```
SimPrinter.csproj        Project file
Program.cs                Entry point
MainForm.cs / ConfigForm.cs / PastePrintForm.cs   UI
UiStyle.cs                 Shared theming and custom-drawn controls
SimBriefFlightPlan.cs      Flight plan data model + JSON parsing
SimBriefClient.cs          Calls the SimBrief API
SayIntentionsClient.cs     Weather/gate lookups
SimConnectClient.cs        Reads the sim's Zulu clock via SimConnect
LoadsheetGenerator.cs      Builds preliminary/final loadsheet values and tickets
EscPosBuilder.cs           Builds ESC/POS byte sequences and ticket layouts
TicketTemplate.cs          User-editable flight-plan ticket template
PrinterService.cs          Sends bytes via COM port or the Windows print spooler
RemoteControlServer.cs     Localhost HTTP server used by the Stream Deck plugin
Preferences.cs             Settings persistence (%APPDATA%\SimPrinter)
installer/                 WiX installer source (build-installer.ps1 to build the MSI)
streamdeck-plugin/         Companion Elgato Stream Deck plugin
```

## License

MIT - see [LICENSE](LICENSE). The vendored SimConnect files under `lib/SimConnect/` are
Microsoft's own redistributables and are not covered by that license.
