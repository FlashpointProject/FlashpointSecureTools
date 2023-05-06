namespace FlashpointSecurePlayer {
    partial class WebBrowserMode {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WebBrowserMode));
            this.toolBarToolStrip = new System.Windows.Forms.ToolStrip();
            this.backButton = new System.Windows.Forms.ToolStripButton();
            this.forwardButton = new System.Windows.Forms.ToolStripButton();
            this.stopButton = new System.Windows.Forms.ToolStripButton();
            this.refreshButton = new System.Windows.Forms.ToolStripButton();
            this.addressToolStripSpringTextBox = new FlashpointSecurePlayer.ToolStripSpringTextBox();
            this.goButton = new System.Windows.Forms.ToolStripButton();
            this.toolBarToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.saveAsWebpageButton = new System.Windows.Forms.ToolStripButton();
            this.printButton = new System.Windows.Forms.ToolStripButton();
            this.newWindowButton = new System.Windows.Forms.ToolStripButton();
            this.fullscreenButton = new System.Windows.Forms.ToolStripButton();
            this.statusBarStatusStrip = new System.Windows.Forms.StatusStrip();
            this.statusToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.progressToolStripProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.closableWebBrowser = new FlashpointSecurePlayer.ClosableWebBrowser();
            this.exitFullscreenLabel = new System.Windows.Forms.Label();
            this.toolBarToolStrip.SuspendLayout();
            this.statusBarStatusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolBarToolStrip
            // 
            this.toolBarToolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.backButton,
            this.forwardButton,
            this.stopButton,
            this.refreshButton,
            this.addressToolStripSpringTextBox,
            this.goButton,
            this.toolBarToolStripSeparator,
            this.saveAsWebpageButton,
            this.printButton,
            this.newWindowButton,
            this.fullscreenButton});
            this.toolBarToolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolBarToolStrip.Name = "toolBarToolStrip";
            this.toolBarToolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.toolBarToolStrip.Size = new System.Drawing.Size(640, 25);
            this.toolBarToolStrip.TabIndex = 0;
            this.toolBarToolStrip.Text = "toolStrip1";
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
            // addressToolStripSpringTextBox
            // 
            this.addressToolStripSpringTextBox.AutoToolTip = true;
            this.addressToolStripSpringTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.addressToolStripSpringTextBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addressToolStripSpringTextBox.Name = "addressToolStripSpringTextBox";
            this.addressToolStripSpringTextBox.Padding = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.addressToolStripSpringTextBox.Size = new System.Drawing.Size(336, 25);
            this.addressToolStripSpringTextBox.Enter += new System.EventHandler(this.addressToolStripSpringTextBox_Enter);
            this.addressToolStripSpringTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.addressToolStripTextBox_KeyDown);
            this.addressToolStripSpringTextBox.Click += new System.EventHandler(this.addressToolStripSpringTextBox_Click);
            this.addressToolStripSpringTextBox.Paint += new System.Windows.Forms.PaintEventHandler(this.addressToolStripTextBox_Paint);
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
            // toolBarToolStripSeparator
            // 
            this.toolBarToolStripSeparator.Name = "toolBarToolStripSeparator";
            this.toolBarToolStripSeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // saveAsWebpageButton
            // 
            this.saveAsWebpageButton.AutoToolTip = false;
            this.saveAsWebpageButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.saveAsWebpageButton.Image = ((System.Drawing.Image)(resources.GetObject("saveAsWebpageButton.Image")));
            this.saveAsWebpageButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.saveAsWebpageButton.Name = "saveAsWebpageButton";
            this.saveAsWebpageButton.Size = new System.Drawing.Size(23, 22);
            this.saveAsWebpageButton.Text = "toolStripButton5";
            this.saveAsWebpageButton.ToolTipText = "Save As Webpage...";
            this.saveAsWebpageButton.Click += new System.EventHandler(this.saveAsWebpageButton_Click);
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
            // newWindowButton
            // 
            this.newWindowButton.AutoToolTip = false;
            this.newWindowButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.newWindowButton.Image = ((System.Drawing.Image)(resources.GetObject("newWindowButton.Image")));
            this.newWindowButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.newWindowButton.Name = "newWindowButton";
            this.newWindowButton.Size = new System.Drawing.Size(23, 22);
            this.newWindowButton.Text = "toolStripButton1";
            this.newWindowButton.ToolTipText = "New Window";
            this.newWindowButton.Click += new System.EventHandler(this.newWindowButton_Click);
            // 
            // fullscreenButton
            // 
            this.fullscreenButton.AutoToolTip = false;
            this.fullscreenButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.fullscreenButton.Image = ((System.Drawing.Image)(resources.GetObject("fullscreenButton.Image")));
            this.fullscreenButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.fullscreenButton.Name = "fullscreenButton";
            this.fullscreenButton.Size = new System.Drawing.Size(23, 22);
            this.fullscreenButton.Text = "toolStripButton2";
            this.fullscreenButton.ToolTipText = "Fullscreen";
            this.fullscreenButton.Click += new System.EventHandler(this.fullscreenButton_Click);
            // 
            // statusBarStatusStrip
            // 
            this.statusBarStatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusToolStripStatusLabel,
            this.progressToolStripProgressBar});
            this.statusBarStatusStrip.Location = new System.Drawing.Point(0, 505);
            this.statusBarStatusStrip.Name = "statusBarStatusStrip";
            this.statusBarStatusStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.statusBarStatusStrip.ShowItemToolTips = true;
            this.statusBarStatusStrip.Size = new System.Drawing.Size(640, 22);
            this.statusBarStatusStrip.TabIndex = 2;
            this.statusBarStatusStrip.Text = "statusStrip1";
            // 
            // statusToolStripStatusLabel
            // 
            this.statusToolStripStatusLabel.AutoToolTip = true;
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
            // closableWebBrowser
            // 
            this.closableWebBrowser.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.closableWebBrowser.Location = new System.Drawing.Point(0, 25);
            this.closableWebBrowser.Margin = new System.Windows.Forms.Padding(0);
            this.closableWebBrowser.MinimumSize = new System.Drawing.Size(32, 32);
            this.closableWebBrowser.Name = "closableWebBrowser";
            this.closableWebBrowser.ScriptErrorsSuppressed = true;
            this.closableWebBrowser.Size = new System.Drawing.Size(640, 480);
            this.closableWebBrowser.TabIndex = 1;
            this.closableWebBrowser.Url = new System.Uri("about:blank", System.UriKind.Absolute);
            this.closableWebBrowser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(this.closableWebBrowser_Navigated);
            this.closableWebBrowser.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(this.closableWebBrowser_ProgressChanged);
            // 
            // exitFullscreenLabel
            // 
            this.exitFullscreenLabel.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.exitFullscreenLabel.AutoSize = true;
            this.exitFullscreenLabel.BackColor = System.Drawing.Color.DimGray;
            this.exitFullscreenLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.exitFullscreenLabel.ForeColor = System.Drawing.Color.White;
            this.exitFullscreenLabel.Location = new System.Drawing.Point(171, 50);
            this.exitFullscreenLabel.Name = "exitFullscreenLabel";
            this.exitFullscreenLabel.Padding = new System.Windows.Forms.Padding(10);
            this.exitFullscreenLabel.Size = new System.Drawing.Size(299, 45);
            this.exitFullscreenLabel.TabIndex = 3;
            this.exitFullscreenLabel.Text = "Press F11 to exit fullscreen.";
            this.exitFullscreenLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.exitFullscreenLabel.Visible = false;
            // 
            // WebBrowserMode
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 527);
            this.Controls.Add(this.exitFullscreenLabel);
            this.Controls.Add(this.statusBarStatusStrip);
            this.Controls.Add(this.toolBarToolStrip);
            this.Controls.Add(this.closableWebBrowser);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "WebBrowserMode";
            this.Text = "Flashpoint Secure Player";
            this.Activated += new System.EventHandler(this.WebBrowserMode_Activated);
            this.Deactivate += new System.EventHandler(this.WebBrowserMode_Deactivate);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.WebBrowserMode_FormClosing);
            this.Load += new System.EventHandler(this.WebBrowserMode_Load);
            this.toolBarToolStrip.ResumeLayout(false);
            this.toolBarToolStrip.PerformLayout();
            this.statusBarStatusStrip.ResumeLayout(false);
            this.statusBarStatusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ClosableWebBrowser closableWebBrowser;
        private System.Windows.Forms.ToolStrip toolBarToolStrip;
        private System.Windows.Forms.StatusStrip statusBarStatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusToolStripStatusLabel;
        private System.Windows.Forms.ToolStripProgressBar progressToolStripProgressBar;
        private System.Windows.Forms.ToolStripButton backButton;
        private System.Windows.Forms.ToolStripButton forwardButton;
        private System.Windows.Forms.ToolStripButton stopButton;
        private System.Windows.Forms.ToolStripButton refreshButton;
        private System.Windows.Forms.ToolStripButton saveAsWebpageButton;
        private System.Windows.Forms.ToolStripButton printButton;
        private System.Windows.Forms.ToolStripSeparator toolBarToolStripSeparator;
        private ToolStripSpringTextBox addressToolStripSpringTextBox;
        private System.Windows.Forms.ToolStripButton goButton;
        private System.Windows.Forms.ToolStripButton newWindowButton;
        private System.Windows.Forms.ToolStripButton fullscreenButton;
        private System.Windows.Forms.Label exitFullscreenLabel;
    }
}

