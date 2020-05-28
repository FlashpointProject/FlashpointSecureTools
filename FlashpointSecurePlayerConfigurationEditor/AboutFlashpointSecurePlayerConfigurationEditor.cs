using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlashpointSecurePlayerConfigurationEditor {
    public partial class AboutFlashpointSecurePlayerConfigurationEditor : Form {
        public AboutFlashpointSecurePlayerConfigurationEditor() {
            InitializeComponent();
        }

        private void websiteLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            Process.Start("http://bluemaxima.org/flashpoint");
        }

        private void button1_Click(object sender, EventArgs e) {
            Close();
        }

        private void AboutFlashpointSecurePlayerConfigurationEditor_Load(object sender, EventArgs e) {
            titleLabel.Text += " " + typeof(AboutFlashpointSecurePlayerConfigurationEditor).Assembly.GetName().Version;
        }
    }
}
