using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SimPrinter
{
    /// <summary>
    /// Minimal builder for ESC/POS commands, targeting generic 58mm thermal printers
    /// (32 columns at standard font size). Text is encoded as CP437, the most common
    /// code page these printers default to. If your printer prints garbled accented
    /// characters, check its manual for which code page table it actually uses.
    /// </summary>
    public class EscPosBuilder
    {
        private const int LineWidth = 32; // typical for 58mm paper at normal font
        private readonly MemoryStream _stream = new();
        private readonly Encoding _encoding;

        public EscPosBuilder()
        {
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            _encoding = Encoding.GetEncoding(437); // CP437
        }

        public EscPosBuilder Init()
        {
            _stream.Write(new byte[] { 0x1B, 0x40 }); // ESC @
            return this;
        }

        public EscPosBuilder AlignLeft() => WriteBytes(0x1B, 0x61, 0x00);
        public EscPosBuilder AlignCenter() => WriteBytes(0x1B, 0x61, 0x01);

        public EscPosBuilder Bold(bool on) => WriteBytes(0x1B, 0x45, (byte)(on ? 1 : 0));

        public EscPosBuilder DoubleSize(bool on) => WriteBytes(0x1D, 0x21, (byte)(on ? 0x11 : 0x00));

        public EscPosBuilder Text(string text)
        {
            var bytes = _encoding.GetBytes(text);
            _stream.Write(bytes);
            return this;
        }

        public EscPosBuilder Line(string text = "")
        {
            Text(text);
            _stream.WriteByte(0x0A); // LF
            return this;
        }

        public EscPosBuilder Divider(char c = '-')
        {
            return Line(new string(c, LineWidth));
        }

        /// <summary>Two-column line: label left-aligned, value right-aligned, wrapped to LineWidth.</summary>
        public EscPosBuilder KeyValue(string label, string value)
        {
            string combined = label + value;
            if (combined.Length >= LineWidth)
            {
                Line(label);
                Line(value.PadLeft(LineWidth));
            }
            else
            {
                int padding = LineWidth - combined.Length;
                Line(label + new string(' ', padding) + value);
            }
            return this;
        }

        public EscPosBuilder FeedLines(int n = 3)
        {
            for (int i = 0; i < n; i++) _stream.WriteByte(0x0A);
            return this;
        }

        public EscPosBuilder Cut()
        {
            _stream.Write(new byte[] { 0x1D, 0x56, 0x42, 0x00 }); // GS V B 0 (partial cut with feed)
            return this;
        }

        public byte[] Build() => _stream.ToArray();

        private EscPosBuilder WriteBytes(params byte[] b)
        {
            _stream.Write(b);
            return this;
        }

        private static readonly Regex PlaceholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Builds a full flight plan summary ticket from a parsed SimBrief flight plan,
        /// rendering the user-editable template from <see cref="TicketTemplate"/>.
        /// </summary>
        public static byte[] BuildFlightPlanTicket(SimBriefFlightPlan fp)
        {
            var b = new EscPosBuilder();
            b.Init();
            b.AlignLeft();

            var placeholders = BuildPlaceholderMap(fp);
            foreach (var rawLine in TicketTemplate.LoadLines())
            {
                RenderLine(b, rawLine.Trim(), placeholders);
            }

            b.FeedLines(3);
            b.Cut();

            return b.Build();
        }

        /// <summary>
        /// Builds a METAR/ATIS ticket fetched from SayIntentions, wrapped in the same
        /// ACARS-style banner as the flight plan ticket.
        /// </summary>
        public static byte[] BuildWeatherTicket(string icao, string label, string weatherText)
        {
            var b = new EscPosBuilder();
            b.Init();
            b.AlignLeft();

            b.Bold(true);
            b.Line("ACARS START");
            b.Bold(false);
            b.Divider('=');

            b.Bold(true);
            b.Line($"{label} {icao}");
            b.Bold(false);
            b.Divider();

            WriteWrapped(b, weatherText);

            b.Divider('=');
            b.Bold(true);
            b.Line("ACARS END");
            b.Bold(false);

            b.FeedLines(3);
            b.Cut();

            return b.Build();
        }

        /// <summary>
        /// Builds a gate/parking assignment ticket fetched from SayIntentions, wrapped in
        /// the same ACARS-style banner as the flight plan ticket.
        /// </summary>
        public static byte[] BuildGateTicket(string gateInfo)
        {
            var b = new EscPosBuilder();
            b.Init();
            b.AlignLeft();

            b.Bold(true);
            b.Line("ACARS START");
            b.Bold(false);
            b.Divider('=');

            b.Bold(true);
            b.Line("GATE ASSIGNMENT");
            b.Bold(false);
            b.Divider();

            foreach (var line in gateInfo.Split('\n'))
                WriteWrapped(b, line);

            b.Divider('=');
            b.Bold(true);
            b.Line("ACARS END");
            b.Bold(false);

            b.FeedLines(3);
            b.Cut();

            return b.Build();
        }

        /// <summary>
        /// Builds a ticket from arbitrary pasted text (e.g. a SimBrief takeoff/landing
        /// performance report copied from its web calculator). Lines are printed as-is rather
        /// than word-wrapped, since word-wrapping would break the alignment of tabular reports
        /// worse than just letting the printer hardware-wrap any line over LineWidth.
        /// </summary>
        public static byte[] BuildPlainTextTicket(string text)
        {
            var b = new EscPosBuilder();
            b.Init();
            b.AlignLeft();

            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
                b.Line(line);

            b.FeedLines(3);
            b.Cut();

            return b.Build();
        }

        private static Dictionary<string, string> BuildPlaceholderMap(SimBriefFlightPlan fp)
        {
            var map = new Dictionary<string, string>();
            foreach (var prop in typeof(SimBriefFlightPlan).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                map[prop.Name] = prop.GetValue(fp)?.ToString() ?? "N/A";
            }
            return map;
        }

        private static void RenderLine(EscPosBuilder b, string line, Dictionary<string, string> placeholders)
        {
            if (line.Length == 0)
            {
                b.Line();
                return;
            }
            if (line.StartsWith('#')) return;
            if (line == "---") { b.Divider('-'); return; }
            if (line == "===") { b.Divider('='); return; }

            bool bold = line.StartsWith("**") && line.EndsWith("**") && line.Length >= 4;
            string content = bold ? line[2..^2] : line;
            content = PlaceholderPattern.Replace(content, m =>
                placeholders.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);

            if (bold) b.Bold(true);

            int pipeIndex = content.IndexOf('|');
            if (pipeIndex >= 0)
            {
                b.KeyValue(content[..pipeIndex], content[(pipeIndex + 1)..]);
            }
            else
            {
                WriteWrapped(b, content);
            }

            if (bold) b.Bold(false);
        }

        private static void WriteWrapped(EscPosBuilder b, string text)
        {
            if (text.Length <= LineWidth)
            {
                b.Line(text);
                return;
            }

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();
            foreach (var word in words)
            {
                if (line.Length + word.Length + 1 > LineWidth)
                {
                    b.Line(line.ToString());
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            if (line.Length > 0) b.Line(line.ToString());
        }
    }
}
