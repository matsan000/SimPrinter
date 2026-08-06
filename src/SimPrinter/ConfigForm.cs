using System.Diagnostics;
using System.IO.Ports;
using System.Drawing.Printing;

namespace SimPrinter
{
    /// <summary>
    /// Settings dialog: SimBrief ID, printer setup, and the ticket template shortcut.
    /// Edits the shared Preferences instance in place, but only on Save - Cancel leaves
    /// it untouched.
    /// </summary>
    public class ConfigForm : Form
    {
        private readonly Preferences _preferences;

        private readonly TextBox _txtSimBriefId = new();
        private readonly TextBox _txtSiApiKey = new();
        private readonly RoundedSwitch _chkUseVatsimWeather = new();
        private readonly RoundedToggleButton _radSerial = new();
        private readonly RoundedToggleButton _radWindowsPrinter = new();
        private readonly ComboBox _cmbPort = new();
        private readonly ComboBox _cmbBaud = new();
        private readonly ComboBox _cmbPrinter = new();
        private readonly RoundedButton _btnRefresh = new();
        private readonly RoundedSwitch _chkRandomizeFinal = new();
        private readonly RoundedSwitch _chkDarkMode = new();
        private readonly RoundedSwitch _chkBrowserPrintServer = new();
        private readonly RoundedButton _btnEditTemplate = new();
        private readonly RoundedButton _btnSave = new();
        private readonly RoundedButton _btnCancel = new();

        public ConfigForm(Preferences preferences)
        {
            _preferences = preferences;

            Text = "Settings";
            Font = new Font("Segoe UI", 10f);
            BackColor = UiStyle.BackgroundColor;
            ClientSize = new Size(560, 600);
            MinimumSize = new Size(500, 350);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Padding = new Padding(20);

            BuildUi();
            RefreshPortsAndPrinters();
            ApplyPreferencesToUi();
        }

