namespace FlashpointSecurePlayer {
    partial class WebBrowser {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WebBrowser));
            this.SuspendLayout();
            // 
            // closableWebBrowser1
            // 
            this.closableWebBrowser1 = new ClosableWebBrowser(this);
            this.closableWebBrowser1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.closableWebBrowser1.Location = new System.Drawing.Point(0, 0);
            this.closableWebBrowser1.Margin = new System.Windows.Forms.Padding(0);
            this.closableWebBrowser1.MinimumSize = new System.Drawing.Size(32, 32);
            this.closableWebBrowser1.Name = "closableWebBrowser1";
            this.closableWebBrowser1.ScriptErrorsSuppressed = true;
            this.closableWebBrowser1.Size = new System.Drawing.Size(640, 480);
            this.closableWebBrowser1.TabIndex = 0;
            this.closableWebBrowser1.Url = new System.Uri("about:blank", System.UriKind.Absolute);
            // 
            // WebBrowser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 480);
            this.Controls.Add(this.closableWebBrowser1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "WebBrowser";
            this.Text = "Flashpoint Secure Player";
            this.Activated += new System.EventHandler(this.WebBrowser_Activated);
            this.Deactivate += new System.EventHandler(this.WebBrowser_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.WebBrowser_FormClosing);
            this.Load += new System.EventHandler(this.WebBrowser_Load);
            this.Shown += new System.EventHandler(this.WebBrowser_Shown);
            this.ResumeLayout(false);

        }

        #endregion

        private ClosableWebBrowser closableWebBrowser1;
    }
}

