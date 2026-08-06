namespace SimPrinter
{
    public class MainForm : Form
    {
        private readonly RoundedButton _btnLoad = new();
        private readonly RoundedButton _btnSettings = new();
        private readonly RoundedButton _btnPrintText = new();
        private readonly RoundedButton _btnPrintFlightPlan = new();
        private readonly RoundedButton _btnPrintPrelim = new();
        private readonly RoundedButton _btnPrintFinal = new();
        private readonly TextBox _txtIcao = new();
        private readonly RoundedToggleButton _radMetar = new();
        private readonly RoundedToggleButton _radAtis = new();
        private readonly RoundedButton _btnGetWeather = new();
        private readonly RoundedButton _btnRequestGate = new();
        private readonly Label _lblStatus = new();
        private readonly Label _lblOffBlock = new();
        private readonly Preferences _preferences = Preferences.Load();
        private readonly SimConnectClient _simConnect = new();
        private readonly LocalPrintServer _printServer = new();
        private Panel _cardActions = null!;
        private Panel _actionsContent = null!;

        private SimBriefFlightPlan? _currentPlan;
        private LoadsheetGenerator.LoadsheetValues? _finalLoadsheetValues;
        private double? _latestZuluSeconds;

        public MainForm()
        {
            Text = "SimPrinter";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            Font = new Font("Segoe UI", 10f);
            BackColor = UiStyle.BackgroundColor;
            Size = new Size(600, 780);
            MinimumSize = new Size(500, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            Padding = new Padding(20);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            StartSimConnect();
            StartPrintServer();
        }

        /// <summary>
        /// Opt-in localhost server that lets the SimPrinter browser extension print text
        /// (e.g. a SimBrief performance calculation) with one click. See LocalPrintServer.
        /// </summary>
        private void StartPrintServer()
        {
            _printServer.OnPrintTextRequested = text => BeginInvoke(new Action(() =>
            {
                var ticket = EscPosBuilder.BuildPlainTextTicket(text);
                PrintTicket(ticket, "browser performance calculation");
            }));

            if (_preferences.EnableBrowserPrintServer)
                _printServer.Start();
        }

        /// <summary>
        /// Shows a live countdown to the flight plan's scheduled off-block time, driven by the
        /// sim's own Zulu clock (rather than the PC's system clock) so it stays correct if the
        /// user runs the sim with time acceleration or a non-real-time sim clock.
        /// </summary>
        private void StartSimConnect()
        {
            _simConnect.ZuluSecondsUpdated += seconds =>
            {
                _latestZuluSeconds = seconds;
                RefreshOffBlockLabel();
            };
            _simConnect.Disconnected += () =>
            {
                _latestZuluSeconds = null;
                RefreshOffBlockLabel();
            };
            RefreshOffBlockLabel();
        }

        private void RefreshOffBlockLabel()
        {
            if (_currentPlan?.SchedOutSecondsOfDayUtc is not int schedOutSeconds)
            {
                _lblOffBlock.Text = "";
                return;
            }

            if (_latestZuluSeconds is not double nowSeconds)
            {
                _lblOffBlock.Text = "Off-block: waiting for simulator...";
                return;
            }

            double diff = schedOutSeconds - nowSeconds;
            if (diff < -43200) diff += 86400; // scheduled time has already rolled past local midnight

            var span = TimeSpan.FromSeconds(Math.Abs(diff));
            var formatted = span.ToString(@"hh\:mm\:ss");
            _lblOffBlock.Text = diff >= 0
                ? $"Off-block in {formatted}"
                : $"Off-block was {formatted} ago";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _printServer.Dispose();
            _simConnect.Dispose();
            base.OnFormClosed(e);
        }

        private void BuildUi()
        {
            // The footer (Settings + off-block countdown) is a direct child of the form with
            // Dock=Bottom, not part of the scrollable content below - that's what keeps it
            // pinned to the bottom of the window instead of just trailing after the Print
            // card, which would otherwise leave it floating awkwardly on a taller window.
            var footer = BuildFooter();
            footer.Dock = DockStyle.Bottom;
            Controls.Add(footer);

            // AutoSize + Dock=Top (not Fill) so contentRoot's height is its own natural total,
            // handed to the AutoScroll host below - shrinking the window past that natural
            // size scrolls instead of clipping the Print card's buttons, which is what actually
            // lets the window go small on low-resolution screens.
            var contentRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiStyle.BackgroundColor
            };
            contentRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            contentRoot.Controls.Add(BuildTopPanel(), 0, 0);

            _cardActions = UiStyle.CreateCard("Print", out _actionsContent);
            // CreateCard defaults to Dock=Fill, which is ambiguous when placed in an AutoSize
            // row (WinForms can't ask "what's your preferred size" of a Fill-docked control).
            // An explicit Height + Dock=Top sidesteps that - matches the card's real content:
            // padding(36) + header(~37) + ICAO label(~25) + input row(54) + 5 buttons at 40px
            // with 10px gaps between them.
            _cardActions.Dock = DockStyle.Top;
            _cardActions.Height = 406;
            _cardActions.Margin = new Padding(0, 14, 0, 14);
            BuildActionsContent(_actionsContent);
            SetFlightPlanActionsEnabled(false);
            contentRoot.Controls.Add(_cardActions, 0, 1);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiStyle.BackgroundColor
            };
            scrollHost.Controls.Add(contentRoot);

            Controls.Add(scrollHost);
        }

        private Control BuildTopPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = UiStyle.BackgroundColor,
                Margin = new Padding(0)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _btnLoad.Text = "Load Flight Plan";
            _btnLoad.AutoSize = false;
            _btnLoad.Width = 190;
            _btnLoad.Height = 40;
            _btnLoad.Anchor = AnchorStyles.Left;
            _btnLoad.Margin = new Padding(0, 0, 0, 10);
            _btnLoad.Click += BtnLoad_Click;
            UiStyle.StylePrimaryButton(_btnLoad, UiStyle.BackgroundColor);

            _lblStatus.AutoSize = true;
            _lblStatus.Text = "Set up your SimBrief ID in Settings, then load your latest flight plan.";
            _lblStatus.ForeColor = UiStyle.MutedTextColor;
            _lblStatus.Margin = new Padding(2, 0, 0, 0);

            panel.Controls.Add(_btnLoad, 0, 0);
            panel.Controls.Add(_lblStatus, 0, 1);

            return panel;
        }

        private void BuildActionsContent(Panel content)
        {
            // Everything that ends up on paper lives in one "Print" card: the ICAO/weather
            // lookup (Get Weather, Request Gate) and the SimBrief-plan prints are all "print
            // something" actions from the user's point of view, so they're grouped together
            // rather than split across a separate "Get Weather" card.
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblIcao = new Label
            {
                Text = "ICAO AIRPORT CODE",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 6)
            };

            var inputRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 14)
            };
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtIcao.MaxLength = 4;
            _txtIcao.CharacterCasing = CharacterCasing.Upper;
            _txtIcao.TextAlign = HorizontalAlignment.Center;
            _txtIcao.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            var icaoField = UiStyle.CreateInputField(_txtIcao);
            icaoField.Margin = new Padding(0, 0, 12, 0);
            icaoField.Dock = DockStyle.Fill;

            _radMetar.Text = "METAR";
            _radAtis.Text = "ATIS";
            var weatherToggle = UiStyle.CreateSegmentedToggle(_radMetar, _radAtis);
            _radMetar.Checked = true;
            weatherToggle.Dock = DockStyle.Fill;

            inputRow.Controls.Add(icaoField, 0, 0);
            inputRow.Controls.Add(weatherToggle, 1, 0);

            _btnGetWeather.Text = "Get Weather";
            _btnGetWeather.Dock = DockStyle.Top;
            _btnGetWeather.Height = 40;
            // Extra bottom margin (vs. the 10px between the other buttons) visually groups
            // this with the ICAO/METAR-ATIS input above it, since it acts on that input,
            // while Request Gate and the print actions below are their own separate group.
            _btnGetWeather.Margin = new Padding(0, 0, 0, 24);
            _btnGetWeather.Click += BtnGetWeather_Click;
            UiStyle.StyleSecondaryButton(_btnGetWeather);

            _btnRequestGate.Text = "Request Gate";
            _btnRequestGate.Dock = DockStyle.Top;
            _btnRequestGate.Height = 40;
            _btnRequestGate.Margin = new Padding(0, 0, 0, 10);
            _btnRequestGate.Click += BtnRequestGate_Click;
            UiStyle.StyleSecondaryButton(_btnRequestGate);

            _btnPrintFlightPlan.Text = "Print Flight Plan";
            _btnPrintFlightPlan.Dock = DockStyle.Fill;
            _btnPrintFlightPlan.Height = 40;
            _btnPrintFlightPlan.Margin = new Padding(0, 0, 0, 10);
            _btnPrintFlightPlan.Click += BtnPrintFlightPlan_Click;
            UiStyle.StyleSecondaryButton(_btnPrintFlightPlan);

            _btnPrintPrelim.Text = "Print Preliminary Loadsheet";
            _btnPrintPrelim.Dock = DockStyle.Fill;
            _btnPrintPrelim.Height = 40;
            _btnPrintPrelim.Margin = new Padding(0, 0, 0, 10);
            _btnPrintPrelim.Click += BtnPrintPrelim_Click;
            UiStyle.StyleSecondaryButton(_btnPrintPrelim);

            _btnPrintFinal.Text = "Print Final Loadsheet";
            _btnPrintFinal.Dock = DockStyle.Fill;
            _btnPrintFinal.Height = 40;
            _btnPrintFinal.Margin = new Padding(0, 0, 0, 10);
            _btnPrintFinal.Click += BtnPrintFinal_Click;
            UiStyle.StyleSecondaryButton(_btnPrintFinal);

            layout.Controls.Add(lblIcao, 0, 0);
            layout.Controls.Add(inputRow, 0, 1);
            layout.Controls.Add(_btnGetWeather, 0, 2);
            layout.Controls.Add(_btnRequestGate, 0, 3);
            layout.Controls.Add(_btnPrintFlightPlan, 0, 4);
            layout.Controls.Add(_btnPrintPrelim, 0, 5);
            layout.Controls.Add(_btnPrintFinal, 0, 6);

            content.Controls.Add(layout);
        }

        private void SetFlightPlanActionsEnabled(bool enabled)
        {
            _btnPrintFlightPlan.Enabled = enabled;
            _btnPrintPrelim.Enabled = enabled;
            _btnPrintFinal.Enabled = enabled;
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnSettings.Text = "Settings";
            _btnSettings.AutoSize = false;
            _btnSettings.Width = 120;
            _btnSettings.Height = 38;
            _btnSettings.Margin = new Padding(0);
            _btnSettings.Click += BtnSettings_Click;
            UiStyle.StyleSecondaryButton(_btnSettings, UiStyle.BackgroundColor);

            _lblOffBlock.Dock = DockStyle.Fill;
            _lblOffBlock.TextAlign = ContentAlignment.MiddleRight;
            _lblOffBlock.ForeColor = UiStyle.MutedTextColor;
            _lblOffBlock.Margin = new Padding(0, 0, 10, 0);

            // Doesn't need a flight plan loaded, unlike the three buttons in the Print card -
            // see SetFlightPlanActionsEnabled, which only touches those three individually.
            _btnPrintText.Text = "Print Text";
            _btnPrintText.AutoSize = false;
            _btnPrintText.Width = 120;
            _btnPrintText.Height = 38;
            _btnPrintText.Margin = new Padding(0);
            _btnPrintText.Click += BtnPrintText_Click;
            UiStyle.StyleSecondaryButton(_btnPrintText, UiStyle.BackgroundColor);

            footer.Controls.Add(_btnSettings, 0, 0);
            footer.Controls.Add(_lblOffBlock, 1, 0);
            footer.Controls.Add(_btnPrintText, 2, 0);

            return footer;
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using var dlg = new ConfigForm(_preferences);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (_preferences.EnableBrowserPrintServer)
                    _printServer.Start();
                else
                    _printServer.Stop();

                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblStatus.Text = "Settings saved.";
            }
        }

        private void BtnPrintText_Click(object? sender, EventArgs e)
        {
            using var dlg = new PastePrintForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                var ticket = EscPosBuilder.BuildPlainTextTicket(dlg.PastedText);
                PrintTicket(ticket, "pasted text");
            }
        }

        private async void BtnLoad_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_preferences.SimBriefId))
            {
                MessageBox.Show(this, "Please enter your SimBrief username or pilot ID in Settings first.",
                    "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnLoad.Enabled = false;
            SetFlightPlanActionsEnabled(false);
            _lblStatus.ForeColor = UiStyle.MutedTextColor;
            _lblStatus.Text = "Loading latest flight plan from SimBrief...";

            try
            {
                var plan = await SimBriefClient.FetchLatestAsync(_preferences.SimBriefId);
                _currentPlan = plan;
                _finalLoadsheetValues = _preferences.RandomizeFinalLoadsheet
                    ? LoadsheetGenerator.BuildFinalValues(plan, new Random())
                    : LoadsheetGenerator.BuildPreliminaryValues(plan);
                SetFlightPlanActionsEnabled(true);
                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblStatus.Text = $"Loaded: {plan.OriginIcao} -> {plan.DestIcao} ({plan.Callsign})";
                RefreshOffBlockLabel();
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = UiStyle.ErrorColor;
                _lblStatus.Text = "Failed to load flight plan.";
                MessageBox.Show(this, ex.Message, "SimBrief Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnLoad.Enabled = true;
            }
        }

        private async void BtnGetWeather_Click(object? sender, EventArgs e)
        {
            bool useVatsim = _preferences.UseVatsimWeather;

            if (!useVatsim && string.IsNullOrWhiteSpace(_preferences.SiApiKey))
            {
                MessageBox.Show(this, "Please enter your SayIntentions API key in Settings first.",
                    "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string icao = _txtIcao.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(icao))
            {
                MessageBox.Show(this, "Please enter an ICAO airport code.", "ICAO Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool atis = _radAtis.Checked;
            string label = atis ? "ATIS" : "METAR";

            _btnGetWeather.Enabled = false;
            _lblStatus.ForeColor = UiStyle.MutedTextColor;
            _lblStatus.Text = $"Fetching {label} for {icao}...";

            try
            {
                string weatherText = useVatsim
                    ? await (atis ? VatsimClient.GetAtisAsync(icao) : VatsimClient.GetMetarAsync(icao))
                    : await SayIntentionsClient.GetWeatherAsync(_preferences.SiApiKey, icao, atis);
                byte[] ticket = EscPosBuilder.BuildWeatherTicket(icao, label, weatherText);
                PrintTicket(ticket, $"{label} for {icao}");
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = UiStyle.ErrorColor;
                _lblStatus.Text = "Failed to fetch weather.";
                MessageBox.Show(this, ex.Message, "Weather Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnGetWeather.Enabled = true;
            }
        }

        private async void BtnRequestGate_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_preferences.SiApiKey))
            {
                MessageBox.Show(this, "Please enter your SayIntentions API key in Settings first.",
                    "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnRequestGate.Enabled = false;
            _lblStatus.ForeColor = UiStyle.MutedTextColor;
            _lblStatus.Text = "Requesting gate assignment...";

            try
            {
                string gateInfo = await SayIntentionsClient.GetParkingAsync(_preferences.SiApiKey);
                byte[] ticket = EscPosBuilder.BuildGateTicket(gateInfo);
                PrintTicket(ticket, "gate assignment");
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = UiStyle.ErrorColor;
                _lblStatus.Text = "Failed to request gate.";
                MessageBox.Show(this, ex.Message, "Gate Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRequestGate.Enabled = true;
            }
        }

        private void BtnPrintFlightPlan_Click(object? sender, EventArgs e) => ExecutePrintFlightPlan();

        private void BtnPrintPrelim_Click(object? sender, EventArgs e) => ExecutePrintPreliminary();

        private void BtnPrintFinal_Click(object? sender, EventArgs e) => ExecutePrintFinal();

        private void ExecutePrintFlightPlan()
        {
            if (_currentPlan == null) return;
            byte[] ticket = EscPosBuilder.BuildFlightPlanTicket(_currentPlan);
            PrintTicket(ticket, "flight plan");
        }

        private void ExecutePrintPreliminary()
        {
            if (_currentPlan == null) return;
            var values = LoadsheetGenerator.BuildPreliminaryValues(_currentPlan);
            byte[] ticket = LoadsheetGenerator.BuildTicket(_currentPlan, "PRELIMINARY", values);
            PrintTicket(ticket, "preliminary loadsheet");
        }

        private void ExecutePrintFinal()
        {
            if (_currentPlan == null || _finalLoadsheetValues == null) return;
            byte[] ticket = LoadsheetGenerator.BuildTicket(_currentPlan, "FINAL", _finalLoadsheetValues);
            PrintTicket(ticket, "final loadsheet");
        }

        private void PrintTicket(byte[] ticket, string description)
        {
            if (_preferences.UseSerial && string.IsNullOrWhiteSpace(_preferences.ComPort))
            {
                MessageBox.Show(this, "Please select a COM port in Settings before printing.",
                    "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_preferences.UseSerial && string.IsNullOrWhiteSpace(_preferences.PrinterName))
            {
                MessageBox.Show(this, "Please select a Windows printer in Settings before printing.",
                    "Settings Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_preferences.UseSerial)
                {
                    PrinterService.PrintViaSerial(_preferences.ComPort!, _preferences.BaudRate, ticket);
                }
                else
                {
                    PrinterService.PrintViaWindowsPrinter(_preferences.PrinterName!, ticket);
                }

                _lblStatus.ForeColor = UiStyle.SuccessColor;
                _lblStatus.Text = $"Printed {description}.";
            }
            catch (Exception ex)
            {
                _lblStatus.ForeColor = UiStyle.ErrorColor;
                _lblStatus.Text = "Print failed.";
                MessageBox.Show(this, ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
