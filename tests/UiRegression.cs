using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using StartupTidy;
class UiRegression
{
    static int failures, checks;
    static void Check(bool ok, string name)
    { if (!ok) throw new Exception("FAIL " + name); checks++; Console.WriteLine("PASS " + name); }
    static T Field<T>(object instance, string name)
    { return (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e) {
            failures++; Console.WriteLine("UI EXCEPTION: " + e.Exception);
        };
        string root = Path.Combine(Path.GetFullPath(args[0]), "ui-regression");
        string config = Path.Combine(root, "config.json");
        var saved = new Settings { Rules = new List<Rule> {
            new Rule { Path = Path.Combine(root, "first.exe"), Enabled = true },
            new Rule { Path = Path.Combine(root, "second.exe"), Enabled = false, Force = true },
            new Rule { Path = Path.Combine(root, "third.exe"), Enabled = true }
        }};
        Storage.Save(config, saved);
        byte[] original = File.ReadAllBytes(config);
        try
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                using (var form = new MainForm(false, root, delegate { return ""; }))
                {
                    form.Show(); Application.DoEvents();
                    Check(failures == 0, "reopen nonempty list cycle " + cycle);
                    var list = Field<ListView>(form, "rules");
                    Check(list.Items.Count == 3 && list.Items[0].Checked && !list.Items[1].Checked && list.Items[2].Checked,
                        "mixed checkbox states restored cycle " + cycle);
                    Check(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(config)), "opening does not rewrite config cycle " + cycle);
                    form.Hide(); form.Show(); Application.DoEvents();
                    Check(failures == 0 && list.Items[0].Checked && !list.Items[1].Checked, "hide and reveal preserves state cycle " + cycle);
                    typeof(Control).GetMethod("RecreateHandle", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(list, null);
                    Application.DoEvents();
                    Check(failures == 0 && list.Items[0].Checked && !list.Items[1].Checked, "handle recreation preserves state cycle " + cycle);
                    Check(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(config)), "handle recreation does not rewrite config cycle " + cycle);
                    if (cycle == 2)
                    {
                        list.Items[1].Checked = true; Application.DoEvents();
                        var updated = Storage.Load(config);
                        Check(updated.Rules[0].Enabled && updated.Rules[1].Enabled && updated.Rules[2].Enabled, "checkbox change saves only intended rule");
                        Check(updated.Rules[1].Force, "checkbox change preserves close mode");
                    }
                }
                Application.DoEvents();
            }
            using (var background = new MainForm(true, root, delegate { return ""; }))
            {
                background.Show(); Application.DoEvents();
                Check(!background.Visible && failures == 0, "background launch with saved rules");
            }
            Console.WriteLine("TOTAL " + checks + " UI PASS"); return 0;
        }
        catch (Exception error) { Console.WriteLine(error); return 1; }
    }
}
