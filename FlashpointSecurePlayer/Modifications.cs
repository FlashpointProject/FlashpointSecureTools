﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    // virtual class for modifications
    public abstract class Modifications {
        protected readonly Form form;
        private readonly object importPausedLock = new object();
        private bool importPaused = true;

        protected string TemplateName { get; set; } = String.Empty;
        protected bool ImportStarted { get; set; } = false;

        protected bool ImportPaused {
            get {
                lock (importPausedLock) {
                    return importPaused;
                }
            }

            set {
                lock (importPausedLock) {
                    importPaused = value;
                }
            }
        }

        public Modifications(Form form) {
            this.form = form;
        }

        ~Modifications() {
            if (ImportStarted) {
                StopImport();
            }

            Deactivate();
        }

        protected void SetControlBox() {
            if (form != null) {
                form.ControlBox = !ImportStarted;
            }
        }

        public void PauseImport() {
            ImportPaused = true;
        }

        public void ResumeImport() {
            ImportPaused = false;
        }

        protected void StartImport(string templateName) {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Start Import when the Import is in progress.");
            }

            if (String.IsNullOrEmpty(templateName)) {
                throw new FormatException("templateName cannot be null or empty.");
            }

            TemplateName = templateName;
            Deactivate();

            /*
            Running = true;

            if (Form != null) {
                Form.ControlBox = !Running;
            }

            Resume();
            */
        }

        // async for inheritence reasons
        protected void StopImport() {
            if (!ImportStarted) {
                throw new InvalidOperationException("Cannot Stop Import when the Import has not started.");
            }

            /*
            Suspend();

            SetFlashpointSecurePlayerSection();
            Running = false;

            if (Form != null) {
                Form.ControlBox = !Running;
            }
            */
        }

        public void Activate(string templateName) {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Activate when the Import is in progress.");
            }

            //if (String.IsNullOrEmpty(name)) {
            //throw new FormatException("templateName cannot be null or empty.");
            //}

            TemplateName = templateName;
            Deactivate();

            //SetFlashpointSecurePlayerSection();
        }

        public void Deactivate() {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Deactivate when the Import is in progress.");
            }

            //SetFlashpointSecurePlayerSection();
        }
    }
}
 