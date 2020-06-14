using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace S4L_Login
{
    static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetProcessDPIAware();
        public static LoginWindow LoginWindow;

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                MessageBox.Show("ERR arguments");
            }
            else
            {
                if (Environment.OSVersion.Version.Major >= 6)
                    SetProcessDPIAware();
                Application.SetCompatibleTextRenderingDefault(false);
                LoginWindow = new LoginWindow(new IPEndPoint(IPAddress.Parse(args[0]), 28001), args[1]);
                Application.Run(LoginWindow);
            }
        }
    }
}
