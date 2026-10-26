using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StartupTidy;
class ThemeRegression
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static void Render(Form form,string path){using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height));image.Save(path);}}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string root=Path.Combine(Path.GetFullPath(args[0]),"theme-regression");Directory.CreateDirectory(root);string config=Path.Combine(root,"config.json");
        var settings=new Settings{Rules=new List<Rule>{new Rule{Path=Path.Combine(root,"ExampleApp.exe")},new Rule{Path=Path.Combine(root,"ExampleHelper.exe"),Force=true,Enabled=false}}};
        Storage.Save(config,settings);
        try
        {
            Check(!settings.DarkTheme,"light default for existing users");
            var copy=settings.Copy();copy.DarkTheme=true;Check(copy.Copy().DarkTheme,"settings copy preserves theme");
            string legacy=File.ReadAllText(config).Replace("\"DarkTheme\":false,","");File.WriteAllText(Path.Combine(root,"legacy.json"),legacy);
            Check(!Storage.Load(Path.Combine(root,"legacy.json")).DarkTheme,"legacy JSON without theme loads");
            using(var form=new MainForm(false,root,delegate{return "";}))
            {
                form.Show();Application.DoEvents();var list=Field<ListView>(form,"rules");list.Items[0].Selected=true;
                var tray=Field<NotifyIcon>(form,"tray");var originalIcon=tray.Icon;
                Render(form,Path.Combine(root,"light.png"));
                form.SetDarkTheme(true);Application.DoEvents();
                Check(form.BackColor==Theme.Background && list.BackColor==Theme.Surface,"dark palette applied to window and list");
                Check(Field<TextBox>(form,"journal").ForeColor==Theme.Text,"journal text readable on dark surface");
                Check(list.SelectedItems.Count==1 && list.Items[0].Checked && !list.Items[1].Checked,"switch preserves selection and checkbox states");
                Check(Object.ReferenceEquals(originalIcon,tray.Icon),"switch preserves tray status icon");
                var saved=Storage.Load(config);Check(saved.DarkTheme && saved.Rules.Count==2 && !saved.Rules[1].Enabled && saved.Rules[1].Force,"theme persisted without altering rules");
                Render(form,Path.Combine(root,"dark.png"));
                var tabs=form.Controls.Cast<Control>().SelectMany(c=>c.Controls.Cast<Control>()).OfType<TabControl>().Single();tabs.SelectedIndex=1;Application.DoEvents();Render(form,Path.Combine(root,"dark-settings.png"));
                using(var picker=new ProcessPicker(new[]{settings.Rules[0].Path,settings.Rules[1].Path}))
                {Theme.Apply(picker,true);picker.Show();Application.DoEvents();Check(picker.BackColor==Theme.Background,"process picker themed");Render(picker,Path.Combine(root,"dark-picker.png"));}
                form.StartWatching();form.SetDarkTheme(false);Check(Field<bool>(form,"watching") && Object.ReferenceEquals(tray.Icon,AppIcons.Active),"theme switch leaves observation active");
                Check(!Storage.Load(config).DarkTheme && list.BackColor==Color.White,"light theme restored and saved");
                typeof(MainForm).GetMethod("StopWatching",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                form.SetDarkTheme(true);
            }
            using(var reopened=new MainForm(false,root,delegate{return "";}))
            {reopened.Show();Application.DoEvents();Check(reopened.BackColor==Theme.Background && Field<CheckBox>(reopened,"darkTheme").Checked,"dark theme restored after reopening");}
            Console.WriteLine("TOTAL "+checks+" THEME PASS");return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
