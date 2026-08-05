using System.Net.Http;
using System.Text.Json;

namespace SimPrinter
{
    public static class SayIntentionsClient
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// Fetches METAR or ATIS text for an airport via the SayIntentions.AI SAPI getWX endpoint.
        /// </summary>
        public static async Task<string> GetWeatherAsync(string apiKey, string icao, bool atis)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Enter your SayIntentions API key in Settings first.");

            if (string.IsNullOrWhiteSpace(icao))
                throw new ArgumentException("Enter an ICAO airport code first.");

            icao = icao.Trim().ToUpperInvariant();
            string url = $"https://apipri.sayintentions.ai/sapi/getWX?api_key={Uri.EscapeDataString(apiKey)}&icao={Uri.EscapeDataString(icao)}";

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not reach SayIntentions (network error): {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"SayIntentions returned HTTP {(int)response.StatusCode}. Check your API key.");

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("airports", out var airports) ||
                airports.ValueKind != JsonValueKind.Array || airports.GetArrayLength() == 0)
                throw new Exception($"No weather data returned for {icao}.");

            var airport = airports[0];
            string field = atis ? "atis" : "metar";

            if (!airport.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
                throw new Exception($"No {(atis ? "ATIS" : "METAR")} available for {icao}.");

            return value.GetString()!.Trim();
        }

        /// <summary>
        /// Fetches the current gate/parking assignment via the SayIntentions.AI SAPI
        /// getParking endpoint. Unlike getWX, this doesn't take an ICAO - it's tied to
        /// your current active flight/session on the SayIntentions side.
        /// </summary>
        public static async Task<string> GetParkingAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Enter your SayIntentions API key in Settings first.");

            string url = $"https://apipri.sayintentions.ai/sapi/getParking?api_key={Uri.EscapeDataString(apiKey)}";

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not reach SayIntentions (network error): {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"SayIntentions returned HTTP {(int)response.StatusCode}. Check your API key.");

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("parking", out var parking) || parking.ValueKind != JsonValueKind.Object)
                throw new Exception("No gate/parking assignment available. Make sure you have an active flight in SayIntentions.");

            string name = parking.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString() ?? "N/A"
                : "N/A";

            string heading = parking.TryGetProperty("heading", out var headingEl)
                ? headingEl.ToString()
                : "N/A";

            return $"Gate: {name}\nHeading: {heading}";
        }
    }
}
