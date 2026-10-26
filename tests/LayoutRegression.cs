using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StartupTidy;
class LayoutRegression
{
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static Bitmap Render(Control control){var bmp=new Bitmap(control.Width,control.Height);control.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));return bmp;}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string root=Path.Combine(Path.GetFullPath(args[0]),"layout-regression");Directory.CreateDirectory(root);
        Storage.Save(Path.Combine(root,"config.json"),new Settings{DarkTheme=true,Rules=Enumerable.Range(0,100).Select(i=>new Rule{Path=Path.Combine(root,"Example"+i+".exe")}).ToList()});
        try
        {
            using(var form=new MainForm(false,root,delegate{return "";}))
            {
                form.Show();Application.DoEvents();var start=Field<Button>(form,"start");var once=Field<Button>(form,"once");
                var exit=start.Parent.Controls.OfType<Button>().Single(b=>b.Text=="Выход");
                foreach(var size in new[]{new Size(1000,660),new Size(840,580),new Size(1280,800)})
                {
                    form.ClientSize=size;Application.DoEvents();
                    Check(start.Top==once.Top && once.Top==exit.Top,"footer buttons aligned at "+size);
                    Check(start.Parent.ClientRectangle.Contains(start.Bounds) && start.Parent.ClientRectangle.Contains(exit.Bounds),"footer buttons not clipped at "+size);
                }
                form.ClientSize=new Size(1000,660);Application.DoEvents();
                var list=(ThemedListView)Field<ListView>(form,"rules");Rectangle bar=list.ScrollbarBounds(true);
                Check(!bar.IsEmpty,"populated list has visible native scrollbar");
                using(var image=Render(list))
                {
                    image.Save(Path.Combine(root,"dark-scrollbar.png"));
                    Check(image.GetPixel(bar.Left+1,bar.Top+bar.Width+6).ToArgb()==Theme.Background.ToArgb(),"dark scrollbar track rendered without white");
                }
                int before=list.TopItem.Index;list.EnsureVisible(99);Application.DoEvents();
                Check(list.TopItem.Index>before,"native vertical scroll still moves items");
                using(var image=Render(list))Check(image.GetPixel(bar.Left+1,bar.Top+bar.Width+6).ToArgb()==Theme.Background.ToArgb(),"dark track remains dark after scrolling");
                using(var image=Render(form))image.Save(Path.Combine(root,"dark-populated.png"));
                form.SetDarkTheme(false);Application.DoEvents();bar=list.ScrollbarBounds(true);
                using(var image=Render(list))Check(image.GetPixel(bar.Left+1,bar.Top+bar.Width+6).ToArgb()!=Theme.Background.ToArgb(),"light theme restores native scrollbar");
                Check(start.Top==once.Top && once.Top==exit.Top,"alignment preserved in light theme");
                form.SetDarkTheme(true);Application.DoEvents();bar=list.ScrollbarBounds(true);
                using(var image=Render(list))Check(image.GetPixel(bar.Left+1,bar.Top+bar.Width+6).ToArgb()==Theme.Background.ToArgb(),"dark scrollbar restored after theme roundtrip");
            }
            Console.WriteLine("TOTAL "+checks+" LAYOUT PASS");return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
