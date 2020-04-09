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

namespace FlashpointSecurePlayer {
    public partial class Server : Form {
        private CustomSecurityManager CustomSecurityManager;
        private Uri WebBrowserURL = null;

        public Server(Uri WebBrowserURL) {
            InitializeComponent();
            this.WebBrowserURL = WebBrowserURL;
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
                MessageBox.Show(Properties.Resources.ConnectionNotEstablished, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try {
                CustomSecurityManager = new CustomSecurityManager(webBrowser1);
            } catch (Win32Exception) {
                MessageBox.Show(Properties.Resources.FailedCreateCustomSecurityManager, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            BringToFront();
            Activate();
        }

        private void Server_Shown(object sender, EventArgs e) {
            if (WebBrowserURL != null) {
                webBrowser1.Url = WebBrowserURL;
            }
        }

        private void Server_FormClosing(object sender, FormClosingEventArgs e) {
            Application.Exit();
        }
    }
}