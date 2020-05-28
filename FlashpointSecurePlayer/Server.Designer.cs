namespace FlashpointSecurePlayer {
    partial class Server {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Server));
            this.closableWebBrowser1 = new ClosableWebBrowser(this);
            this.SuspendLayout();
            // 
            // closableWebBrowser1
            // 
            this.closableWebBrowser1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.closableWebBrowser1.Location = new System.Drawing.Point(0, 0);
            this.closableWebBrowser1.MinimumSize = new System.Drawing.Size(20, 20);
            this.closableWebBrowser1.Name = "closableWebBrowser1";
            this.closableWebBrowser1.ScriptErrorsSuppressed = true;
            this.closableWebBrowser1.Size = new System.Drawing.Size(640, 480);
            this.closableWebBrowser1.TabIndex = 0;
            this.closableWebBrowser1.Url = new System.Uri("about:blank", System.UriKind.Absolute);
            // 
            // Server
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 480);
            this.Controls.Add(this.closableWebBrowser1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Server";
            this.Text = "Flashpoint Secure Player";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Server_FormClosing);
            this.Load += new System.EventHandler(this.Server_Load);
            this.Shown += new System.EventHandler(this.Server_Shown);
            this.ResumeLayout(false);

        }

        #endregion

        private ClosableWebBrowser closableWebBrowser1;
    }
}

