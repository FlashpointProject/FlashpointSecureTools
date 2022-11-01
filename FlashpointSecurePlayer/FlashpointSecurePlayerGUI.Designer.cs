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
            this.errorLabel = new System.Windows.Forms.Label();
            this.securePlaybackProgressBar = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // errorLabel
            // 
            this.errorLabel.AutoSize = true;
            this.errorLabel.Location = new System.Drawing.Point(12, 49);
            this.errorLabel.Name = "errorLabel";
            this.errorLabel.Size = new System.Drawing.Size(54, 13);
            this.errorLabel.TabIndex = 1;
            this.errorLabel.Text = "Loading...";
            this.errorLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // securePlaybackProgressBar
            // 
            this.securePlaybackProgressBar.Location = new System.Drawing.Point(13, 13);
            this.securePlaybackProgressBar.Name = "securePlaybackProgressBar";
            this.securePlaybackProgressBar.Size = new System.Drawing.Size(389, 23);
            this.securePlaybackProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.securePlaybackProgressBar.TabIndex = 2;
            // 
            // FlashpointSecurePlayer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(414, 71);
            this.Controls.Add(this.securePlaybackProgressBar);
            this.Controls.Add(this.errorLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FlashpointSecurePlayer";
            this.Text = "Flashpoint Secure Player";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FlashpointSecurePlayer_FormClosing);
            this.Load += new System.EventHandler(this.FlashpointSecurePlayer_Load);
            this.Shown += new System.EventHandler(this.FlashpointSecurePlayer_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label errorLabel;
        private System.Windows.Forms.ProgressBar securePlaybackProgressBar;
    }
}