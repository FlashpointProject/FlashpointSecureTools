﻿using SHDocVw;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    public partial class WebBrowserMode : Form {
        private readonly HookProc lowLevelMouseProc;
        private IntPtr mouseHook = IntPtr.Zero;

        private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam) {
            try {
                if (nCode >= 0) {
                    // always confirm the message first so we don't do unnecessary work
                    if (wParam.ToInt32() == WM_MOUSEMOVE) {
                        if (Fullscreen) {
                            ShowToolBar();
                        }
                    }
                }
            } catch {
                // we don't want exceptions to prevent passing the window message
            }
            return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        private WINDOWPLACEMENT WindowPlacement {
            get {
                if (!IsHandleCreated || Handle == IntPtr.Zero) {
                    throw new InvalidOperationException("Handle is null.");
                }

                WINDOWPLACEMENT value = new WINDOWPLACEMENT();
                value.length = Marshal.SizeOf(value);

                if (!GetWindowPlacement(Handle, ref value)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
                return value;
            }

            set {
                if (!IsHandleCreated || Handle == IntPtr.Zero) {
                    throw new InvalidOperationException("Handle is null.");
                }

                value.length = Marshal.SizeOf(value);

                if (!SetWindowPlacement(Handle, ref value)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
        }

        private const int EXIT_FULLSCREEN_LABEL_TIMER_TIME = 3000;

        private System.Windows.Forms.Timer exitFullscreenLabelTimer = null;

        private bool ExitFullscreenLabelTimer {
            get {
                return exitFullscreenLabelTimer != null;
            }

            set {
                if (exitFullscreenLabelTimer == null) {
                    if (!value) {
                        // timer not started, already hidden
                        return;
                    }
                } else {
                    exitFullscreenLabelTimer.Stop();
                    exitFullscreenLabelTimer.Tick -= exitFullscreenLabelTimer_Tick;
                    exitFullscreenLabelTimer.Dispose();
                    exitFullscreenLabelTimer = null;
                }

                exitFullscreenLabel.Visible = value;

                if (exitFullscreenLabel.Visible) {
                    exitFullscreenLabelTimer = new System.Windows.Forms.Timer {
                        Interval = EXIT_FULLSCREEN_LABEL_TIMER_TIME
                    };

                    exitFullscreenLabelTimer.Tick += exitFullscreenLabelTimer_Tick;
                    exitFullscreenLabelTimer.Start();
                }
            }
        }

        private bool fullscreen = false;
        private FormBorderStyle fullscreenFormBorderStyle = FormBorderStyle.Sizable;
        private WINDOWPLACEMENT fullscreenWindowPlacement;

        private Rectangle closableWebBrowserBounds;

        // be very careful modifying this property
        // it is very picky about the order things happen
        // and likes to cause bugs if you're not careful changing it
        private bool Fullscreen {
            get {
                return fullscreen;
            }

            set {
                if (!IsHandleCreated || Handle == IntPtr.Zero) {
                    return;
                }

                if (closableWebBrowser == null) {
                    return;
                }

                if (fullscreen == value) {
                    return;
                }

                fullscreen = value;

                if (fullscreen) {
                    // get the original properties before modifying them
                    fullscreenFormBorderStyle = FormBorderStyle;
                    fullscreenWindowPlacement = WindowPlacement;

                    closableWebBrowserBounds = closableWebBrowser.Bounds;

                    WINDOWPLACEMENT windowPlacement = fullscreenWindowPlacement;

                    // need to do this first to have an effect if starting maximized
                    // we use WindowPlacement instead of WindowState to maintain
                    // the window position and skip the animation
                    windowPlacement.showCmd = SW.SW_NORMAL;
                    WindowPlacement = windowPlacement;

                    // disable resizing
                    FormBorderStyle = FormBorderStyle.None;

                    // enter fullscreen
                    windowPlacement.showCmd = SW.SW_MAXIMIZE;
                    WindowPlacement = windowPlacement;

                    // make strips invisible so the Closable Web Browser can be Docked
                    // (this must happen AFTER entering fullscreen to prevent toolbar mouseover bug)
                    fullscreenButton.Checked = true;
                    toolBarToolStrip.Visible = false;
                    statusBarStatusStrip.Visible = false;
                    closableWebBrowser.Dock = DockStyle.Fill;

                    ShowToolBar();

                    // set Windows Hook for toolbar
                    if (mouseHook == IntPtr.Zero && lowLevelMouseProc != null) {
                        mouseHook = SetWindowsHookEx(HookType.WH_MOUSE_LL, lowLevelMouseProc, IntPtr.Zero, 0);
                    }

                    // show "Press F11 to exit fullscreen" label
                    ExitFullscreenLabelTimer = true;

                    // commit by bringing the window to the front
                    BringToFront();
                } else {
                    // hide "Press F11 to exit fullscreen" label
                    ExitFullscreenLabelTimer = false;

                    // unhook Windows Hook for toolbar
                    if (mouseHook != IntPtr.Zero) {
                        if (UnhookWindowsHookEx(mouseHook)) {
                            mouseHook = IntPtr.Zero;
                        }
                    }

                    ShowToolBar();

                    WINDOWPLACEMENT windowPlacement = fullscreenWindowPlacement;

                    // need to do this first to reset the window to its former size
                    FormBorderStyle = FormBorderStyle.Sizable;

                    // exit fullscreen
                    windowPlacement.showCmd = SW.SW_NORMAL;
                    WindowPlacement = windowPlacement;

                    // make strips visible so the Closable Web Browser can be Anchored
                    fullscreenButton.Checked = false;
                    toolBarToolStrip.Visible = true;
                    statusBarStatusStrip.Visible = true;
                    closableWebBrowser.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                    // reset to the original properties before modifying them
                    FormBorderStyle = fullscreenFormBorderStyle;
                    WindowPlacement = fullscreenWindowPlacement;

                    closableWebBrowser.Bounds = closableWebBrowserBounds;

                    // commit by bringing the window to the front
                    BringToFront();
                }
            }
        }
        
        private class MessageFilter : IMessageFilter {
            private readonly EventHandler stopExitFullscreenLabelTimer;
            private readonly EventHandler back;
            private readonly EventHandler forward;

            public MessageFilter(EventHandler stopExitFullscreenLabelTimer, EventHandler back, EventHandler forward) {
                this.stopExitFullscreenLabelTimer = stopExitFullscreenLabelTimer;
                this.back = back;
                this.forward = forward;
            }

            private void OnStopExitFullscreenLabelTimer(EventArgs e) {
                EventHandler eventHandler = stopExitFullscreenLabelTimer;

                if (eventHandler == null) {
                    return;
                }

                eventHandler(this, e);
            }

            private void OnBack(EventArgs e) {
                EventHandler eventHandler = back;

                if (eventHandler == null) {
                    return;
                }

                eventHandler(this, e);
            }

            private void OnForward(EventArgs e) {
                EventHandler eventHandler = forward;

                if (eventHandler == null) {
                    return;
                }

                eventHandler(this, e);
            }
            
            public bool PreFilterMessage(ref Message m) {
                // this happens in PreFilterMessage because
                // the mouse back/forward buttons should only
                // have an effect if our window is active
                // for example, if there is a popup, the
                // mouse buttons shouldn't navigate both the
                // main and popup windows
                switch (m.Msg) {
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                    case WM_LBUTTONDOWN:
                    case WM_RBUTTONDOWN:
                    case WM_MBUTTONDOWN:
                    case WM_XBUTTONDOWN:
                    // these are intended to always return false
                    OnStopExitFullscreenLabelTimer(EventArgs.Empty);
                    return false;
                    case WM_XBUTTONUP:
                    int wParam = m.WParam.ToInt32();

                    if ((wParam & XBUTTON1) == XBUTTON1) {
                        OnBack(EventArgs.Empty);
                        return true;
                    }

                    if ((wParam & XBUTTON2) == XBUTTON2) {
                        OnForward(EventArgs.Empty);
                        return true;
                    }
                    return false;
                }
                return false;
            }
        }

        private readonly MessageFilter messageFilter;

        private class TitleChangedEventArgs : EventArgs {
            public string Text { get; set; } = null;

            public TitleChangedEventArgs(string text) {
                Text = text;
            }
        }

        private class WebBrowserModeTitle {
            private readonly EventHandler<TitleChangedEventArgs> titleChanged;

            private readonly string applicationTitle = Properties.Resources.FlashpointSecurePlayer + " " + typeof(WebBrowserModeTitle).Assembly.GetName().Version;
            private string documentTitle = null;
            private int progress = -1;

            public WebBrowserModeTitle(EventHandler<TitleChangedEventArgs> titleChanged) {
                this.titleChanged = titleChanged;

                Show();
            }

            private void OnTitleChanged(TitleChangedEventArgs e) {
                EventHandler<TitleChangedEventArgs> eventHandler = titleChanged;

                if (eventHandler == null) {
                    return;
                }

                eventHandler(this, e);
            }

            private void Show() {
                StringBuilder text = new StringBuilder();

                if (!String.IsNullOrWhiteSpace(documentTitle)) {
                    text.Append(documentTitle);
                    text.Append(" - ");
                }

                text.Append(applicationTitle);

                if (progress != -1) {
                    text.Append(" [");
                    text.Append(progress);
                    text.Append("%]");
                }

                OnTitleChanged(new TitleChangedEventArgs(text.ToString()));
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

        private readonly WebBrowserModeTitle webBrowserModeTitle;

        private class EndEllipsisTextRenderer : ToolStripProfessionalRenderer {
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
                if (e.Item is ToolStripStatusLabel) {
                    TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, e.TextColor, e.TextFormat | TextFormatFlags.EndEllipsis);
                    return;
                }

                base.OnRenderItemText(e);
            }
        }

        static private List<WebBrowserMode> webBrowserModes = new List<WebBrowserMode>() { };

        private readonly EventHandler webBrowserModeExit;

        private Uri WebBrowserURL { get; set; } = null;
        private bool UseFlashActiveXControl { get; set; } = false;

        public WebBrowserMode(EventHandler webBrowserModeExit, bool useFlashActiveXControl = false) {
            InitializeComponent();

            this.webBrowserModeExit = webBrowserModeExit;

            if (webBrowserModes != null) {
                webBrowserModes.Add(this);
            }

            UseFlashActiveXControl = useFlashActiveXControl;

            lowLevelMouseProc = new HookProc(LowLevelMouseProc);
            messageFilter = new MessageFilter(StopExitFullscreenLabelTimer, Back, Forward);
            webBrowserModeTitle = new WebBrowserModeTitle(TitleChanged);

            statusBarStatusStrip.Renderer = new EndEllipsisTextRenderer();
        }

        public WebBrowserMode(EventHandler webBrowserModeExit, Uri webBrowserURL, bool useFlashActiveXControl = false) {
            InitializeComponent();

            this.webBrowserModeExit = webBrowserModeExit;

            if (webBrowserModes != null) {
                webBrowserModes.Add(this);
            }

            WebBrowserURL = webBrowserURL;
            UseFlashActiveXControl = useFlashActiveXControl;

            lowLevelMouseProc = new HookProc(LowLevelMouseProc);
            messageFilter = new MessageFilter(StopExitFullscreenLabelTimer, Back, Forward);
            webBrowserModeTitle = new WebBrowserModeTitle(TitleChanged);

            statusBarStatusStrip.Renderer = new EndEllipsisTextRenderer();
        }

        private void OnWebBrowserModeExit(EventArgs e) {
            EventHandler eventHandler = webBrowserModeExit;

            if (eventHandler == null) {
                return;
            }

            eventHandler(this, e);
        }

        static private bool exiting = false;

        static public void DeactivateMode() {
            // if we are here, we don't want the OnWebBrowserModeExit event to fire
            // that would cause recursion, because the mode would be deactivated again
            // instead, we handle the exit ourselves
            exiting = true;

            if (webBrowserModes != null) {
                for (int i = 0; i < webBrowserModes.Count; i++) {
                    webBrowserModes[i].Close();
                }
            }
        }

        private bool isActive = false;

        private void ShowToolBar() {
            // this is checked in LowLevelMouseProc because
            // otherwise plugins such as Viscape which
            // create their own window can steal the
            // mouse move event
            // it cannot happen in PreFilterMessage!
            // our window may not even get these messages
            // all that matters is the mouse position, regardless
            // of if our window is active
            Point toolBarToolStripMousePosition = toolBarToolStrip.PointToClient(MousePosition);

            if (toolBarToolStripMousePosition.Y != 0 && !toolBarToolStrip.Visible && Fullscreen) {
                return;
            }

            if (toolBarToolStrip.ClientRectangle.Contains(toolBarToolStripMousePosition)) {
                // mouse in toolbar rectangle
                // if in fullscreen, show toolbar if we can
                // if not in fullscreen, ensure toolbar is visible
                toolBarToolStrip.Visible = isActive || CanFocus || !Fullscreen;
                return;
            }
            
            // the standard layout when the mouse is not in the toolbar rectangle
            // if in fullscreen, ensure toolbar is invisible
            // if not in fullscreen, ensure toolbar is visible
            toolBarToolStrip.Visible = !Fullscreen;
        }

        private void ShowAddress(Uri url) {
            if (url == null
                || url.Equals("about:blank")) {
                addressToolStripSpringTextBox.Text = String.Empty;
                return;
            }

            addressToolStripSpringTextBox.Text = url.ToString();
        }

        private bool addressToolStripSpringTextBoxEntered = false;

        public void AddressInvalid() {
            // focus the address again, if the user clicked the go button
            SystemSounds.Beep.Play();

            addressToolStripSpringTextBox.Focus();
            addressToolStripSpringTextBox.SelectionStart = addressToolStripSpringTextBox.TextLength;
            addressToolStripSpringTextBoxEntered = false;
        }

        public void AddressValid() {
            // unfocus the address before navigation
            ActiveControl = null;
        }

        public void BrowserBack() {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.GoBack();
        }

        public void BrowserForward() {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.GoForward();
        }

        public void BrowserStop() {
            if (closableWebBrowser == null) {
                return;
            }

            ShowAddress(closableWebBrowser.Url);
            closableWebBrowser.Stop();
        }

        public void BrowserRefresh() {
            if (closableWebBrowser == null) {
                return;
            }

            ShowAddress(closableWebBrowser.Url);
            closableWebBrowser.Refresh();
        }

        public void BrowserGo(string url) {
            if (closableWebBrowser == null) {
                return;
            }

            Uri webBrowserURL;

            try {
                webBrowserURL = new Uri(GetValidatedURL(url), UriKind.Absolute);
            } catch {
                AddressInvalid();
                return;
            }

            AddressValid();
            closableWebBrowser.Navigate(webBrowserURL);
        }

        public void BrowserSaveAsWebpage() {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.ShowSaveAsDialog();
        }

        public void BrowserPrint() {
            if (closableWebBrowser == null) {
                return;
            }

            closableWebBrowser.ShowPrintDialog();
        }

        public WebBrowserMode BrowserNewWindow() {
            // we don't want this window to be the parent, breaks fullscreen and not otherwise useful
            WebBrowserMode webBrowserForm = new WebBrowserMode(webBrowserModeExit, UseFlashActiveXControl);
            webBrowserForm.Show(/*this*/);
            return webBrowserForm;
        }

        public void BrowserFullscreen() {
            Fullscreen = !Fullscreen;
        }

        private void StopExitFullscreenLabelTimer(object sender, EventArgs e) {
            ExitFullscreenLabelTimer = false;
        }

        private void Back(object sender, EventArgs e) {
            BrowserBack();
        }

        private void Forward(object sender, EventArgs e) {
            BrowserForward();
        }

        private void TitleChanged(object sender, TitleChangedEventArgs e) {
            Text = e.Text;
        }

        private CustomSecurityManager customSecurityManager = null;

        private void WebBrowserMode_Load(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                closableWebBrowser = new ClosableWebBrowser();
            }

            try {
                FlashpointProxy.Enable();
            } catch (FlashpointProxyException ex) {
                // popup message box but allow through anyway
                LogExceptionToLauncher(ex);
                MessageBox.Show(Properties.Resources.FlashpointProxyNotEnabled, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try {
                customSecurityManager = new CustomSecurityManager(closableWebBrowser, UseFlashActiveXControl);
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                MessageBox.Show(Properties.Resources.UnableToCreateCustomSecurityManager, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            // events are created/destroyed here
            // to avoid bugs with window.open/window.close
            closableWebBrowser.CanGoBackChanged += closableWebBrowser_CanGoBackChanged;
            closableWebBrowser.CanGoForwardChanged += closableWebBrowser_CanGoForwardChanged;
            closableWebBrowser.DocumentTitleChanged += closableWebBrowser_DocumentTitleChanged;
            closableWebBrowser.StatusTextChanged += closableWebBrowser_StatusTextChanged;

            closableWebBrowser.WebBrowserClose += closableWebBrowser_WebBrowserClose;
            closableWebBrowser.WebBrowserPaint += closableWebBrowser_WebBrowserPaint;

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

            // now that we've created events, load the URL
            if (WebBrowserURL != null) {
                closableWebBrowser.Url = WebBrowserURL;
            }

            BringToFront();
            Activate();
        }

        private void WebBrowserMode_FormClosing(object sender, FormClosingEventArgs e) {
            // disposing the browser can actually take a while
            // to keep things snappy, we hide the window here
            Hide();

            // important that this is done first so that
            // we don't access the disposed toolbar
            if (mouseHook != IntPtr.Zero) {
                if (UnhookWindowsHookEx(mouseHook)) {
                    mouseHook = IntPtr.Zero;
                }
            }

            if (closableWebBrowser == null) {
                return;
            }

            // the WebBrowserClose event must be disabled here, otherwise we
            // end up closing the current form when it's already closed
            // (browser reports being closed > we close the form and so on)
            closableWebBrowser.CanGoBackChanged -= closableWebBrowser_CanGoBackChanged;
            closableWebBrowser.CanGoForwardChanged -= closableWebBrowser_CanGoForwardChanged;
            closableWebBrowser.DocumentTitleChanged -= closableWebBrowser_DocumentTitleChanged;
            closableWebBrowser.StatusTextChanged -= closableWebBrowser_StatusTextChanged;

            closableWebBrowser.WebBrowserClose -= closableWebBrowser_WebBrowserClose;
            closableWebBrowser.WebBrowserPaint -= closableWebBrowser_WebBrowserPaint;

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
            closableWebBrowser = null;

            customSecurityManager = null;

            if (webBrowserModes == null) {
                return;
            }

            webBrowserModes.Remove(this);

            if (webBrowserModes.Any()) {
                return;
            }

            // code beyond this point is single shot
            if (exiting) {
                return;
            }

            exiting = true;

            // stop form closing recursion
            FormClosing -= WebBrowserMode_FormClosing;

            OnWebBrowserModeExit(EventArgs.Empty);
        }

        // there used to be some lengthy (buggy) code here for some custom fullscreen behaviour
        // I was spending too much time trying to fix it for a non-essential feature of the program
        // so I've gotten rid of it for the default behaviour instead
        private void WebBrowserMode_Activated(object sender, EventArgs e) {
            isActive = true;

            if (messageFilter != null) {
                Application.AddMessageFilter(messageFilter);
            }
            
            ShowToolBar();
        }

        private void WebBrowserMode_Deactivate(object sender, EventArgs e) {
            isActive = false;

            if (messageFilter != null) {
                Application.RemoveMessageFilter(messageFilter);
            }

            ShowToolBar();
        }

        private void closableWebBrowser_Navigated(object sender, WebBrowserNavigatedEventArgs e) {
            ShowAddress(e.Url);
        }

        private readonly object downloadCompletedLock = new object();
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

        private void closableWebBrowser_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs e) {
            if (e.CurrentProgress < 0) {
                DownloadCompleted = true;
            }

            if (DownloadCompleted) {
                return;
            }

            int progress = e.MaximumProgress > 0 ? (int)Math.Min((double)e.CurrentProgress / e.MaximumProgress * 100, 100) : 0;

            if (webBrowserModeTitle != null) {
                webBrowserModeTitle.Progress = progress;
            }
            
            progressToolStripProgressBar.Style = progress == 0 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
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

            if (webBrowserModeTitle != null) {
                webBrowserModeTitle.DocumentTitle = closableWebBrowser.DocumentTitle;
            }
        }

        private void closableWebBrowser_StatusTextChanged(object sender, EventArgs e) {
            if (closableWebBrowser == null) {
                return;
            }
            
            statusToolStripStatusLabel.Text = closableWebBrowser.StatusText;
        }

        private void closableWebBrowser_WebBrowserClose(object sender, EventArgs e) {
            Close();
        }

        private void closableWebBrowser_WebBrowserPaint(object sender, EventArgs e) {
            // lame fix: browser hangs when window.open top attribute > control height (why?)
            // Width, Height, and WindowState changes all work here
            // Width/Height are less obvious and Height doesn't cause text reflow
            if (WindowPlacement.showCmd != SW.SW_SHOWMAXIMIZED) {
                // add first in case it's zero
                Height++;
                Height--;
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

        private void ShDocVwWebBrowser_NewWindow2(ref object ppDisp, ref bool Cancel) {
            ppDisp = BrowserNewWindow().PPDisp;
            Cancel = false;
        }

        private void ShDocVwWebBrowser_NewWindow3(ref object ppDisp, ref bool Cancel, uint dwFlags, string bstrUrlContext, string bstrUrl) {
            ShDocVwWebBrowser_NewWindow2(ref ppDisp, ref Cancel);
        }

        // although we don't need closableWebBrowser for the WindowSetTop/WindowSetLeft functions
        // it'd be weird if they worked but the WindowSetWidth/WindowSetHeight functions did not
        // so we check if it's null anyway
        private void ShDocVwWebBrowser_WindowSetTop(int top) {
            if (closableWebBrowser == null) {
                return;
            }

            Fullscreen = false;
            Top = top;
        }

        private void ShDocVwWebBrowser_WindowSetLeft(int left) {
            if (closableWebBrowser == null) {
                return;
            }

            Fullscreen = false;
            Left = left;
        }

        private void ShDocVwWebBrowser_WindowSetWidth(int width) {
            if (closableWebBrowser == null) {
                return;
            }

            Fullscreen = false;
            Width += width - closableWebBrowser.Width;
        }

        private void ShDocVwWebBrowser_WindowSetHeight(int height) {
            if (closableWebBrowser == null) {
                return;
            }

            Fullscreen = false;
            Height += height - closableWebBrowser.Height;
        }

        private void ShDocVwWebBrowser_WindowSetResizable(bool resizable) {
            if (closableWebBrowser == null) {
                return;
            }

            Fullscreen = false;

            if (resizable) {
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                statusBarStatusStrip.SizingGrip = true;
            } else {
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                statusBarStatusStrip.SizingGrip = false;
            }
        }

        private void ShDocVwWebBrowser_DownloadBegin() {
            /*
            if (closableWebBrowser == null) {
                return;
            }

            Control closableWebBrowserControl = closableWebBrowser as Control;

            if (closableWebBrowserControl == null) {
                return;
            }
            */

            DownloadCompleted = false;

            if (webBrowserModeTitle != null) {
                webBrowserModeTitle.Progress = 0;
            }

            progressToolStripProgressBar.Style = ProgressBarStyle.Marquee;
            progressToolStripProgressBar.Value = 0;
            progressToolStripProgressBar.ToolTipText = "0%";
            //UseWaitCursor = true;
            //closableWebBrowserControl.Enabled = false;
        }

        private void ShDocVwWebBrowser_DownloadComplete() {
            /*
            if (closableWebBrowser == null) {
                return;
            }

            Control closableWebBrowserControl = closableWebBrowser as Control;

            if (closableWebBrowserControl == null) {
                return;
            }
            */

            DownloadCompleted = true;

            if (webBrowserModeTitle != null) {
                webBrowserModeTitle.Progress = -1;
            }

            progressToolStripProgressBar.Style = ProgressBarStyle.Blocks;
            progressToolStripProgressBar.Value = 0;
            progressToolStripProgressBar.ToolTipText = String.Empty;
            //closableWebBrowserControl.Enabled = true;
            //UseWaitCursor = false;
        }

        private void backButton_Click(object sender, EventArgs e) {
            BrowserBack();
        }

        private void forwardButton_Click(object sender, EventArgs e) {
            BrowserForward();
        }

        private void stopButton_Click(object sender, EventArgs e) {
            BrowserStop();
        }

        private void refreshButton_Click(object sender, EventArgs e) {
            BrowserRefresh();
        }

        private void addressToolStripSpringTextBox_Click(object sender, EventArgs e) {
            if (!addressToolStripSpringTextBoxEntered) {
                return;
            }

            addressToolStripSpringTextBoxEntered = false;

            if (String.IsNullOrEmpty(addressToolStripSpringTextBox.SelectedText)) {
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
                e.SuppressKeyPress = true;
                BrowserGo(addressToolStripSpringTextBox.Text);
            }
        }

        private void goButton_Click(object sender, EventArgs e) {
            BrowserGo(addressToolStripSpringTextBox.Text);
        }

        private void saveAsWebpageButton_Click(object sender, EventArgs e) {
            BrowserSaveAsWebpage();
        }

        private void printButton_Click(object sender, EventArgs e) {
            BrowserPrint();
        }

        private void newWindowButton_Click(object sender, EventArgs e) {
            BrowserNewWindow();
        }

        private void fullscreenButton_Click(object sender, EventArgs e) {
            BrowserFullscreen();
        }

        private void exitFullscreenLabelTimer_Tick(object sender, EventArgs e) {
            ExitFullscreenLabelTimer = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            // don't disable keys on e.g. the address bar
            if (ActiveControl != null
                && ActiveControl == closableWebBrowser) {
                // IMPORTANT: these controls (such as Backspace to navigate back)
                // must be handled here in ProcessCmdKey, not on PreviewKeyDown!
                // otherwise, controls on the page won't recieve the input
                // for example, while editing a textbox, Backspace should
                // erase characters, not navigate back
                // ProcessCmdKey handles this properly, PreviewKeyDown would circumvent this
                switch (keyData) {
                    case Keys.Back:
                    case Keys.Alt | Keys.Left:
                    case Keys.BrowserBack:
                    BrowserBack();
                    return true;
                    case Keys.Alt | Keys.Right:
                    case Keys.BrowserForward:
                    BrowserForward();
                    return true;
                    case Keys.Escape:
                    case Keys.BrowserStop:
                    BrowserStop();
                    return true;
                    case Keys.F5:
                    case Keys.Control | Keys.R:
                    case Keys.BrowserRefresh:
                    BrowserRefresh();
                    return true;
                    case Keys.Control | Keys.S:
                    BrowserSaveAsWebpage();
                    return true;
                    case Keys.Control | Keys.P:
                    BrowserPrint();
                    return true;
                    case Keys.Control | Keys.N:
                    BrowserNewWindow();
                    return true;
                    case Keys.F11:
                    case Keys.Alt | Keys.Enter:
                    BrowserFullscreen();
                    return true;
                }
            }
            // don't forget to call the base!
            // (better fix for Atmosphere plugin)
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}