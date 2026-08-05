using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SimPrinter
{
    /// <summary>
    /// Windows 11 draws its own native theme decoration (a subtle accent-colored focus/hover
    /// ring) directly on Button/CheckBox HWNDs, independent of owner-draw WM_PAINT handling.
    /// SetWindowTheme("", "") opts a control out of that so only our custom painting shows.
    /// </summary>
    internal static class NativeTheme
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        public static void DisableVisualStyle(Control control) => SetWindowTheme(control.Handle, "", "");
    }

    /// <summary>Shared colors and control styling so MainForm and ConfigForm look consistent.</summary>
    public static class UiStyle
    {
        public static bool IsDarkMode { get; set; }

        public static Color BackgroundColor => IsDarkMode
            ? Color.FromArgb(24, 25, 28)
            : Color.FromArgb(244, 245, 247);

        public static Color CardBackgroundColor => IsDarkMode
            ? Color.FromArgb(34, 35, 39)
            : Color.White;

        public static Color CardBorderColor => IsDarkMode
            ? Color.FromArgb(55, 57, 62)
            : Color.FromArgb(224, 226, 230);

        public static Color InputBackgroundColor => IsDarkMode
            ? Color.FromArgb(44, 46, 51)
            : Color.White;

        public static Color TrackColor => IsDarkMode
            ? Color.FromArgb(45, 47, 51)
            : Color.FromArgb(233, 235, 239);

        public static Color AccentColor => IsDarkMode
            ? Color.FromArgb(59, 130, 246)
            : Color.FromArgb(37, 99, 235);

        public static Color AccentColorHover => IsDarkMode
            ? Color.FromArgb(96, 165, 250)
            : Color.FromArgb(29, 78, 216);

        public static Color TextColor => IsDarkMode
            ? Color.FromArgb(230, 231, 233)
            : Color.FromArgb(33, 37, 41);

        public static Color MutedTextColor => IsDarkMode
            ? Color.FromArgb(155, 158, 163)
            : Color.FromArgb(108, 117, 125);

        public static Color SecondaryButtonColor => IsDarkMode
            ? Color.FromArgb(45, 47, 51)
            : Color.FromArgb(241, 242, 245);

        public static Color SuccessColor => IsDarkMode
            ? Color.FromArgb(74, 222, 128)
            : Color.DarkGreen;

        public static Color ErrorColor => IsDarkMode
            ? Color.FromArgb(248, 113, 113)
            : Color.Firebrick;

        private const int CardCornerRadius = 14;
        private const int ButtonCornerRadius = 8;
        private const int InputCornerRadius = 8;

        // ============================== Buttons ==============================

        public static void StylePrimaryButton(RoundedButton button, Color? blendBackColor = null)
        {
            button.BlendBackColor = blendBackColor ?? CardBackgroundColor;
            button.NormalColor = AccentColor;
            button.HoverColor = AccentColorHover;
            button.PressColor = Darken(AccentColorHover, 0.12f);
            button.TextColor = Color.White;
            button.BorderColor = null;
        }

        public static void StyleSecondaryButton(RoundedButton button, Color? blendBackColor = null)
        {
            button.BlendBackColor = blendBackColor ?? CardBackgroundColor;
            button.NormalColor = SecondaryButtonColor;
            button.HoverColor = HoverVariant(SecondaryButtonColor);
            button.PressColor = PressVariant(SecondaryButtonColor);
            button.TextColor = TextColor;
            button.BorderColor = CardBorderColor;
        }

        // ============================== Segmented toggle (2-way radio group) ==============================

        /// <summary>
        /// A pill-shaped two-option switch (e.g. METAR/ATIS, Serial/Windows Printer) built
        /// from real RadioButtons so existing .Checked / CheckedChanged wiring keeps working
        /// unchanged - only the painting is replaced.
        /// </summary>
        public static Control CreateSegmentedToggle(RoundedToggleButton left, RoundedToggleButton right, Color? blendBackColor = null)
        {
            Color blend = blendBackColor ?? CardBackgroundColor;

            var track = new CardPanel
            {
                BackColor = TrackColor,
                Padding = new Padding(3),
                Height = 40
            };
            EnableRoundedRegion(track, () => track.Height / 2);

            void Restyle()
            {
                StyleToggleButton(left, left.Checked, TrackColor);
                StyleToggleButton(right, right.Checked, TrackColor);
            }

            left.Appearance = Appearance.Button;
            right.Appearance = Appearance.Button;
            left.RoundLeftCorners = true;
            left.RoundRightCorners = false;
            right.RoundLeftCorners = false;
            right.RoundRightCorners = true;
            left.CheckedChanged += (_, _) => Restyle();
            right.CheckedChanged += (_, _) => Restyle();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            left.Dock = DockStyle.Fill;
            right.Dock = DockStyle.Fill;
            left.Margin = new Padding(0, 0, 2, 0);
            right.Margin = new Padding(2, 0, 0, 0);
            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);
            track.Controls.Add(layout);

            Restyle();

            var outer = new Panel { Dock = DockStyle.Fill, BackColor = blend, Height = 40 };
            outer.Controls.Add(track);
            track.Dock = DockStyle.Fill;
            return outer;
        }

        private static void StyleToggleButton(RoundedToggleButton button, bool selected, Color trackColor)
        {
            if (selected)
            {
                button.NormalColor = AccentColor;
                button.HoverColor = AccentColor;
                button.PressColor = Darken(AccentColor, 0.12f);
                button.TextColor = Color.White;
            }
            else
            {
                button.NormalColor = trackColor;
                button.HoverColor = HoverVariant(trackColor);
                button.PressColor = PressVariant(trackColor);
                button.TextColor = MutedTextColor;
            }
            button.BlendBackColor = trackColor;
            button.Invalidate();
        }

        // ============================== Switch (modern checkbox) ==============================

        public static void StyleSwitch(RoundedSwitch toggle, Color? blendBackColor = null)
        {
            toggle.BlendBackColor = blendBackColor ?? CardBackgroundColor;
            toggle.OnColor = AccentColor;
            toggle.OffColor = TrackColor;
            toggle.KnobColor = Color.White;
        }

        /// <summary>A label + switch row, e.g. for a settings toggle ("Dark theme  [oo]").</summary>
        public static Control CreateSwitchRow(string labelText, RoundedSwitch toggle, Color? blendBackColor = null)
        {
            Color blend = blendBackColor ?? CardBackgroundColor;
            StyleSwitch(toggle, blend);

            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                BackColor = blend
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                MaximumSize = new Size(320, 0),
                ForeColor = TextColor,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 12, 8)
            };

            toggle.Margin = new Padding(0, 6, 0, 6);
            toggle.Anchor = AnchorStyles.Right;

            row.Controls.Add(label, 0, 0);
            row.Controls.Add(toggle, 1, 0);

            return row;
        }

        // ============================== Text inputs ==============================

        public static void StyleTextBox(TextBox textBox)
        {
            textBox.BackColor = InputBackgroundColor;
            textBox.ForeColor = TextColor;
            textBox.BorderStyle = BorderStyle.None;
        }

        /// <summary>Wraps a borderless TextBox in a rounded, focus-highlighting container.</summary>
        public static Control CreateInputField(TextBox textBox, Color? blendBackColor = null)
        {
            Color blend = blendBackColor ?? CardBackgroundColor;
            StyleTextBox(textBox);
            textBox.Dock = DockStyle.Fill;
            textBox.BackColor = InputBackgroundColor;

            var wrapper = new CardPanel
            {
                BackColor = InputBackgroundColor,
                Padding = new Padding(12, 6, 12, 6),
                Height = 40
            };
            EnableRoundedRegion(wrapper, () => InputCornerRadius);

            bool focused = false;
            wrapper.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, wrapper.Width - 1, wrapper.Height - 1);
                using var path = RoundedRectPath(rect, InputCornerRadius);
                using var pen = new Pen(focused ? AccentColor : CardBorderColor, focused ? 1.6f : 1f);
                e.Graphics.DrawPath(pen, path);
            };
            textBox.Enter += (_, _) => { focused = true; wrapper.Invalidate(); };
            textBox.Leave += (_, _) => { focused = false; wrapper.Invalidate(); };

            wrapper.Controls.Add(textBox);

            var outer = new Panel { BackColor = blend, Margin = new Padding(0), Height = 40 };
            outer.Controls.Add(wrapper);
            wrapper.Dock = DockStyle.Fill;
            return outer;
        }

        public static void StyleComboBox(ComboBox comboBox)
        {
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = InputBackgroundColor;
            comboBox.ForeColor = TextColor;
        }

        // ============================== Cards ==============================

        public static Panel CreateCard(string title, out Panel content)
        {
            var card = new CardPanel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBackgroundColor,
                Padding = new Padding(18)
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var borderRect = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var path = RoundedRectPath(borderRect, CardCornerRadius);
                using var pen = new Pen(CardBorderColor);
                e.Graphics.DrawPath(pen, path);
            };
            EnableRoundedRegion(card, () => CardCornerRadius);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var header = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = TextColor,
                Margin = new Padding(0, 0, 0, 12)
            };

            content = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            layout.Controls.Add(header, 0, 0);
            layout.Controls.Add(content, 0, 1);
            card.Controls.Add(layout);

            return card;
        }

        // ============================== Shared helpers ==============================

        private static void EnableRoundedRegion(Control control, Func<int> radiusProvider)
        {
            void Update()
            {
                if (control.Width <= 0 || control.Height <= 0) return;
                using var path = RoundedRectPath(new Rectangle(0, 0, control.Width, control.Height), radiusProvider());
                control.Region?.Dispose();
                control.Region = new Region(path);
            }
            control.Resize += (_, _) => Update();
            Update();
        }

        private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Darken(Color c, float amount) => Color.FromArgb(
            c.A,
            (int)(c.R * (1 - amount)),
            (int)(c.G * (1 - amount)),
            (int)(c.B * (1 - amount)));

        private static Color Lighten(Color c, float amount) => Color.FromArgb(
            c.A,
            (int)(c.R + (255 - c.R) * amount),
            (int)(c.G + (255 - c.G) * amount),
            (int)(c.B + (255 - c.B) * amount));

        private static Color HoverVariant(Color c) => IsDarkMode ? Lighten(c, 0.15f) : Darken(c, 0.06f);
        private static Color PressVariant(Color c) => IsDarkMode ? Lighten(c, 0.28f) : Darken(c, 0.14f);

        /// <summary>
        /// Plain Panel doesn't repaint its whole surface on resize by default, which leaves
        /// stale pixels behind from the previous size (visible as border "ghosting" when the
        /// window is resized). ResizeRedraw forces a full repaint instead. Reused as the base
        /// for every custom-painted rounded surface in this app.
        /// </summary>
        internal sealed class CardPanel : Panel
        {
            public CardPanel()
            {
                SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                DoubleBuffered = true;
            }
        }
    }

    /// <summary>A push button with smooth, anti-aliased rounded corners and hover/press states.</summary>
    public sealed class RoundedButton : Button
    {
        private const int CornerRadius = 8;
        private bool _hovered;
        private bool _pressed;

        public Color NormalColor { get; set; } = Color.Gray;
        public Color HoverColor { get; set; } = Color.Gray;
        public Color PressColor { get; set; } = Color.Gray;
        public Color TextColor { get; set; } = Color.White;
        public Color? BorderColor { get; set; }
        public Color BlendBackColor { get; set; } = Color.White;

        public RoundedButton()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            Resize += (_, _) => UpdateRegion();
        }

        protected override bool ShowFocusCues => false;

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.DisableVisualStyle(this); UpdateRegion(); }

        /// <summary>
        /// Clips the control's own window to its painted rounded silhouette. Some Windows 11
        /// native decoration (focus/default-button accent) is drawn on the underlying Button
        /// HWND outside our WM_PAINT override and survives ShowFocusCues=false and
        /// SetWindowTheme; a hard region clip physically prevents anything from showing
        /// outside the rounded shape regardless of what's drawing it.
        /// </summary>
        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using var path = RoundedRectPath(new Rectangle(0, 0, Width, Height), CornerRadius);
            Region?.Dispose();
            Region = new Region(path);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(BlendBackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = !Enabled ? NormalColor : (_pressed ? PressColor : (_hovered ? HoverColor : NormalColor));

            // Fill covers the full control rect so no edge pixel is left for stray native
            // painting to show through; the border pen uses an inset rect since a stroke
            // drawn exactly on the boundary can get clipped/antialiased oddly there.
            using (var fillPath = RoundedRectPath(new Rectangle(0, 0, Width, Height), CornerRadius))
            using (var brush = new SolidBrush(fill))
                e.Graphics.FillPath(brush, fillPath);

            if (BorderColor.HasValue)
            {
                var strokeRect = new Rectangle(0, 0, Width - 1, Height - 1);
                using var strokePath = RoundedRectPath(strokeRect, CornerRadius);
                using var pen = new Pen(BorderColor.Value);
                e.Graphics.DrawPath(pen, strokePath);
            }

            Color textColor = Enabled ? TextColor : Color.FromArgb(160, TextColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// A RadioButton rendered as a rounded pill segment (Appearance.Button under the hood),
    /// used in pairs to build a modern segmented toggle while keeping normal RadioButton
    /// grouping/.Checked semantics for the surrounding business logic.
    /// </summary>
    public sealed class RoundedToggleButton : RadioButton
    {
        private const int CornerRadius = 15;
        private bool _hovered;

        public Color NormalColor { get; set; } = Color.Gray;
        public Color HoverColor { get; set; } = Color.Gray;
        public Color PressColor { get; set; } = Color.Gray;
        public Color TextColor { get; set; } = Color.Black;
        public Color BlendBackColor { get; set; } = Color.White;

        /// <summary>Which corners this segment rounds - set by CreateSegmentedToggle so two
        /// adjacent segments meet with a flush straight seam instead of both curving inward.</summary>
        public bool RoundLeftCorners { get; set; } = true;
        public bool RoundRightCorners { get; set; } = true;

        public RoundedToggleButton()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            TextAlign = ContentAlignment.MiddleCenter;
            Resize += (_, _) => UpdateRegion();
        }

        protected override bool ShowFocusCues => false;

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.DisableVisualStyle(this); UpdateRegion(); }

        /// <summary>See RoundedButton.UpdateRegion - physically clips out native decoration
        /// that survives ShowFocusCues=false and SetWindowTheme.</summary>
        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int leftRadius = RoundLeftCorners ? CornerRadius : 0;
            int rightRadius = RoundRightCorners ? CornerRadius : 0;
            using var path = RoundedRectPath(new Rectangle(0, 0, Width, Height), leftRadius, rightRadius);
            Region?.Dispose();
            Region = new Region(path);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(BlendBackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = _hovered ? HoverColor : NormalColor;

            var rect = new Rectangle(0, 0, Width, Height);
            int leftRadius = RoundLeftCorners ? CornerRadius : 0;
            int rightRadius = RoundRightCorners ? CornerRadius : 0;
            using (var path = RoundedRectPath(rect, leftRadius, rightRadius))
            using (var brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>Rounds only the left corners (radiusLeft) and/or right corners (radiusRight),
        /// so two segments can meet in the middle with a flush straight edge instead of both
        /// curving inward and leaving a gap at the seam.</summary>
        private static GraphicsPath RoundedRectPath(Rectangle bounds, int radiusLeft, int radiusRight)
        {
            int dL = Math.Min(Math.Max(0, radiusLeft) * 2, Math.Min(bounds.Width, bounds.Height));
            int dR = Math.Min(Math.Max(0, radiusRight) * 2, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();

            if (dL > 0) path.AddArc(bounds.X, bounds.Y, dL, dL, 180, 90);
            else path.AddLine(bounds.X, bounds.Y, bounds.X, bounds.Y);

            if (dR > 0) path.AddArc(bounds.Right - dR, bounds.Y, dR, dR, 270, 90);
            else path.AddLine(bounds.Right, bounds.Y, bounds.Right, bounds.Y);

            if (dR > 0) path.AddArc(bounds.Right - dR, bounds.Bottom - dR, dR, dR, 0, 90);
            else path.AddLine(bounds.Right, bounds.Bottom, bounds.Right, bounds.Bottom);

            if (dL > 0) path.AddArc(bounds.X, bounds.Bottom - dL, dL, dL, 90, 90);
            else path.AddLine(bounds.X, bounds.Bottom, bounds.X, bounds.Bottom);

            path.CloseFigure();
            return path;
        }
    }

    /// <summary>A rounded pill on/off switch used in place of a native CheckBox.</summary>
    public sealed class RoundedSwitch : CheckBox
    {
        public Color OnColor { get; set; } = Color.Gray;
        public Color OffColor { get; set; } = Color.Gray;
        public Color KnobColor { get; set; } = Color.White;
        public Color BlendBackColor { get; set; } = Color.White;

        public RoundedSwitch()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Size = new Size(42, 24);
            CheckedChanged += (_, _) => Invalidate();
            Resize += (_, _) => UpdateRegion();
        }

        protected override bool ShowFocusCues => false;

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); NativeTheme.DisableVisualStyle(this); UpdateRegion(); }

        /// <summary>See RoundedButton.UpdateRegion - physically clips out native decoration
        /// that survives ShowFocusCues=false and SetWindowTheme.</summary>
        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using var path = RoundedRectPath(new Rectangle(0, 0, Width, Height), Height / 2);
            Region?.Dispose();
            Region = new Region(path);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(BlendBackColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int h = Height;
            var rect = new Rectangle(0, 0, Width, h);
            using (var path = RoundedRectPath(rect, h / 2))
            using (var brush = new SolidBrush(Checked ? OnColor : OffColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            int knobDiameter = h - 6;
            int knobX = Checked ? Width - knobDiameter - 4 : 3;
            var knobRect = new Rectangle(knobX, 3, knobDiameter, knobDiameter);
            using var knobBrush = new SolidBrush(KnobColor);
            e.Graphics.FillEllipse(knobBrush, knobRect);
        }

        private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
