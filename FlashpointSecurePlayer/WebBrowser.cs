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
        private class WebBrowserTitle {
            private const string APPLICATION_TITLE = "Flashpoint Secure Player";

            protected readonly Form form;
            private string documentTitle = null;
            private long progress = -1;

            public WebBrowserTitle(Form form) {
                this.form = form;
            }

            private void Show() {
                StringBuilder text = new StringBuilder();

                if (!String.IsNullOrEmpty(documentTitle)) {
                    text.Append(documentTitle);
                    text.Append(" - ");
                }

                text.Append(APPLICATION_TITLE);

                if (progress != -1) {
                    text.Append(" [");
                    text.Append(progress);
                    text.Append("%]");
                }

                form.Text = text.ToString();
            }

            public string DocumentTitle {
                set {
                    documentTitle = value;
                    Show();
                }
            }

            public long Progress {
                set {
                    progress = value;
                    Show();
                }
            }
        }

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
        private readonly WebBrowserTitle webBrowserTitle;
        private Uri webBrowserURL = null;
        private MessageFilter messageFilter = null;
        private bool resizable = true;
        private object downloadCompletedLock = new object();
        private bool downloadCompleted = false;

        private bool DownloadCompleted {
            get {
                lock (downloadCompletedLock) {
                    return downloadCompleted;
                }
            }

            set {
                lock (downloadCompletedLock) {
                    downloadCompleted = value;
                }
            }
        }

        private bool Resizable {
            get {
                return resizable;
            }

            set {
                resizable = value;

                if (resizable) {
                    FormBorderStyle = FormBorderStyle.Sizable;
                    MaximizeBox = true;
                } else {
                    FormBorderStyle = FormBorderStyle.FixedSingle;
                    MaximizeBox = false;
                }
            }
        }

        public object PPDisp {
            get {
                if (closableWebBrowser1 == null) {
                    return null;
                }
                return closableWebBrowser1.ActiveXInstance;
            }
        }

        private void _WebBrowser() {
            InitializeComponent();

            if (closableWebBrowser1 == null) {
                return;
            }
            
            closableWebBrowser1.DocumentTitleChanged += closableWebBrowser1_DocumentTitleChanged;
            messageFilter = new MessageFilter(this, new EventHandler(OnBack), new EventHandler(OnForward));
        }

        public WebBrowser() {
            _WebBrowser();
            webBrowserTitle = new WebBrowserTitle(this);
        }

        public WebBrowser(Uri WebBrowserURL) {
            _WebBrowser();
            webBrowserTitle = new WebBrowserTitle(this);
            webBrowserURL = WebBrowserURL;
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

            if (closableWebBrowser1 == null) {
                return;
            }

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
                shDocVwWebBrowser.NewWindow2 += ShDocVwWebBrowser_NewWindow2;
                // IE6
                shDocVwWebBrowser.NewWindow3 += ShDocVwWebBrowser_NewWindow3;
                shDocVwWebBrowser.WindowSetTop += ShDocVwWebBrowser_WindowSetTop;
                shDocVwWebBrowser.WindowSetLeft += ShDocVwWebBrowser_WindowSetLeft;
                shDocVwWebBrowser.WindowSetWidth += ShDocVwWebBrowser_WindowSetWidth;
                shDocVwWebBrowser.WindowSetHeight += ShDocVwWebBrowser_WindowSetHeight;
                shDocVwWebBrowser.WindowSetResizable += ShDocVwWebBrowser_WindowSetResizable;
                shDocVwWebBrowser.DownloadBegin += ShDocVwWebBrowser_DownloadBegin;
                shDocVwWebBrowser.DownloadComplete += ShDocVwWebBrowser_DownloadComplete;
            }

            BringToFront();
            Activate();
        }

        private void WebBrowser_Shown(object sender, EventArgs e) {
            if (closableWebBrowser1 == null || webBrowserURL == null) {
                return;
            }
            
            closableWebBrowser1.Url = webBrowserURL;
        }
        
        private void WebBrowser_FormClosing(object sender, FormClosingEventArgs e) {
            //Application.Exit();
            Hide();

            if (closableWebBrowser1 == null) {
                return;
            }

            if (closableWebBrowser1.ActiveXInstance is SHDocVw.WebBrowser shDocVwWebBrowser) {
                // IE5
                shDocVwWebBrowser.NewWindow2 -= ShDocVwWebBrowser_NewWindow2;
                // IE6
                shDocVwWebBrowser.NewWindow3 -= ShDocVwWebBrowser_NewWindow3;
                shDocVwWebBrowser.WindowSetTop -= ShDocVwWebBrowser_WindowSetTop;
                shDocVwWebBrowser.WindowSetLeft -= ShDocVwWebBrowser_WindowSetLeft;
                shDocVwWebBrowser.WindowSetWidth -= ShDocVwWebBrowser_WindowSetWidth;
                shDocVwWebBrowser.WindowSetHeight -= ShDocVwWebBrowser_WindowSetHeight;
                shDocVwWebBrowser.WindowSetResizable -= ShDocVwWebBrowser_WindowSetResizable;
                shDocVwWebBrowser.DownloadBegin -= ShDocVwWebBrowser_DownloadBegin;
                shDocVwWebBrowser.DownloadComplete -= ShDocVwWebBrowser_DownloadComplete;
            }

            closableWebBrowser1.Dispose();
        }

        private void WebBrowser_Activated(object sender, EventArgs e) {
            Application.AddMessageFilter(messageFilter);
        }

        private void WebBrowser_Deactivate(object sender, EventArgs e) {
            Application.RemoveMessageFilter(messageFilter);
        }

        private void closableWebBrowser1_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs e) {
            if (e.CurrentProgress == -1) {
                DownloadCompleted = true;
            }

            if (DownloadCompleted) {
                return;
            }
            
            webBrowserTitle.Progress = e.MaximumProgress > 0 ? (long)Math.Floor((double)e.CurrentProgress / e.MaximumProgress) : 0;
        }

        private void closableWebBrowser1_DocumentTitleChanged(object sender, EventArgs e) {
            if (closableWebBrowser1 == null) {
                return;
            }

            webBrowserTitle.DocumentTitle = closableWebBrowser1.DocumentTitle;
        }

        private void ShDocVwWebBrowser_NewWindow2(ref object ppDisp, ref bool Cancel) {
            WebBrowser webBrowserForm = new WebBrowser();
            webBrowserForm.Show(this);
            ppDisp = webBrowserForm.PPDisp;
            Cancel = false;
        }

        private void ShDocVwWebBrowser_NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl) {
            ShDocVwWebBrowser_NewWindow2(ref ppDisp, ref Cancel);
        }

        private void ShDocVwWebBrowser_WindowSetTop(int Top) {
            this.Top = Top;
        }

        private void ShDocVwWebBrowser_WindowSetLeft(int Left) {
            this.Left = Left;
        }

        private void ShDocVwWebBrowser_WindowSetWidth(int Width) {
            if (closableWebBrowser1 == null) {
                return;
            }

            this.Width = this.Width - closableWebBrowser1.Width + Width;
        }

        private void ShDocVwWebBrowser_WindowSetHeight(int Height) {
            if (closableWebBrowser1 == null) {
                return;
            }

            this.Height = this.Height - closableWebBrowser1.Height + Height;
        }

        private void ShDocVwWebBrowser_WindowSetResizable(bool Resizable) {
            this.Resizable = Resizable;
        }

        private void ShDocVwWebBrowser_DownloadBegin() {
            if (closableWebBrowser1 == null) {
                return;
            }

            Control closableWebBrowser1Control = closableWebBrowser1 as Control;

            if (closableWebBrowser1Control == null) {
                return;
            }

            DownloadCompleted = false;
            webBrowserTitle.Progress = 0;
            UseWaitCursor = true;
            closableWebBrowser1Control.Enabled = false;
        }

        private void ShDocVwWebBrowser_DownloadComplete() {
            if (closableWebBrowser1 == null) {
                return;
            }

            Control closableWebBrowser1Control = closableWebBrowser1 as Control;

            if (closableWebBrowser1Control == null) {
                return;
            }

            DownloadCompleted = true;
            webBrowserTitle.Progress = -1;
            closableWebBrowser1Control.Enabled = true;
            UseWaitCursor = false;
        }

        private void OnBack(object sender, EventArgs e) {
            if (closableWebBrowser1 == null) {
                return;
            }

            if (closableWebBrowser1.CanGoBack) {
                closableWebBrowser1.GoBack();
            }
        }

        private void OnForward(object sender, EventArgs e) {
            if (closableWebBrowser1 == null) {
                return;
            }

            if (closableWebBrowser1.CanGoForward) {
                closableWebBrowser1.GoForward();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if (keyData == Keys.F11 || keyData == (Keys.Alt | Keys.Enter)) {
                if (TopMost) {
                    // allow other windows over this one
                    TopMost = false;
                    // need to do this now to reset the window to its set size
                    FormBorderStyle = FormBorderStyle.Sizable;
                    // exit fullscreen
                    WindowState = FormWindowState.Normal;
                    // show resizable if this is a resizable window
                    Resizable = resizable;
                } else {
                    // need to do this first to have an effect if starting maximized
                    WindowState = FormWindowState.Normal;
                    // knock out borders, temporarily disabling resizing
                    FormBorderStyle = FormBorderStyle.None;
                    // enter fullscreen
                    WindowState = FormWindowState.Maximized;
                    // don't allow other windows over this one
                    TopMost = true;
                }
            }
            return true;
        }
    }
}