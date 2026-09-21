using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DesktopCalendar;

/// <summary>A plain, code-built (no .resx/designer) settings window.</summary>
internal sealed class SettingsForm : Form
{
    public event EventHandler<AppSettings>? SettingsSaved;

    private static readonly string[] TurkishMonths =
    {
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık",
    };

    private readonly string _holidaysFile;
    private readonly string _remindersFile;

    // Genel
    private readonly CheckBox _chkRunAtStartup = new() { Text = "Windows ile birlikte başlat", AutoSize = true };
    private readonly ComboBox _cmbFontName = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly CheckBox _chkBold = new() { Text = "Kalın", AutoSize = true };
    private readonly CheckBox _chkItalic = new() { Text = "İtalik", AutoSize = true };
    private readonly ComboBox _cmbShape = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _cmbFirstDayOfWeek = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly ComboBox _cmbTargetMonitor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    // Shown to the user as an actual pixel size (computed from the target screen's
    // height), instead of the internal "screenHeight / ratio" divisor - much clearer.
    private readonly NumericUpDown _numMonthFontSize = new() { Minimum = 8, Maximum = 2000, Width = 80 };
    private readonly NumericUpDown _numNextMonthFontSize = new() { Minimum = 8, Maximum = 2000, Width = 80 };
    private readonly NumericUpDown _numNextMonthsCount = new() { Minimum = 0, Maximum = 36, Width = 60 };
    private readonly NumericUpDown _numNextMonthColumns = new() { Minimum = 1, Maximum = 12, Width = 60 };
    private readonly ComboBox _cmbNextMonthLayout = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly Label _lblMaxMonthsInfo = new() { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(6, 6, 0, 0) };
    private readonly Label _lblMaxColumnsInfo = new() { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(6, 6, 0, 0) };

    // Konum
    private readonly RadioButton[] _anchorButtons = new RadioButton[9];
    private static readonly string[] AnchorValues =
    {
        "TopLeft", "TopCenter", "TopRight",
        "MiddleLeft", "MiddleCenter", "MiddleRight",
        "BottomLeft", "BottomCenter", "BottomRight",
    };
    private readonly NumericUpDown _numOffsetX = new() { Minimum = -3000, Maximum = 3000, Width = 80 };
    private readonly NumericUpDown _numOffsetY = new() { Minimum = -3000, Maximum = 3000, Width = 80 };

    // Metin efekti - each is an independent toggle, not a single choice
    private readonly CheckBox _chkShadowEnabled = new() { Text = "Etkinleştir", AutoSize = true };
    private readonly CheckBox _chkOutlineEnabled = new() { Text = "Etkinleştir", AutoSize = true };
    private readonly CheckBox _chkGlowEnabled = new() { Text = "Etkinleştir", AutoSize = true };

