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

        public void Activate(string name, ref string software, ref ProcessStartInfo softwareProcessStartInfo) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }
            
            if (!modificationsElement.OldCPUSimulator.ElementInformation.IsPresent) {
                return;
            }
            
            if (modificationsElement.OldCPUSimulator.TargetRate == null) {
                throw new OldCPUSimulatorFailedException("The Target Rate is required.");
            }

            long currentMhz = 0;

            ProcessStartInfo oldCPUSimulatorProcessStartInfo = new ProcessStartInfo("OldCPUSimulator/OldCPUSimulator.exe", "--dev-get-current-mhz") {
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

            if (currentMhz <= modificationsElement.OldCPUSimulator.TargetRate) {
                return;
            }

            StringBuilder oldCPUSimulatorSoftware = new StringBuilder("OldCPUSimulator/OldCPUSimulator.exe -t ");
            oldCPUSimulatorSoftware.Append(modificationsElement.OldCPUSimulator.TargetRate.GetValueOrDefault());

            if (modificationsElement.OldCPUSimulator.RefreshRate != null) {
                oldCPUSimulatorSoftware.Append(" -r ");
                oldCPUSimulatorSoftware.Append(modificationsElement.OldCPUSimulator.RefreshRate.GetValueOrDefault());
            }

            if (modificationsElement.OldCPUSimulator.SetProcessPriorityHigh) {
                oldCPUSimulatorSoftware.Append(" --set-process-priority-high");
            }

            if (modificationsElement.OldCPUSimulator.SetSyncedProcessAffinityOne) {
                oldCPUSimulatorSoftware.Append(" --set-synced-process-affinity-one");
            }

            if (modificationsElement.OldCPUSimulator.SyncedProcessMainThreadOnly) {
                oldCPUSimulatorSoftware.Append(" --synced-process-main-thread-only");
            }

            if (modificationsElement.OldCPUSimulator.RefreshRateFloorFifteen) {
                oldCPUSimulatorSoftware.Append(" --refresh-rate-floor-fifteen");
            }

            oldCPUSimulatorSoftware.Append(" -sw \"");

            try {
                string[] argv = CommandLineToArgv(software, out int argc);
                // TODO: deal with paths with quotes... someday
                oldCPUSimulatorSoftware.Append(Path.GetFullPath(argv[0]));
            } catch {
                throw new OldCPUSimulatorFailedException("The command line is invalid.");
            }

            oldCPUSimulatorSoftware.Append("\" ");
            oldCPUSimulatorSoftware.Append(GetCommandLineArgumentRange(software, 1, -1));
            software = oldCPUSimulatorSoftware.ToString();

            if (softwareProcessStartInfo == null) {
                softwareProcessStartInfo = new ProcessStartInfo();
            }

            HideWindow(ref softwareProcessStartInfo);

            if (String.IsNullOrEmpty(softwareProcessStartInfo.WorkingDirectory)) {
                softwareProcessStartInfo.WorkingDirectory = Environment.CurrentDirectory;
            }
        }
    }
}