        private void BuildUi()
        {
            // AutoSize + Dock=Top (not Fill) so root's height is its own natural total,
            // handed to the AutoScroll host below - shrinking the window past that natural
            // size scrolls instead of clipping the Save button, letting the dialog go small
            // on low-resolution screens instead of being stuck at a fixed 950px-tall size.
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = UiStyle.BackgroundColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 158));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 282));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 570));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var cardSimBrief = UiStyle.CreateCard("SimBrief", out Panel simBriefContent);
            cardSimBrief.Margin = new Padding(0, 0, 0, 14);
            BuildSimBriefContent(simBriefContent);
            root.Controls.Add(cardSimBrief, 0, 0);

            var cardWeather = UiStyle.CreateCard("Weather", out Panel weatherContent);
            cardWeather.Margin = new Padding(0, 0, 0, 14);
            BuildWeatherContent(weatherContent);
            root.Controls.Add(cardWeather, 0, 1);

            var cardPrinterGeneral = UiStyle.CreateCard("Printer & General Settings", out Panel printerGeneralContent);
            cardPrinterGeneral.Margin = new Padding(0, 0, 0, 14);
            BuildPrinterAndGeneralContent(printerGeneralContent);
            root.Controls.Add(cardPrinterGeneral, 0, 2);

            root.Controls.Add(BuildFooter(), 0, 3);

            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = UiStyle.BackgroundColor
            };
            scrollHost.Controls.Add(root);

            Controls.Add(scrollHost);
        }

        private void BuildSimBriefContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblId = new Label
            {
                Text = "SimBrief username or pilot ID",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 8)
            };

            var field = UiStyle.CreateInputField(_txtSimBriefId);
            field.Dock = DockStyle.Top;

            layout.Controls.Add(lblId, 0, 0);
            layout.Controls.Add(field, 0, 1);
            content.Controls.Add(layout);
        }

        private void BuildWeatherContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblKey = new Label
            {
                Text = "SayIntentions API key",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 8)
            };

            _txtSiApiKey.UseSystemPasswordChar = true;
            var field = UiStyle.CreateInputField(_txtSiApiKey);
            field.Dock = DockStyle.Top;
            field.Margin = new Padding(0, 0, 0, 14);

            var vatsimRow = UiStyle.CreateSwitchRow(
                "Use VATSIM for METAR/ATIS instead (no API key needed)", _chkUseVatsimWeather);
            vatsimRow.Margin = new Padding(0, 0, 0, 6);

            var lblVatsimNote = new Label
            {
                Text = "VATSIM ATIS only exists while a controller is online broadcasting it. " +
                       "Gate/parking requests always use SayIntentions.",
                AutoSize = true,
                MaximumSize = new Size(480, 0),
                Font = new Font("Segoe UI", 8f),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 0)
            };

            layout.Controls.Add(lblKey, 0, 0);
            layout.Controls.Add(field, 0, 1);
            layout.Controls.Add(vatsimRow, 0, 2);
            layout.Controls.Add(lblVatsimNote, 0, 3);
            content.Controls.Add(layout);
        }

        private void BuildPrinterAndGeneralContent(Panel content)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                Margin = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _radSerial.Text = "Serial / COM Port";
            _radWindowsPrinter.Text = "Windows Printer";
            var printerModeToggle = UiStyle.CreateSegmentedToggle(_radSerial, _radWindowsPrinter);
            printerModeToggle.Dock = DockStyle.Fill;
            printerModeToggle.Margin = new Padding(0, 0, 0, 14);
            _radSerial.Checked = true;
            _radSerial.CheckedChanged += (_, _) => UpdatePrinterModeControls();
            _radWindowsPrinter.CheckedChanged += (_, _) => UpdatePrinterModeControls();

            var portRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            _cmbPort.Dock = DockStyle.Fill;
            _cmbPort.Margin = new Padding(0, 0, 10, 0);
            _cmbPort.DropDownStyle = ComboBoxStyle.DropDownList;
            UiStyle.StyleComboBox(_cmbPort);

            var lblBaud = new Label
            {
                Text = "Baud:",
                AutoSize = true,
                ForeColor = UiStyle.TextColor,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 0)
            };

            _cmbBaud.Dock = DockStyle.Fill;
            _cmbBaud.Margin = new Padding(0);
            _cmbBaud.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbBaud.Items.AddRange(new object[] { 9600, 19200, 38400, 57600, 115200 });
            _cmbBaud.SelectedItem = 9600;
            UiStyle.StyleComboBox(_cmbBaud);

            portRow.Controls.Add(_cmbPort, 0, 0);
            portRow.Controls.Add(lblBaud, 1, 0);
            portRow.Controls.Add(_cmbBaud, 2, 0);

            var printerRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            printerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            printerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _cmbPrinter.Dock = DockStyle.Fill;
            _cmbPrinter.Margin = new Padding(0, 0, 10, 0);
            _cmbPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
            UiStyle.StyleComboBox(_cmbPrinter);

            _btnRefresh.Text = "Refresh";
            _btnRefresh.AutoSize = false;
            _btnRefresh.Width = 100;
            _btnRefresh.Height = 30;
            _btnRefresh.Margin = new Padding(0);
            _btnRefresh.Click += (_, _) => RefreshPortsAndPrinters();
            UiStyle.StyleSecondaryButton(_btnRefresh);

            printerRow.Controls.Add(_cmbPrinter, 0, 0);
            printerRow.Controls.Add(_btnRefresh, 1, 0);

            var browserServerRow = UiStyle.CreateSwitchRow(
                "Allow the SimPrinter browser extension to print", _chkBrowserPrintServer);
            browserServerRow.Margin = new Padding(0, 14, 0, 6);

            var lblBrowserServerNote = new Label
            {
                Text = "Runs a small local-only server (127.0.0.1, no internet access) so the " +
                       "extension can send things like a SimBrief performance calculation straight " +
                       "to this printer.",
                AutoSize = true,
                MaximumSize = new Size(480, 0),
                Font = new Font("Segoe UI", 8f),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 0)
            };

            var randomizeRow = UiStyle.CreateSwitchRow(
                "Simulate last-minute pax/cargo changes on final loadsheet", _chkRandomizeFinal);
            randomizeRow.Margin = new Padding(0, 24, 0, 6);

            var darkModeRow = UiStyle.CreateSwitchRow(
                "Dark theme (restart required to apply)", _chkDarkMode);
            darkModeRow.Margin = new Padding(0, 0, 0, 16);

            _btnEditTemplate.Text = "Edit Ticket Template";
            _btnEditTemplate.Dock = DockStyle.Top;
            _btnEditTemplate.Height = 40;
            _btnEditTemplate.Margin = new Padding(0);
            _btnEditTemplate.Click += BtnEditTemplate_Click;
            UiStyle.StyleSecondaryButton(_btnEditTemplate);

            layout.Controls.Add(printerModeToggle, 0, 0);
            layout.Controls.Add(portRow, 0, 1);
            layout.Controls.Add(printerRow, 0, 2);
            layout.Controls.Add(browserServerRow, 0, 3);
            layout.Controls.Add(lblBrowserServerNote, 0, 4);
            layout.Controls.Add(randomizeRow, 0, 5);
            layout.Controls.Add(darkModeRow, 0, 6);
            layout.Controls.Add(_btnEditTemplate, 0, 7);

            content.Controls.Add(layout);
        }

        private Control BuildFooter()
        {
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnCancel.Text = "Cancel";
            _btnCancel.AutoSize = false;
            _btnCancel.Width = 110;
            _btnCancel.Height = 38;
            _btnCancel.Margin = new Padding(0, 0, 10, 0);
            _btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            UiStyle.StyleSecondaryButton(_btnCancel, UiStyle.BackgroundColor);

            _btnSave.Text = "Save";
            _btnSave.AutoSize = false;
            _btnSave.Width = 110;
            _btnSave.Height = 38;
            _btnSave.Margin = new Padding(0);
            _btnSave.Click += BtnSave_Click;
            UiStyle.StylePrimaryButton(_btnSave, UiStyle.BackgroundColor);

            footer.Controls.Add(_btnCancel, 1, 0);
            footer.Controls.Add(_btnSave, 2, 0);

            return footer;
        }

        private void UpdatePrinterModeControls()
        {
            _cmbPort.Enabled = _radSerial.Checked;
            _cmbBaud.Enabled = _radSerial.Checked;
            _cmbPrinter.Enabled = _radWindowsPrinter.Checked;
        }

        private void RefreshPortsAndPrinters()
        {
            _cmbPort.Items.Clear();
            _cmbPort.Items.AddRange(SerialPort.GetPortNames());
            if (_cmbPort.Items.Count > 0) _cmbPort.SelectedIndex = 0;

            _cmbPrinter.Items.Clear();
            foreach (string p in PrinterSettings.InstalledPrinters)
                _cmbPrinter.Items.Add(p);
            if (_cmbPrinter.Items.Count > 0) _cmbPrinter.SelectedIndex = 0;
        }

        private void ApplyPreferencesToUi()
        {
            _txtSimBriefId.Text = _preferences.SimBriefId;
            _txtSiApiKey.Text = _preferences.SiApiKey;
            _radSerial.Checked = _preferences.UseSerial;
            _radWindowsPrinter.Checked = !_preferences.UseSerial;

            if (_preferences.ComPort != null && _cmbPort.Items.Contains(_preferences.ComPort))
                _cmbPort.SelectedItem = _preferences.ComPort;

            if (_cmbBaud.Items.Contains(_preferences.BaudRate))
                _cmbBaud.SelectedItem = _preferences.BaudRate;

            if (_preferences.PrinterName != null && _cmbPrinter.Items.Contains(_preferences.PrinterName))
                _cmbPrinter.SelectedItem = _preferences.PrinterName;

            _chkRandomizeFinal.Checked = _preferences.RandomizeFinalLoadsheet;
            _chkDarkMode.Checked = _preferences.DarkMode;
            _chkUseVatsimWeather.Checked = _preferences.UseVatsimWeather;
            _chkBrowserPrintServer.Checked = _preferences.EnableBrowserPrintServer;

            UpdatePrinterModeControls();
        }

        private void BtnEditTemplate_Click(object? sender, EventArgs e)
        {
            try
            {
                TicketTemplate.EnsureFileExists();
                Process.Start(new ProcessStartInfo(TicketTemplate.TemplatePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could Not Open Template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _preferences.SimBriefId = _txtSimBriefId.Text.Trim();
            _preferences.SiApiKey = _txtSiApiKey.Text.Trim();
            _preferences.UseSerial = _radSerial.Checked;
            _preferences.ComPort = _cmbPort.SelectedItem as string;
            _preferences.BaudRate = (int)(_cmbBaud.SelectedItem ?? 9600);
            _preferences.PrinterName = _cmbPrinter.SelectedItem as string;
            _preferences.RandomizeFinalLoadsheet = _chkRandomizeFinal.Checked;
            _preferences.DarkMode = _chkDarkMode.Checked;
            _preferences.UseVatsimWeather = _chkUseVatsimWeather.Checked;
            _preferences.EnableBrowserPrintServer = _chkBrowserPrintServer.Checked;
            _preferences.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
