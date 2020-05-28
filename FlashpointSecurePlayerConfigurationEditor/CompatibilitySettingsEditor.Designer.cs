namespace FlashpointSecurePlayerConfigurationEditor {
    partial class CompatibilitySettingsEditor {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CompatibilitySettingsEditor));
            this.compatibilityModeGroupBox = new System.Windows.Forms.GroupBox();
            this.compatibilityModeCheckBox = new System.Windows.Forms.CheckBox();
            this.compatibilityModeComboBox = new System.Windows.Forms.ComboBox();
            this.settingsGroupBox = new System.Windows.Forms.GroupBox();
            this.colorCheckBox = new System.Windows.Forms.CheckBox();
            this.colorComboBox = new System.Windows.Forms.ComboBox();
            this.sixHundredFortyXFourHundredEightyCheckBox = new System.Windows.Forms.CheckBox();
            this.disableFullscreenOptimizationsCheckBox = new System.Windows.Forms.CheckBox();
            this.runAsAdminCheckBox = new System.Windows.Forms.CheckBox();
            this.highDPIScalingOverrideGroupBox = new System.Windows.Forms.GroupBox();
            this.highDpiCheckBox = new System.Windows.Forms.CheckBox();
            this.highDpiComboBox = new System.Windows.Forms.ComboBox();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.compatibilityModeGroupBox.SuspendLayout();
            this.settingsGroupBox.SuspendLayout();
            this.highDPIScalingOverrideGroupBox.SuspendLayout();
            this.SuspendLayout();
            // 
            // compatibilityModeGroupBox
            // 
            this.compatibilityModeGroupBox.Controls.Add(this.compatibilityModeComboBox);
            this.compatibilityModeGroupBox.Controls.Add(this.compatibilityModeCheckBox);
            this.compatibilityModeGroupBox.Location = new System.Drawing.Point(13, 13);
            this.compatibilityModeGroupBox.Name = "compatibilityModeGroupBox";
            this.compatibilityModeGroupBox.Size = new System.Drawing.Size(304, 70);
            this.compatibilityModeGroupBox.TabIndex = 0;
            this.compatibilityModeGroupBox.TabStop = false;
            this.compatibilityModeGroupBox.Text = "Compatibility mode";
            // 
            // compatibilityModeCheckBox
            // 
            this.compatibilityModeCheckBox.AutoSize = true;
            this.compatibilityModeCheckBox.Location = new System.Drawing.Point(7, 20);
            this.compatibilityModeCheckBox.Name = "compatibilityModeCheckBox";
            this.compatibilityModeCheckBox.Size = new System.Drawing.Size(224, 17);
            this.compatibilityModeCheckBox.TabIndex = 0;
            this.compatibilityModeCheckBox.Text = "Run this program in compatibility mode for:";
            this.compatibilityModeCheckBox.UseVisualStyleBackColor = true;
            this.compatibilityModeCheckBox.CheckedChanged += new System.EventHandler(this.compatibilityModeCheckBox_CheckedChanged);
            // 
            // compatibilityModeComboBox
            // 
            this.compatibilityModeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.compatibilityModeComboBox.Enabled = false;
            this.compatibilityModeComboBox.FormattingEnabled = true;
            this.compatibilityModeComboBox.Items.AddRange(new object[] {
            "Windows 95",
            "Windows 98 / Windows Me",
            "Windows XP (Service Pack 2)",
            "Windows XP (Service Pack 3)",
            "Windows Vista",
            "Windows Vista (Service Pack 1)",
            "Windows Vista (Service Pack 2)",
            "Windows 7",
            "Windows 8"});
            this.compatibilityModeComboBox.Location = new System.Drawing.Point(6, 43);
            this.compatibilityModeComboBox.Name = "compatibilityModeComboBox";
            this.compatibilityModeComboBox.Size = new System.Drawing.Size(225, 21);
            this.compatibilityModeComboBox.TabIndex = 1;
            // 
            // settingsGroupBox
            // 
            this.settingsGroupBox.Controls.Add(this.runAsAdminCheckBox);
            this.settingsGroupBox.Controls.Add(this.disableFullscreenOptimizationsCheckBox);
            this.settingsGroupBox.Controls.Add(this.sixHundredFortyXFourHundredEightyCheckBox);
            this.settingsGroupBox.Controls.Add(this.colorComboBox);
            this.settingsGroupBox.Controls.Add(this.colorCheckBox);
            this.settingsGroupBox.Location = new System.Drawing.Point(13, 89);
            this.settingsGroupBox.Name = "settingsGroupBox";
            this.settingsGroupBox.Size = new System.Drawing.Size(304, 143);
            this.settingsGroupBox.TabIndex = 1;
            this.settingsGroupBox.TabStop = false;
            this.settingsGroupBox.Text = "Settings";
            // 
            // colorCheckBox
            // 
            this.colorCheckBox.AutoSize = true;
            this.colorCheckBox.Location = new System.Drawing.Point(7, 20);
            this.colorCheckBox.Name = "colorCheckBox";
            this.colorCheckBox.Size = new System.Drawing.Size(125, 17);
            this.colorCheckBox.TabIndex = 0;
            this.colorCheckBox.Text = "Reduced color mode";
            this.colorCheckBox.UseVisualStyleBackColor = true;
            this.colorCheckBox.CheckedChanged += new System.EventHandler(this.colorCheckBox_CheckedChanged);
            // 
            // colorComboBox
            // 
            this.colorComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.colorComboBox.Enabled = false;
            this.colorComboBox.FormattingEnabled = true;
            this.colorComboBox.Items.AddRange(new object[] {
            "8-bit (256) color",
            "16-bit (65536) color"});
            this.colorComboBox.Location = new System.Drawing.Point(6, 45);
            this.colorComboBox.Name = "colorComboBox";
            this.colorComboBox.Size = new System.Drawing.Size(126, 21);
            this.colorComboBox.TabIndex = 1;
            // 
            // sixHundredFortyXFourHundredEightyCheckBox
            // 
            this.sixHundredFortyXFourHundredEightyCheckBox.AutoSize = true;
            this.sixHundredFortyXFourHundredEightyCheckBox.Location = new System.Drawing.Point(7, 72);
            this.sixHundredFortyXFourHundredEightyCheckBox.Name = "sixHundredFortyXFourHundredEightyCheckBox";
            this.sixHundredFortyXFourHundredEightyCheckBox.Size = new System.Drawing.Size(190, 17);
            this.sixHundredFortyXFourHundredEightyCheckBox.TabIndex = 2;
            this.sixHundredFortyXFourHundredEightyCheckBox.Text = "Run in 640 x 480 screen resolution";
            this.sixHundredFortyXFourHundredEightyCheckBox.UseVisualStyleBackColor = true;
            // 
            // disableFullscreenOptimizationsCheckBox
            // 
            this.disableFullscreenOptimizationsCheckBox.AutoSize = true;
            this.disableFullscreenOptimizationsCheckBox.Location = new System.Drawing.Point(7, 96);
            this.disableFullscreenOptimizationsCheckBox.Name = "disableFullscreenOptimizationsCheckBox";
            this.disableFullscreenOptimizationsCheckBox.Size = new System.Drawing.Size(172, 17);
            this.disableFullscreenOptimizationsCheckBox.TabIndex = 3;
            this.disableFullscreenOptimizationsCheckBox.Text = "Disable fullscreen optimizations";
            this.disableFullscreenOptimizationsCheckBox.UseVisualStyleBackColor = true;
            // 
            // runAsAdminCheckBox
            // 
            this.runAsAdminCheckBox.AutoSize = true;
            this.runAsAdminCheckBox.Location = new System.Drawing.Point(7, 120);
            this.runAsAdminCheckBox.Name = "runAsAdminCheckBox";
            this.runAsAdminCheckBox.Size = new System.Drawing.Size(197, 17);
            this.runAsAdminCheckBox.TabIndex = 4;
            this.runAsAdminCheckBox.Text = "Run this program as an administrator";
            this.runAsAdminCheckBox.UseVisualStyleBackColor = true;
            // 
            // highDPIScalingOverrideGroupBox
            // 
            this.highDPIScalingOverrideGroupBox.Controls.Add(this.highDpiComboBox);
            this.highDPIScalingOverrideGroupBox.Controls.Add(this.highDpiCheckBox);
            this.highDPIScalingOverrideGroupBox.Location = new System.Drawing.Point(13, 238);
            this.highDPIScalingOverrideGroupBox.Name = "highDPIScalingOverrideGroupBox";
            this.highDPIScalingOverrideGroupBox.Size = new System.Drawing.Size(304, 84);
            this.highDPIScalingOverrideGroupBox.TabIndex = 2;
            this.highDPIScalingOverrideGroupBox.TabStop = false;
            this.highDPIScalingOverrideGroupBox.Text = "High DPI scaling override";
            // 
            // highDpiCheckBox
            // 
            this.highDpiCheckBox.AutoSize = true;
            this.highDpiCheckBox.Location = new System.Drawing.Point(7, 20);
            this.highDpiCheckBox.Name = "highDpiCheckBox";
            this.highDpiCheckBox.Size = new System.Drawing.Size(193, 30);
            this.highDpiCheckBox.TabIndex = 0;
            this.highDpiCheckBox.Text = "Override high DPI scaling behavior.\r\nScaling performed by:";
            this.highDpiCheckBox.UseVisualStyleBackColor = true;
            this.highDpiCheckBox.CheckedChanged += new System.EventHandler(this.highDpiCheckBox_CheckedChanged);
            // 
            // highDpiComboBox
            // 
            this.highDpiComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.highDpiComboBox.Enabled = false;
            this.highDpiComboBox.FormattingEnabled = true;
            this.highDpiComboBox.Items.AddRange(new object[] {
            "Application",
            "System",
            "System (Enhanced)"});
            this.highDpiComboBox.Location = new System.Drawing.Point(7, 57);
            this.highDpiComboBox.Name = "highDpiComboBox";
            this.highDpiComboBox.Size = new System.Drawing.Size(193, 21);
            this.highDpiComboBox.TabIndex = 1;
            // 
            // okButton
            // 
            this.okButton.Location = new System.Drawing.Point(161, 328);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 3;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(242, 328);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 4;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            // 
            // CompatibilitySettingsEditor
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(329, 363);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.highDPIScalingOverrideGroupBox);
            this.Controls.Add(this.settingsGroupBox);
            this.Controls.Add(this.compatibilityModeGroupBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "CompatibilitySettingsEditor";
            this.Text = "Compatibility Settings Editor";
            this.Load += new System.EventHandler(this.CompatibilitySettingsEditor_Load);
            this.compatibilityModeGroupBox.ResumeLayout(false);
            this.compatibilityModeGroupBox.PerformLayout();
            this.settingsGroupBox.ResumeLayout(false);
            this.settingsGroupBox.PerformLayout();
            this.highDPIScalingOverrideGroupBox.ResumeLayout(false);
            this.highDPIScalingOverrideGroupBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox compatibilityModeGroupBox;
        private System.Windows.Forms.CheckBox compatibilityModeCheckBox;
        private System.Windows.Forms.ComboBox compatibilityModeComboBox;
        private System.Windows.Forms.GroupBox settingsGroupBox;
        private System.Windows.Forms.CheckBox colorCheckBox;
        private System.Windows.Forms.ComboBox colorComboBox;
        private System.Windows.Forms.CheckBox sixHundredFortyXFourHundredEightyCheckBox;
        private System.Windows.Forms.CheckBox disableFullscreenOptimizationsCheckBox;
        private System.Windows.Forms.CheckBox runAsAdminCheckBox;
        private System.Windows.Forms.GroupBox highDPIScalingOverrideGroupBox;
        private System.Windows.Forms.ComboBox highDpiComboBox;
        private System.Windows.Forms.CheckBox highDpiCheckBox;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
    }
}