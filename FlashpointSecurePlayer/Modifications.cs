using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    // virtual class for modifications
    public abstract class Modifications {
        private readonly EventHandler importStart;
        private readonly EventHandler importStop;

        public Modifications(EventHandler importStart, EventHandler importStop) {
            this.importStart = importStart;
            this.importStop = importStop;
        }

        ~Modifications() {
            if (ImportStarted) {
                StopImport();
            }

            Deactivate();
        }

        protected virtual void OnImportStart(EventArgs e) {
            EventHandler eventHandler = importStart;

            if (eventHandler == null) {
                return;
            }

            eventHandler(this, e);
        }

        protected virtual void OnImportStop(EventArgs e) {
            EventHandler eventHandler = importStop;

            if (eventHandler == null) {
                return;
            }

            eventHandler(this, e);
        }

        private bool importStarted = false;

        protected bool ImportStarted {
            get {
                return importStarted;
            }

            set {
                importStarted = value;

                if (importStarted) {
                    OnImportStart(EventArgs.Empty);
                } else {
                    OnImportStop(EventArgs.Empty);
                }
            }
        }

        private readonly object importPausedLock = new object();
        private bool importPaused = true;

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

        protected string TemplateName { get; set; } = String.Empty;

        protected void StartImport(string templateName) {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Start Import when the Import is in progress.");
            }

            if (String.IsNullOrEmpty(templateName)) {
                throw new InvalidTemplateException("The Template Name may not be the Active Template Name.");
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
            //throw new FormatException("templateName must not be null or empty.");
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
 