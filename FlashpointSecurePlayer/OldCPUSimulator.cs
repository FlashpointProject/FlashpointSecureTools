using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.OldCPUSimulatorElement;

namespace FlashpointSecurePlayer {
    public class OldCPUSimulator : Modifications {
        public OldCPUSimulator(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        public bool TestRunningWithOldCPUSimulator() {
            // now, we might already be running under Old CPU Simulator
            // we don't want to start a new instance in that case
            // the user has manually started Old CPU Simulator already
            Process parentProcess = null;

            try {
                parentProcess = GetParentProcess();
            } catch {
                // fail silently
            }

            string parentProcessFileName = null;

            if (parentProcess != null) {
                try {
                    parentProcessFileName = Path.GetFileName(GetProcessName(parentProcess));
                } catch (Exception ex) {
                    LogExceptionToLauncher(ex);
                    throw new InvalidOldCPUSimulatorException("Failed to get the parent process EXE name.");
                }
            }

            if (parentProcessFileName != null) {
                if (parentProcessFileName.Equals(OLD_CPU_SIMULATOR_PARENT_PROCESS_FILE_NAME, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        public void Activate(string templateName, ref ProcessStartInfo softwareProcessStartInfo, out bool softwareIsOldCPUSimulator) {
            OldCPUSimulatorElement oldCPUSimulatorElement = null;
            softwareIsOldCPUSimulator = false;

            base.Activate(templateName);

            if (String.IsNullOrEmpty(TemplateName)) {
                // no argument
                return;
            }

            TemplateElement templateElement = GetTemplateElement(false, TemplateName);

            if (templateElement == null) {
                return;
            }

            ModeElement modeElement = templateElement.Mode;
            ModificationsElement modificationsElement = templateElement.Modifications;

            if (!modificationsElement.ElementInformation.IsPresent) {
                return;
            }

            oldCPUSimulatorElement = modificationsElement.OldCPUSimulator;

            if (!oldCPUSimulatorElement.ElementInformation.IsPresent) {
                return;
            }

            // sigh... okay
            // first, we check the target rate
            if (!int.TryParse(Environment.ExpandEnvironmentVariables(oldCPUSimulatorElement.TargetRate), out int targetRate)) {
                throw new InvalidOldCPUSimulatorException("The Target Rate is required.");
            }

            if (TestRunningWithOldCPUSimulator()) {
                // aaand we're done
                return;
            }

            // next... we need to check if the CPU speed is actually faster than
            // what we want to underclock to
            long maxRate = 0;

            ProcessStartInfo oldCPUSimulatorProcessStartInfo = new ProcessStartInfo(OLD_CPU_SIMULATOR_PATH, "--dev-get-max-mhz") {
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

                if (!oldCPUSimulatorProcess.HasExited) {
                    oldCPUSimulatorProcess.WaitForExit();
                }

                string oldCPUSimulatorProcessStandardError = null;
                string oldCPUSimulatorProcessStandardOutput = null;

                if (oldCPUSimulatorProcessStartInfo.RedirectStandardError) {
                    oldCPUSimulatorProcessStandardError = oldCPUSimulatorProcess.StandardError.ReadToEnd();
                }

                if (oldCPUSimulatorProcessStartInfo.RedirectStandardOutput) {
                    oldCPUSimulatorProcessStandardOutput = oldCPUSimulatorProcess.StandardOutput.ReadToEnd();
                }

                if (oldCPUSimulatorProcess.ExitCode != 0 || !long.TryParse(oldCPUSimulatorProcessStandardOutput.Split('\n').Last(), out maxRate)) {
                    throw new InvalidOldCPUSimulatorException("Failed to get Max Rate.");
                }
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidOldCPUSimulatorException("Failed to get Max Rate.");
            }

            // if our CPU is too slow, just ignore the modification
            if (targetRate >= maxRate) {
                return;
            }

            switch (modeElement.Name) {
                case ModeElement.NAME.WEB_BROWSER:
                // server mode, need to restart the whole app
                // handled in the GUI side of things
                throw new OldCPUSimulatorRequiresApplicationRestartException("The Old CPU Simulator in Web Browser Mode requires a restart.");
                case ModeElement.NAME.SOFTWARE:
                // USB the HDMI to .exe the database
                string commandLineExpanded = Environment.ExpandEnvironmentVariables(modeElement.CommandLine);
                StringBuilder oldCPUSimulatorSoftware = new StringBuilder();
                string workingDirectory = null;

                // the problem we're dealing with here
                // is that we need to get the full path to
                // the executable we want to launch
                // because we want to change the working directory
                // but still launch the executable from a path
                // potentially relative to this executable
                try {
                    string[] argv = GetCommandLineToArgv(commandLineExpanded, out int argc);

                    if (argc <= 0) {
                        throw new IndexOutOfRangeException("The command line argument is out of range.");
                    }
                    
                    string fullPath = Path.GetFullPath(argv[0]);
                    workingDirectory = Path.GetDirectoryName(fullPath);
                    oldCPUSimulatorSoftware.Append(GetValidArgument(fullPath, true));
                } catch (Exception ex) {
                    LogExceptionToLauncher(ex);
                    throw new InvalidOldCPUSimulatorException("The command line is invalid.");
                }

                oldCPUSimulatorSoftware.Append(" ");
                oldCPUSimulatorSoftware.Append(GetArgumentSliceFromCommandLine(commandLineExpanded, 1));
                // this becomes effectively the new thing passed as --software
                // the shared function is used both here and GUI side for restarts
                //modeElement.CommandLine = OLD_CPU_SIMULATOR_PATH + " " + GetOldCPUSimulatorProcessStartInfoArguments(oldCPUSimulatorElement, oldCPUSimulatorSoftware.ToString());
                softwareIsOldCPUSimulator = true;

                if (softwareProcessStartInfo == null) {
                    softwareProcessStartInfo = new ProcessStartInfo();
                }

                softwareProcessStartInfo.FileName = OLD_CPU_SIMULATOR_PATH;
                softwareProcessStartInfo.Arguments = GetOldCPUSimulatorProcessStartInfoArguments(oldCPUSimulatorElement, oldCPUSimulatorSoftware.ToString());
                //softwareProcessStartInfo.RedirectStandardError = true;
                softwareProcessStartInfo.RedirectStandardError = false;
                softwareProcessStartInfo.RedirectStandardOutput = false;
                softwareProcessStartInfo.RedirectStandardInput = false;

                // hide the Old CPU Simulator window... we always do this
                HideWindow(ref softwareProcessStartInfo);

                // default the working directory to here
                // (otherwise it'd get set to Old CPU Simulator's directory, not desirable)
                if (String.IsNullOrEmpty(modeElement.WorkingDirectory)) {
                    if (String.IsNullOrEmpty(workingDirectory)) {
                        softwareProcessStartInfo.WorkingDirectory = Environment.CurrentDirectory;
                    } else {
                        softwareProcessStartInfo.WorkingDirectory = workingDirectory;
                    }
                }
                return;
            }

            throw new InvalidOldCPUSimulatorException("The Old CPU Simulator in this Mode is not supported.");
        }
    }
}
