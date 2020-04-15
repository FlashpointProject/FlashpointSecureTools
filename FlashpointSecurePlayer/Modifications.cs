using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    public abstract class Modifications {
        // virtual class for modifications
        protected readonly Form form = null;
        private readonly object importPausedLock = new object();
        private bool importPaused = true;

        protected string Name { get; set; } = "";
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

        public void PauseImport() {
            ImportPaused = true;
        }

        public void ResumeImport() {
            ImportPaused = false;
        }

        protected void StartImport(string name) {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Start Import when the Import has started.");
            }

            if (String.IsNullOrEmpty(name)) {
                throw new FormatException("name cannot be null or empty.");
            }

            Name = name;
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

            SetConfigurationSection();
            Running = false;

            if (Form != null) {
                Form.ControlBox = !Running;
            }
            */
        }

        public void Activate(string name) {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Activate when the Import has started.");
            }

            if (String.IsNullOrEmpty(name)) {
                throw new FormatException("name cannot be null or empty.");
            }

            Name = name;
            Deactivate();

            //SetConfigurationSection();
        }

        public void Deactivate() {
            if (ImportStarted) {
                throw new InvalidOperationException("Cannot Deactivate when the Import has started.");
            }

            //SetConfigurationSection();
        }
    }
}
 