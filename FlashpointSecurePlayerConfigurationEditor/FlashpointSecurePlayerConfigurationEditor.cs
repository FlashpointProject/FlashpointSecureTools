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
    public partial class FlashpointSecurePlayerConfigurationEditor : Form {
        // TODO: https://www.codeproject.com/Articles/18025/Generic-Memento-Pattern-for-Undo-Redo-in-C
        public FlashpointSecurePlayerConfigurationEditor() {
            InitializeComponent();
        }

        private bool GetDataGridViewValid(DataGridView dataGridView) {
            bool valid = true;
            
            foreach (DataGridViewRow dataGridViewRow in dataGridView.Rows) {
                foreach (DataGridViewCell dataGridViewCell in dataGridViewRow.Cells) {
                    if (!String.IsNullOrEmpty(dataGridViewCell.ErrorText)) {
                        valid = false;
                        break;
                    }
                }

                if (!valid) {
                    break;
                }
            }
            return valid;
        }

        private void ShowTabPageWithControl(TabControl tabControl, Control control) {
            foreach (TabPage tabPage in tabControl.TabPages) {
                foreach (Control tabPageControl in tabPage.Controls) {
                    if (control == tabPageControl) {
                        tabControl.SelectedTab = tabPage;
                        return;
                    }
                }
            }
        }

        private void ShowCompatibilitySettingsEditor() {
            CompatibilitySettingsEditor compatibilitySettingsEditor = new CompatibilitySettingsEditor();
            compatibilitySettingsEditor.ShowDialog(this);
        }

        private void ShowModificationNameEditor() {
            ModificationNameEditor modificationNameEditor = new ModificationNameEditor();
            modificationNameEditor.ShowDialog(this);
        }

        private void ShowAboutFlashpointSecurePlayerConfigurationEditor() {
            AboutFlashpointSecurePlayerConfigurationEditor aboutFlashpointSecurePlayerConfigurationEditor = new AboutFlashpointSecurePlayerConfigurationEditor();
            aboutFlashpointSecurePlayerConfigurationEditor.ShowDialog(this);
        }

        private void EnableTextEditing(object sender, EventArgs e) {
            cutToolStripMenuItem.Enabled = true;
            copyToolStripMenuItem.Enabled = true;
            pasteToolStripMenuItem.Enabled = true;
            deleteToolStripMenuItem.Enabled = true;
            selectAllToolStripMenuItem.Enabled = true;
        }

        private void DisableTextEditing(object sender, EventArgs e) {
            cutToolStripMenuItem.Enabled = false;
            copyToolStripMenuItem.Enabled = false;
            pasteToolStripMenuItem.Enabled = false;
            deleteToolStripMenuItem.Enabled = false;
            selectAllToolStripMenuItem.Enabled = false;
        }

        private void FlashpointSecurePlayerConfigurationEditor_Load(object sender, EventArgs e) {
            targetMhzComboBox.SelectedIndex = 0;

            commandLineTextBox.GotFocus += EnableTextEditing;
            softwareModeTemplateFormatTextBox.GotFocus += EnableTextEditing;
            softwareModeTemplateWorkingDirectoryTextBox.GotFocus += EnableTextEditing;

            commandLineTextBox.LostFocus += DisableTextEditing;
            softwareModeTemplateFormatTextBox.LostFocus += DisableTextEditing;
            softwareModeTemplateWorkingDirectoryTextBox.LostFocus += DisableTextEditing;
        }

        private void FlashpointSecurePlayerConfigurationEditor_FormClosing(object sender, FormClosingEventArgs e) {
            e.Cancel = false;
            DialogResult saveDialogResult = MessageBox.Show(String.Format(Properties.Resources.SaveChangesToConfiguration, "ModificationName"), Properties.Resources.FlashpointSecurePlayerConfigurationEditor, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            switch (saveDialogResult) {
                case DialogResult.Yes:
                // save
                break;
                case DialogResult.Cancel:
                e.Cancel = true;
                break;
            }
        }

        // MENU STRIP
        // FILE
        private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        // EDIT
        private void cutToolStripMenuItem_Click(object sender, EventArgs e) {
            if (ActiveControl is TextBox activeTextBox && !String.IsNullOrEmpty(activeTextBox.SelectedText)) {
                Clipboard.SetText(activeTextBox.SelectedText);
                int selectionStart = activeTextBox.SelectionStart;
                activeTextBox.Text = activeTextBox.Text.Remove(selectionStart, activeTextBox.SelectionLength);
                activeTextBox.SelectionStart = selectionStart;
                activeTextBox.SelectionLength = 0;
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e) {
            if (ActiveControl is TextBox activeTextBox && !String.IsNullOrEmpty(activeTextBox.SelectedText)) {
                Clipboard.SetText(activeTextBox.SelectedText);
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) {
            if (ActiveControl is TextBox activeTextBox) {
                int selectionStart = activeTextBox.SelectionStart;
                string clipboardText = Clipboard.GetText();
                activeTextBox.Text = activeTextBox.Text.Substring(0, selectionStart) + clipboardText + activeTextBox.Text.Substring(selectionStart + activeTextBox.SelectionLength);
                activeTextBox.SelectionStart = selectionStart + clipboardText.Length;
                activeTextBox.SelectionLength = 0;
            }
        }

        private void modificationNameToolStripMenuItem_Click(object sender, EventArgs e) {
            ShowModificationNameEditor();
        }

        private void compatibilitySettingsToolStripMenuItem_Click(object sender, EventArgs e) {
            ShowCompatibilitySettingsEditor();
        }

        // VIEW
        private void showTooltipsToolStripMenuItem_Click(object sender, EventArgs e) {
            if (showTooltipsToolStripMenuItem.CheckState == CheckState.Checked) {
                showTooltipsToolStripMenuItem.CheckState = CheckState.Unchecked;
            } else {
                showTooltipsToolStripMenuItem.CheckState = CheckState.Checked;
            }
        }

        private void showTooltipsToolStripMenuItem_CheckStateChanged(object sender, EventArgs e) {
            // TODO
        }

        // HELP
        private void onlineHelpToolStripMenuItem_Click(object sender, EventArgs e) {
            Process.Start("https://github.com/FlashpointProject/FlashpointSecureTools/blob/master/FlashpointSecurePlayer/README.md");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
            ShowAboutFlashpointSecurePlayerConfigurationEditor();
        }

        // ENVIRONMENT VARIABLES
        private void editCompatibilitySettingsButton_Click(object sender, EventArgs e) {
            ShowCompatibilitySettingsEditor();
        }

        // REGISTRY BACKUPS
        private void registryBackupsDataGridView_CurrentCellDirtyStateChanged(object sender, EventArgs e) {
            // causes CellValueChanged to be run unrecursively when Type is edited
            if (registryBackupsDataGridView.IsCurrentCellDirty) {
                registryBackupsDataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void registryBackupsDataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex < 0) {
                return;
            }

            // don't allow Value Name to be set if Type is Key
            DataGridViewComboBoxCell dataGridViewComboBoxCell = registryBackupsDataGridView.Rows[e.RowIndex].Cells[0] as DataGridViewComboBoxCell;

            if (dataGridViewComboBoxCell != null) {
                for(int i = 2;i < 5;i++) {
                    // TODO: Tooltips
                    if (dataGridViewComboBoxCell.Value.Equals("KEY")) {
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].ReadOnly = true;
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].Value = String.Empty;
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].Style.BackColor = Color.LightGray;
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].Style.ForeColor = Color.DarkGray;
                    } else {
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].ReadOnly = false;
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].Style.BackColor = registryBackupsDataGridView.Rows[e.RowIndex].Cells[2].OwningColumn.DefaultCellStyle.BackColor;
                        registryBackupsDataGridView.Rows[e.RowIndex].Cells[i].Style.ForeColor = registryBackupsDataGridView.Rows[e.RowIndex].Cells[2].OwningColumn.DefaultCellStyle.ForeColor;
                    }
                }

                // invalidate the state of the grid view so it redraws now
                registryBackupsDataGridView.Invalidate();
            }
        }

        private void registryBackupsDataGridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e) {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) {
                return;
            }

            string headerText = registryBackupsDataGridView.Columns[e.ColumnIndex].HeaderText;

            // type is a required attribute
            if (headerText.Equals("Type")) {
                if (String.IsNullOrEmpty(e.FormattedValue.ToString())) {
                    registryBackupsDataGridView.Rows[e.RowIndex].ErrorText = "Type must not be empty";
                    e.Cancel = true;
                }
            }

            // value kind is a required attribute if type is not key
            if (headerText.Equals("Value Kind") && !registryBackupsDataGridView.Rows[e.RowIndex].Cells[0].Value.Equals("KEY")) {
                if (String.IsNullOrEmpty(e.FormattedValue.ToString())) {
                    registryBackupsDataGridView.Rows[e.RowIndex].ErrorText = "Value Kind must not be empty";
                    e.Cancel = true;
                }
            }
        }

        private void registryBackupsDataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex < 0) {
                return;
            }

            registryBackupsDataGridView.Rows[e.RowIndex].ErrorText = String.Empty;
        }
    }
}
