using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StartupTidy;
class BulkRegression
{
    static int checks;
    static void Check(bool ok, string title) { if (!ok) throw new Exception("FAIL " + title); checks++; Console.WriteLine("PASS " + title); }
    static T Field<T>(object o,string name) { return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o); }
    static void Call(object o,string name,params object[] args) { o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args); }
    static void Select(ListView list,params int[] indexes) { foreach(ListViewItem item in list.Items) item.Selected=false; foreach(int i in indexes) list.Items[i].Selected=true; Application.DoEvents(); }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string root=Path.Combine(Path.GetFullPath(args[0]),"bulk-regression"); Directory.CreateDirectory(root);
        string[] paths=Enumerable.Range(0,4).Select(i=>Path.Combine(root,"test"+i+".exe")).ToArray();
        foreach(string p in paths) File.WriteAllText(p,"");
        string config=Path.Combine(root,"config.json"); Storage.Save(config,new Settings());
        try
        {
            using(var picker=new ProcessPicker(paths.Concat(new[]{paths[0]})))
            {
                picker.Show(); Application.DoEvents(); var list=Field<ListView>(picker,"list");
                Check(list.MultiSelect,"picker native multiselect enabled");
                Check(list.Items.Count==4,"picker deduplicates paths");
                Select(list,0,2); Check(list.SelectedItems.Count==2,"picker accepts nonadjacent selection");
                Call(picker,"Choose"); Check(picker.SelectedPaths.Count==2 && picker.SelectedPaths.Contains(paths[0]) && picker.SelectedPaths.Contains(paths[2]),"picker returns every selected path");
            }
            using(var form=new MainForm(false,root,delegate{return "";}))
            {
                form.Show();Application.DoEvents();List<string> skipped;
                int added=form.AddPaths(paths.Concat(new[]{paths[0],Path.Combine(root,"missing.exe"),Path.Combine(root,"bad.txt")}),out skipped);
                Check(added==4 && skipped.Count==3,"batch add deduplicates and reports invalid files");
                var list=Field<ListView>(form,"rules");Check(list.MultiSelect && list.Items.Count==4,"main list native multiselect enabled");
                Check(list.SelectedItems.Count==4,"newly added batch selected for editing");
                Check(Storage.Load(config).Rules.All(r=>!r.Force),"new batch uses gentle defaults");
                Select(list,0,2);Call(form,"ApplySelected",null,(bool?)true,false);
                var saved=Storage.Load(config);Check(saved.Rules[0].Force && !saved.Rules[1].Force && saved.Rules[2].Force && !saved.Rules[3].Force,"bulk force affects selected rules only");
                Check(list.SelectedItems.Count==2,"selection survives refresh");
                Call(form,"ApplySelected",(bool?)false,null,false);saved=Storage.Load(config);
                Check(!saved.Rules[0].Enabled && saved.Rules[1].Enabled && !saved.Rules[2].Enabled && saved.Rules[3].Enabled,"bulk disable affects selected rules only");
                Call(form,"ApplySelected",(bool?)true,null,false);Check(Storage.Load(config).Rules.All(r=>r.Enabled),"bulk enable persisted");
                Select(list,0,1); Exception modalError=null;
                using(var timer=new Timer {Interval=50})
                {
                    timer.Tick+=delegate {
                        var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Способ закрытия");if(dialog==null)return;
                        timer.Stop();
                        try {var check=dialog.Controls.OfType<CheckBox>().Single();Check(check.ThreeState && check.CheckState==CheckState.Indeterminate,"mixed mode dialog starts unchanged");check.CheckState=CheckState.Checked;dialog.DialogResult=DialogResult.OK;}
                        catch(Exception e){modalError=e;dialog.DialogResult=DialogResult.Cancel;}
                    };timer.Start();Call(form,"EditRule");
                }
                if(modalError!=null)throw modalError;
                saved=Storage.Load(config);Check(saved.Rules[0].Force && saved.Rules[1].Force && saved.Rules[2].Force && !saved.Rules[3].Force,"dialog applies force to whole selection");
                byte[] before=File.ReadAllBytes(config);Select(list);Call(form,"ApplySelected",null,(bool?)false,false);
                Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(config)),"empty selection is no-op");
                Select(list,1,3);Call(form,"ApplySelected",null,null,true);saved=Storage.Load(config);
                Check(saved.Rules.Count==2 && saved.Rules[0].Path==paths[0] && saved.Rules[1].Path==paths[2],"bulk delete removes selected rules only");
            }
            using(var reopened=new MainForm(false,root,delegate{return "";}))
            {reopened.Show();Application.DoEvents();var list=Field<ListView>(reopened,"rules");Check(list.Items.Count==2 && list.Items.Cast<ListViewItem>().All(i=>i.Checked && i.SubItems[1].Text=="Принудительное"),"batch results survive reopening");}
            Console.WriteLine("TOTAL "+checks+" BULK PASS");return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
