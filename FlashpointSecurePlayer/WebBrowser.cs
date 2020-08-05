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
using System.Security.Permissions;

namespace FlashpointSecurePlayer {
    public partial class WebBrowser : Form {
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        private class MessageFilter : IMessageFilter {
            private const int MK_XBUTTON1 = 0x00010000;
            private const int MK_XBUTTON2 = 0x00020000;
            private const int MK_XBUTTONUP = 0x0000020C;

            private readonly Form form;
            private readonly EventHandler onBack;
            private readonly EventHandler onForward;

            public MessageFilter(Form form, EventHandler onBack, EventHandler onForward) {
                this.form = form;
                this.onBack = onBack;
                this.onForward = onForward;
            }

            [SecurityPermission(SecurityAction.Demand)]
            public bool PreFilterMessage(ref Message m) {
                // Blocks all the messages relating to the left mouse button.
                if (m.Msg == MK_XBUTTONUP) {
                    int wParam = m.WParam.ToInt32();

                    if ((wParam & MK_XBUTTON1) == MK_XBUTTON1) {
                        this.onBack.Invoke(form, EventArgs.Empty);
                        return true;
                    } else if ((wParam & MK_XBUTTON2) == MK_XBUTTON2) {
                        this.onForward.Invoke(form, EventArgs.Empty);
                        return true;
                    }
                }
                return false;
            }
        }

        private CustomSecurityManager customSecurityManager = null;
        private Uri webBrowserURL = null;
        private MessageFilter messageFilter = null;

        public object PPDisp {
            get {
                return closableWebBrowser1.ActiveXInstance;
            }
        }

        public WebBrowser() {
            InitializeComponent();
            closableWebBrowser1.DocumentTitleChanged += closableWebBrowser1_DocumentTitleChanged;
            this.messageFilter = new MessageFilter(this, new EventHandler(OnBack), new EventHandler(OnForward));
        }

        public WebBrowser(Uri WebBrowserURL) {
            InitializeComponent();
            closableWebBrowser1.DocumentTitleChanged += closableWebBrowser1_DocumentTitleChanged;
            this.messageFilter = new MessageFilter(this, new EventHandler(OnBack), new EventHandler(OnForward));
            this.webBrowserURL = WebBrowserURL;
        }

        private void WebBrowser_Load(object sender, EventArgs e) {
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
                customSecurityManager = new CustomSecurityManager(closableWebBrowser1);
            } catch (Win32Exception) {
                ProgressManager.ShowError();
                MessageBox.Show(Properties.Resources.FailedCreateCustomSecurityManager, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            if (closableWebBrowser1.ActiveXInstance is SHDocVw.WebBrowser shDocVwWebBrowser) {
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

        private void WebBrowser_Shown(object sender, EventArgs e) {
            if (webBrowserURL != null) {
                closableWebBrowser1.Url = webBrowserURL;
            }
        }
        
        private void WebBrowser_FormClosing(object sender, FormClosingEventArgs e) {
            //Application.Exit();

            if (closableWebBrowser1.ActiveXInstance is SHDocVw.WebBrowser shDocVwWebBrowser) {
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

        private void WebBrowser_Activated(object sender, EventArgs e) {
            Application.AddMessageFilter(messageFilter);
        }

        private void WebBrowser_Deactivate(object sender, EventArgs e) {
            Application.RemoveMessageFilter(messageFilter);
        }
        

        private void closableWebBrowser1_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs e) {
            // get Progress HTML Style Element DOM Node
            Control closableWebBrowser1Control = closableWebBrowser1 as Control;

            if (e.CurrentProgress == -1) {
                closableWebBrowser1Control.Enabled = true;
                UseWaitCursor = false;
                return;
            }

            UseWaitCursor = false;
            closableWebBrowser1Control.Enabled = true;
        }

        private void closableWebBrowser1_DocumentTitleChanged(object sender, EventArgs e) {
            if (String.IsNullOrEmpty(closableWebBrowser1.DocumentTitle)) {
                Text = "Flashpoint Secure Player";
                return;
            }

            Text = closableWebBrowser1.DocumentTitle + " - Flashpoint Secure Player";
        }

        private void dWebBrowserEvents2_NewWindow2(ref object ppDisp, ref bool Cancel) {
            WebBrowser webBrowserForm = new WebBrowser();
            webBrowserForm.Show(this);
            ppDisp = webBrowserForm.PPDisp;
            Cancel = false;
        }

        private void dWebBrowserEvents2_NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl) {
            dWebBrowserEvents2_NewWindow2(ref ppDisp, ref Cancel);
        }

        private void dWebBrowserEvents2_WindowSetTop(int Top) {
            this.Top = Top;
        }

        private void dWebBrowserEvents2_WindowSetLeft(int Left) {
            this.Left = Left;
        }

        private void dWebBrowserEvents2_WindowSetWidth(int Width) {
            this.Width = this.Width - closableWebBrowser1.Width + Width;
        }

        private void dWebBrowserEvents2_WindowSetHeight(int Height) {
            this.Height = this.Height - closableWebBrowser1.Height + Height;
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

        private void OnBack(object sender, EventArgs e) {
            if (closableWebBrowser1.CanGoBack) {
                closableWebBrowser1.GoBack();
            }
        }

        private void OnForward(object sender, EventArgs e) {
            if (closableWebBrowser1.CanGoForward) {
                closableWebBrowser1.GoForward();
            }
        }
    }
}