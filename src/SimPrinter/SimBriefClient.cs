using System.Net.Http;
using System.Text.Json;

namespace SimPrinter
{
    public static class SimBriefClient
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        /// <summary>
        /// Fetches the pilot's latest SimBrief OFP.
        /// Accepts either a numeric SimBrief pilot ID or a SimBrief username.
        /// </summary>
        public static async Task<SimBriefFlightPlan> FetchLatestAsync(string userIdentifier)
        {
            if (string.IsNullOrWhiteSpace(userIdentifier))
                throw new ArgumentException("Enter your SimBrief username or pilot ID first.");

            userIdentifier = userIdentifier.Trim();

            // SimBrief accepts either "userid" (numeric) or "username" (text)
            string param = userIdentifier.All(char.IsDigit) ? "userid" : "username";
            string url = $"https://www.simbrief.com/api/xml.fetcher.php?{param}={Uri.EscapeDataString(userIdentifier)}&json=1";

            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not reach SimBrief (network error): {ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception($"SimBrief returned HTTP {(int)response.StatusCode}. Check your username/ID.");

            string json = await response.Content.ReadAsStringAsync();

            // Save the raw response next to the exe for debugging field-name mismatches
            try
            {
                string debugPath = Path.Combine(AppContext.BaseDirectory, "SimBriefRaw.json");
                await File.WriteAllTextAsync(debugPath, json);
            }
            catch
            {
                // non-critical, ignore if we can't write (e.g. read-only folder)
            }

            using var doc = JsonDocument.Parse(json);

            // SimBrief embeds a "fetch" status block indicating success/failure
            if (doc.RootElement.TryGetProperty("fetch", out var fetchEl) &&
                fetchEl.TryGetProperty("status", out var statusEl))
            {
                string status = statusEl.GetString() ?? "";
                if (!status.Contains("Success", StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"SimBrief error: {status}");
            }

            return SimBriefFlightPlan.FromJson(doc);
        }
    }
}
