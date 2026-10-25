using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyVersion("1.2.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.1.0")]

namespace StartupTidy
{
    [DataContract] public class Rule
    {
        [DataMember] public string Path = "";
        [DataMember] public bool Enabled = true;
        [DataMember] public bool Force = false;
    }
    [DataContract] public class Settings
    {
        [DataMember] public List<Rule> Rules = new List<Rule>();
        [DataMember] public int IntervalSeconds = 10;
        [DataMember] public int DurationMinutes = 3;
        [DataMember] public bool Continuous = false;
        [DataMember] public bool DarkTheme = false;
        public Settings Copy()
        {
            return new Settings { IntervalSeconds = IntervalSeconds, DurationMinutes = DurationMinutes,
                Continuous = Continuous, DarkTheme = DarkTheme, Rules = Rules.Select(r => new Rule { Path = r.Path, Enabled = r.Enabled, Force = r.Force }).ToList() };
        }
        public void Validate()
        {
            IntervalSeconds = Math.Max(5, Math.Min(120, IntervalSeconds));
            DurationMinutes = Math.Max(1, Math.Min(60, DurationMinutes));
            Rules = (Rules ?? new List<Rule>()).Where(r => r != null && !String.IsNullOrWhiteSpace(r.Path)).ToList();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in Rules) { r.Path = Safety.Normalize(r.Path); if (!unique.Add(r.Path)) throw new InvalidDataException("Повторяющийся путь в настройках."); }
        }
    }
    public static class Storage
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool MoveFileEx(string existing, string destination, uint flags);
        public static readonly string DirectoryPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StartupTidy");
        public static readonly string ConfigPath = System.IO.Path.Combine(DirectoryPath, "config.json");
        public static Settings Load(string path)
        {
            if (!File.Exists(path)) return new Settings();
            using (var stream = File.OpenRead(path))
            {
                var result = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                if (result == null) throw new InvalidDataException("Пустые настройки.");
                result.Validate(); return result;
            }
        }
        public static void Save(string path, Settings settings)
        {
            settings.Validate(); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, settings); stream.Flush(true); }
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            if (!MoveFileEx(temp, path, 0x1 | 0x8)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }
    public static class Startup
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static string Command { get { return "\"" + Application.ExecutablePath + "\" --background"; } }
        public static string Read()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(Key)) return key == null ? "" : Convert.ToString(key.GetValue("StartupTidy", ""));
        }
        public static void Set(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Key))
            { if (enabled) key.SetValue("StartupTidy", Command); else key.DeleteValue("StartupTidy", false); }
        }
    }
    public static class Safety
    {
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder text, ref int size);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        static readonly string Sid = WindowsIdentity.GetCurrent().User.Value;
        static readonly int Session = Process.GetCurrentProcess().SessionId;
        static readonly HashSet<string> ProtectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "explorer.exe", "dwm.exe", "winlogon.exe", "csrss.exe", "smss.exe", "services.exe", "lsass.exe", "svchost.exe",
            "MsMpEng.exe", "MsSense.exe", "NisSrv.exe", "SecurityHealthService.exe", "SecurityHealthSystray.exe",
            "avp.exe", "avpui.exe", "avguard.exe", "avgnt.exe", "AvastSvc.exe", "AvastUI.exe", "AVGSvc.exe", "AVGUI.exe", "ekrn.exe", "egui.exe",
            "Memory Compression", "Registry", "System"
        };
        public static string Normalize(string path) { return System.IO.Path.GetFullPath(path); }
        public static bool Inside(string path, string directory)
        {
            return path.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) || String.Equals(path, directory, StringComparison.OrdinalIgnoreCase);
        }
        public static string Rejection(string path)
        {
            try
            {
                path = Normalize(path);
                if (!String.Equals(System.IO.Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) return "Выбери исполняемый файл .exe.";
                if (String.Equals(path, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return "Утилита не может закрывать саму себя.";
                if (Inside(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows)) || ProtectedNames.Contains(System.IO.Path.GetFileName(path)))
                    return "Этот системный или защищённый процесс нельзя добавить.";
                return null;
            }
            catch { return "Некорректный путь к файлу."; }
        }
        public static bool TryIdentity(Process process, out string path)
        {
            path = null; IntPtr handle = IntPtr.Zero, token = IntPtr.Zero;
            try
            {
                if (process.Id == Process.GetCurrentProcess().Id || process.SessionId != Session) return false;
                handle = OpenProcess(0x1000, false, process.Id);
                if (handle == IntPtr.Zero || !OpenProcessToken(handle, 8, out token)) return false;
                using (var identity = new WindowsIdentity(token)) if (identity.User == null || identity.User.Value != Sid) return false;
                var buffer = new StringBuilder(32768); int size = buffer.Capacity;
                if (!QueryFullProcessImageName(handle, 0, buffer, ref size)) return false;
                path = Normalize(buffer.ToString()); return Rejection(path) == null;
            }
            catch { return false; }
            finally { if (token != IntPtr.Zero) CloseHandle(token); if (handle != IntPtr.Zero) CloseHandle(handle); }
        }
    }
    public class Engine
    {
        readonly Action<string> log;
        readonly Dictionary<string, DateTime> attempted = new Dictionary<string, DateTime>();
        public Engine(Action<string> logger) { log = logger; }
        public void Scan(Settings settings, CancellationToken cancellation)
        {
            var rules = settings.Rules.Where(r => r.Enabled && Safety.Rejection(r.Path) == null)
                .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            if (rules.Count == 0) return;
            var alive = new HashSet<string>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    if (cancellation.IsCancellationRequested) continue;
                    try
                    {
                        string path; Rule rule;
                        if (!Safety.TryIdentity(process, out path) || !rules.TryGetValue(path, out rule)) continue;
                        string identity = process.Id + ":" + process.StartTime.ToUniversalTime().Ticks;
                        alive.Add(identity); DateTime last;
                        if (attempted.TryGetValue(identity, out last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(60)) continue;
                        attempted[identity] = DateTime.UtcNow;
                        if (cancellation.IsCancellationRequested) continue;
                        bool requested = process.CloseMainWindow();
                        if (requested)
                        {
                            log("Запрошено обычное закрытие: " + path);
                            for (int i = 0; i < 30 && !process.HasExited; i++) if (cancellation.WaitHandle.WaitOne(100)) break;
                        }
                        if (process.HasExited) { log("Закрыто: " + path); continue; }
                        if (cancellation.IsCancellationRequested) continue;
                        if (rule.Force)
                        {
                            // Revalidate the same Process object before termination; never kill by a stale PID alone.
                            string currentPath;
                            if (Safety.TryIdentity(process, out currentPath) && String.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase))
                            {
                                process.Kill();
                                log((process.WaitForExit(2000) ? "Завершено принудительно: " : "Отправлена команда завершения: ") + path);
                            }
                        }
                        else log("Оставлено открытым: " + path + (requested ? " — приложение ещё работает; принудительное завершение выключено." : " — нет доступного окна; принудительное завершение выключено."));
                    }
                    catch (Exception error) { log("Не удалось закрыть PID " + process.Id + ": " + error.Message); }
                }
            }
            foreach (string key in attempted.Keys.Where(k => !alive.Contains(k)).ToArray()) attempted.Remove(key);
        }
    }
    internal static class AppIcons
    {
        internal static readonly Icon App = Load("StartupTidy.App.ico", SystemInformation.IconSize);
        internal static readonly Icon Inactive = Load("StartupTidy.Inactive.ico", SystemInformation.SmallIconSize);
        internal static readonly Icon Active = Load("StartupTidy.Active.ico", SystemInformation.SmallIconSize);
        static Icon Load(string name, Size size)
        {
            using (var stream = typeof(AppIcons).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("В сборке отсутствует значок: " + name);
                using (var icon = new Icon(stream, size)) return (Icon)icon.Clone();
            }
        }
    }
    public class MainForm : Form
    {
        Settings settings;
        readonly ListView rules = new ThemedListView();
        readonly TextBox journal = new TextBox();
        readonly NumericUpDown interval = new NumericUpDown(), duration = new NumericUpDown();
        readonly CheckBox auto = new CheckBox(), continuous = new CheckBox(), darkTheme = new CheckBox();
        readonly Label status = new Label(), count = new Label();
        readonly Button start = new Button(), once = new Button();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Engine engine;
        readonly object logLock = new object();
        readonly string dataDirectory, configPath;
        readonly Func<string> readStartup;
        CancellationTokenSource cancellation = new CancellationTokenSource();
        bool busy, watching, rebuilding, exiting, background, initialized;
        DateTime end;
        string loadError;
        public MainForm(bool hidden) : this(hidden, Storage.DirectoryPath, Startup.Read) { }
        internal MainForm(bool hidden, string directory, Func<string> startupReader)
        {
            dataDirectory = directory;
            readStartup = startupReader;
            configPath = System.IO.Path.Combine(directory, "config.json");
            background = hidden;
            try { settings = Storage.Load(configPath); }
            catch (Exception e) { settings = new Settings(); loadError = e.Message; background = false; }
            Text = "Тихий старт — управление фоновыми программами";
            Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1000, 660); MinimumSize = new Size(840, 580); StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 248, 251); Icon = AppIcons.App;
            BuildUi();
            engine = new Engine(Log);
            timer.Interval = 1000;
            timer.Tick += async delegate { await Tick(); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть настройки", null, delegate { Reveal(); });
            menu.Items.Add("Начать наблюдение", null, delegate { StartWatching(); });
            menu.Items.Add("Остановить", null, delegate { StopWatching(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Выход", null, delegate { ExitApp(); });
            tray.Icon = AppIcons.Inactive; tray.Text = "Тихий старт: остановлено"; tray.ContextMenuStrip = menu; tray.Visible = true;
            tray.DoubleClick += delegate { Reveal(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(3000, "Тихий старт", "Настройки доступны через значок возле часов. Для завершения выбери «Выход».", ToolTipIcon.Info); }
                else { cancellation.Cancel(); tray.Visible = false; }
            };
            Shown += delegate {
                RefreshRules(); initialized = true;
                if (loadError != null) { Log("Ошибка чтения настроек: " + loadError); MessageBox.Show(this, "Не удалось прочитать настройки. Наблюдение остановлено.\nИсходный файл сохранён: " + configPath + "\n\n" + loadError, "Ошибка настроек", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                if (background) { Hide(); StartWatching(); }
            };
            HandleCreated += delegate { Theme.TitleBar(this, settings.DarkTheme); };
            ApplyTheme(); UpdateStatus();
        }
        void ApplyTheme()
        { Theme.Apply(this, settings.DarkTheme); Theme.ApplyMenu(tray.ContextMenuStrip, settings.DarkTheme); }
        internal void SetDarkTheme(bool dark)
        {
            if (darkTheme.Checked != dark) { darkTheme.Checked = dark; return; }
            settings.DarkTheme = dark; ApplyTheme(); if (initialized) SaveQuietly();
        }
        Button ActionButton(string text, EventHandler action)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(115, 34), Margin = new Padding(0, 0, 10, 8), FlatStyle = FlatStyle.System };
            button.Click += action; return button;
        }
        Label Note(string text, int height)
        { return new Label { Text = text, Dock = DockStyle.Top, Height = height, Padding = new Padding(4, 5, 4, 2), ForeColor = Color.FromArgb(72, 82, 100) }; }
        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 4, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.Controls.Add(new Label { Text = "Тихий старт", Font = new Font("Segoe UI", 23, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
            root.Controls.Add(Note("Выбери приложения, которые нужно закрывать после входа в Windows.", 38), 0, 1);
            var tabs = new ThemedTabs { Dock = DockStyle.Fill, Padding = new Point(18, 8) };
            var programs = new TabPage("Программы") { Padding = new Padding(12), BackColor = Color.White };
            var options = new TabPage("Настройки") { Padding = new Padding(14), BackColor = Color.White };
            var logs = new TabPage("Журнал") { Padding = new Padding(12), BackColor = Color.White };
            tabs.TabPages.AddRange(new[] { programs, options, logs }); root.Controls.Add(tabs, 0, 2);
            rules.View = View.Details; rules.CheckBoxes = true; rules.FullRowSelect = true; rules.MultiSelect = true; rules.Dock = DockStyle.Fill;
            rules.HideSelection = false; rules.Columns.Add("Программа", 200); rules.Columns.Add("Закрытие", 170); rules.Columns.Add("Полный путь к .exe", 490);
            rules.ItemChecked += delegate(object sender, ItemCheckedEventArgs e) {
                // Native ListView construction can report temporary items without a Rule.
                // Only persist an actual change to the one bound item after initialization.
                if (rebuilding || !initialized || e.Item == null) return;
                var rule = e.Item.Tag as Rule;
                if (rule == null || !settings.Rules.Contains(rule) || rule.Enabled == e.Item.Checked) return;
                CancelPending(); rule.Enabled = e.Item.Checked; SaveQuietly(); UpdateStatus();
            };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(0, 10, 0, 0) };
            actions.Controls.Add(ActionButton("Из запущенных…", delegate { PickRunning(); }));
            actions.Controls.Add(ActionButton("Выбрать .exe…", delegate { PickFile(); }));
            actions.Controls.Add(ActionButton("Способ закрытия…", delegate { EditRule(); }));
            actions.Controls.Add(ActionButton("Включить выбранные", delegate { ApplySelected(true, null, false); }));
            actions.Controls.Add(ActionButton("Выключить выбранные", delegate { ApplySelected(false, null, false); }));
            actions.Controls.Add(ActionButton("Удалить выбранные", delegate { ApplySelected(null, null, true); }));
            rules.ItemSelectionChanged += delegate { if (!rebuilding) UpdateCount(); };
            rules.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem item in rules.Items) item.Selected = true; e.SuppressKeyPress = true; } };
            count.Dock = DockStyle.Top; count.Height = 50;
            programs.Controls.Add(rules); programs.Controls.Add(actions); programs.Controls.Add(count);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, AutoScroll = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 0 ? 58 : 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            auto.Text = "Запускать при входе в Windows"; auto.AutoSize = true; auto.Checked = readStartup().Length > 0;
            layout.Controls.Add(auto, 0, 0); layout.SetColumnSpan(auto, 2);
            layout.Controls.Add(new Label { Text = "Проверять каждые, секунд", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 0, 1);
            interval.Minimum = 5; interval.Maximum = 120; interval.Value = settings.IntervalSeconds; interval.Width = 100; layout.Controls.Add(interval, 1, 1);
            layout.Controls.Add(new Label { Text = "Длительность наблюдения, минут", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 0, 2);
            duration.Minimum = 1; duration.Maximum = 60; duration.Value = settings.DurationMinutes; duration.Width = 100; layout.Controls.Add(duration, 1, 2);
            continuous.Text = "Наблюдать постоянно, пока утилита работает"; continuous.AutoSize = true; continuous.Checked = settings.Continuous;
            continuous.CheckedChanged += delegate { duration.Enabled = !continuous.Checked; }; duration.Enabled = !continuous.Checked;
            layout.Controls.Add(continuous, 0, 3); layout.SetColumnSpan(continuous, 2);
            darkTheme.Text = "Тёмная тема — тёмный фон и мятные акценты"; darkTheme.AutoSize = true; darkTheme.Checked = settings.DarkTheme;
            darkTheme.CheckedChanged += delegate { SetDarkTheme(darkTheme.Checked); };
            layout.Controls.Add(darkTheme, 0, 4); layout.SetColumnSpan(darkTheme, 2);
            var save = ActionButton("Применить настройки", delegate { ApplyOptions(); }); layout.Controls.Add(save, 0, 5);
            var note = Note("При обычном запуске наблюдение включается кнопкой внизу.\nПри запуске вместе с Windows оно начинается автоматически.\nПостоянное наблюдение закроет и приложения, открытые вручную.", 85);
            layout.Controls.Add(note, 0, 6); layout.SetColumnSpan(note, 2); layout.SetRowSpan(note, 2);
            options.Controls.Add(layout);
            journal.Multiline = true; journal.ReadOnly = true; journal.ScrollBars = ScrollBars.Both; journal.WordWrap = false; journal.Dock = DockStyle.Fill;
            journal.Font = new Font("Consolas", 9); journal.BackColor = Color.White;
            var logActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(0, 8, 0, 0) };
            logActions.Controls.Add(ActionButton("Папка данных", delegate { try { Directory.CreateDirectory(dataDirectory); Process.Start("explorer.exe", "\"" + dataDirectory + "\""); } catch (Exception e) { Error(e); } }));
            logActions.Controls.Add(Note("Журнал хранится локально; файлы ограничены по размеру.", 35));
            logs.Controls.Add(journal); logs.Controls.Add(logActions);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
            start.Text = "Начать наблюдение"; start.AutoSize = true; start.Height = 34; start.MinimumSize = new Size(0, 34); start.Margin = new Padding(0, 0, 10, 0); start.Click += delegate { if (watching) StopWatching(); else StartWatching(); };
            once.Text = "Закрыть сейчас"; once.AutoSize = true; once.Height = 34; once.MinimumSize = new Size(0, 34); once.Margin = new Padding(0, 0, 10, 0); once.Click += async delegate { await Scan(); };
            status.AutoSize = true; status.Margin = new Padding(4, 8, 8, 0);
            footer.Controls.Add(start); footer.Controls.Add(once); footer.Controls.Add(status);
            footer.Controls.Add(ActionButton("Выход", delegate { ExitApp(); }));
            root.Controls.Add(footer, 0, 3); Controls.Add(root);
        }
        void Error(Exception error) { MessageBox.Show(this, error.Message, "Тихий старт", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        void SaveQuietly()
        {
            try { Storage.Save(configPath, settings); }
            catch (Exception e) { StopWatching(); Error(new IOException("Изменения не сохранены. Наблюдение остановлено.\n" + e.Message)); }
        }
        void ApplyOptions()
        {
            try
            {
                var next = settings.Copy(); next.IntervalSeconds = (int)interval.Value; next.DurationMinutes = (int)duration.Value; next.Continuous = continuous.Checked;
                Storage.Save(configPath, next); settings = next; RefreshRules();
                Startup.Set(auto.Checked);
                Log("Настройки сохранены. Автозапуск " + (auto.Checked ? "включён." : "выключен."));
                if (watching) { StopWatching(); StartWatching(); }
                MessageBox.Show(this, "Настройки применены.", "Тихий старт", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e) { auto.Checked = readStartup().Length > 0; Error(e); }
        }
        void RefreshRules()
        {
            var selected = new HashSet<Rule>(rules.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as Rule));
            rebuilding = true; rules.BeginUpdate();
            try
            {
                rules.Items.Clear();
                foreach (var rule in settings.Rules)
                {
                    var item = new ListViewItem(System.IO.Path.GetFileName(rule.Path)) { Tag = rule, Checked = rule.Enabled };
                    item.SubItems.Add(rule.Force ? "Принудительное" : "Обычное"); item.SubItems.Add(rule.Path);
                    if (Safety.Rejection(rule.Path) != null) { item.ForeColor = settings.DarkTheme ? Color.FromArgb(255,161,151) : Color.DarkRed; item.ToolTipText = "Защищён: правило будет пропущено"; }
                    rules.Items.Add(item); item.Selected = selected.Contains(rule);
                }
            }
            finally { try { rules.EndUpdate(); } finally { rebuilding = false; } }
            UpdateCount(); UpdateStatus();
        }
        void UpdateCount()
        {
            count.Text = settings.Rules.Count == 0 ? "Список пуст. Добавь программу кнопками внизу." :
                "Программ: " + settings.Rules.Count + "   Выбрано: " + rules.SelectedItems.Count + "   Галочка включает правило.";
            count.Text += "\nCtrl + щелчок — выбрать несколько; Shift + щелчок — диапазон; Ctrl + A — выбрать все.";
        }
        List<Rule> SelectedRules()
        { return rules.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as Rule).Where(r => r != null && settings.Rules.Contains(r)).ToList(); }
        void ApplySelected(bool? enabled, bool? force, bool remove)
        {
            var selected = SelectedRules(); if (selected.Count == 0) return;
            if (!remove && !enabled.HasValue && !force.HasValue) return;
            CancelPending();
            foreach (var rule in selected)
            {
                if (remove) settings.Rules.Remove(rule);
                else { if (enabled.HasValue) rule.Enabled = enabled.Value; if (force.HasValue) rule.Force = force.Value; }
            }
            RefreshRules(); SaveQuietly();
        }
        internal int AddPaths(IEnumerable<string> paths, out List<string> skipped)
        {
            skipped = new List<string>(); int added = 0;
            var newlyAdded = new HashSet<Rule>();
            var existing = new HashSet<string>(settings.Rules.Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                string rejection = Safety.Rejection(path);
                if (rejection != null) { skipped.Add(path + " — " + rejection); continue; }
                string normalized = Safety.Normalize(path);
                if (!File.Exists(normalized)) { skipped.Add(path + " — файл не найден."); continue; }
                if (!existing.Add(normalized)) { skipped.Add(path + " — уже в списке."); continue; }
                var rule = new Rule { Path = normalized }; settings.Rules.Add(rule); newlyAdded.Add(rule); added++;
            }
            if (added > 0) { RefreshRules(); foreach (ListViewItem item in rules.Items) item.Selected = newlyAdded.Contains(item.Tag as Rule); SaveQuietly(); }
            return added;
        }
        void AddMany(IEnumerable<string> paths)
        {
            List<string> skipped; int added = AddPaths(paths, out skipped);
            if (skipped.Count > 0) MessageBox.Show(this, "Добавлено: " + added + ". Пропущено: " + skipped.Count + ".\n\n" +
                String.Join("\n", skipped.Take(10)) + (skipped.Count > 10 ? "\n…" : ""), "Добавление программ", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void PickFile()
        {
            using (var dialog = new OpenFileDialog { Filter = "Программы Windows (*.exe)|*.exe", Title = "Какие программы закрывать?", CheckFileExists = true, Multiselect = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) AddMany(dialog.FileNames);
        }
        void PickRunning()
        {
            using (var dialog = new ProcessPicker()) { Theme.Apply(dialog, settings.DarkTheme); dialog.HandleCreated += delegate { Theme.TitleBar(dialog, settings.DarkTheme); }; if (dialog.ShowDialog(this) == DialogResult.OK) AddMany(dialog.SelectedPaths); }
        }
        void EditRule()
        {
            var selected = SelectedRules();
            if (selected.Count == 0) { MessageBox.Show(this, "Сначала выбери программы в списке."); return; }
            using (var dialog = new Form { Text = "Способ закрытия", Font = Font, ClientSize = new Size(580, 280), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false })
            {
                var label = new Label { Text = selected.Count == 1 ? selected[0].Path : "Выбрано программ: " + selected.Count + ". Настройка применяется ко всем выбранным.", Left = 20, Top = 18, Width = 540, Height = 45, AutoEllipsis = true };
                var force = new CheckBox { Text = "Принудительно завершать, если обычное закрытие не помогло", Left = 20, Top = 75, Width = 540, Height = 45, ThreeState = selected.Count > 1, CheckState = selected.All(r => r.Force) ? CheckState.Checked : selected.All(r => !r.Force) ? CheckState.Unchecked : CheckState.Indeterminate };
                var warning = new Label { Text = "Принудительное завершение может привести к потере несохранённых данных.\nОбычное закрытие отправляет запрос окну и ждёт до 3 секунд.\nПромежуточная галочка — оставить прежние способы закрытия.", Left = 20, Top = 130, Width = 540, Height = 80, ForeColor = Color.FromArgb(143, 76, 23), Tag = "warning" };
                var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Left = 330, Top = 235, Width = 110 };
                var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 450, Top = 235, Width = 110 };
                dialog.Controls.AddRange(new Control[] { label, force, warning, ok, cancel }); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                Theme.Apply(dialog, settings.DarkTheme); dialog.HandleCreated += delegate { Theme.TitleBar(dialog, settings.DarkTheme); };
                if (dialog.ShowDialog(this) == DialogResult.OK && force.CheckState != CheckState.Indeterminate) ApplySelected(null, force.Checked, false);
            }
        }
        void Reveal() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        void CancelPending() { if (busy) cancellation.Cancel(); }
        public void StartWatching()
        {
            if (watching || busy) return;
            if (!settings.Rules.Any(r => r.Enabled && Safety.Rejection(r.Path) == null))
            { Log("Наблюдение не запущено: нет включённых правил."); return; }
            cancellation.Dispose(); cancellation = new CancellationTokenSource();
            watching = true; end = DateTime.UtcNow.AddMinutes(settings.DurationMinutes); nextScan = DateTime.MinValue;
            timer.Start(); Log(settings.Continuous ? "Начато постоянное наблюдение." : "Начато наблюдение на " + settings.DurationMinutes + " мин."); UpdateStatus();
        }
        void StopWatching()
        { bool wasWatching = watching; watching = false; timer.Stop(); cancellation.Cancel(); if (wasWatching) Log("Наблюдение остановлено."); UpdateStatus(); }
        DateTime nextScan;
        async Task Tick()
        {
            if (!watching) return;
            if (!settings.Continuous && DateTime.UtcNow >= end) { StopWatching(); Log("Время наблюдения истекло."); return; }
            UpdateStatus();
            if (DateTime.UtcNow >= nextScan && !busy) { nextScan = DateTime.UtcNow.AddSeconds(settings.IntervalSeconds); await Scan(); }
        }
        async Task Scan()
        {
            if (busy || exiting) return;
            if (!settings.Rules.Any(r => r.Enabled)) { Log("Список включённых правил пуст."); return; }
            if (cancellation.IsCancellationRequested) { cancellation.Dispose(); cancellation = new CancellationTokenSource(); }
            busy = true; once.Enabled = false; var snapshot = settings.Copy(); var token = cancellation.Token;
            try { await Task.Run(delegate { engine.Scan(snapshot, token); }); }
            catch (Exception e) { Log("Ошибка проверки: " + e.Message); }
            finally { busy = false; if (!IsDisposed) { once.Enabled = true; UpdateStatus(); } }
        }
        void UpdateStatus()
        {
            start.Text = watching ? "Остановить" : "Начать наблюдение";
            string text = watching ? (settings.Continuous ? "Наблюдение постоянно" : "Осталось: " + Math.Max(0, (int)(end - DateTime.UtcNow).TotalSeconds) + " с") : "Остановлено";
            status.Text = text; tray.Text = "Тихий старт: " + text;
            Icon desiredIcon = watching ? AppIcons.Active : AppIcons.Inactive;
            if (!Object.ReferenceEquals(tray.Icon, desiredIcon)) tray.Icon = desiredIcon;
        }
        public void Log(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine;
            lock (logLock)
            {
                try
                {
                    Directory.CreateDirectory(dataDirectory); string path = System.IO.Path.Combine(dataDirectory, "journal.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) { string old = path + ".1"; if (File.Exists(old)) File.Delete(old); File.Move(path, old); }
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
                catch { line = "[Не удалось записать журнал на диск] " + line; }
            }
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate { if (IsDisposed) return; if (journal.TextLength > 120000) journal.Clear(); journal.AppendText(line); }); }
            catch (InvalidOperationException) { }
        }
        void ExitApp() { exiting = true; StopWatching(); tray.Visible = false; Close(); }
        protected override void Dispose(bool disposing)
        { if (disposing) { timer.Dispose(); tray.Dispose(); cancellation.Cancel(); } base.Dispose(disposing); }
    }
    public class ProcessPicker : Form
    {
        readonly ListView list = new ThemedListView();
        public List<string> SelectedPaths = new List<string>();
        public ProcessPicker() : this(null) { }
        internal ProcessPicker(IEnumerable<string> candidates)
        {
            Text = "Выбери запущенную программу"; Font = new Font("Segoe UI", 10); ClientSize = new Size(860, 460);
            StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(650, 400);
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = true; list.Dock = DockStyle.Fill;
            list.Columns.Add("Программа", 190); list.Columns.Add("Путь", 630);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (candidates == null)
            {
                foreach (var process in Process.GetProcesses()) using (process)
                { string path; if (Safety.TryIdentity(process, out path)) paths.Add(path); }
            }
            else foreach (string path in candidates) paths.Add(path);
            foreach (string path in paths) list.Items.Add(new ListViewItem(new[] { System.IO.Path.GetFileName(path), path }) { Tag = path });
            list.Sorting = SortOrder.Ascending;
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(10) };
            var add = new Button { Text = "Добавить выбранные (0)", AutoSize = true, Enabled = false };
            list.ItemSelectionChanged += delegate { add.Enabled = list.SelectedItems.Count > 0; add.Text = "Добавить выбранные (" + list.SelectedItems.Count + ")"; };
            list.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem item in list.Items) item.Selected = true; e.SuppressKeyPress = true; } };
            add.Click += delegate { Choose(); };
            var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
            panel.Controls.Add(add); panel.Controls.Add(cancel); CancelButton = cancel; list.DoubleClick += delegate { Choose(); };
            var note = new Label { Text = "Выделение: Ctrl + щелчок, Shift + щелчок, Ctrl + A. Затем нажми «Добавить выбранные».\nПоказаны доступные приложения текущего пользователя. Системные процессы скрыты.", Height = 55, Dock = DockStyle.Top, Padding = new Padding(8) };
            Controls.Add(list); Controls.Add(panel); Controls.Add(note);
        }
        void Choose() { if (list.SelectedItems.Count == 0) return; SelectedPaths = list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList(); DialogResult = DialogResult.OK; Close(); }
    }
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool created;
            using (var mutex = new Mutex(true, "Local\\StartupTidy-" + WindowsIdentity.GetCurrent().User.Value, out created))
            {
                if (!created) { MessageBox.Show("Тихий старт уже запущен. Открой его через значок возле часов.", "Тихий старт"); return; }
                try
                {
                    using (var form = new MainForm(args.Contains("--background")))
                    {
                        Application.Run(form);
                    }
                }
                catch (Exception error) { MessageBox.Show(error.Message, "Не удалось запустить Тихий старт", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
