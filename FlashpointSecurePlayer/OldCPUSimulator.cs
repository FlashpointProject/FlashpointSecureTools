using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement;

namespace FlashpointSecurePlayer {
    class OldCPUSimulator : Modifications {
        public OldCPUSimulator(Form form) : base(form) { }

        public void Activate(string name, ref string server, ref string software, ref ProcessStartInfo softwareProcessStartInfo) {
            OldCPUSimulatorElement oldCPUSimulatorElement = null;

            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }
            
            oldCPUSimulatorElement = modificationsElement.OldCPUSimulator;

            if (!oldCPUSimulatorElement.ElementInformation.IsPresent) {
                return;
            }

            // sigh... okay
            // first, we check the target rate

            if (oldCPUSimulatorElement.TargetRate == null) {
                throw new OldCPUSimulatorFailedException("The target rate is required.");
            }

            // now, we might already be running under Old CPU Simulator
            // we don't want to start a new instance in that case
            // the user has manually started Old CPU Simulator already
            Process parentProcess = GetParentProcess();
            string parentProcessEXEFileName = null;

            if (parentProcess != null) {
                try {
                    parentProcessEXEFileName = Path.GetFileName(GetProcessEXEName(parentProcess)).ToLower();
                } catch {
                    throw new OldCPUSimulatorFailedException("Failed to get the parent process EXE name.");
                }
            }

            if (parentProcessEXEFileName == OLD_CPU_SIMULATOR_PARENT_PROCESS_EXE_FILE_NAME) {
                return;
            }

            // next... we need to check if the CPU speed is actually faster than
            // what we want to underclock to
            long currentMhz = 0;

            ProcessStartInfo oldCPUSimulatorProcessStartInfo = new ProcessStartInfo(OLD_CPU_SIMULATOR_PATH, "--dev-get-current-mhz") {
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                ErrorDialog = false
            };

            try {
                Process oldCPUSimulatorProcess = Process.Start(oldCPUSimulatorProcessStartInfo);
                string oldCPUSimulatorProcessStandardOutput = oldCPUSimulatorProcess.StandardOutput.ReadToEnd();

                if (!oldCPUSimulatorProcess.HasExited) {
                    oldCPUSimulatorProcess.WaitForExit();
                }

                if (oldCPUSimulatorProcess.ExitCode != 0 || !long.TryParse(oldCPUSimulatorProcessStandardOutput.Split('\n').Last(), out currentMhz)) {
                    throw new OldCPUSimulatorFailedException("Failed to get current rate.");
                }
            } catch {
                throw new OldCPUSimulatorFailedException("Failed to get current rate.");
            }

            // if our CPU is too slow, just ignore the modification
            if (currentMhz <= oldCPUSimulatorElement.TargetRate) {
                return;
            }

            if (!String.IsNullOrEmpty(server)) {
                // server mode, need to restart the whole app
                // handled in the GUI side of things
                throw new OldCPUSimulatorRequiresApplicationRestartException("The Old CPU Simulator in Server Mode requires a restart.");
            } else if (!String.IsNullOrEmpty(software)) {
                // USB the HDMI to .exe the database
                StringBuilder oldCPUSimulatorSoftware = new StringBuilder("\"");

                // the problem we're dealing with here
                // is that we need to get the full path to
                // the executable we want to launch
                // because we want to change the working directory
                // but still launch the executable from a path
                // potentially relative to this executable
                try {
                    string[] argv = CommandLineToArgv(software, out int argc);
                    // TODO: deal with paths with quotes... someday
                    oldCPUSimulatorSoftware.Append(Path.GetFullPath(argv[0]));
                } catch {
                    throw new OldCPUSimulatorFailedException("The command line is invalid.");
                }

                oldCPUSimulatorSoftware.Append("\" ");
                oldCPUSimulatorSoftware.Append(GetCommandLineArgumentRange(software, 1, -1));
                // this becomes effectively the new thing passed as --software
                // the shared function is used both here and GUI side for restarts
                software = OLD_CPU_SIMULATOR_PATH + " " + GetOldCPUSimulatorProcessStartInfoArguments(oldCPUSimulatorElement, oldCPUSimulatorSoftware.ToString());

                if (softwareProcessStartInfo == null) {
                    softwareProcessStartInfo = new ProcessStartInfo();
                }

                // hide the Old CPU Simulator window... we always do this
                HideWindow(ref softwareProcessStartInfo);

                // default the working directory to here
                // (otherwise it'd get set to Old CPU Simulator's directory, not desirable)
                if (String.IsNullOrEmpty(softwareProcessStartInfo.WorkingDirectory)) {
                    softwareProcessStartInfo.WorkingDirectory = Environment.CurrentDirectory;
                }
                return;
            }
            throw new OldCPUSimulatorFailedException("No Mode was used which Old CPU Simulator supports.");
        }
    }
}
