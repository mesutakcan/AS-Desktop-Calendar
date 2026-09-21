using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DesktopCalendar;

/// <summary>
/// Replaces the old "run once, draw wallpaper, exit" behavior with a
/// persistent tray app: the calendar is regenerated on demand, whenever
/// settings change, and automatically when the date rolls over.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly string[] TurkishMonths =
    {
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık",
    };

    public const string ProductName = "AS Desktop Calendar";

    private readonly string _dataDir;
    private readonly string _iniFile;
    private readonly string _wallpaperFile;
    private readonly string _holidaysFile;
    private readonly string _remindersFile;

    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _clockTimer;

    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private int _lastGeneratedDay = -1;

    public TrayApplicationContext()
    {
        _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductName);
        if (!Directory.Exists(_dataDir))
            Directory.CreateDirectory(_dataDir);

        _iniFile = Path.Combine(_dataDir, "settings.ini");
        _wallpaperFile = Path.Combine(_dataDir, WallpaperService.WallpaperFileName);
        _holidaysFile = Path.Combine(_dataDir, "holidays.csv");
        _remindersFile = Path.Combine(_dataDir, "reminders.csv");

        _settings = AppSettings.Load(_iniFile);
        StartupManager.Apply(ProductName, _settings.RunAtStartup);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Takvimi yenile", null, (_, _) => RefreshWallpaper());
        menu.Items.Add("Ayarlar...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Github Repo", null, OpenGithubRepo);
        menu.Items.Add("Hakkında", null, OnAbout);
        menu.Items.Add("Çıkış", null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = ProductName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        RefreshWallpaper();

        // Polling every minute is cheap and reliably catches midnight/sleep-wake/date changes.
        _clockTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _clockTimer.Tick += (_, _) => { if (DateTime.Now.Day != _lastGeneratedDay) RefreshWallpaper(); };
        _clockTimer.Start();
    }

    private Icon LoadIcon()
    {
        try
        {
            var assembly = typeof(TrayApplicationContext).Assembly;
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("icon1.ico", StringComparison.OrdinalIgnoreCase));

            if (resourceName is not null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is not null)
                    return new Icon(stream);
            }
        }
        catch
        {
            // fall through to default app icon
        }

        return SystemIcons.Application;
    }

    public void RefreshWallpaper()
    {
        try
        {
            _settings = AppSettings.Load(_iniFile); // pick up manual settings.ini edits too
            string source = WallpaperService.DetermineSourceImage(_iniFile, _wallpaperFile);
            var holidays = DateEntryStore.Load(_holidaysFile);
            var reminders = DateEntryStore.Load(_remindersFile);

            var renderer = new CalendarRenderer(_settings);
            using Bitmap bmp = renderer.Render(source, holidays, reminders);
            WallpaperService.SaveAndApply(bmp, _wallpaperFile);

            bool isNewDay = DateTime.Now.Day != _lastGeneratedDay;
            _lastGeneratedDay = DateTime.Now.Day;

            if (isNewDay)
            {
                CheckAndNotifyReminders(reminders);
            }
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(4000, ProductName, "Duvar kağıdı güncellenemedi: " + ex.Message, ToolTipIcon.Error);
        }
    }

    private void CheckAndNotifyReminders(IReadOnlyList<DateEntry> reminders)
    {
        var now = DateTime.Today;
        var todayMatches = reminders
            .Where(r => IsReminderForDate(r, now))
            .OrderBy(r => r.Month)
            .ThenBy(r => r.Day)
            .ToList();

        var missedMatches = reminders
            .Where(r => IsMissedReminder(r, now))
            .OrderByDescending(r => r.Year ?? now.Year)
            .ThenBy(r => r.Month)
            .ThenBy(r => r.Day)
            .ToList();

        if (todayMatches.Count == 0 && missedMatches.Count == 0)
            return;

        var form = new ReminderNotificationForm(todayMatches, missedMatches);
        form.Show();
    }

    private static bool IsReminderForDate(DateEntry reminder, DateTime date)
    {
        if (reminder.Year is null)
            return reminder.Month == date.Month && reminder.Day == date.Day;

        var reminderDate = new DateTime(reminder.Year.Value, reminder.Month, reminder.Day);
        return reminderDate.Date == date.Date;
    }

    private static bool IsMissedReminder(DateEntry reminder, DateTime today)
    {
        if (reminder.Year is null)
        {
            if (reminder.Month == today.Month && reminder.Day == today.Day)
                return false;

            return reminder.Month < today.Month || (reminder.Month == today.Month && reminder.Day < today.Day);
        }

        var reminderDate = new DateTime(reminder.Year.Value, reminder.Month, reminder.Day);
        return reminderDate.Date < today.Date;
    }

    private static string FormatReminderLabel(DateEntry reminder)
    {
        return string.IsNullOrWhiteSpace(reminder.Label) ? "Hatırlatıcı" : reminder.Label.Trim();
    }

    private sealed class ReminderNotificationForm : Form
    {
        public ReminderNotificationForm(IReadOnlyList<DateEntry> todayMatches, IReadOnlyList<DateEntry> missedMatches)
        {
            Text = "Desktop Calendar - Hatırlatıcılar";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(560, 420);
            MinimumSize = new Size(420, 280);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "Hatırlatıcılar",
                AutoSize = true,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8),
            };
            root.Controls.Add(title, 0, 0);

            var sections = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0, 0, 8, 0),
                BackColor = SystemColors.Control,
                ColumnCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            };
            sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddSection(Control section)
            {
                int row = sections.RowCount++;
                sections.RowStyles.Add(new RowStyle(SizeType.Absolute, section.Height));
                section.Dock = DockStyle.Fill;
                sections.Controls.Add(section, 0, row);
            }

            if (todayMatches.Count > 0)
                AddSection(CreateReminderSection("Bugün", todayMatches));
            if (missedMatches.Count > 0)
                AddSection(CreateReminderSection("Kaçırılan hatırlatıcılar", missedMatches));

            root.Controls.Add(sections, 0, 1);

            var closeButton = new Button { Text = "Kapat", AutoSize = true, DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right, MinimumSize = new Size(90, 32) };
            closeButton.Click += (_, _) => Close();
            var buttonPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            buttonPanel.Controls.Add(closeButton);
            root.Controls.Add(buttonPanel, 0, 2);

            Controls.Add(root);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }

        private static Control CreateReminderSection(string title, IReadOnlyList<DateEntry> items)
        {
            var section = new GroupBox
            {
                Text = title,
                AutoSize = false,
                Padding = new Padding(6),
                Margin = new Padding(0, 0, 0, 8),
            };

            var list = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.FixedSingle,
                HideSelection = false,
                Dock = DockStyle.Fill,
                Height = Math.Min(180, Math.Max(90, items.Count * 28 + 36)),
                MultiSelect = false,
            };

            list.Columns.Add("Tarih", 150);
            list.Columns.Add("Açıklama", -2);
            foreach (var item in items)
            {
                var row = new ListViewItem(new[]
                {
                    FormatDateForDisplay(item),
                    FormatReminderLabel(item),
                });
                row.BackColor = IsReminderForDate(item, DateTime.Today) ? Color.White : Color.FromArgb(250, 250, 250);
                list.Items.Add(row);
            }

            section.Controls.Add(list);
            section.Height = list.Height + 34;
            return section;
        }

        private static string FormatDateForDisplay(DateEntry item)
        {
            if (item.Year is null)
                return $"{item.Day} {GetMonthName(item.Month)}";

            var date = new DateTime(item.Year.Value, item.Month, item.Day);
            if (date.Date == DateTime.Today)
                return "Bugün";

            return date.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));
        }

        private static string GetMonthName(int month)
        {
            return month is >= 1 and <= 12 ? TurkishMonths[month - 1] : TurkishMonths[0];
        }

    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings, _holidaysFile, _remindersFile);
        _settingsForm.SettingsSaved += (_, savedSettings) =>
        {
            _settings = savedSettings;
            _settings.Save(_iniFile);
            StartupManager.Apply(ProductName, _settings.RunAtStartup);
            RefreshWallpaper();
        };
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnAbout(object? sender, EventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        string versionText = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0";

        MessageBox.Show(
            $"{ProductName}\nSürüm {versionText}\n\n" +
            "Masaüstü duvar kağıdınızın üzerine güncel ayı, sonraki ayları, resmi tatilleri ve kişisel hatırlatıcıları yerleştirerek çalışma alanınızı düzenli tutmanıza yardımcı olan, sistem tepsisinden çalışan hafif ve özelleştirilebilir bir takvim uygulamasıdır.\n\n" +
            "Yazı tipi, renk, konum, vurgu şekli ve metin efektleri gibi birçok görünüm ayarını ihtiyaçlarınıza göre değiştirebilir; hatırlatıcılarınızı tarihleri geldiğinde veya kaçırıldığında bildirim penceresinden takip edebilirsiniz." +
            "\n\nGeliştirici: Mesut Akcan\n" +
            "GitHub: https://github.com/mesutakcan\n" +
            "YouTube: https://youtube.com/mesutakcan\n" +
            "Blog: https://mesutakcan.blogspot.com",
            "Hakkında",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void OpenGithubRepo(object? sender, EventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/mesutakcan/AS-Desktop-Calendar",
            UseShellExecute = true,
        });
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _clockTimer.Stop();
        _clockTimer.Dispose();
        _settingsForm?.Close();
        ExitThread();
    }
}
