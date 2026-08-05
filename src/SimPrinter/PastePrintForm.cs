namespace SimPrinter
{
    /// <summary>
    /// Lets the user paste arbitrary text - typically a SimBrief takeoff/landing performance
    /// report copied from its web calculator, since that calculator's editable inputs aren't
    /// available through any public API - and print it via the same pipeline as the other
    /// tickets. This form doesn't print itself; the caller reads PastedText after the dialog
    /// returns DialogResult.OK.
    /// </summary>
    public class PastePrintForm : Form
    {
        private readonly TextBox _txtPaste = new();
        private readonly RoundedButton _btnPrint = new();
        private readonly RoundedButton _btnCancel = new();

        public string PastedText => _txtPaste.Text;

        public PastePrintForm()
        {
            Text = "Print Custom Text";
            Font = new Font("Segoe UI", 10f);
            BackColor = UiStyle.BackgroundColor;
            ClientSize = new Size(480, 420);
            MinimumSize = new Size(360, 280);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Padding = new Padding(20);

            BuildUi();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = UiStyle.BackgroundColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lbl = new Label
            {
                Text = "PASTE TEXT TO PRINT",
                AutoSize = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = UiStyle.MutedTextColor,
                Margin = new Padding(2, 0, 0, 8)
            };

            _txtPaste.Multiline = true;
            _txtPaste.ScrollBars = ScrollBars.Vertical;
            _txtPaste.AcceptsReturn = true;
            _txtPaste.AcceptsTab = true;
            _txtPaste.Font = new Font("Consolas", 9f);
            var field = UiStyle.CreateInputField(_txtPaste);
            field.Dock = DockStyle.Fill;
            field.Margin = new Padding(0, 0, 0, 14);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnCancel.Text = "Cancel";
            _btnCancel.AutoSize = false;
            _btnCancel.Width = 100;
            _btnCancel.Height = 38;
            _btnCancel.Margin = new Padding(0, 0, 10, 0);
            _btnCancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            UiStyle.StyleSecondaryButton(_btnCancel, UiStyle.BackgroundColor);

            _btnPrint.Text = "Print";
            _btnPrint.AutoSize = false;
            _btnPrint.Width = 100;
            _btnPrint.Height = 38;
            _btnPrint.Margin = new Padding(0);
            _btnPrint.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_txtPaste.Text)) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            UiStyle.StylePrimaryButton(_btnPrint, UiStyle.BackgroundColor);

            footer.Controls.Add(_btnCancel, 1, 0);
            footer.Controls.Add(_btnPrint, 2, 0);

            root.Controls.Add(lbl, 0, 0);
            root.Controls.Add(field, 0, 1);
            root.Controls.Add(footer, 0, 2);

            Controls.Add(root);
        }
    }
}
