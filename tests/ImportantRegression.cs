using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using StartupTidy;
class ImportantRegression
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string root=Path.Combine(Path.GetFullPath(args[0]),"important-regression");Directory.CreateDirectory(root);
        string important=Path.Combine(root,"avpui.exe");File.WriteAllText(important,"");
        string ordinary=Path.Combine(root,"ordinary.exe");File.WriteAllText(ordinary,"");
        string config=Path.Combine(root,"config.json");Storage.Save(config,new Settings());Process child=null;
        try
        {
            Check(Safety.HardRejection(important)==null && Safety.Importance(important)!=null,"important executable can be added with classification");
            var risky=new Rule{Path=important,Enabled=true};Check(!Safety.RuleAllowed(risky),"important rule blocked without acknowledgement");risky.RiskAccepted=true;Check(Safety.RuleAllowed(risky),"acknowledged important rule eligible");
            string legacyPath=Path.Combine(root,"legacy-important.json");Storage.Save(legacyPath,new Settings{Rules=new List<Rule>{new Rule{Path=important,Enabled=true}}});
            Check(!Storage.Load(legacyPath).Rules[0].Enabled,"loading unapproved important rule keeps it disabled");
            Check(Safety.HardRejection(Application.ExecutablePath)!=null,"self termination still excluded");
            string ownPath;using(var own=Process.GetCurrentProcess())Check(Safety.TryIdentity(own,out ownPath,true) && ownPath==Application.ExecutablePath,"unfiltered inspection includes own process");
            using(var picker=new ProcessPicker())
            {
                var list=Field<ListView>(picker,"list");string pid=Process.GetCurrentProcess().Id.ToString();
                Check(list.Items.Cast<ListViewItem>().Any(i=>i.SubItems[1].Text==pid),"picker shows own PID instead of silently filtering");
                typeof(ProcessPicker).GetMethod("AddProcessRow",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(picker,new object[]{"Unreadable process","test-unreadable",null,"Path unavailable"});
                Check(list.Items.Cast<ListViewItem>().Any(i=>i.SubItems[1].Text=="test-unreadable" && i.Tag==null),"picker retains unreadable process row");
            }
            using(var form=new MainForm(false,root,delegate{return "";}))
            {
                form.Show();Application.DoEvents();List<string> skipped;Check(form.AddPaths(new[]{important,ordinary},out skipped)==2,"both important and ordinary entries added");
                var saved=Storage.Load(config);var rule=saved.Rules.Single(r=>r.Path==important);Check(!rule.Enabled && !rule.RiskAccepted,"new important entry disabled and unapproved");
                var list=Field<ListView>(form,"rules");Check(list.Items.Count==2 && list.Items.Cast<ListViewItem>().Any(i=>i.SubItems[2].Text.Contains("заблокирован")),"main list labels blocked important rules");
                var view=Field<ComboBox>(form,"ruleView");view.SelectedIndex=2;Application.DoEvents();Check(list.Items.Count==1 && ((Rule)list.Items[0].Tag).Path==important,"important section isolated");
                Check(Field<Label>(form,"count").Text.Contains("важны для работы системы"),"important section warning visible");
                form.SetDarkTheme(true);
                using(var image=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new System.Drawing.Rectangle(0,0,form.Width,form.Height));image.Save(Path.Combine(root,"important-section.png"));}
                var model=Field<Settings>(form,"settings");var selected=model.Rules.Single(r=>r.Path==important);selected.RiskAccepted=true;selected.Enabled=true;list.Items[0].Selected=true;
                typeof(MainForm).GetMethod("RevokeSelected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                saved=Storage.Load(config);rule=saved.Rules.Single(r=>r.Path==important);Check(!rule.Enabled && !rule.RiskAccepted,"revocation disables rule and persists");
                view.SelectedIndex=1;Application.DoEvents();Check(list.Items.Count==1 && ((Rule)list.Items[0].Tag).Path==ordinary,"ordinary section excludes important entries");
                var observed=new Dictionary<string,string>{{ordinary,"Владелец недоступен для проверки"}};
                form.AddPaths(new[]{ordinary},out skipped,observed);saved=Storage.Load(config);rule=saved.Rules.Single(r=>r.Path==ordinary);
                Check(!rule.Enabled && rule.ImportantReason.Length>0,"observed ownership risk reclassifies duplicate safely");
                rule.RiskAccepted=true;var copied=saved.Copy();Check(copied.Rules.Single(r=>r.Path==ordinary).RiskAccepted && copied.Rules.Single(r=>r.Path==ordinary).ImportantReason==rule.ImportantReason,"copy preserves acknowledgement and reason");
            }
            string dummy=Path.Combine(Path.GetFullPath(args[0]),"TidyTestDummy.exe");
            child=Process.Start(new ProcessStartInfo(dummy,"background"){UseShellExecute=false,CreateNoWindow=true});Thread.Sleep(200);
            var testRule=new Rule{Path=dummy,Force=true,Enabled=true,ImportantReason="Test classification"};var settings=new Settings{Rules=new List<Rule>{testRule}};
            new Engine(delegate{}).Scan(settings,CancellationToken.None);Check(!child.HasExited,"unapproved important helper not terminated");
            testRule.RiskAccepted=true;new Engine(delegate{}).Scan(settings,CancellationToken.None);Check(child.WaitForExit(3000),"approved test helper terminates normally");
            Console.WriteLine("TOTAL "+checks+" IMPORTANT PASS");return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
        finally{if(child!=null){if(!child.HasExited)child.Kill();child.Dispose();}}
    }
}