    private readonly GroupBox _grpShadow = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Text = "Gölge", Margin = new Padding(0, 0, 0, 6) };
    private readonly GroupBox _grpOutline = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Text = "Kontur", Margin = new Padding(0, 0, 0, 6) };
    private readonly GroupBox _grpGlow = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Text = "Parıltı", Margin = new Padding(0, 0, 0, 6) };

    // The settings rows below each checkbox - disabled (grayed out) as a whole when unchecked.
    private readonly FlowLayoutPanel _pnlShadowSettings = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(18, 4, 0, 0) };
    private readonly FlowLayoutPanel _pnlOutlineSettings = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(18, 4, 0, 0) };
    private readonly FlowLayoutPanel _pnlGlowSettings = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(18, 4, 0, 0) };

    private readonly NumericUpDown _numShadowOffsetX = new() { Minimum = -50, Maximum = 50, Width = 60 };
    private readonly NumericUpDown _numShadowOffsetY = new() { Minimum = -50, Maximum = 50, Width = 60 };
    private readonly NumericUpDown _numShadowBlur = new() { Minimum = 0, Maximum = 24, Width = 60 };
    private readonly Button _btnShadowColor = NewColorButton();

    private readonly NumericUpDown _numOutlineThickness = new() { Minimum = 1, Maximum = 30, Width = 60 };
    private readonly Button _btnOutlineColor = NewColorButton();

    private readonly Button _btnGlowColor = NewColorButton();
    private readonly NumericUpDown _numGlowRadius = new() { Minimum = 1, Maximum = 24, Width = 60 };

    // Renkler (temel renkler + tatil/hatırlatıcı renklerinin senkron kopyaları)
    private readonly Button _btnFontColor = NewColorButton();
    private readonly Button _btnWeekdayColor = NewColorButton();
    private readonly Button _btnShapeFillColor = NewColorButton();
    private readonly Button _btnHolidayColorInColorsTab = NewColorButton();
    private readonly Button _btnReminderColorInColorsTab = NewColorButton();

    // Tatiller / Hatırlatıcılar
    private readonly Button _btnHolidayColor = NewColorButton();
    private readonly Button _btnReminderColor = NewColorButton();
    private readonly BindingList<DateEntryRow> _holidayRows;
    private readonly BindingList<DateEntryRow> _reminderRows;

    private Color _fontColor, _weekdayColor, _shapeFillColor, _holidayColor, _reminderColor;
    private Color _shadowColor, _outlineColor, _glowColor;

    public SettingsForm(AppSettings current, string holidaysFile, string remindersFile)
    {
        _holidaysFile = holidaysFile;
        _remindersFile = remindersFile;
        _holidayRows = new BindingList<DateEntryRow>(DateEntryStore.Load(holidaysFile).Select(ToRow).ToList());
        _reminderRows = new BindingList<DateEntryRow>(DateEntryStore.Load(remindersFile).Select(ToRow).ToList());

        Text = "AS Desktop Calendar - Ayarlar";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ClientSize = new Size(500, 540);
        MinimumSize = new Size(460, 500);
        Font = new Font("Segoe UI", 9f);
        ShowIcon = false;

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var btnCancel = new Button { Text = "İptal", AutoSize = true, MinimumSize = new Size(90, 32) };
        var btnApply = new Button { Text = "Uygula", AutoSize = true, MinimumSize = new Size(90, 32) };
        var btnSave = new Button { Text = "Tamam", AutoSize = true, MinimumSize = new Size(90, 32) };
        btnCancel.Click += (_, _) => Close();
        btnApply.Click += (_, _) => SaveAndNotify(closeAfter: false);
        btnSave.Click += (_, _) => SaveAndNotify(closeAfter: true);
        // FlowDirection.RightToLeft places the FIRST-added control at the right edge,
        // so add in reverse of the desired left-to-right visual order (İptal, Uygula, Tamam).
        btnPanel.Controls.Add(btnSave);
        btnPanel.Controls.Add(btnApply);
        btnPanel.Controls.Add(btnCancel);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildPositionTab());
        tabs.TabPages.Add(BuildEffectTab());
        tabs.TabPages.Add(BuildColorsTab());
        tabs.TabPages.Add(BuildDateEntryTab("Tatiller", "Tatil rengi:", _btnHolidayColor,
            () => _holidayColor, c => _holidayColor = c, _holidayRows, showPortionColumn: true, syncedColorSibling: _btnHolidayColorInColorsTab));
        tabs.TabPages.Add(BuildDateEntryTab("Hatırlatıcılar", "Hatırlatıcı rengi:", _btnReminderColor,
            () => _reminderColor, c => _reminderColor = c, _reminderRows, syncedColorSibling: _btnReminderColorInColorsTab));

        Controls.Add(btnPanel);
        Controls.Add(tabs);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        int lastMonIndex = -1;
        _cmbTargetMonitor.SelectedIndexChanged += (_, _) =>
        {
            if (_cmbTargetMonitor.SelectedIndex < 0) return;
            Screen[] screens = Screen.AllScreens;
            int newIdx = Math.Clamp(_cmbTargetMonitor.SelectedIndex, 0, Math.Max(0, screens.Length - 1));
            if (lastMonIndex >= 0 && lastMonIndex != newIdx && lastMonIndex < screens.Length)
            {
                int oldHeight = screens[lastMonIndex].Bounds.Height;
                int newHeight = screens[newIdx].Bounds.Height;
                if (oldHeight > 0 && newHeight > 0)
                {
                    double ratio1 = oldHeight / (double)_numMonthFontSize.Value;
                    double ratio2 = oldHeight / (double)_numNextMonthFontSize.Value;
                    _numMonthFontSize.Value = (decimal)Math.Clamp(newHeight / ratio1, (double)_numMonthFontSize.Minimum, (double)_numMonthFontSize.Maximum);
                    _numNextMonthFontSize.Value = (decimal)Math.Clamp(newHeight / ratio2, (double)_numNextMonthFontSize.Minimum, (double)_numNextMonthFontSize.Maximum);
                }
            }
            lastMonIndex = newIdx;
            UpdateDynamicLimits();
        };

        _numMonthFontSize.ValueChanged += (_, _) => UpdateDynamicLimits();
        _numNextMonthFontSize.ValueChanged += (_, _) => UpdateDynamicLimits();
        _numNextMonthColumns.ValueChanged += (_, _) =>
        {
            _cmbNextMonthLayout.Enabled = _numNextMonthColumns.Value > 1;
            UpdateDynamicLimits();
        };

        LoadFromSettings(current);
        lastMonIndex = _cmbTargetMonitor.SelectedIndex;
    }

    private static Button NewColorButton() => new() { Width = 60, Height = 26, FlatStyle = FlatStyle.Popup, Text = "" };

    private void WireColorButton(Button btn, Func<Color> getColor, Action<Color> setColor, params Button[] siblingsToSync)
    {
        btn.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = getColor(), FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                setColor(dlg.Color);
                btn.BackColor = dlg.Color;
                foreach (var sibling in siblingsToSync)
                    sibling.BackColor = dlg.Color;
            }
        };
    }

    // ---- Genel ----

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage("Genel");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void AddRow(Control? label, Control control)
        {
            int row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (label != null)
            {
                layout.Controls.Add(label, 0, row);
                layout.Controls.Add(control, 1, row);
            }
            else
            {
                layout.Controls.Add(control, 0, row);
                layout.SetColumnSpan(control, 2);
            }
        }

        AddRow(null, _chkRunAtStartup);
        AddRow(new Label { Text = "Hedef monitör:", AutoSize = true, Anchor = AnchorStyles.Left }, _cmbTargetMonitor);
        AddRow(new Label { Text = "Haftanın ilk günü:", AutoSize = true, Anchor = AnchorStyles.Left }, _cmbFirstDayOfWeek);
        AddRow(new Label { Text = "Yazı tipi:", AutoSize = true, Anchor = AnchorStyles.Left }, _cmbFontName);

        var styleRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        styleRow.Controls.Add(_chkBold);
        styleRow.Controls.Add(_chkItalic);
        AddRow(new Label { Text = "Stil:", AutoSize = true, Anchor = AnchorStyles.Left }, styleRow);

        AddRow(new Label { Text = "Bugünü vurgulama şekli:", AutoSize = true, Anchor = AnchorStyles.Left }, _cmbShape);

        var sizeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        sizeRow.Controls.Add(new Label { Text = "Bu ayın yazı boyutu (piksel):", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        sizeRow.Controls.Add(_numMonthFontSize);
        AddRow(null, sizeRow);

        var nextSizeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        nextSizeRow.Controls.Add(new Label { Text = "Gelecek ayın yazı boyutu (piksel):", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        nextSizeRow.Controls.Add(_numNextMonthFontSize);
        AddRow(null, nextSizeRow);

        var colsRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        colsRow.Controls.Add(_numNextMonthColumns);
        colsRow.Controls.Add(_lblMaxColumnsInfo);
        AddRow(new Label { Text = "Sonraki aylar sütun sayısı:", AutoSize = true, Anchor = AnchorStyles.Left }, colsRow);

        var monthsAheadRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        monthsAheadRow.Controls.Add(_numNextMonthsCount);
        monthsAheadRow.Controls.Add(_lblMaxMonthsInfo);
        AddRow(new Label { Text = "Sonraki aylar sayısı:", AutoSize = true, Anchor = AnchorStyles.Left }, monthsAheadRow);

        var nextMonthLayoutRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        nextMonthLayoutRow.Controls.Add(new Label { Text = "Sonraki aylar yerleşim düzeni:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        nextMonthLayoutRow.Controls.Add(_cmbNextMonthLayout);
        AddRow(null, nextMonthLayoutRow);

        page.Controls.Add(layout);
        return page;
    }

    // ---- Konum ----

    private TabPage BuildPositionTab()
    {
        var page = new TabPage("Konum");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label { Text = "Takvimin ekrandaki konumu:", AutoSize = true, Margin = new Padding(0, 0, 0, 6) }, 0, 0);

        var anchorGrid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 3, Margin = new Padding(0, 0, 0, 14) };
        for (int i = 0; i < 3; i++)
        {
            anchorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            anchorGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }

        string[] anchorLabels =
        {
            "Sol Üst", "Üst Orta", "Sağ Üst",
            "Sol Orta", "Tam Orta", "Sağ Orta",
            "Sol Alt", "Alt Orta", "Sağ Alt",
        };

        for (int i = 0; i < 9; i++)
        {
            var rb = new RadioButton
            {
                Text = anchorLabels[i],
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Margin = new Padding(2),
            };
            _anchorButtons[i] = rb;
            anchorGrid.Controls.Add(rb, i % 3, i / 3);
        }
        root.Controls.Add(anchorGrid, 0, 1);

        var offsetRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        offsetRow.Controls.Add(new Label { Text = "Yatay ince ofset:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        offsetRow.Controls.Add(_numOffsetX);
        offsetRow.Controls.Add(new Label { Text = "  Dikey ince ofset:", AutoSize = true, Margin = new Padding(8, 6, 4, 0) });
        offsetRow.Controls.Add(_numOffsetY);
        root.Controls.Add(offsetRow, 0, 2);

        page.Controls.Add(root);
        return page;
    }

    // ---- Metin Efekti ----

    private TabPage BuildEffectTab()
    {
        var page = new TabPage("Metin Efekti");
        var dynamicPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
        dynamicPanel.Controls.Add(BuildShadowPanel());
        dynamicPanel.Controls.Add(BuildOutlinePanel());
        dynamicPanel.Controls.Add(BuildGlowPanel());
        page.Controls.Add(dynamicPanel);
        return page;
    }

    private static FlowLayoutPanel EffectRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 1, 0, 1) };
        row.Controls.AddRange(controls);
        return row;
    }

    private static Label EffectLabel(string text) => new() { Text = text, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 6, 0) };

    /// <summary>
    /// Lays out one effect's GroupBox: an "Etkinleştir" checkbox followed by its settings,
    /// wrapped in a FlowLayoutPanel that is explicitly inset from (0,0). GroupBox does not
    /// reserve space for its own border/caption for child controls the way Panel does - a
    /// child placed at (0,0) sits on top of the border and caption text, making them look
    /// like they're partly missing. The explicit Location clears that, and Padding on the
    /// right/bottom gives the same breathing room on those sides.
    /// </summary>
    private static void LayoutEffectGroup(GroupBox group, CheckBox checkbox, FlowLayoutPanel settingsPanel)
    {
        var outer = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Location = new Point(10, 19),
            Padding = new Padding(0, 0, 8, 0),
        };
        outer.Controls.Add(checkbox);
        outer.Controls.Add(settingsPanel);
        group.Controls.Add(outer);

        checkbox.CheckedChanged += (_, _) => settingsPanel.Enabled = checkbox.Checked;
        settingsPanel.Enabled = checkbox.Checked;
    }

    private GroupBox BuildShadowPanel()
    {
        _pnlShadowSettings.Controls.Add(EffectRow(EffectLabel("Yatay ofset:"), _numShadowOffsetX, EffectLabel("  Dikey ofset:"), _numShadowOffsetY));
        _pnlShadowSettings.Controls.Add(EffectRow(EffectLabel("Bulanıklık:"), _numShadowBlur));
        _pnlShadowSettings.Controls.Add(EffectRow(EffectLabel("Renk:"), _btnShadowColor));
        LayoutEffectGroup(_grpShadow, _chkShadowEnabled, _pnlShadowSettings);
        WireColorButton(_btnShadowColor, () => _shadowColor, c => _shadowColor = c);
        return _grpShadow;
    }

    private GroupBox BuildOutlinePanel()
    {
        _pnlOutlineSettings.Controls.Add(EffectRow(EffectLabel("Kalınlık (piksel):"), _numOutlineThickness));
        _pnlOutlineSettings.Controls.Add(EffectRow(EffectLabel("Renk:"), _btnOutlineColor));
        LayoutEffectGroup(_grpOutline, _chkOutlineEnabled, _pnlOutlineSettings);
        WireColorButton(_btnOutlineColor, () => _outlineColor, c => _outlineColor = c);
        return _grpOutline;
    }

    private GroupBox BuildGlowPanel()
    {
        _pnlGlowSettings.Controls.Add(EffectRow(EffectLabel("Yarıçap:"), _numGlowRadius));
        _pnlGlowSettings.Controls.Add(EffectRow(EffectLabel("Renk:"), _btnGlowColor));
        LayoutEffectGroup(_grpGlow, _chkGlowEnabled, _pnlGlowSettings);
        WireColorButton(_btnGlowColor, () => _glowColor, c => _glowColor = c);
        return _grpGlow;
    }

    // ---- Renkler ----

    private TabPage BuildColorsTab()
    {
        var page = new TabPage("Renkler");
        var root = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };

        const int rowWidth = 440, rowHeight = 30, labelWidth = 280;

        Panel MakeRow(string label, Button colorButton, Func<Color> getColor, Action<Color> setColor, Button? syncedSibling = null)
        {
            var row = new Panel { Width = rowWidth, Height = rowHeight, Margin = new Padding(0, 0, 0, 6) };
            var lbl = new Label { Text = label, Location = new Point(0, 0), Size = new Size(labelWidth, rowHeight), TextAlign = ContentAlignment.MiddleLeft };
            colorButton.Location = new Point(labelWidth + 10, (rowHeight - colorButton.Height) / 2);
            if (syncedSibling != null)
                WireColorButton(colorButton, getColor, setColor, syncedSibling);
            else
                WireColorButton(colorButton, getColor, setColor);
            row.Controls.Add(lbl);
            row.Controls.Add(colorButton);
            return row;
        }

        root.Controls.Add(MakeRow("Ay adı ve tatil olmayan günler", _btnFontColor, () => _fontColor, c => _fontColor = c));
        root.Controls.Add(MakeRow("Gün isimleri", _btnWeekdayColor, () => _weekdayColor, c => _weekdayColor = c));
        root.Controls.Add(MakeRow("Bugün vurgu dolgu rengi", _btnShapeFillColor, () => _shapeFillColor, c => _shapeFillColor = c));
        root.Controls.Add(MakeRow("Tatil rengi", _btnHolidayColorInColorsTab, () => _holidayColor, c => _holidayColor = c, _btnHolidayColor));
        root.Controls.Add(MakeRow("Hatırlatıcı rengi", _btnReminderColorInColorsTab, () => _reminderColor, c => _reminderColor = c, _btnReminderColor));

        page.Controls.Add(root);
        return page;
    }

    // ---- Tatiller / Hatırlatıcılar ----

    private const string FullDayDisplayText = "Tam Gün";
    private const string AfternoonDisplayText = "Öğleden Sonra";
    private const string AfternoonHolidayDisplayText = "Öğleden sonra tatil";
    private static readonly string[] PortionDisplayValues = { FullDayDisplayText, AfternoonDisplayText };

    private static int CompareDateEntryRows(DateEntryRow left, DateEntryRow right, bool ascending)
    {
        int dateCompare = CompareDateEntryDate(left, right);
        if (dateCompare == 0)
            dateCompare = CompareText(left.Label, right.Label);

        return ascending ? dateCompare : -dateCompare;
    }

    private static int CompareDateEntryDate(DateEntryRow left, DateEntryRow right)
    {
        int leftMonth = GetMonthIndex(left.Month);
        int rightMonth = GetMonthIndex(right.Month);

        int leftYear = TryParseYear(left.Year, out int y) ? y : 2000;
        int rightYear = TryParseYear(right.Year, out int y2) ? y2 : 2000;

        bool leftRecurring = string.IsNullOrWhiteSpace(left.Year);
        bool rightRecurring = string.IsNullOrWhiteSpace(right.Year);

        // Recurring dates do not have an actual year; sort them by month/day in a stable reference year,
        // and keep them after explicit-year entries so fixed-date rows stay in a natural chronology.
        int recurringCompare = leftRecurring.CompareTo(rightRecurring);
        if (recurringCompare != 0)
            return recurringCompare;

        int byYear = leftYear.CompareTo(rightYear);
        if (byYear != 0)
            return byYear;

        int byMonth = leftMonth.CompareTo(rightMonth);
        if (byMonth != 0)
            return byMonth;

        return left.Day.CompareTo(right.Day);
    }

    private static int GetMonthIndex(string month)
    {
        int index = Array.IndexOf(TurkishMonths, month);
        return index >= 0 ? index : 0;
    }

    private static bool TryParseYear(string? yearText, out int year)
    {
        return int.TryParse(yearText, out year);
    }

    private static int CompareText(string left, string right)
    {
        return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
    }

    private TabPage BuildDateEntryTab(string title, string colorLabel, Button colorButton,
        Func<Color> getColor, Action<Color> setColor, BindingList<DateEntryRow> rows, bool showPortionColumn = false, Button? syncedColorSibling = null)
    {
        var page = new TabPage(title);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var colorRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
        colorRow.Controls.Add(new Label { Text = colorLabel, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 6, 8, 0) });
        colorRow.Controls.Add(colorButton);
        colorRow.Controls.Add(new Label
        {
            Text = "Yıl boş ise her yıl tekrarlanır",
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(10, 6, 0, 0),
        });
        if (syncedColorSibling != null)
            WireColorButton(colorButton, getColor, setColor, syncedColorSibling);
        else
            WireColorButton(colorButton, getColor, setColor);
        root.Controls.Add(colorRow, 0, 0);

        var toolbar = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        var btnAdd = new Button { Text = "Ekle", AutoSize = true, Margin = new Padding(3, 3, 3, 3) };
        var btnRemovePast = new Button { Text = "Geçmiş tarihleri sil", AutoSize = true, Margin = new Padding(9, 3, 3, 3) };
        var btnSort = new Button { Text = "Sırala", AutoSize = true, Margin = new Padding(9, 3, 3, 3) };
        toolbar.Controls.Add(btnAdd);
        toolbar.Controls.Add(btnRemovePast);
        toolbar.Controls.Add(btnSort);

        root.Controls.Add(toolbar, 0, 1);

        var bindingSource = new BindingSource { DataSource = rows };

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = bindingSource,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Tarih",
            DataPropertyName = nameof(DateEntryRow.DateDisplay),
            Width = 180,
            ReadOnly = true,
        });
        if (showPortionColumn)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Süre",
                DataPropertyName = nameof(DateEntryRow.PortionDisplay),
                Width = 110,
                ReadOnly = true,
            });
        }
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Açıklama",
            DataPropertyName = nameof(DateEntryRow.Label),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true,
        });

        bool sortAscending = true;
        btnSort.Click += (_, _) =>
        {
            var ordered = rows.OrderBy(r => r, Comparer<DateEntryRow>.Create((a, b) => CompareDateEntryRows(a, b, sortAscending))).ToList();
            rows.Clear();
            foreach (var row in ordered)
                rows.Add(row);

            sortAscending = !sortAscending;
            btnSort.Text = sortAscending ? "Sırala" : "Ters sırala";
        };

        void OpenEntryEditor(DateEntryRow? existingRow)
        {
            var rowToEdit = existingRow ?? new DateEntryRow
            {
                Day = DateTime.Now.Day,
                Month = TurkishMonths[DateTime.Now.Month - 1],
                Year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture),
                Label = "",
                Portion = PortionDisplayValues[0],
            };

            using var editor = new DateEntryEditorDialog(rowToEdit, showPortionColumn, title);
            if (editor.ShowDialog(this) != DialogResult.OK)
                return;

            var edited = editor.EditedRow;
            if (existingRow is null)
            {
                rows.Add(edited);
                int idx = bindingSource.Count - 1;
                if (idx >= 0)
                {
                    bindingSource.Position = idx;
                    if (idx < grid.Rows.Count)
                    {
                        grid.CurrentCell = grid.Rows[idx].Cells[0];
                        grid.FirstDisplayedScrollingRowIndex = idx;
                    }
                }
                return;
            }

            int index = rows.IndexOf(existingRow);
            if (index >= 0)
            {
                rows[index] = edited;
                grid.Refresh();
            }
        }

        void AddNewRow() => OpenEntryEditor(null);

        void EditSelectedRow()
        {
            if (grid.CurrentRow is not null && grid.CurrentRow.Index >= 0 && grid.CurrentRow.Index < rows.Count)
                OpenEntryEditor(rows[grid.CurrentRow.Index]);
        }

        void RemoveSelectedRow()
        {
            if (grid.CurrentRow != null && grid.CurrentRow.Index >= 0 && grid.CurrentRow.Index < rows.Count)
                rows.RemoveAt(grid.CurrentRow.Index);
        }

        void RemovePastRows()
        {
            DateTime today = DateTime.Today;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                DateEntryRow row = rows[i];
                if (string.IsNullOrWhiteSpace(row.Year) || !int.TryParse(row.Year, out int year))
                    continue;

                int month = Array.IndexOf(TurkishMonths, row.Month) + 1;
                if (month < 1 || !DateTime.TryParse($"{year}-{month}-{row.Day}", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    continue;

                if (date < today)
                    rows.RemoveAt(i);
            }
        }

        btnAdd.Click += (_, _) => AddNewRow();
        btnRemovePast.Click += (_, _) => RemovePastRows();

        var gridMenu = new ContextMenuStrip();
        gridMenu.Items.Add("Ekle", null, (_, _) => AddNewRow());
        gridMenu.Items.Add("Düzenle", null, (_, _) => EditSelectedRow());
        gridMenu.Items.Add("Seçileni sil", null, (_, _) => RemoveSelectedRow());
        grid.ContextMenuStrip = gridMenu;
        grid.DoubleClick += (_, _) => EditSelectedRow();
        // Right-clicking a row should select it first, so context menu actions act on the row under the cursor.
        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        };

        root.Controls.Add(grid, 0, 2);

        page.Controls.Add(root);
        return page;
    }

    // ---- Load / Save ----

    private void LoadFromSettings(AppSettings s)
    {
        _chkRunAtStartup.Checked = s.RunAtStartup;

        _cmbFontName.Items.Clear();
        foreach (var family in FontFamily.Families)
            _cmbFontName.Items.Add(family.Name);
        _cmbFontName.SelectedItem = s.FontName;
        if (_cmbFontName.SelectedIndex < 0 && _cmbFontName.Items.Count > 0) _cmbFontName.SelectedIndex = 0;

        _chkBold.Checked = s.FontBold;
        _chkItalic.Checked = s.FontItalic;

        _cmbShape.Items.Clear();
        _cmbShape.Items.Add(new ComboItem("Daire", "Circle"));
        _cmbShape.Items.Add(new ComboItem("Elips", "Ellipse"));
        _cmbShape.Items.Add(new ComboItem("Dikdörtgen", "Rectangle"));
        _cmbShape.Items.Add(new ComboItem("Köşeleri yuvarlatılmış dikdörtgen", "RoundedRectangle"));
        _cmbShape.Items.Add(new ComboItem("Kapsül (uçları tam yuvarlak)", "Capsule"));
        SelectByValue(_cmbShape, s.CurrentDayShape);

        _cmbFirstDayOfWeek.Items.Clear();
        _cmbFirstDayOfWeek.Items.Add(new ComboItem("Pazartesi", "Monday"));
        _cmbFirstDayOfWeek.Items.Add(new ComboItem("Pazar", "Sunday"));
        SelectByValue(_cmbFirstDayOfWeek, s.FirstDayOfWeek);

        _cmbTargetMonitor.Items.Clear();
        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            string label = $"Ekran {i + 1} ({screens[i].Bounds.Width}x{screens[i].Bounds.Height})";
            if (screens[i].Primary) label += " [Birincil]";
            _cmbTargetMonitor.Items.Add(new ComboItem(label, i.ToString(CultureInfo.InvariantCulture)));
        }
        int selMon = Math.Clamp(s.TargetMonitorIndex, 0, Math.Max(0, screens.Length - 1));
        if (_cmbTargetMonitor.Items.Count > 0) _cmbTargetMonitor.SelectedIndex = selMon;

        _cmbNextMonthLayout.Items.Clear();
        _cmbNextMonthLayout.Items.Add(new ComboItem("Yatay (Satır satır)", "RowFirst"));
        _cmbNextMonthLayout.Items.Add(new ComboItem("Dikey (Sütun sütun)", "ColumnFirst"));
        SelectByValue(_cmbNextMonthLayout, s.NextMonthsLayout);

        Screen targetScreen = screens.Length > 0 ? screens[selMon] : Screen.PrimaryScreen!;
        int screenHeight = targetScreen.Bounds.Height;
        _numMonthFontSize.Value = (decimal)Math.Clamp(screenHeight / s.FontRatio1, (double)_numMonthFontSize.Minimum, (double)_numMonthFontSize.Maximum);
        _numNextMonthFontSize.Value = (decimal)Math.Clamp(screenHeight / s.FontRatio2, (double)_numNextMonthFontSize.Minimum, (double)_numNextMonthFontSize.Maximum);

        // Load dependent layout values after font sizes so the dynamic limits use the saved display metrics.
        _numNextMonthColumns.Value = Math.Clamp(s.NextMonthsColumns, (int)_numNextMonthColumns.Minimum, (int)_numNextMonthColumns.Maximum);
        _cmbNextMonthLayout.Enabled = _numNextMonthColumns.Value > 1;
        _numNextMonthsCount.Value = Math.Clamp(s.NextMonthsCount, (int)_numNextMonthsCount.Minimum, (int)_numNextMonthsCount.Maximum);

        int anchorIndex = Math.Max(0, Array.IndexOf(AnchorValues, s.CalendarAnchor));
        _anchorButtons[anchorIndex].Checked = true;
        _numOffsetX.Value = Math.Clamp(s.StartOffsetX, (int)_numOffsetX.Minimum, (int)_numOffsetX.Maximum);
        _numOffsetY.Value = Math.Clamp(s.StartOffsetY, (int)_numOffsetY.Minimum, (int)_numOffsetY.Maximum);

        _chkShadowEnabled.Checked = s.ShadowEnabled;
        _pnlShadowSettings.Enabled = s.ShadowEnabled;

        _chkOutlineEnabled.Checked = s.OutlineEnabled;
        _pnlOutlineSettings.Enabled = s.OutlineEnabled;

        _chkGlowEnabled.Checked = s.GlowEnabled;
        _pnlGlowSettings.Enabled = s.GlowEnabled;

        _numShadowOffsetX.Value = Math.Clamp(s.ShadowOffsetX, (int)_numShadowOffsetX.Minimum, (int)_numShadowOffsetX.Maximum);
        _numShadowOffsetY.Value = Math.Clamp(s.ShadowOffsetY, (int)_numShadowOffsetY.Minimum, (int)_numShadowOffsetY.Maximum);
        _numShadowBlur.Value = Math.Clamp(s.ShadowBlur, (int)_numShadowBlur.Minimum, (int)_numShadowBlur.Maximum);
        _shadowColor = s.ShadowColor; _btnShadowColor.BackColor = _shadowColor;

        _numOutlineThickness.Value = Math.Clamp(s.OutlineThicknessPixels, (int)_numOutlineThickness.Minimum, (int)_numOutlineThickness.Maximum);
        _outlineColor = s.OutlineColor; _btnOutlineColor.BackColor = _outlineColor;

        _numGlowRadius.Value = Math.Clamp(s.GlowRadius, (int)_numGlowRadius.Minimum, (int)_numGlowRadius.Maximum);
        _glowColor = s.GlowColor; _btnGlowColor.BackColor = _glowColor;

        _fontColor = s.FontColor; _btnFontColor.BackColor = _fontColor;
        _weekdayColor = s.WeekdayColor; _btnWeekdayColor.BackColor = _weekdayColor;
        _shapeFillColor = s.ShapeFillColor; _btnShapeFillColor.BackColor = _shapeFillColor;

        _holidayColor = s.HolidayColor; _btnHolidayColor.BackColor = _holidayColor; _btnHolidayColorInColorsTab.BackColor = _holidayColor;
        _reminderColor = s.ReminderColor; _btnReminderColor.BackColor = _reminderColor; _btnReminderColorInColorsTab.BackColor = _reminderColor;

        UpdateDynamicLimits();
    }

    private void UpdateDynamicLimits()
    {
        Screen[] screens = Screen.AllScreens;
        int monIndex = _cmbTargetMonitor.SelectedIndex >= 0 ? _cmbTargetMonitor.SelectedIndex : 0;
        Screen targetScreen = screens.Length > 0 ? screens[Math.Clamp(monIndex, 0, screens.Length - 1)] : Screen.PrimaryScreen!;
        int screenWidth = targetScreen.Bounds.Width;
        int screenHeight = targetScreen.Bounds.Height;

        // Current month and next month approximate dimensions
        // 7 day-columns with 3-char pitch: col 0..6 → 18 chars + 2 chars for last day text = 20 char widths.
        // In typical proportional UI fonts (Segoe UI, Tahoma), width per char is ~0.55-0.6 of font size.
        double monthFontPx = Math.Max(8.0, (double)_numMonthFontSize.Value);
        double nextFontPx = Math.Max(8.0, (double)_numNextMonthFontSize.Value);

        double curMonthWidth = monthFontPx * 0.6 * 20.0;
        double nextColWidth = nextFontPx * 0.6 * 20.0;
        double colGap = nextFontPx * 0.6 * 2.0;

        // Max columns of future months that can fit within active current month's width
        // (Next months should not exceed current month width for visual harmony)
        int maxCols = Math.Max(1, (int)Math.Floor((curMonthWidth + colGap) / (nextColWidth + colGap)));
        maxCols = Math.Clamp(maxCols, 1, 12);

        _numNextMonthColumns.Maximum = maxCols;
        if (_numNextMonthColumns.Value > maxCols)
            _numNextMonthColumns.Value = maxCols;

        _lblMaxColumnsInfo.Text = $"(Maks: {maxCols})";

        // Estimate vertical height needed for current month:
        // 1 title row + 1 weekday row + 6 weeks = 8 rows of monthFontPx plus line margins
        double curMonthHeight = monthFontPx * 1.3 * 8.5;
        double remainingHeight = Math.Max(0.0, screenHeight - curMonthHeight - (nextFontPx * 1.5) - 40.0);

        // Height per row of future month:
        // 1 title row + 1 weekday row + 6 weeks = 8 rows of nextFontPx plus gap
        double futureMonthHeight = (nextFontPx * 1.3 * 8.5) + (nextFontPx * 1.3);
        int maxRowsPerCol = Math.Max(1, (int)Math.Floor(remainingHeight / Math.Max(1.0, futureMonthHeight)));

        int selectedCols = Math.Max(1, (int)_numNextMonthColumns.Value);
        int maxMonths = Math.Clamp(selectedCols * maxRowsPerCol, 1, 36);

        _numNextMonthsCount.Maximum = maxMonths;
        if (_numNextMonthsCount.Value > maxMonths)
            _numNextMonthsCount.Value = maxMonths;

        _lblMaxMonthsInfo.Text = $"(Maks: {maxMonths})";
    }

    private static void SelectByValue(ComboBox combo, string value)
    {
        foreach (ComboItem item in combo.Items)
        {
            if (string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private AppSettings BuildSettingsFromUI()
    {
        Screen[] screens = Screen.AllScreens;
        int monIndex = _cmbTargetMonitor.SelectedIndex >= 0 ? _cmbTargetMonitor.SelectedIndex : 0;
        Screen targetScreen = screens.Length > 0 ? screens[Math.Clamp(monIndex, 0, screens.Length - 1)] : Screen.PrimaryScreen!;
        int screenHeight = targetScreen.Bounds.Height;

        int selectedAnchor = Array.FindIndex(_anchorButtons, rb => rb.Checked);
        if (selectedAnchor < 0) selectedAnchor = 1; // TopCenter fallback

        return new AppSettings
        {
            RunAtStartup = _chkRunAtStartup.Checked,
            TargetMonitorIndex = monIndex,
            FirstDayOfWeek = (_cmbFirstDayOfWeek.SelectedItem as ComboItem)?.Value ?? "Monday",
            FontName = _cmbFontName.SelectedItem?.ToString() ?? "Tahoma",
            FontBold = _chkBold.Checked,
            FontItalic = _chkItalic.Checked,
            CurrentDayShape = (_cmbShape.SelectedItem as ComboItem)?.Value ?? "Capsule",
            NextMonthsCount = (int)_numNextMonthsCount.Value,
            NextMonthsColumns = (int)_numNextMonthColumns.Value,
            NextMonthsLayout = (_cmbNextMonthLayout.SelectedItem as ComboItem)?.Value ?? "RowFirst",
            FontRatio1 = screenHeight / (double)_numMonthFontSize.Value,
            FontRatio2 = screenHeight / (double)_numNextMonthFontSize.Value,

            CalendarAnchor = AnchorValues[selectedAnchor],
            StartOffsetX = (int)_numOffsetX.Value,
            StartOffsetY = (int)_numOffsetY.Value,

            ShadowEnabled = _chkShadowEnabled.Checked,
            ShadowColor = _shadowColor,
            ShadowOffsetX = (int)_numShadowOffsetX.Value,
            ShadowOffsetY = (int)_numShadowOffsetY.Value,
            ShadowBlur = (int)_numShadowBlur.Value,

            OutlineEnabled = _chkOutlineEnabled.Checked,
            OutlineColor = _outlineColor,
            OutlineThicknessPixels = (int)_numOutlineThickness.Value,

            GlowEnabled = _chkGlowEnabled.Checked,
            GlowColor = _glowColor,
            GlowRadius = (int)_numGlowRadius.Value,

            FontColor = _fontColor,
            WeekdayColor = _weekdayColor,
            ShapeFillColor = _shapeFillColor,

            HolidayColor = _holidayColor,
            ReminderColor = _reminderColor,
        };
    }

    private void SaveAndNotify(bool closeAfter)
    {
        // Validate holiday rows
        var holidayEntries = new List<DateEntry>();
        for (int i = 0; i < _holidayRows.Count; i++)
        {
            if (!ValidateRow(_holidayRows[i], out var entry, out string? err))
            {
                MessageBox.Show($"Tatiller tablosu, Satır {i + 1} geçersiz:\n{err}",
                    "Doğrulama Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            holidayEntries.Add(entry!);
        }

        // Validate reminder rows
        var reminderEntries = new List<DateEntry>();
        for (int i = 0; i < _reminderRows.Count; i++)
        {
            if (!ValidateRow(_reminderRows[i], out var entry, out string? err))
            {
                MessageBox.Show($"Hatırlatıcılar tablosu, Satır {i + 1} geçersiz:\n{err}",
                    "Doğrulama Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            reminderEntries.Add(entry!);
        }

        var settings = BuildSettingsFromUI();

        DateEntryStore.Save(_holidaysFile, holidayEntries);
        DateEntryStore.Save(_remindersFile, reminderEntries);

        SettingsSaved?.Invoke(this, settings);

        if (closeAfter)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private static DateEntryRow ToRow(DateEntry e) => new()
    {
        Day = e.Day,
        Month = e.Month is >= 1 and <= 12 ? TurkishMonths[e.Month - 1] : TurkishMonths[0],
        Year = e.Year?.ToString(CultureInfo.InvariantCulture) ?? "",
        Label = e.Label,
        Portion = e.Portion == "Afternoon" ? PortionDisplayValues[1] : PortionDisplayValues[0],
    };

    private static bool ValidateRow(DateEntryRow r, out DateEntry? entry, out string? error)
    {
        entry = null;
        error = null;

        int monthIndex = Array.IndexOf(TurkishMonths, r.Month);
        if (monthIndex < 0)
        {
            error = "Lütfen geçerli bir ay seçin.";
            return false;
        }
        int month = monthIndex + 1;

        int? year = null;
        if (!string.IsNullOrWhiteSpace(r.Year))
        {
            if (!int.TryParse(r.Year, out int y) || y < 1900 || y > 2100)
            {
                error = "Yıl 1900 ile 2100 arasında geçerli bir sayı olmalı veya boş bırakılmalıdır.";
                return false;
            }
            year = y;
        }

        int maxDaysInMonth = year.HasValue
            ? DateTime.DaysInMonth(year.Value, month)
            : DateTime.DaysInMonth(2024, month); // Leap year: allows 29 for repeating Feb

        if (r.Day < 1 || r.Day > maxDaysInMonth)
        {
            string yearNote = year.HasValue ? $" ({year} yılı için)" : "";
            error = $"Gün değeri seçilen ay{yearNote} için 1 ile {maxDaysInMonth} arasında olmalıdır.";
            return false;
        }

        string portion = r.Portion == PortionDisplayValues[1] ? "Afternoon" : "Full";
        entry = new DateEntry { Day = r.Day, Month = month, Year = year, Label = r.Label?.Trim() ?? "", Portion = portion };
        return true;
    }

    /// <summary>Editable row shown in the holidays/reminders grid.</summary>
    private sealed class DateEntryRow
    {
        public int Day { get; set; } = DateTime.Now.Day;
        public string Month { get; set; } = TurkishMonths[DateTime.Now.Month - 1];
        public string Year { get; set; } = "";
        public string Label { get; set; } = "";
        public string Portion { get; set; } = PortionDisplayValues[0];

        public string DateDisplay => string.IsNullOrWhiteSpace(Year)
            ? $"{Day} {Month}"
            : $"{Day} {Month} {Year}";

        public string PortionDisplay => Portion == PortionDisplayValues[1] ? AfternoonDisplayText : FullDayDisplayText;
    }

    private sealed class DateEntryEditorDialog : Form
    {
        private readonly DateTimePicker _datePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMMM yyyy", Width = 180 };
        private readonly CheckBox _chkRepeatYear = new() { Text = "Her yıl tekrarla", AutoSize = true };
        private readonly CheckBox _chkAfternoon = new() { Text = AfternoonHolidayDisplayText, AutoSize = true };
        private readonly TextBox _txtLabel = new() { Width = 240 };
        private readonly Button _btnOk = new() { Text = "Tamam", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(90, 32) };
        private readonly Button _btnCancel = new() { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 32) };

        public DateEntryEditorDialog(DateEntryRow row, bool showPortionOption, string title)
        {
            Text = string.IsNullOrWhiteSpace(title) ? "Giriş düzenle" : title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Padding = new Padding(0), Margin = new Padding(0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (showPortionOption)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblDate = new Label { Text = "Tarih:", AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
            var lblLabel = new Label { Text = "Açıklama:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) };

            int referenceYear = int.TryParse(row.Year, out int y) ? y : DateTime.Now.Year;
            _datePicker.Value = new DateTime(referenceYear, GetMonthIndex(row.Month) + 1, Math.Clamp(row.Day, 1, 31));
            _datePicker.MaxDate = new DateTime(2100, 12, 31);
            _datePicker.MinDate = new DateTime(1900, 1, 1);

            _chkRepeatYear.Checked = string.IsNullOrWhiteSpace(row.Year);
            _chkRepeatYear.CheckedChanged += (_, _) =>
            {
                if (_chkRepeatYear.Checked)
                {
                    _datePicker.ShowCheckBox = false;
                    _datePicker.Value = new DateTime(DateTime.Now.Year, _datePicker.Value.Month, _datePicker.Value.Day);
                }
            };

            _chkAfternoon.Checked = row.Portion == PortionDisplayValues[1];
            _txtLabel.Text = row.Label;

            layout.Controls.Add(lblDate, 0, 0);
            layout.Controls.Add(_datePicker, 1, 0);
            layout.Controls.Add(_chkRepeatYear, 1, 1);
            if (showPortionOption)
            {
                layout.Controls.Add(_chkAfternoon, 1, 2);
            }
            layout.Controls.Add(lblLabel, 0, showPortionOption ? 3 : 2);
            layout.Controls.Add(_txtLabel, 1, showPortionOption ? 3 : 2);

            var btnPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
            btnPanel.Controls.Add(_btnOk);
            btnPanel.Controls.Add(_btnCancel);

            var root = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(layout, 0, 0);
            root.Controls.Add(btnPanel, 0, 1);
            Controls.Add(root);

            _btnOk.Click += (_, _) =>
            {
                if (_chkRepeatYear.Checked && _datePicker.Value.Month == 2 && _datePicker.Value.Day == 29)
                {
                    MessageBox.Show(this, "Her yıl tekrarla seçeneği ile 29 Şubat kabul edilmez.", "Geçersiz tarih", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }

                if (string.IsNullOrWhiteSpace(_txtLabel.Text.Trim()))
                {
                    MessageBox.Show(this, "Açıklama alanı boş olamaz.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }

                var newDate = _datePicker.Value;
                var dateRow = new DateEntryRow
                {
                    Day = newDate.Day,
                    Month = TurkishMonths[newDate.Month - 1],
                    Year = _chkRepeatYear.Checked ? "" : newDate.Year.ToString(CultureInfo.InvariantCulture),
                    Label = _txtLabel.Text.Trim(),
                    Portion = _chkAfternoon.Checked ? PortionDisplayValues[1] : PortionDisplayValues[0],
                };
                EditedRow = dateRow;
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        public DateEntryRow EditedRow { get; private set; } = new();

        private static int GetMonthIndex(string month) => Math.Max(0, Array.IndexOf(TurkishMonths, month));
    }

    private sealed record ComboItem(string Display, string Value)
    {
        public override string ToString() => Display;
    }
}
