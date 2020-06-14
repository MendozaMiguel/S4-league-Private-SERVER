using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MetroFramework;
using MetroFramework.Forms;
using System.Diagnostics;

namespace S4L_Login
{
    public partial class LoginWindow : MetroForm
    {
        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        internal static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImportAttribute("user32.dll")]
        internal static extern bool ReleaseCapture();

        internal const int WM_NCLBUTTONDOWN = 0xA1;

        internal const int HT_CAPTION = 0x2;

        internal IPEndPoint LoginServer_endpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

        string lang = "eng";

        internal string auth_code = "";

        public LoginWindow(IPEndPoint endPoint, string lac)
        {
            InitializeComponent();
            LoginServer_endpoint = endPoint;
            lang = lac;
        }

        public void UpdateLabel(string message)
        {
            this.BeginInvoke((MethodInvoker)delegate { dbgtx.Text = message; });
        }

        public void UpdateErrorLabel(string message)
        {
            this.BeginInvoke((MethodInvoker)delegate { errtx_label.Text = message; });
        }

        public string GetUsername()
        {
            return login_username.Text;
        }

        public string GetPassword()
        {
            return login_passwd.Text;
        }

        public void Ready(string code)
        {
            auth_code = code;
            this.BeginInvoke((MethodInvoker)delegate {
                button1.Visible = false;
                start_btn.Visible = true;
                start_btn.ForeColor = Color.Green;
                label4.Visible = false;
            });
        }

        public void Reset()
        {
            this.BeginInvoke((MethodInvoker)delegate {
                errtx_label.Text = "";
                dbgtx.Text = "";
                label4.Visible = false;
                button1.Enabled = true;
                start_btn.Visible = false;
                login_passwd.Enabled = false;
                login_username.Enabled = false;
            });
        }

        private void start_btn_Click(object sender, EventArgs e)
        {
            try
            {
                start_btn.Enabled = false;
                Process.Start("s4client.exe", $"-rc:eu -lac:{lang} -auth_server_ip:{LoginServer_endpoint.Address.ToString()} -aeria_acc_code:{auth_code}");
                Thread thread = new Thread(() => { Thread.Sleep(1500); Environment.Exit(0); });
                thread.Start();
            }
            catch(Exception ex)
            {
                start_btn.Enabled = true;
                MessageBox.Show("failed to execute s4client.exe");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(login_passwd.Text.Length < 6 || login_username.Text.Length < 6)
            {
                errtx_label.Text = "Account information must have atleast 6 chars!";
            }
            else
            {
                errtx_label.Text = "";
                label4.Visible = true;
                button1.Enabled = false;
                start_btn.Visible = false;
                login_passwd.Enabled = false;
                login_username.Enabled = false;
                Thread newThread = new Thread(() => { LoginClient.Connect(LoginServer_endpoint); });
                newThread.Start();
            }
        }
        
        private void Form1_Load(object sender, EventArgs e)
        {
            Reset();
            Thread newThread = new Thread(() => {
                while (true)
                {
                    Thread.Sleep(500);
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        this.Refresh();
                    });
                }
            });
        }

        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void label3_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void login_username_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                if (sender != null)
                    ((TextBox)sender).SelectAll();
            }
        }
        
        private void login_passwd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                if (sender != null)
                    ((TextBox)sender).SelectAll();
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }
    }
}
