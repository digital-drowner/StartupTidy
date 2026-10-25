using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using StartupTidy;
class IconRegression
{
    static int checks;
    static void Check(bool ok,string title){if(!ok)throw new Exception("FAIL "+title);checks++;Console.WriteLine("PASS "+title);}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
    static void Call(object o,string name){o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.Combine(Path.GetFullPath(args[0]),"icon-regression");
        Storage.Save(Path.Combine(root,"config.json"),new Settings{Rules=new List<Rule>{new Rule{Path=Path.Combine(root,"test.exe")}}});
        try
        {
            var names=typeof(MainForm).Assembly.GetManifestResourceNames();
            Check(Array.IndexOf(names,"StartupTidy.App.ico")>=0 && Array.IndexOf(names,"StartupTidy.Inactive.ico")>=0 && Array.IndexOf(names,"StartupTidy.Active.ico")>=0,"all three icon resources embedded");
            using(var form=new MainForm(false,root,delegate{return "";}))
            {
                form.Show();Application.DoEvents();var tray=Field<NotifyIcon>(form,"tray");
                Check(Object.ReferenceEquals(form.Icon,AppIcons.App),"window uses supplied app icon");
                Check(Object.ReferenceEquals(tray.Icon,AppIcons.Inactive),"startup stopped uses inactive icon");
                form.StartWatching();Check(Object.ReferenceEquals(tray.Icon,AppIcons.Active),"start watching uses active icon");
                Call(form,"StopWatching");Check(Object.ReferenceEquals(tray.Icon,AppIcons.Inactive),"manual stop uses inactive icon");
                form.StartWatching();Set(form,"end",DateTime.UtcNow.AddMinutes(-1));Call(form,"Tick");
                Check(Object.ReferenceEquals(tray.Icon,AppIcons.Inactive),"duration expiry uses inactive icon");
                form.StartWatching();form.Hide();form.Show();Application.DoEvents();
                Check(Object.ReferenceEquals(tray.Icon,AppIcons.Active),"hide and reveal keeps active status");
                Call(form,"StopWatching");
            }
            using(var form=new MainForm(true,root,delegate{return "";}))
            {form.Show();Application.DoEvents();Check(!form.Visible && Object.ReferenceEquals(Field<NotifyIcon>(form,"tray").Icon,AppIcons.Active),"background launch sets active tray icon");Call(form,"StopWatching");}
            Console.WriteLine("TOTAL "+checks+" ICON PASS");return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
