using System.Globalization;
using System.Text.Json;

namespace SimPrinter
{
    /// <summary>
    /// A flattened, display-ready subset of a SimBrief OFP (flight plan).
    /// Parsing is defensive: any missing/renamed field just falls back to "N/A"
    /// instead of throwing, since SimBrief's JSON schema has some undocumented quirks.
    /// If you see a lot of "N/A" values, check the saved SimBriefRaw.json (written next
    /// to the .exe after every fetch) and adjust the property paths below to match.
    /// </summary>
    public class SimBriefFlightPlan
    {
        public string Callsign { get; set; } = "N/A";
        public string AirlineIcao { get; set; } = "N/A";
        public string FlightNumber { get; set; } = "N/A";

        public string AircraftIcao { get; set; } = "N/A";
        public string AircraftName { get; set; } = "N/A";
        public string AircraftReg { get; set; } = "N/A";

        public string OriginIcao { get; set; } = "N/A";
        public string OriginIata { get; set; } = "N/A";
        public string OriginName { get; set; } = "N/A";

        public string DestIcao { get; set; } = "N/A";
        public string DestIata { get; set; } = "N/A";
        public string DestName { get; set; } = "N/A";

        public string AlternateIcao { get; set; } = "N/A";
        public string AlternateName { get; set; } = "N/A";

        public string Route { get; set; } = "N/A";
        public string CruiseAltitude { get; set; } = "N/A";
        public string DistanceNm { get; set; } = "N/A";
        public string FlightTimeFormatted { get; set; } = "N/A";

        public string Units { get; set; } = "kg";
        public string BlockFuel { get; set; } = "N/A";
        public string TaxiFuel { get; set; } = "N/A";
        public string TakeoffFuel { get; set; } = "N/A";

        public string Zfw { get; set; } = "N/A";
        public string Tow { get; set; } = "N/A";
        public string MaxZfw { get; set; } = "N/A";
        public string MaxTow { get; set; } = "N/A";

        public string PaxCount { get; set; } = "N/A";
        public string PaxWeightAvg { get; set; } = "N/A";
        public string CargoWeight { get; set; } = "N/A";

        public string SchedOutZulu { get; set; } = "N/A";
        public string SchedInZulu { get; set; } = "N/A";

        public static SimBriefFlightPlan FromJson(JsonDocument doc)
        {
            var root = doc.RootElement;
            var fp = new SimBriefFlightPlan
            {
                AirlineIcao = GetProp(root, "general", "icao_airline"),
                FlightNumber = GetProp(root, "general", "flight_number"),
                Route = GetProp(root, "general", "route"),
                DistanceNm = GetProp(root, "general", "route_distance"),

                AircraftIcao = GetProp(root, "aircraft", "icaocode"),
                AircraftName = GetProp(root, "aircraft", "name"),
                AircraftReg = GetProp(root, "aircraft", "reg"),

                OriginIcao = GetProp(root, "origin", "icao_code"),
                OriginIata = GetProp(root, "origin", "iata_code"),
                OriginName = GetProp(root, "origin", "name"),

                DestIcao = GetProp(root, "destination", "icao_code"),
                DestIata = GetProp(root, "destination", "iata_code"),
                DestName = GetProp(root, "destination", "name"),

                AlternateIcao = GetProp(root, "alternate", "icao_code"),
                AlternateName = GetProp(root, "alternate", "name"),

                Units = GetProp(root, "params", "units"),

                BlockFuel = GetProp(root, "fuel", "plan_ramp"),
                TaxiFuel = GetProp(root, "fuel", "taxi"),
                TakeoffFuel = GetProp(root, "fuel", "plan_takeoff"),

                Zfw = GetProp(root, "weights", "est_zfw"),
                Tow = GetProp(root, "weights", "est_tow"),
                MaxZfw = GetProp(root, "weights", "max_zfw"),
                MaxTow = GetProp(root, "weights", "max_tow"),
                PaxCount = GetProp(root, "weights", "pax_count_actual"),
                PaxWeightAvg = GetProp(root, "weights", "pax_weight"),
                CargoWeight = GetProp(root, "weights", "cargo"),
            };

            // Callsign: prefer ATC callsign, fall back to airline+flight number
            var atcCallsign = GetProp(root, "atc", "callsign");
            fp.Callsign = atcCallsign != "N/A" ? atcCallsign : $"{fp.AirlineIcao}{fp.FlightNumber}";

            // Cruise altitude (feet -> flight level display)
            var altFt = GetProp(root, "general", "initial_altitude");
            fp.CruiseAltitude = FormatAltitude(altFt);

            // Flight time: seconds -> "Hh Mm"
            var estSeconds = GetProp(root, "times", "est_time_enroute");
            fp.FlightTimeFormatted = FormatDurationSeconds(estSeconds);

            // Scheduled out/in: epoch seconds -> "HH:mm Z"
            fp.SchedOutZulu = FormatEpochZulu(GetProp(root, "times", "sched_out"));
            fp.SchedInZulu = FormatEpochZulu(GetProp(root, "times", "sched_in"));

            return fp;
        }

        private static string GetProp(JsonElement root, params string[] path)
        {
            JsonElement current = root;
            foreach (var p in path)
            {
                // SimBrief returns some sections (e.g. "alternate") as a one-or-more-element
                // array rather than a single object; use the first entry in that case.
                if (current.ValueKind == JsonValueKind.Array)
                {
                    if (current.GetArrayLength() == 0) return "N/A";
                    current = current[0];
                }

                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(p, out var next))
                    return "N/A";
                current = next;
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString() ?? "N/A",
                JsonValueKind.Number => current.ToString(),
                _ => "N/A"
            };
        }

        private static string FormatAltitude(string feetStr)
        {
            if (int.TryParse(feetStr, out int feet) && feet > 0)
            {
                return feet >= 18000 ? $"FL{feet / 100}" : $"{feet} ft";
            }
            return "N/A";
        }

        private static string FormatDurationSeconds(string secondsStr)
        {
            if (long.TryParse(secondsStr, out long seconds) && seconds > 0)
            {
                var ts = TimeSpan.FromSeconds(seconds);
                return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m";
            }
            return "N/A";
        }

        private static string FormatEpochZulu(string epochStr)
        {
            if (long.TryParse(epochStr, out long epoch) && epoch > 0)
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                return dt.ToString("HH:mm", CultureInfo.InvariantCulture) + "Z";
            }
            return "N/A";
        }
    }
}
