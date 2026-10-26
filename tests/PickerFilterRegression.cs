using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StartupTidy;
class PickerFilterRegression
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string root=Path.Combine(Path.GetFullPath(args[0]),"picker-filter");Directory.CreateDirectory(root);
        string a=Path.Combine(root,"old.exe"),b=Path.Combine(root,"new.exe"),sameName=Path.Combine(root,"elsewhere","old.exe"),important=Path.Combine(root,"avpui.exe");
        try
        {
            using(var picker=new ProcessPicker(new[]{a,b,sameName,important},new[]{a.ToUpperInvariant(),important}))
            {
                picker.Show();Application.DoEvents();var list=Field<ListView>(picker,"list");
                Check(list.Items.Count==2,"existing paths excluded regardless of case");
                Check(list.Items.Cast<ListViewItem>().Any(i=>(string)i.Tag==b),"new executable stays visible");
                Check(list.Items.Cast<ListViewItem>().Any(i=>(string)i.Tag==sameName),"same filename in other folder stays visible");
                Check(!picker.ImportantPaths.ContainsKey(important),"hidden important entry not re-added as observation");
                var add=typeof(ProcessPicker).GetMethod("AddProcessRow",BindingFlags.NonPublic|BindingFlags.Instance);
                add.Invoke(picker,new object[]{"old","111",a,null});add.Invoke(picker,new object[]{"old","112",a,null});
                Check(list.Items.Count==2,"every PID for an existing executable hidden");
                add.Invoke(picker,new object[]{"unknown","113",null,"Path unavailable"});
                Check(list.Items.Cast<ListViewItem>().Any(i=>i.Tag==null),"unreadable path stays visible instead of name guessing");
                list.Items[0].Selected=true;list.Items[1].Selected=true;typeof(ProcessPicker).GetMethod("Choose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(picker,null);
                Check(picker.SelectedPaths.Count==2 && !picker.SelectedPaths.Contains(a),"selection returns only new paths");
            }
            using(var picker=new ProcessPicker(new[]{a},new[]{a}))Check(Field<ListView>(picker,"list").Items.Count==0,"all existing produces empty candidate list");
            using(var picker=new ProcessPicker(new[]{a},new string[0]))Check(Field<ListView>(picker,"list").Items.Count==1,"removed rule becomes selectable again");
            string own=Application.ExecutablePath,config=Path.Combine(root,"config.json");
            Storage.Save(config,new Settings{Rules=new List<Rule>{new Rule{Path=own,Enabled=false,ImportantReason="Test risk"}}});
            using(var form=new MainForm(false,root,delegate{return "";}))
            {
                form.Show();Application.DoEvents();Field<ComboBox>(form,"ruleView").SelectedIndex=1;
                using(var picker=form.CreateProcessPicker())
                {
                    string pid=Process.GetCurrentProcess().Id.ToString();
                    Check(!Field<ListView>(picker,"list").Items.Cast<ListViewItem>().Any(i=>i.SubItems[1].Text==pid),"main form excludes disabled important rule even in ordinary view");
                }
            }
            Console.WriteLine("TOTAL "+checks+" PICKER FILTER PASS");return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
