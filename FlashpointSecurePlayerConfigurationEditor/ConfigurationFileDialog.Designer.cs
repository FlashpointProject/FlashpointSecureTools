namespace FlashpointSecurePlayer {
    partial class ConfigurationFileDialog {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConfigurationFileDialog));
            this.configurationFilesListBox = new System.Windows.Forms.ListBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.flashpointPathTextBox = new System.Windows.Forms.TextBox();
            this.browseButton = new System.Windows.Forms.Button();
            this.flashpointPathLabel = new System.Windows.Forms.Label();
            this.configurationFilesLabel = new System.Windows.Forms.Label();
            this.openButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.saveButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // configurationFilesListBox
            // 
            this.configurationFilesListBox.Enabled = false;
            this.configurationFilesListBox.FormattingEnabled = true;
            this.configurationFilesListBox.Location = new System.Drawing.Point(15, 64);
            this.configurationFilesListBox.Name = "configurationFilesListBox";
            this.configurationFilesListBox.Size = new System.Drawing.Size(281, 95);
            this.configurationFilesListBox.TabIndex = 0;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // flashpointPathTextBox
            // 
            this.flashpointPathTextBox.Location = new System.Drawing.Point(15, 25);
            this.flashpointPathTextBox.Name = "flashpointPathTextBox";
            this.flashpointPathTextBox.Size = new System.Drawing.Size(200, 20);
            this.flashpointPathTextBox.TabIndex = 1;
            // 
            // browseButton
            // 
            this.browseButton.Location = new System.Drawing.Point(221, 25);
            this.browseButton.Name = "browseButton";
            this.browseButton.Size = new System.Drawing.Size(75, 20);
            this.browseButton.TabIndex = 2;
            this.browseButton.Text = "Browse...";
            this.browseButton.UseVisualStyleBackColor = true;
            // 
            // flashpointPathLabel
            // 
            this.flashpointPathLabel.AutoSize = true;
            this.flashpointPathLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.flashpointPathLabel.Location = new System.Drawing.Point(12, 9);
            this.flashpointPathLabel.Name = "flashpointPathLabel";
            this.flashpointPathLabel.Size = new System.Drawing.Size(99, 13);
            this.flashpointPathLabel.TabIndex = 3;
            this.flashpointPathLabel.Text = "Flashpoint Path:";
            // 
            // configurationFilesLabel
            // 
            this.configurationFilesLabel.AutoSize = true;
            this.configurationFilesLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.configurationFilesLabel.Location = new System.Drawing.Point(12, 48);
            this.configurationFilesLabel.Name = "configurationFilesLabel";
            this.configurationFilesLabel.Size = new System.Drawing.Size(112, 13);
            this.configurationFilesLabel.TabIndex = 4;
            this.configurationFilesLabel.Text = "Configuration Files";
            // 
            // openButton
            // 
            this.openButton.Enabled = false;
            this.openButton.Location = new System.Drawing.Point(15, 165);
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size(138, 23);
            this.openButton.TabIndex = 5;
            this.openButton.Text = "Open";
            this.openButton.UseVisualStyleBackColor = true;
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Enabled = false;
            this.cancelButton.Location = new System.Drawing.Point(159, 165);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(137, 23);
            this.cancelButton.TabIndex = 6;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // saveButton
            // 
            this.saveButton.Enabled = false;
            this.saveButton.Location = new System.Drawing.Point(15, 165);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(138, 23);
            this.saveButton.TabIndex = 7;
            this.saveButton.Text = "Save";
            this.saveButton.UseVisualStyleBackColor = true;
            // 
            // ConfigurationFileDialog
            // 
            this.AcceptButton = this.openButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(310, 196);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.openButton);
            this.Controls.Add(this.configurationFilesLabel);
            this.Controls.Add(this.flashpointPathLabel);
            this.Controls.Add(this.browseButton);
            this.Controls.Add(this.flashpointPathTextBox);
            this.Controls.Add(this.configurationFilesListBox);
            this.Controls.Add(this.saveButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ConfigurationFileDialog";
            this.Text = "Open Configuration File";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox configurationFilesListBox;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.TextBox flashpointPathTextBox;
        private System.Windows.Forms.Button browseButton;
        private System.Windows.Forms.Label flashpointPathLabel;
        private System.Windows.Forms.Label configurationFilesLabel;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button saveButton;
    }
}