using System.Net.Http;
using System.Text.Json;

namespace SimPrinter
{
    /// <summary>
    /// Fetches METAR and ATIS text from VATSIM's public data feeds. No API key required,
    /// but unlike SayIntentions this only covers real-world weather (metar.vatsim.net just
    /// proxies NOAA) and ATIS text actually being broadcast by an online VATSIM controller -
    /// there's no synthetic/always-available ATIS like SayIntentions provides.
    /// </summary>
    public static class VatsimClient
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public static async Task<string> GetMetarAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao))
                throw new ArgumentException("Enter an ICAO airport code first.");

            icao = icao.Trim().ToUpperInvariant();
            string url = $"https://metar.vatsim.net/{Uri.EscapeDataString(icao)}";

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not reach VATSIM (network error): {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"VATSIM returned HTTP {(int)response.StatusCode}.");

            string text = (await response.Content.ReadAsStringAsync()).Trim();
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception($"No METAR available for {icao}.");

            return text;
        }

        /// <summary>
        /// ATIS has no per-airport endpoint on VATSIM - it only exists as part of the full
        /// network data feed, tied to whichever controller is currently online broadcasting it.
        /// </summary>
        public static async Task<string> GetAtisAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao))
                throw new ArgumentException("Enter an ICAO airport code first.");

            icao = icao.Trim().ToUpperInvariant();

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync("https://data.vatsim.net/v3/vatsim-data.json");
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not reach VATSIM (network error): {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"VATSIM returned HTTP {(int)response.StatusCode}.");

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("atis", out var atisList) || atisList.ValueKind != JsonValueKind.Array)
                throw new Exception("VATSIM data feed did not include ATIS data.");

            var matches = new List<(string Callsign, string Text)>();
            foreach (var entry in atisList.EnumerateArray())
            {
                if (!entry.TryGetProperty("callsign", out var callsignEl) || callsignEl.ValueKind != JsonValueKind.String)
                    continue;

                string callsign = callsignEl.GetString() ?? "";
                if (!callsign.StartsWith(icao + "_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!entry.TryGetProperty("text_atis", out var linesEl) || linesEl.ValueKind != JsonValueKind.Array)
                    continue;

                var lines = new List<string>();
                foreach (var lineEl in linesEl.EnumerateArray())
                {
                    if (lineEl.ValueKind == JsonValueKind.String)
                        lines.Add(lineEl.GetString() ?? "");
                }

                if (lines.Count > 0)
                    matches.Add((callsign, string.Join(" ", lines)));
            }

            if (matches.Count == 0)
                throw new Exception($"No ATIS online for {icao} on VATSIM.");

            return matches.Count == 1
                ? matches[0].Text
                : string.Join("\n\n", matches.ConvertAll(m => $"{m.Callsign}: {m.Text}"));
        }
    }
}
