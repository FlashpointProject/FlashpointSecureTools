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
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.backButton = new System.Windows.Forms.ToolStripButton();
            this.forwardButton = new System.Windows.Forms.ToolStripButton();
            this.stopButton = new System.Windows.Forms.ToolStripButton();
            this.refreshButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.saveAsButton = new System.Windows.Forms.ToolStripButton();
            this.printButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.addressToolStripTextBox = new ToolStripSpringTextBox();
            this.goButton = new System.Windows.Forms.ToolStripButton();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.progressToolStripProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backButton,
            this.forwardButton,
            this.stopButton,
            this.refreshButton,
            this.toolStripSeparator1,
            this.saveAsButton,
            this.printButton,
            this.toolStripSeparator2,
            this.addressToolStripTextBox,
            this.goButton});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(640, 25);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // backButton
            // 
            this.backButton.Enabled = false;
            this.backButton.Image = ((System.Drawing.Image)(resources.GetObject("backButton.Image")));
            this.backButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.backButton.Name = "backButton";
            this.backButton.Size = new System.Drawing.Size(52, 22);
            this.backButton.Text = "Back";
            this.backButton.Click += new System.EventHandler(this.backButton_Click);
            // 
            // forwardButton
            // 
            this.forwardButton.AutoToolTip = false;
            this.forwardButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.forwardButton.Enabled = false;
            this.forwardButton.Image = ((System.Drawing.Image)(resources.GetObject("forwardButton.Image")));
            this.forwardButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.forwardButton.Name = "forwardButton";
            this.forwardButton.Size = new System.Drawing.Size(23, 22);
            this.forwardButton.Text = "toolStripButton2";
            this.forwardButton.ToolTipText = "Forward";
            this.forwardButton.Click += new System.EventHandler(this.forwardButton_Click);
            // 
            // stopButton
            // 
            this.stopButton.AutoToolTip = false;
            this.stopButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.stopButton.Image = ((System.Drawing.Image)(resources.GetObject("stopButton.Image")));
            this.stopButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(23, 22);
            this.stopButton.Text = "toolStripButton4";
            this.stopButton.ToolTipText = "Stop";
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // refreshButton
            // 
            this.refreshButton.AutoToolTip = false;
            this.refreshButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.refreshButton.Image = ((System.Drawing.Image)(resources.GetObject("refreshButton.Image")));
            this.refreshButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(23, 22);
            this.refreshButton.Text = "toolStripButton3";
            this.refreshButton.ToolTipText = "Refresh";
            this.refreshButton.Click += new System.EventHandler(this.refreshButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // saveAsButton
            // 
            this.saveAsButton.AutoToolTip = false;
            this.saveAsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.saveAsButton.Image = ((System.Drawing.Image)(resources.GetObject("saveAsButton.Image")));
            this.saveAsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveAsButton.Name = "saveAsButton";
            this.saveAsButton.Size = new System.Drawing.Size(23, 22);
            this.saveAsButton.Text = "toolStripButton5";
            this.saveAsButton.ToolTipText = "Save As Webpage...";
            this.saveAsButton.Click += new System.EventHandler(this.saveAsButton_Click);
            // 
            // printButton
            // 
            this.printButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.printButton.Image = ((System.Drawing.Image)(resources.GetObject("printButton.Image")));
            this.printButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.printButton.Name = "printButton";
            this.printButton.Size = new System.Drawing.Size(23, 22);
            this.printButton.Text = "toolStripButton6";
            this.printButton.ToolTipText = "Print...";
            this.printButton.Click += new System.EventHandler(this.printButton_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // addressToolStripTextBox
            // 
            this.addressToolStripTextBox.Name = "addressToolStripTextBox";
            this.addressToolStripTextBox.Size = new System.Drawing.Size(100, 25);
            this.addressToolStripTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.addressToolStripTextBox_KeyDown);
            this.addressToolStripTextBox.Click += new System.EventHandler(this.addressToolStripTextBox_Click);
            // 
            // goButton
            // 
            this.goButton.Image = ((System.Drawing.Image)(resources.GetObject("goButton.Image")));
            this.goButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.goButton.Name = "goButton";
            this.goButton.Size = new System.Drawing.Size(42, 22);
            this.goButton.Text = "Go";
            this.goButton.Click += new System.EventHandler(this.goButton_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusToolStripStatusLabel,
            this.progressToolStripProgressBar});
            this.statusStrip1.Location = new System.Drawing.Point(0, 458);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(640, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusToolStripStatusLabel
            // 
            this.statusToolStripStatusLabel.AutoSize = false;
            this.statusToolStripStatusLabel.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
            this.statusToolStripStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.statusToolStripStatusLabel.Name = "statusToolStripStatusLabel";
            this.statusToolStripStatusLabel.Size = new System.Drawing.Size(523, 17);
            this.statusToolStripStatusLabel.Spring = true;
            this.statusToolStripStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // progressToolStripProgressBar
            // 
            this.progressToolStripProgressBar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.progressToolStripProgressBar.Name = "progressToolStripProgressBar";
            this.progressToolStripProgressBar.Size = new System.Drawing.Size(100, 16);
            // 
            // closableWebBrowser1
            // 
            this.closableWebBrowser1 = new ClosableWebBrowser(this);
            this.closableWebBrowser1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.closableWebBrowser1.Location = new System.Drawing.Point(0, 25);
            this.closableWebBrowser1.Margin = new System.Windows.Forms.Padding(0);
            this.closableWebBrowser1.MinimumSize = new System.Drawing.Size(32, 32);
            this.closableWebBrowser1.Name = "closableWebBrowser1";
            this.closableWebBrowser1.ScriptErrorsSuppressed = true;
            this.closableWebBrowser1.Size = new System.Drawing.Size(640, 433);
            this.closableWebBrowser1.TabIndex = 0;
            this.closableWebBrowser1.Url = new System.Uri("about:blank", System.UriKind.Absolute);
            this.closableWebBrowser1.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(this.closableWebBrowser1_ProgressChanged);
            // 
            // WebBrowser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 480);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.closableWebBrowser1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "WebBrowser";
            this.Text = "Flashpoint Secure Player";
            this.Activated += new System.EventHandler(this.WebBrowser_Activated);
            this.Deactivate += new System.EventHandler(this.WebBrowser_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.WebBrowser_FormClosing);
            this.Load += new System.EventHandler(this.WebBrowser_Load);
            this.Shown += new System.EventHandler(this.WebBrowser_Shown);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ClosableWebBrowser closableWebBrowser1;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusToolStripStatusLabel;
        private System.Windows.Forms.ToolStripProgressBar progressToolStripProgressBar;
        private System.Windows.Forms.ToolStripButton backButton;
        private System.Windows.Forms.ToolStripButton forwardButton;
        private System.Windows.Forms.ToolStripButton stopButton;
        private System.Windows.Forms.ToolStripButton refreshButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton saveAsButton;
        private System.Windows.Forms.ToolStripButton printButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private ToolStripSpringTextBox addressToolStripTextBox;
        private System.Windows.Forms.ToolStripButton goButton;
    }
}

