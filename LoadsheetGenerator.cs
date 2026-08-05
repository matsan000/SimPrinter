using System.Globalization;

namespace SimPrinter
{
    /// <summary>
    /// Builds preliminary and final loadsheet tickets. The preliminary sheet mirrors the
    /// SimBrief numbers as-is; the final sheet applies a small randomized last-minute
    /// change to pax count (a few no-shows/late adds), with cargo and ZFW/TOW following
    /// from that same delta - each passenger is assumed to bring ~84 kg of body weight and
    /// ~20 kg of bags - then keeps that same result for as long as the current flight plan
    /// stays loaded, so reprints match.
    /// </summary>
    public static class LoadsheetGenerator
    {
        private const double PaxWeightKg = 84.0;
        private const double BagWeightPerPaxKg = 20.0;
        private const double KgToLbs = 2.2046226218;

        public sealed class LoadsheetValues
        {
            public required int PaxCount { get; init; }
            public required double CargoWeight { get; init; }
            public required double Zfw { get; init; }
            public required double Tow { get; init; }
            public int? PaxDelta { get; init; }
            public double? CargoDelta { get; init; }
            public double? ZfwDelta { get; init; }
            public double? TowDelta { get; init; }
        }

        public static LoadsheetValues BuildPreliminaryValues(SimBriefFlightPlan fp)
        {
            return new LoadsheetValues
            {
                PaxCount = ParseInt(fp.PaxCount),
                CargoWeight = ParseDouble(fp.CargoWeight),
                Zfw = ParseDouble(fp.Zfw),
                Tow = ParseDouble(fp.Tow),
            };
        }

        public static LoadsheetValues BuildFinalValues(SimBriefFlightPlan fp, Random rng)
        {
            int pax = ParseInt(fp.PaxCount);
            double cargo = ParseDouble(fp.CargoWeight);
            double zfw = ParseDouble(fp.Zfw);
            double tow = ParseDouble(fp.Tow);
            double takeoffFuel = ParseDouble(fp.TakeoffFuel);

            // A handful of no-shows/late adds, centered on zero (three -1/0/+1 draws summed
            // gives a small bell-shaped spread of -3..+3, not a flat coin flip).
            int paxDelta = 0;
            for (int i = 0; i < 3; i++) paxDelta += RandomTrit(rng);

            // Each passenger added or removed takes their bags with them. SimBrief's own
            // weight fields (Zfw/Tow/CargoWeight) are already in fp.Units, so the fixed
            // per-passenger constants (defined in kg) need converting to match.
            double paxWeight = ConvertFromKg(PaxWeightKg, fp.Units);
            double bagWeightPerPax = ConvertFromKg(BagWeightPerPaxKg, fp.Units);

            double cargoDelta = paxDelta * bagWeightPerPax;
            double weightDelta = paxDelta * paxWeight + cargoDelta;

            int newPax = Math.Max(0, pax + paxDelta);
            double newCargo = Math.Max(0, cargo + cargoDelta);

            double newZfw = zfw + weightDelta;
            double newTow = newZfw + takeoffFuel;

            return new LoadsheetValues
            {
                PaxCount = newPax,
                CargoWeight = newCargo,
                Zfw = newZfw,
                Tow = newTow,
                PaxDelta = paxDelta,
                CargoDelta = cargoDelta,
                ZfwDelta = newZfw - zfw,
                TowDelta = newTow - tow,
            };
        }

        public static byte[] BuildTicket(SimBriefFlightPlan fp, string label, LoadsheetValues values)
        {
            var b = new EscPosBuilder();
            b.Init();
            b.AlignLeft();

            b.Bold(true);
            b.Line("LOAD SHEET");
            b.Line(label);
            b.Bold(false);
            b.Divider('=');

            b.Line($"{fp.Callsign}  {fp.OriginIcao}-{fp.DestIcao}");
            b.Line($"{fp.AircraftReg} {fp.AircraftName}");
            b.Divider();

            b.KeyValue("PAX:", FormatPax(values));
            b.KeyValue("CARGO:", FormatCargo(values, fp.Units));
            b.Divider();

            b.KeyValue("ZFW:", FormatWeightWithDelta(values.Zfw, values.ZfwDelta, fp.Units));
            b.KeyValue("MAX ZFW:", $"{fp.MaxZfw} {fp.Units}");
            b.KeyValue("TOW:", FormatWeightWithDelta(values.Tow, values.TowDelta, fp.Units));
            b.KeyValue("MAX TOW:", $"{fp.MaxTow} {fp.Units}");
            b.Divider();

            b.KeyValue("TAKEOFF FUEL:", $"{fp.TakeoffFuel} {fp.Units}");
            b.Divider('=');

            b.FeedLines(3);
            b.Cut();

            return b.Build();
        }

        private static string FormatPax(LoadsheetValues v)
        {
            string s = v.PaxCount.ToString(CultureInfo.InvariantCulture);
            if (v.PaxDelta.HasValue) s += $" ({FormatSignedInt(v.PaxDelta.Value)})";
            return s;
        }

        private static string FormatCargo(LoadsheetValues v, string units) =>
            FormatWeightWithDelta(v.CargoWeight, v.CargoDelta, units);

        private static string FormatWeightWithDelta(double weight, double? delta, string units)
        {
            string s = $"{FormatWeight(weight)} {units}";
            if (delta.HasValue) s += $" ({FormatSignedWeight(delta.Value)})";
            return s;
        }

        private static double ConvertFromKg(double kg, string units) =>
            IsPounds(units) ? kg * KgToLbs : kg;

        private static bool IsPounds(string units) =>
            units.Trim().Equals("lbs", StringComparison.OrdinalIgnoreCase) ||
            units.Trim().Equals("lb", StringComparison.OrdinalIgnoreCase);

        private static int RandomTrit(Random rng) => rng.Next(0, 3) - 1;

        private static string FormatWeight(double w) => Math.Round(w).ToString("F0", CultureInfo.InvariantCulture);

        private static string FormatSignedInt(int v) => v >= 0 ? $"+{v}" : v.ToString(CultureInfo.InvariantCulture);

        private static string FormatSignedWeight(double v) => v >= 0 ? $"+{FormatWeight(v)}" : FormatWeight(v);

        private static int ParseInt(string s) =>
            int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static double ParseDouble(string s, double fallback = 0) =>
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
