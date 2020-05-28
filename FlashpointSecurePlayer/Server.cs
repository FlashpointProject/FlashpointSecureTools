using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

using SHDocVw;

namespace FlashpointSecurePlayer {
    public partial class Server : Form {
        private CustomSecurityManager customSecurityManager;
        private Uri webBrowserURL = null;

        public object PPDisp {
            get {
                return webBrowser1.ActiveXInstance;
            }
        }

        public Server() {
            InitializeComponent();
        }

        public Server(Uri WebBrowserURL) {
            InitializeComponent();
            this.webBrowserURL = WebBrowserURL;
        }

        private void Server_Load(object sender, EventArgs e) {
            // default value is Redirector port
            /*
            short port = 8888;
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            if (configuration.AppSettings.Settings["Port"].Value != null) {
                string portString = configuration.AppSettings.Settings["Port"].Value;

                try {
                    port = short.Parse(portString);
                }
                catch (ArgumentNullException) { }
                catch (FormatException) { }
                catch (OverflowException) { }
            }
            */

            try {
                //string portString = port.ToString();
                FlashpointProxy.Enable("http=127.0.0.1:22500;https=127.0.0.1:22500;ftp=127.0.0.1:22500");
            } catch (FlashpointProxyException) {
                // popup message box but allow through anyway
                MessageBox.Show(Properties.Resources.FlashpointProxyNotEnabled, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try {
                customSecurityManager = new CustomSecurityManager(webBrowser1);
            } catch (Win32Exception) {
                ProgressManager.ShowError();
                MessageBox.Show(Properties.Resources.FailedCreateCustomSecurityManager, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            SHDocVw.WebBrowser shDocVwWebBrowser = webBrowser1.ActiveXInstance as SHDocVw.WebBrowser;

            if (shDocVwWebBrowser != null) {
                // IE5
                shDocVwWebBrowser.NewWindow2 += dWebBrowserEvents2_NewWindow2;
                // IE6
                shDocVwWebBrowser.NewWindow3 += dWebBrowserEvents2_NewWindow3;
                shDocVwWebBrowser.WindowSetTop += dWebBrowserEvents2_WindowSetTop;
                shDocVwWebBrowser.WindowSetLeft += dWebBrowserEvents2_WindowSetLeft;
                shDocVwWebBrowser.WindowSetWidth += dWebBrowserEvents2_WindowSetWidth;
                shDocVwWebBrowser.WindowSetHeight += dWebBrowserEvents2_WindowSetHeight;
                shDocVwWebBrowser.WindowSetResizable += dWebBrowserEvents2_WindowSetResizable;
            }

            BringToFront();
            Activate();
        }

        private void Server_Shown(object sender, EventArgs e) {
            if (webBrowserURL != null) {
                webBrowser1.Url = webBrowserURL;
            }
        }
        
        private void Server_FormClosing(object sender, FormClosingEventArgs e) {
            //Application.Exit();
            SHDocVw.WebBrowser shDocVwWebBrowser = webBrowser1.ActiveXInstance as SHDocVw.WebBrowser;

            if (shDocVwWebBrowser != null) {
                // IE5
                shDocVwWebBrowser.NewWindow2 -= dWebBrowserEvents2_NewWindow2;
                // IE6
                shDocVwWebBrowser.NewWindow3 -= dWebBrowserEvents2_NewWindow3;
                shDocVwWebBrowser.WindowSetTop -= dWebBrowserEvents2_WindowSetTop;
                shDocVwWebBrowser.WindowSetLeft -= dWebBrowserEvents2_WindowSetLeft;
                shDocVwWebBrowser.WindowSetWidth -= dWebBrowserEvents2_WindowSetWidth;
                shDocVwWebBrowser.WindowSetHeight -= dWebBrowserEvents2_WindowSetHeight;
                shDocVwWebBrowser.WindowSetResizable -= dWebBrowserEvents2_WindowSetResizable;
            }
        }

        private void dWebBrowserEvents2_NewWindow2(ref object ppDisp, ref bool Cancel) {
            Server serverForm = new Server();
            serverForm.Show(this);
            ppDisp = serverForm.PPDisp;
            Cancel = false;
        }

        private void dWebBrowserEvents2_NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl) {
            dWebBrowserEvents2_NewWindow2(ref ppDisp, ref Cancel);
        }

        private void dWebBrowserEvents2_WindowSetTop(int Top) {
            // TODO: fix for windows where top > height
            this.Top = Top;
        }

        private void dWebBrowserEvents2_WindowSetLeft(int Left) {
            this.Left = Left;
        }

        private void dWebBrowserEvents2_WindowSetWidth(int Width) {
            this.Width = this.Width - webBrowser1.Width + Width;
        }

        private void dWebBrowserEvents2_WindowSetHeight(int Height) {
            this.Height = this.Height - webBrowser1.Height + Height;
        }

        private void dWebBrowserEvents2_WindowSetResizable(bool Resizable) {
            if (Resizable) {
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
            } else {
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
            }
        }
    }
}