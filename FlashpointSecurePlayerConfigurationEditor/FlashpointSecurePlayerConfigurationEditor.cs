using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlashpointSecurePlayerVisualEditor {
    public partial class FlashpointSecurePlayerConfigurationEditor : Form {
        public FlashpointSecurePlayerConfigurationEditor() {
            InitializeComponent();
        }

        private void EditCompatibilitySettings() {
            CompatibilitySettingsEditor compatibilitySettingsEditor = new CompatibilitySettingsEditor();
            compatibilitySettingsEditor.ShowDialog(this);
        }

        private void FlashpointSecurePlayerConfigurationEditor_Load(object sender, EventArgs e) {
            targetMhzComboBox.SelectedIndex = 0;
        }

        // MENU STRIP
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

        // ENVIRONMENT VARIABLES
        private void editCompatibilitySettingsButton_Click(object sender, EventArgs e) {
            EditCompatibilitySettings();
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
