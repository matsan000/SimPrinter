namespace SimPrinter
{
    /// <summary>
    /// Loads the user-editable template that controls what gets printed on the flight
    /// plan ticket. The template is plain text so it can be edited in Notepad; see
    /// DefaultContent below for the mini-syntax (dividers, bold, two-column lines,
    /// placeholders, comments).
    /// </summary>
    public static class TicketTemplate
    {
        public static string TemplatePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimPrinter", "TicketTemplate.txt");

        public static string[] LoadLines()
        {
            EnsureFileExists();
            return File.ReadAllLines(TemplatePath);
        }

        public static void EnsureFileExists()
        {
            if (File.Exists(TemplatePath)) return;

            string dir = Path.GetDirectoryName(TemplatePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(TemplatePath, DefaultContent);
        }

        private const string DefaultContent =
"""
# SimPrinter ticket template
# Edit this file to control exactly what gets printed on the flight plan ticket,
# then just save and print again - no need to restart SimPrinter.
#
# Syntax:
#   Lines starting with #    are comments and are never printed
#   ---                      prints a thin divider line
#   ===                      prints a thick divider line
#   (an empty line)          prints a blank line
#   **some text**            prints "some text" in bold
#   Label|Value              prints a two-column line (label left, value right)
#   anything else            prints that line as-is (long lines auto word-wrap)
#
# Available placeholders (case-sensitive):
#   {Callsign} {AirlineIcao} {FlightNumber}
#   {AircraftIcao} {AircraftName} {AircraftReg}
#   {OriginIcao} {OriginIata} {OriginName}
#   {DestIcao} {DestIata} {DestName}
#   {AlternateIcao} {AlternateName}
#   {Route} {CruiseAltitude} {DistanceNm} {FlightTimeFormatted}
#   {SchedOutZulu} {SchedInZulu}
#   {Units} {BlockFuel} {TaxiFuel} {TakeoffFuel}
#   {Zfw} {Tow} {MaxZfw} {MaxTow} {PaxCount} {PaxWeightAvg} {CargoWeight}

**ACARS START**
===
**{Callsign}**
{AircraftName} ({AircraftReg})
---
{OriginIcao}/{OriginIata}  ->  {DestIcao}/{DestIata}
ALTN: {AlternateIcao}
---
Route:
{Route}
---
Cruise Alt:|{CruiseAltitude}
Distance:|{DistanceNm} nm
Flight Time:|{FlightTimeFormatted}
---
STD (out):|{SchedOutZulu}
STA (in):|{SchedInZulu}
---
Block Fuel:|{BlockFuel} {Units}
Taxi Fuel:|{TaxiFuel} {Units}
---
EZFW:|{Zfw} {Units}
ETOW:|{Tow} {Units}
---
Pax:|{PaxCount}
Cargo:|{CargoWeight} {Units}
===
**ACARS END**
""";
    }
}
