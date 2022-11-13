using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    // http://blogs.msdn.com/b/jpsanders/archive/2007/05/25/how-to-close-the-form-hosting-the-webbrowser-control-when-scripting-calls-window-close-in-the-net-framework-version-2-0.aspx
    public partial class ClosableWebBrowser : WebBrowser {
        public event EventHandler WebBrowserClose;
        public event EventHandler WebBrowserPaint;

        public ClosableWebBrowser() {
            InitializeComponent();
        }

        protected virtual void OnWebBrowserClose(EventArgs e) {
            EventHandler eventHandler = WebBrowserClose;

            if (eventHandler == null) {
                return;
            }

            eventHandler(this, e);
        }

        protected virtual void OnWebBrowserPaint(EventArgs e) {
            EventHandler eventHandler = WebBrowserPaint;

            if (eventHandler == null) {
                return;
            }

            eventHandler(this, e);
        }

        protected override void WndProc(ref Message m) {
            // always confirm the message first so we don't do unnecessary work
            switch (m.Msg) {
                case WM_PARENTNOTIFY:
                if (m.WParam.ToInt32() == WM_DESTROY) {
                    if (!DesignMode) {
                        // close the form if the browser control closes
                        // (for example, if window.close is called)
                        // needs to be done here because the event
                        // intended for this does not actually fire
                        OnWebBrowserClose(EventArgs.Empty);
                    }
                }

                DefWndProc(ref m);
                return;
                case WM_PAINT:
                if (!DesignMode) {
                    OnWebBrowserPaint(EventArgs.Empty);
                }
                break;
            }

            base.WndProc(ref m);
        }
    }
}
