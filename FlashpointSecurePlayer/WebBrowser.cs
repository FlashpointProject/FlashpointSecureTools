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
            protected readonly Form form;
            private string applicationTitle = "Flashpoint Secure Player";
            private string documentTitle = null;
            private int progress = -1;

            public WebBrowserTitle(Form form) {
                this.form = form;
                this.applicationTitle += " " + typeof(WebBrowserTitle).Assembly.GetName().Version;
            }

            private void Show() {
                if (form == null) {
                    return;
                }

                StringBuilder text = new StringBuilder();

                if (!String.IsNullOrEmpty(documentTitle)) {
                    text.Append(documentTitle);
                    text.Append(" - ");
                }

                text.Append(this.applicationTitle);

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

            public int Progress {
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

        private bool useFlashActiveXControl = false;
        private CustomSecurityManager customSecurityManager = null;
        private readonly WebBrowserTitle webBrowserTitle;
        private Uri webBrowserURL = null;
        private bool addressToolStripSpringTextBoxEntered = false;
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
                if (closableWebBrowser == null) {
                    return null;
                }
                return closableWebBrowser.ActiveXInstance;
            }
        }

        private void _WebBrowser() {
            InitializeComponent();

            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.Form = this;
            closableWebBrowser.CanGoBackChanged += closableWebBrowser_CanGoBackChanged;
            closableWebBrowser.CanGoForwardChanged += closableWebBrowser_CanGoForwardChanged;
            closableWebBrowser.DocumentTitleChanged += closableWebBrowser_DocumentTitleChanged;
            closableWebBrowser.StatusTextChanged += closableWebBrowser_StatusTextChanged;
            closableWebBrowser.Navigated += closableWebBrowser_Navigated;

            statusBarStatusStrip.Renderer = new FlashpointSecurePlayer.EllipsisRenderer();

            messageFilter = new MessageFilter(this, new EventHandler(OnBack), new EventHandler(OnForward));
        }

        public WebBrowser(bool _useFlashActiveXControl = false) {
            _WebBrowser();
            webBrowserTitle = new WebBrowserTitle(this);
            useFlashActiveXControl = _useFlashActiveXControl;
        }

        public WebBrowser(Uri _webBrowserURL, bool _useFlashActiveXControl = false) {
            _WebBrowser();
            webBrowserTitle = new WebBrowserTitle(this);
            webBrowserURL = _webBrowserURL;
            useFlashActiveXControl = _useFlashActiveXControl;
        }

        private void Navigate(String URL) {
            if (closableWebBrowser == null) {
                return;
            }

            if (String.IsNullOrEmpty(URL)) {
                return;
            }

            if (URL.Equals("about:blank")) {
                return;
            }

            try {
                closableWebBrowser.Navigate(new Uri(AddURLProtocol(URL)));
            } catch (System.UriFormatException) {
                return;
            }
        }

        private void WebBrowser_Load(object sender, EventArgs e) {
            Text += " " + typeof(WebBrowser).Assembly.GetName().Version;

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

            if (closableWebBrowser == null) {
                return;
            }

            try {
                //string portString = port.ToString();
                FlashpointProxy.Enable("http=127.0.0.1:22500;https=127.0.0.1:22500;ftp=127.0.0.1:22500");
            } catch (FlashpointProxyException ex) {
                // popup message box but allow through anyway
                LogExceptionToLauncher(ex);
                MessageBox.Show(Properties.Resources.FlashpointProxyNotEnabled, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try {
                customSecurityManager = new CustomSecurityManager(closableWebBrowser, useFlashActiveXControl);
            } catch (Win32Exception ex) {
                LogExceptionToLauncher(ex);
                ProgressManager.ShowError();
                MessageBox.Show(Properties.Resources.FailedCreateCustomSecurityManager, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            if (closableWebBrowser.ActiveXInstance is SHDocVw.WebBrowser shDocVwWebBrowser) {
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
            if (closableWebBrowser == null || webBrowserURL == null) {
                return;
            }

            closableWebBrowser.Url = webBrowserURL;
        }

        private void WebBrowser_FormClosing(object sender, FormClosingEventArgs e) {
            //Application.Exit();
            Hide();

            if (closableWebBrowser == null) {
                return;
            }

            if (closableWebBrowser.ActiveXInstance is SHDocVw.WebBrowser shDocVwWebBrowser) {
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

            closableWebBrowser.Dispose();
        }

        private void WebBrowser_Activated(object sender, EventArgs e) {
            Application.AddMessageFilter(messageFilter);
        }

        private void WebBrowser_Deactivate(object sender, EventArgs e) {
            Application.RemoveMessageFilter(messageFilter);
        }

        private void closableWebBrowser_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs e) {
            if (e.CurrentProgress < 0) {
                DownloadCompleted = true;
            }

            if (DownloadCompleted) {
                return;
            }

            int progress = e.MaximumProgress > 0 ? (int)Math.Min((double)e.CurrentProgress / e.MaximumProgress * 100, 100) : 0;
            webBrowserTitle.Progress = progress;
            progressToolStripProgressBar.Value = progress;
            progressToolStripProgressBar.ToolTipText = progress + "%";
        }

        private void closableWebBrowser_CanGoBackChanged(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            backButton.Enabled = closableWebBrowser.CanGoBack;
        }

        private void closableWebBrowser_CanGoForwardChanged(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            forwardButton.Enabled = closableWebBrowser.CanGoForward;
        }

        private void closableWebBrowser_DocumentTitleChanged(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            webBrowserTitle.DocumentTitle = closableWebBrowser.DocumentTitle;
        }

        private void closableWebBrowser_StatusTextChanged(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            statusToolStripStatusLabel.Text = closableWebBrowser.StatusText;
        }

        private void closableWebBrowser_Navigated(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            addressToolStripSpringTextBox.Text = closableWebBrowser.Url.ToString();
        }

        private void ShDocVwWebBrowser_NewWindow2(ref object ppDisp, ref bool Cancel) {
            WebBrowser webBrowserForm = new WebBrowser(useFlashActiveXControl);
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
            if (closableWebBrowser == null) {
                return;
            }

            this.Width = this.Width - closableWebBrowser.Width + Width;
        }

        private void ShDocVwWebBrowser_WindowSetHeight(int Height) {
            if (closableWebBrowser == null) {
                return;
            }

            this.Height = this.Height - closableWebBrowser.Height + Height;
        }

        private void ShDocVwWebBrowser_WindowSetResizable(bool Resizable) {
            this.Resizable = Resizable;
        }

        private void ShDocVwWebBrowser_DownloadBegin() {
            if (closableWebBrowser == null) {
                return;
            }

            Control closableWebBrowserControl = closableWebBrowser as Control;

            if (closableWebBrowserControl == null) {
                return;
            }

            DownloadCompleted = false;
            webBrowserTitle.Progress = 0;
            progressToolStripProgressBar.Value = 0;
            progressToolStripProgressBar.ToolTipText = "0%";
            UseWaitCursor = true;
            closableWebBrowserControl.Enabled = false;
        }

        private void ShDocVwWebBrowser_DownloadComplete() {
            if (closableWebBrowser == null) {
                return;
            }

            Control closableWebBrowserControl = closableWebBrowser as Control;

            if (closableWebBrowserControl == null) {
                return;
            }

            DownloadCompleted = true;
            webBrowserTitle.Progress = -1;
            progressToolStripProgressBar.Value = 0;
            progressToolStripProgressBar.ToolTipText = String.Empty;
            closableWebBrowserControl.Enabled = true;
            UseWaitCursor = false;
        }

        private void backButton_Click(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.GoBack();
        }

        private void forwardButton_Click(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.GoForward();
        }

        private void stopButton_Click(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.Stop();
        }

        private void refreshButton_Click(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            // Skip refresh if about:blank is loaded to avoid removing
            // content specified by the DocumentText property.
            if (!closableWebBrowser.Url.Equals("about:blank")) {
                closableWebBrowser.Refresh();
            }
        }

        private void saveAsButton_Click(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.ShowSaveAsDialog();
        }

        private void printButton_Click(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.ShowPrintDialog();
        }

        private void addressToolStripSpringTextBox_Click(object sender, EventArgs e) {
            if (addressToolStripSpringTextBoxEntered) {
                addressToolStripSpringTextBoxEntered = false;
                addressToolStripSpringTextBox.SelectAll();
            }
        }

        private void addressToolStripTextBox_Paint(object sender, PaintEventArgs e) {
            // manually draw the border so the text is vertically aligned correctly
            Rectangle borderRectangle = new Rectangle(0, 1, addressToolStripSpringTextBox.Width - 1, addressToolStripSpringTextBox.Height - 3);
            e.Graphics.FillRectangle(SystemBrushes.Window, borderRectangle);
            e.Graphics.DrawRectangle(SystemPens.WindowFrame, borderRectangle);
        }

        private void addressToolStripSpringTextBox_Enter(object sender, EventArgs e) {
            addressToolStripSpringTextBoxEntered = true;
        }

        private void addressToolStripTextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                Navigate(addressToolStripSpringTextBox.Text);
            }
        }

        private void goButton_Click(object sender, EventArgs e) {
            Navigate(addressToolStripSpringTextBox.Text);
        }

        private void OnBack(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.GoBack();
        }

        private void OnForward(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.GoForward();
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