using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlashpointSecurePlayerConfigurationEditor {
    public partial class CompatibilitySettingsEditor : Form {
        public CompatibilitySettingsEditor() {
            InitializeComponent();
        }

        private void CompatibilitySettingsEditor_Load(object sender, EventArgs e) {
            compatibilityModeComboBox.SelectedIndex = 8;
            colorComboBox.SelectedIndex = 0;
            highDpiComboBox.SelectedIndex = 0;
        }

        private void okButton_Click(object sender, EventArgs e) {
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e) {
            Close();
        }

        private void compatibilityModeCheckBox_CheckedChanged(object sender, EventArgs e) {
            compatibilityModeComboBox.Enabled = compatibilityModeCheckBox.Checked;
        }

        private void colorCheckBox_CheckedChanged(object sender, EventArgs e) {
            colorComboBox.Enabled = colorCheckBox.Checked;
        }

        private void highDpiCheckBox_CheckedChanged(object sender, EventArgs e) {
            highDpiComboBox.Enabled = highDpiCheckBox.Checked;
        }
    }
}
