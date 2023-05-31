namespace FlashpointSecurePlayer {
    partial class FlashpointSecurePlayerGUI {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FlashpointSecurePlayerGUI));
            this.messageLabel = new System.Windows.Forms.Label();
            this.securePlaybackProgressBar = new System.Windows.Forms.ProgressBar();
            this.canShowMessageLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // messageLabel
            // 
            this.messageLabel.AutoSize = true;
            this.messageLabel.Location = new System.Drawing.Point(12, 38);
            this.messageLabel.Name = "messageLabel";
            this.messageLabel.Size = new System.Drawing.Size(54, 13);
            this.messageLabel.TabIndex = 1;
            this.messageLabel.Text = "Loading...";
            this.messageLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // securePlaybackProgressBar
            // 
            this.securePlaybackProgressBar.Location = new System.Drawing.Point(12, 12);
            this.securePlaybackProgressBar.Name = "securePlaybackProgressBar";
            this.securePlaybackProgressBar.Size = new System.Drawing.Size(410, 23);
            this.securePlaybackProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.securePlaybackProgressBar.TabIndex = 0;
            // 
            // canShowMessageLabel
            // 
            this.canShowMessageLabel.AutoSize = true;
            this.canShowMessageLabel.Location = new System.Drawing.Point(1000, 1000);
            this.canShowMessageLabel.Name = "canShowMessageLabel";
            this.canShowMessageLabel.Size = new System.Drawing.Size(54, 13);
            this.canShowMessageLabel.TabIndex = 2;
            this.canShowMessageLabel.Text = "Loading...";
            this.canShowMessageLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // FlashpointSecurePlayerGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(434, 60);
            this.Controls.Add(this.canShowMessageLabel);
            this.Controls.Add(this.securePlaybackProgressBar);
            this.Controls.Add(this.messageLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FlashpointSecurePlayerGUI";
            this.Text = "Flashpoint Secure Player";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FlashpointSecurePlayer_FormClosing);
            this.Load += new System.EventHandler(this.FlashpointSecurePlayer_Load);
            this.Shown += new System.EventHandler(this.FlashpointSecurePlayer_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label messageLabel;
        private System.Windows.Forms.ProgressBar securePlaybackProgressBar;
        private System.Windows.Forms.Label canShowMessageLabel;
    }
}