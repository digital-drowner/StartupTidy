using System;
using System.Threading;
using System.Windows.Forms;
class Dummy
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "window") Application.Run(new Form { Text = "StartupTidy test process", Width = 350, Height = 140 });
        else Thread.Sleep(120000);
    }
}
