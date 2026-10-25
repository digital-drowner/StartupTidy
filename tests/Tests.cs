using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using StartupTidy;
class Tests
{
    static int count;
    static void Check(bool success, string title) { if (!success) throw new Exception("FAIL " + title); Console.WriteLine("PASS " + title); count++; }
    static Process Spawn(string path, string args) { return Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = false, CreateNoWindow = true }); }
    static Settings RuleFor(string path, bool force) { return new Settings { Rules = new List<Rule> { new Rule { Path = path, Force = force } } }; }
    [STAThread] static int Main(string[] args)
    {
        string dir = Path.GetFullPath(args[0]); string dummy = Path.Combine(dir, "TidyTestDummy.exe");
        var children = new List<Process>();
        try
        {
            var defaults = new Settings(); Check(defaults.Rules.Count == 0 && !defaults.Continuous, "empty, bounded defaults");
            Check(Safety.Rejection(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe")) != null, "Windows process protected");
            Check(Safety.Rejection(Application.ExecutablePath) != null, "self protected");
            Check(Safety.Rejection(Path.Combine(dir, "MsMpEng.exe")) != null, "protected process name");
            Check(Safety.Rejection(Path.Combine(dir, "readme.txt")) != null, "non-executable rejected");
            Check(!Safety.Inside(@"C:\WindowsExtra\app.exe", @"C:\Windows"), "directory boundary exact");
            string config = Path.Combine(dir, "test-config.json"); var saved = RuleFor(dummy, false); saved.IntervalSeconds = 1;
            Storage.Save(config, saved); var loaded = Storage.Load(config);
            Check(loaded.IntervalSeconds == 5 && loaded.Rules[0].Path == dummy && !loaded.Rules[0].Force, "JSON roundtrip and bounds");
            Storage.Save(config, loaded); Check(File.Exists(config + ".bak"), "atomic replacement with backup");
            File.WriteAllText(Path.Combine(dir, "broken.json"), "{broken"); bool rejected = false;
            try { Storage.Load(Path.Combine(dir, "broken.json")); } catch { rejected = true; }
            Check(rejected, "corrupt config rejected");
            var logs = new List<string>(); var engine = new Engine(logs.Add);
            var background = Spawn(dummy, "background"); children.Add(background); Thread.Sleep(300);
            string actualPath; Check(Safety.TryIdentity(background, out actualPath) && String.Equals(actualPath, dummy, StringComparison.OrdinalIgnoreCase), "process owner, session and full path identified");
            var disabled = RuleFor(dummy, true); disabled.Rules[0].Enabled = false;
            engine.Scan(disabled, CancellationToken.None); Check(!background.HasExited, "disabled rule leaves process running");
            engine.Scan(RuleFor(Path.Combine(dir, "elsewhere", "TidyTestDummy.exe"), true), CancellationToken.None);
            Check(!background.HasExited, "same filename at different path not matched");
            engine.Scan(RuleFor(dummy, false), CancellationToken.None);
            Check(!background.HasExited && logs.Any(l => l.Contains("нет доступного окна")), "background process survives gentle mode");
            var canceled = new CancellationTokenSource(); canceled.Cancel();
            new Engine(logs.Add).Scan(RuleFor(dummy, true), canceled.Token); Check(!background.HasExited, "cancellation prevents termination");
            new Engine(logs.Add).Scan(RuleFor(dummy.ToUpperInvariant(), true), CancellationToken.None);
            Check(background.WaitForExit(3000), "forced exact-path termination, case insensitive");
            var window = Spawn(dummy, "window"); children.Add(window); window.WaitForInputIdle(5000);
            new Engine(logs.Add).Scan(RuleFor(dummy, false), CancellationToken.None);
            Check(window.WaitForExit(3000), "graceful window close");
            Check(Startup.Command == "\"" + Application.ExecutablePath + "\" --background", "startup command quoted");
            Console.WriteLine("TOTAL " + count + " PASS"); return 0;
        }
        catch (Exception e) { Console.WriteLine(e.ToString()); return 1; }
        finally { foreach (var child in children) { try { if (!child.HasExited) { child.Kill(); child.WaitForExit(2000); } } catch { } child.Dispose(); } }
    }
}
