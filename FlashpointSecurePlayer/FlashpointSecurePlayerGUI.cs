using System;
using System.Collections.Generic;
using System.Configuration;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;

namespace FlashpointSecurePlayer {
    public partial class FlashpointSecurePlayerGUI : Form {
        private const string APPLICATION_MUTEX_NAME = "Flashpoint Secure Player";
        private const string MODE_MUTEX_NAME = "Flashpoint Secure Player Mode";
        private const string MODIFICATIONS_MUTEX_NAME = "Flashpoint Secure Player Modifications";
        private const string FLASHPOINT_LAUNCHER_PARENT_PROCESS_FILE_NAME = "CMD.EXE";
        private const string FLASHPOINT_LAUNCHER_PROCESS_NAME = "FLASHPOINT";

        private Mutex applicationMutex = null;

        private readonly RunAsAdministrator runAsAdministrator;
        private readonly EnvironmentVariables environmentVariables;
        private readonly DownloadsBefore downloadsBefore;
        private readonly RegistryStates registryState;
        private readonly SingleInstance singleInstance;
        private readonly OldCPUSimulator oldCPUSimulator;

        // older than Windows 7
        private readonly bool oldWindowsVersion = Environment.OSVersion.Version < new Version(6, 1);

        private bool activeX = false;
        WebBrowserMode webBrowserMode = null;
        private ProcessStartInfo softwareProcessStartInfo = null;
        private bool softwareIsOldCPUSimulator = false;

        private string URL { get; set; } = String.Empty;
        private string TemplateName { get; set; } = ACTIVE_EXE_CONFIGURATION_NAME;
        private string Arguments { get; set; } = String.Empty;
        private MODIFICATIONS_REVERT_METHOD ModificationsRevertMethod { get; set; } = MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY;
        private bool IgnoreActiveXControlInstallFailure { get; set; } = false;
        private bool UseFlashActiveXControl { get; set; } = false;
        private bool RunAsAdministratorModification { get; set; } = false;
        private List<string> DownloadsBeforeModificationNames { get; set; } = null;

        private delegate void ErrorDelegate(string text);

        public FlashpointSecurePlayerGUI() {
            InitializeComponent();

            // default to false in case of error
            bool createdNew = false;

            // test multiple instances
            try {
                // signals the Mutex if it has not been
                applicationMutex = new Mutex(true, APPLICATION_MUTEX_NAME, out createdNew);

                if (!createdNew) {
                    // multiple instances open, blow up immediately
                    applicationMutex.Close();
                    applicationMutex = null;
                    throw new InvalidOperationException("You cannot run multiple instances of Flashpoint Secure Player.");
                }
            } catch (InvalidOperationException ex) {
                LogExceptionToLauncher(ex);
            } finally {
                if (!createdNew) {
                    Environment.Exit(-2);
                }
            }

            runAsAdministrator = new RunAsAdministrator(ImportStart, ImportStop);
            environmentVariables = new EnvironmentVariables(ImportStart, ImportStop);
            downloadsBefore = new DownloadsBefore(ImportStart, ImportStop);
            registryState = new RegistryStates(ImportStart, ImportStop);
            singleInstance = new SingleInstance(ImportStart, ImportStop);
            oldCPUSimulator = new OldCPUSimulator(ImportStart, ImportStop);
        }

        private bool CanShowMessageLabel(string text) {
            if (text == null) {
                return false;
            }

            // the Contains function uses ordinal string comparison by default
            if (text.Contains("\n")) {
                return false;
            }

            canShowMessageLabel.Text = text;

            if (canShowMessageLabel.Width > securePlaybackProgressBar.Width) {
                return false;
            }
            return true;
        }

        private bool ShowOutput(string text) {
            ProgressManager.ShowOutput();

            if (text == null) {
                return false;
            }

            if (!CanShowMessageLabel(text)) {
                MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.None);
                return false;
            }

            messageLabel.Text = text;
            return true;
        }

        private bool ShowError(string text) {
            ProgressManager.ShowError();

            if (text == null) {
                return false;
            }

            if (!CanShowMessageLabel(text)) {
                MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            messageLabel.Text = text;
            return true;
        }

        private void ShowErrorFatal(string text) {
            ProgressManager.ShowError();

            if (text == null) {
                return;
            }
            
            MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        private void ShowNoGameSelected() {
            // detect if this application was started by Flashpoint Launcher
            // none of this is strictly necessary, I'm just trying
            // to reduce the amount of stupid in the #help-me-please channel
            //ShowError(Properties.Resources.GameNotCuratedCorrectly);
            StringBuilder text = new StringBuilder(Properties.Resources.NoGameSelected);
            
            string parentProcessFileName = null;

            try {
                using (Process parentProcess = GetParentProcess()) {
                    parentProcessFileName = Path.GetFileName(GetProcessName(parentProcess).ToString());
                }
            } catch {
                // fail silently
            }

            if (parentProcessFileName == null
                || !parentProcessFileName.Equals(FLASHPOINT_LAUNCHER_PARENT_PROCESS_FILE_NAME, StringComparison.OrdinalIgnoreCase)) {
                text.Append(" " + Properties.Resources.UseFlashpointLauncher);

                Process[] processesByName;

                // detect if Flashpoint Launcher is open
                // we only show this message if it isn't open yet
                // because we don't want to confuse n00bs into
                // opening two instances of it
                try {
                    processesByName = Process.GetProcessesByName(FLASHPOINT_LAUNCHER_PROCESS_NAME);
                } catch (InvalidOperationException ex) {
                    // only occurs on Windows XP which is unsupported
                    LogExceptionToLauncher(ex);
                    ShowErrorFatal(Properties.Resources.WindowsVersionTooOld);
                    return;
                }

                if (processesByName != null) {
                    try {
                        if (!processesByName.Any()) {
                            text.Append(" " + Properties.Resources.OpenFlashpointLauncher);
                        }
                    } finally {
                        for (int i = 0; i < processesByName.Length; i++) {
                            if (processesByName[i] != null) {
                                processesByName[i].Dispose();
                                processesByName[i] = null;
                            }
                        }

                        processesByName = null;
                    }
                }
            }

            ShowErrorFatal(text.ToString());
        }

        private void RestartApplication(bool runAsAdministrator, ref Mutex applicationMutex, ProcessStartInfo processStartInfo = null) {
            if (processStartInfo == null) {
                processStartInfo = new ProcessStartInfo {
                    FileName = GetValidArgument(Application.ExecutablePath, true),
                    // can't use GetCommandLineArgs() and String.Join because arguments that were in quotes will lose their quotes
                    // need to use Environment.CommandLine and find arguments
                    Arguments = GetArgumentSliceFromCommandLine(Environment.CommandLine, 1)
                };
            }

            processStartInfo.RedirectStandardError = false;
            processStartInfo.RedirectStandardOutput = false;
            processStartInfo.RedirectStandardInput = false;

            if (runAsAdministrator) {
                processStartInfo.UseShellExecute = true;
                processStartInfo.Verb = "runas";
            }

            if (applicationMutex != null) {
                applicationMutex.ReleaseMutex();
                applicationMutex.Close();
                applicationMutex = null;
            }

            // hide the current form so two windows are not open at once
            // no this is not a race condition
            // http://stackoverflow.com/questions/33042010/in-what-cases-does-the-process-start-method-return-false
            try {
                Hide();
                ControlBox = true;
                Process.Start(processStartInfo).Dispose();
                Application.Exit();
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                Show();
                ShowErrorFatal(Properties.Resources.ProcessUnableToStart);
                throw new Exceptions.ApplicationRestartRequiredException("The application failed to restart.");
            }
        }

        private void AskLaunch(string applicationRestartMessage, string descriptionMessage = null) {
            ProgressManager.ShowOutput();
            StringBuilder message = new StringBuilder(String.Format(Properties.Resources.LaunchGame, applicationRestartMessage));

            if (!String.IsNullOrWhiteSpace(descriptionMessage)) {
                message.Append("\n\n" + descriptionMessage);
            }

            DialogResult dialogResult = MessageBox.Show(message.ToString(), Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.YesNo, MessageBoxIcon.None);

            if (dialogResult == DialogResult.No) {
                Application.Exit();
                throw new InvalidModificationException("The operation was aborted by the user.");
            }
        }

        private void AskLaunchAsAdministratorUser() {
            try {
                if (!TestLaunchedAsAdministratorUser()) {
                    // popup message box and restart program here
                    /*
                     this dialog is not purely here for aesthetic/politeness reasons
                     it's a stopgap to prevent the program from reloading infinitely
                     in case the TestLaunchedAsAdministratorUser function somehow fails
                     you might say "but the UAC dialog would prevent it reloading unstoppably"
                     to which I say "yes, but some very stupid people turn UAC off"
                     then there'd be no dialog except this one - and I don't want
                     the program to enter an infinite restart loop
                     */
                    AskLaunch(Properties.Resources.AsAdministratorUser);

                    RestartApplication(true, ref applicationMutex);
                }
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
            }

            // we're already running as admin?
            ShowError(String.Format(Properties.Resources.GameUnableToLaunch, Properties.Resources.AsAdministratorUser));
            throw new InvalidModificationException("The Modification failed to run as Administrator User.");
        }

        private void AskLaunchWithCompatibilitySettings() {
            try {
                AskLaunch(Properties.Resources.WithCompatibilitySettings);
            
                RestartApplication(false, ref applicationMutex);
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
            }

            ShowError(String.Format(Properties.Resources.GameUnableToLaunch, Properties.Resources.WithCompatibilitySettings));
            throw new InvalidModificationException("The Modification failed to run with Compatibility Settings.");
        }

        private void AskLaunchWithOldCPUSimulator() {
            try {
                // only ask if Old CPU Simulator Modification exists
                if (String.IsNullOrEmpty(TemplateName)) {
                    return;
                }

                TemplateElement templateElement = GetTemplateElement(false, TemplateName);

                if (templateElement == null) {
                    return;
                }

                ModificationsElement modificationsElement = templateElement.Modifications;

                if (!modificationsElement.ElementInformation.IsPresent) {
                    return;
                }

                string parentProcessFileName = null;
            
                Process parentProcess = null;

                try {
                    parentProcess = GetParentProcess();
                } catch {
                    // fail silently
                }

                using (parentProcess) {
                    if (parentProcess != null) {
                        parentProcessFileName = Path.GetFileName(GetProcessName(parentProcess).ToString());
                    }
                }

                if (parentProcessFileName != null) {
                    if (parentProcessFileName.Equals(OLD_CPU_SIMULATOR_PARENT_PROCESS_FILE_NAME, StringComparison.OrdinalIgnoreCase)) {
                        return;
                    }
                }

                AskLaunch(Properties.Resources.WithOldCPUSimulator, Properties.Resources.OldCPUSimulatorSlow);
                string fullPath = null;

                // Old CPU Simulator needs to be on top, not us
                fullPath = Path.GetFullPath(OLD_CPU_SIMULATOR_PATH);

                ProcessStartInfo processStartInfo;
            
                processStartInfo = new ProcessStartInfo {
                    FileName = GetValidArgument(fullPath, true),
                    Arguments = GetOldCPUSimulatorProcessStartInfoArguments(modificationsElement.OldCPUSimulator, new StringBuilder(Environment.CommandLine)).ToString(),
                    WorkingDirectory = Environment.CurrentDirectory
                };

                HideWindow(ref processStartInfo);
            
                RestartApplication(false, ref applicationMutex, processStartInfo);
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
            }

            ShowError(String.Format(Properties.Resources.GameUnableToLaunch, Properties.Resources.WithOldCPUSimulator));
            throw new InvalidModificationException("The Modification failed to run with Old CPU Simulator.");
        }

        private async Task<StringBuilder> GetHTDOCSFilePath(string url) {
            Uri requestUri = await DownloadAsync(GetValidatedURL(url)).ConfigureAwait(true);

            StringBuilder htdocsFilePath = new StringBuilder(HTDOCS);

            try {
                // ignore host if going through localhost (no proxy)
                if (requestUri != null) {
                    if (!requestUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) {
                        htdocsFilePath.Append("\\");
                        htdocsFilePath.Append(requestUri.Host);
                    }

                    htdocsFilePath.Append(requestUri.LocalPath);
                }
            } catch (UriFormatException) {
                throw new ArgumentException("The URL \"" + url + "\" is malformed.");
            } catch (InvalidOperationException) {
                throw new ArgumentException("The URL \"" + url + "\" is invalid.");
            }
            return htdocsFilePath;
        }

        private async Task ImportActiveXAsync(ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modificationsMutex = new Mutex(true, MODIFICATIONS_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modificationsMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstancePreventingTemplate);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because a Modification is activating.");
                    }
                }

                try {
                    if (String.IsNullOrEmpty(TemplateName)) {
                        errorDelegate(Properties.Resources.CurationMissingTemplateName);
                        throw new InvalidTemplateException("The Template Name may not be the Active Template Name.");
                    }

                    // this requires admin
                    if (!TestLaunchedAsAdministratorUser()) {
                        AskLaunchAsAdministratorUser();
                    }

                    ProgressManager.Reset();
                    ShowOutput(Properties.Resources.RegistryStateInProgress);
                    ProgressManager.CurrentGoal.Start(6);

                    try {
                        // fix for loading dependencies
                        string fullPath = null;

                        try {
                            fullPath = Path.GetFullPath(TemplateName);
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, TemplateName));
                            throw new ActiveXImportFailedException("The ActiveX Import failed because getting the Full Path failed.");
                        }

                        try {
                            Directory.SetCurrentDirectory(Path.GetDirectoryName(fullPath));
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.UnableToSetWorkingDirectory);
                            throw new ActiveXImportFailedException("The ActiveX Import failed because setting the Current Directory failed.");
                        }

                        ActiveXControl activeXControl = null;

                        try {
                            activeXControl = new ActiveXControl(fullPath);
                        } catch (InvalidActiveXControlException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.GameNotActiveXControl);
                            throw new ActiveXImportFailedException("The ActiveX Import failed because the DLL is not an ActiveX Control.");
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, fullPath));
                            throw new ActiveXImportFailedException("The ActiveX Import failed because the DLL was not found.");
                        }

                        BINARY_TYPE binaryType;

                        try {
                            binaryType = GetLibraryBinaryType(fullPath);
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.GameNotActiveXControl);
                            throw new ActiveXImportFailedException("The ActiveX Import failed because getting the Binary Type failed.");
                        }

                        // first, we install the control without a registry state running
                        // this is so we can be sure we can uninstall the control
                        try {
                            activeXControl.Install();
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);

                            if (!IgnoreActiveXControlInstallFailure) {
                                errorDelegate(Properties.Resources.ActiveXControlUnableToInstall);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to install.");
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        // next, uninstall the control
                        // in case it was already installed before this whole process
                        // this is to ensure an existing install
                        // doesn't interfere with our registry state results
                        try {
                            activeXControl.Uninstall();
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);

                            if (!IgnoreActiveXControlInstallFailure) {
                                errorDelegate(Properties.Resources.ActiveXControlUnableToUninstall);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to uninstall.");
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        // Set Current Directory
                        try {
                            Directory.SetCurrentDirectory(Application.StartupPath);
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.UnableToSetWorkingDirectory);
                            throw new ActiveXImportFailedException("The ActiveX Import failed because setting the Current Directory failed.");
                        }

                        try {
                            try {
                                await registryState.StartImportAsync(TemplateName, binaryType).ConfigureAwait(true);
                            } catch (TaskRequiresElevationException ex) {
                                LogExceptionToLauncher(ex);
                                // we're already running as admin?
                                errorDelegate(String.Format(Properties.Resources.GameUnableToLaunch, Properties.Resources.AsAdministratorUser));
                                throw new ActiveXImportFailedException("The ActiveX Import failed running as Administrator User.");
                            } catch (InvalidRegistryStateException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.RegistryStateUnableToCreate);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the Registry States failed when the ActiveX Import was started.");
                            } catch (ConfigurationErrorsException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the configuration failed to load when the ActiveX Import was started.");
                            } catch (InvalidModificationException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.GameNotCuratedCorrectly);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the Modification is invalid.");
                            } catch (InvalidTemplateException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.CurationMissingTemplateName);
                                throw;
                            } catch (InvalidOperationException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.RegistryStateAlreadyInProgress);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because a Registry State is already in progress.");
                            } catch (ArgumentException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, TemplateName));
                                throw new ActiveXImportFailedException("The ActiveX Import failed because getting the Full Path failed.");
                            }

                            ProgressManager.CurrentGoal.Steps++;

                            // a registry states is running, install the control
                            try {
                                activeXControl.Install();
                            } catch (Exception ex) {
                                LogExceptionToLauncher(ex);

                                if (!IgnoreActiveXControlInstallFailure) {
                                    errorDelegate(Properties.Resources.ActiveXControlUnableToInstall);
                                    throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to install.");
                                }
                            }

                            ProgressManager.CurrentGoal.Steps++;

                            try {
                                await registryState.StopImportAsync().ConfigureAwait(true);
                            } catch (InvalidRegistryStateException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.RegistryStateUnableToCreate);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the Registry States failed when the ActiveX Import was stopped.");
                            } catch (ConfigurationErrorsException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the configuration failed to load when the ActiveX Import was stopped.");
                            } catch (InvalidOperationException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.RegistryStateNotInProgress);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because a Registry State is not in progress.");
                            }
                        } finally {
                            // we do this to ensure the user can exit in the case of an error
                            ControlBox = true;
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        // the registry state is stopped, uninstall the control
                        // this will leave the control uninstalled on the system
                        // there is no way to tell if it was installed before
                        // (which is the point of creating the state so we can)
                        try {
                            activeXControl.Uninstall();
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);

                            if (!IgnoreActiveXControlInstallFailure) {
                                errorDelegate(Properties.Resources.ActiveXControlUnableToUninstall);
                                throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to uninstall.");
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    } finally {
                        ProgressManager.CurrentGoal.Stop();
                    }

                    ShowOutput(Properties.Resources.RegistryStateWasSuccessful);
                } finally {
                    modificationsMutex.ReleaseMutex();
                }
            }
        }

        private void ActivateMode(TemplateElement templateElement, ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modeMutex = new Mutex(true, MODE_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modeMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstancePreventingTemplate);
                        throw new InvalidModeException("Another Mode is activating.");
                    }
                }

                try {
                    ModeElement modeElement = templateElement.Mode;

                    switch (modeElement.Name) {
                        case ModeElement.NAME.WEB_BROWSER:
                        if (!String.IsNullOrEmpty(modeElement.WorkingDirectory)) {
                            try {
                                Directory.SetCurrentDirectory(Environment.ExpandEnvironmentVariables(modeElement.WorkingDirectory));
                            } catch (SecurityException ex) {
                                LogExceptionToLauncher(ex);
                                throw new TaskRequiresElevationException("Setting the Current Directory requires elevation.");
                            } catch (ArgumentNullException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.UnableToSetWorkingDirectory);
                                throw new InvalidModeException("The Mode failed because the Working Directory must not be null.");
                            } catch (Exception ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.UnableToSetWorkingDirectory);
                                throw new InvalidModeException("Setting the Current Directory failed.");
                            }
                        }

                        Uri webBrowserURL = null;

                        try {
                            webBrowserURL = new Uri(GetValidatedURL(URL), UriKind.Absolute);
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(String.Format(Properties.Resources.AddressNotUnderstood, URL));
                            throw new InvalidModeException("The address \"" + URL + "\" was not understood by the Mode.");
                        }

                        webBrowserMode = new WebBrowserMode(webBrowserURL, UseFlashActiveXControl) {
                            WindowState = FormWindowState.Maximized
                        };

                        webBrowserMode.FormClosing += webBrowserMode_FormClosing;
                        Hide();
                        webBrowserMode.Show();
                        return;
                        case ModeElement.NAME.SOFTWARE:
                        try {
                            string commandLineExpanded = Environment.ExpandEnvironmentVariables(modeElement.CommandLine);
                            string[] argv = GetCommandLineToArgv(commandLineExpanded, out int argc);

                            if (argc <= 0) {
                                throw new IndexOutOfRangeException("The command line argument is out of range.");
                            }

                            string fullPath = Path.GetFullPath(argv[0]);

                            if (softwareProcessStartInfo == null) {
                                softwareProcessStartInfo = new ProcessStartInfo();
                            }

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.FileName)) {
                                softwareProcessStartInfo.FileName = GetValidArgument(fullPath, true);
                            }

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.Arguments)) {
                                softwareProcessStartInfo.Arguments = GetArgumentSliceFromCommandLine(commandLineExpanded, 1);
                            }

                            softwareProcessStartInfo.UseShellExecute = false;
                            softwareProcessStartInfo.ErrorDialog = false;

                            if (modeElement.HideWindow.GetValueOrDefault()) {
                                HideWindow(ref softwareProcessStartInfo);
                            }

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.WorkingDirectory)) {
                                softwareProcessStartInfo.WorkingDirectory = String.IsNullOrEmpty(modeElement.WorkingDirectory)
                                    ? Path.GetDirectoryName(fullPath)
                                    : Environment.ExpandEnvironmentVariables(modeElement.WorkingDirectory);
                            }

                            Process softwareProcess = null;

                            try {
                                // StartProcessCreateBreakawayFromJob required for Process Sync on Windows 7
                                StartProcessCreateBreakawayFromJob(softwareProcessStartInfo, out softwareProcess);
                            } catch (JobObjectException ex) {
                                // popup message box and blow up
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.JobObjectNotCreated);
                                Environment.Exit(-1);
                                throw new InvalidModeException("The Mode failed to create a Job Object.");
                            }

                            using (softwareProcess) {
                                if (softwareProcess == null) {
                                    throw new InvalidModeException("The Mode failed to create the Process.");
                                }

                                Hide();

                                softwareProcess.WaitForExit();

                                Show();
                                Refresh();

                                /*
                                string softwareProcessStandardError = null;
                                string softwareProcessStandardOutput = null;

                                if (softwareProcessStartInfo.RedirectStandardError) {
                                    softwareProcessStandardError = softwareProcess.StandardError.ReadToEnd();
                                }

                                if (softwareProcessStartInfo.RedirectStandardOutput) {
                                    softwareProcessStandardOutput = softwareProcess.StandardOutput.ReadToEnd();
                                }
                                */

                                if (softwareIsOldCPUSimulator) {
                                    switch (softwareProcess.ExitCode) {
                                        case 0:
                                        break;
                                        // RedirectStandardError is not supported by StartProcessCreateBreakawayFromJob
                                        /*
                                        case -1:
                                        if (!String.IsNullOrEmpty(softwareProcessStandardError)) {
                                            string[] lastSoftwareProcessStandardErrors = softwareProcessStandardError.Split('\n');
                                            string lastSoftwareProcessStandardError = null;

                                            if (lastSoftwareProcessStandardErrors.Length > 1) {
                                                lastSoftwareProcessStandardError = lastSoftwareProcessStandardErrors[lastSoftwareProcessStandardErrors.Length - 2];
                                            }

                                            if (!String.IsNullOrEmpty(lastSoftwareProcessStandardError)) {
                                                MessageBox.Show(lastSoftwareProcessStandardError, Properties.Resources.OldCPUSimulator, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                        }
                                        break;
                                        */
                                        case -2:
                                        MessageBox.Show(Properties.Resources.OCS_NoMultipleInstances, Properties.Resources.OldCPUSimulator, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                        case -3:
                                        MessageBox.Show(Properties.Resources.OCS_CPUSpeedNotDetermined, Properties.Resources.OldCPUSimulator, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                        default:
                                        MessageBox.Show(Properties.Resources.OCS_OldCPUNotSimulated, Properties.Resources.OldCPUSimulator, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                    }
                                }
                            }
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            Show();
                            Refresh();
                            errorDelegate(Properties.Resources.ProcessUnableToStart);
                            throw new InvalidModeException("The Mode failed to start the Process.");
                        }

                        Application.Exit();
                        return;
                    }

                    errorDelegate(Properties.Resources.NoModeSelected);
                    throw new InvalidModeException("No Mode was used.");
                } finally {
                    modeMutex.ReleaseMutex();
                }
            }
        }

        private void DeactivateMode(TemplateElement templateElement, ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modeMutex = new Mutex(true, MODE_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modeMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstancePreventingTemplate);
                        throw new InvalidModeException("Another Mode is activating.");
                    }
                }

                try {
                    if (webBrowserMode != null) {
                        webBrowserMode.FormClosing -= webBrowserMode_FormClosing;
                        webBrowserMode.Close();
                        webBrowserMode = null;
                    }
                } finally {
                    modeMutex.ReleaseMutex();
                }
            }
        }

        private async Task ActivateModificationsAsync(TemplateElement templateElement, ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modificationsMutex = new Mutex(true, MODIFICATIONS_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modificationsMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstancePreventingTemplate);
                        throw new InvalidModificationException("Another Modification is activating.");
                    }
                }

                try {
                    //if (String.IsNullOrEmpty(ModificationsName)) {
                    //errorDelegate(Properties.Resources.CurationMissingModificationName);
                    //throw new InvalidModificationException();
                    //return;
                    //}

                    ProgressManager.CurrentGoal.Start(8);

                    try {
                        //try {
                        TemplateElement activeTemplateElement = null;

                        try {
                            activeTemplateElement = GetActiveTemplateElement(false);
                        } catch (ConfigurationErrorsException ex) {
                            // fail silently
                            LogExceptionToLauncher(ex);
                        }

                        if (activeTemplateElement != null) {
                            if (!String.IsNullOrEmpty(activeTemplateElement.Active)) {
                                throw new InvalidModificationException("The Template Element \"" + activeTemplateElement.Active + "\" is active.");
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        // throw on activation
                        if (templateElement == null) {
                            errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                            throw new InvalidTemplateException("The Template Element \"" + TemplateName + "\" is null.");
                        }

                        ModeElement modeElement = templateElement.Mode;
                        ModificationsElement modificationsElement = templateElement.Modifications;

                        if (DownloadsBeforeModificationNames == null) {
                            DownloadsBeforeModificationNames = new List<string>();
                        }

                        try {
                            if (modificationsElement.ElementInformation.IsPresent) {
                                if (modificationsElement.RunAsAdministrator) {
                                    RunAsAdministratorModification = true;
                                }

                                if (modificationsElement.DownloadsBefore.Count > 0) {
                                    ModificationsElement.DownloadBeforeElementCollection.DownloadBeforeElement downloadsBeforeElement = null;

                                    for (int i = 0; i < modificationsElement.DownloadsBefore.Count; i++) {
                                        downloadsBeforeElement = modificationsElement.DownloadsBefore.Get(i) as ModificationsElement.DownloadBeforeElementCollection.DownloadBeforeElement;

                                        if (downloadsBeforeElement == null) {
                                            throw new ConfigurationErrorsException("The Downloads Before Element (" + i + ") is null.");
                                        }

                                        DownloadsBeforeModificationNames.Add(downloadsBeforeElement.Name);
                                    }

                                    //SetModificationsElement(modificationsElement, Name);
                                }
                            }
                        } catch (ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                        }

                        if (ModificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY) {
                            ModificationsRevertMethod = MODIFICATIONS_REVERT_METHOD.REVERT_ALL;
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        try {
                            runAsAdministrator.Activate(TemplateName, RunAsAdministratorModification);
                        } catch (TaskRequiresElevationException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchAsAdministratorUser();
                        } catch (ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.EnvironmentVariables.Count > 0) {
                                try {
                                    environmentVariables.Activate(TemplateName);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch (CompatibilityLayersException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchWithCompatibilitySettings();
                                } catch (InvalidEnvironmentVariablesException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.EnvironmentVariablesProblem);
                                } catch (ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                                }
                            }
                        }

                        /*
                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement != null) {
                            if (modificationsElement.ModeTemplates.ElementInformation.IsPresent) {
                                try {
                                    modeTemplates.Activate(TemplateName, ref server, ref software, ref softwareProcessStartInfo);
                                } catch (ModeTemplatesFailedException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ModeTemplatesFailed);
                                } catch (ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (!String.IsNullOrEmpty(DownloadSourceModificationName)) {
                            try {
                                await downloadSource.Activate(TemplateName, DownloadSourceModificationName, ref software).ConfigureAwait(true);
                            } catch (DownloadFailedException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(String.Format(Properties.Resources.GameIsMissingFile, DownloadSourceModificationName));
                            } catch (ConfigurationErrorsException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                            } catch (TaskRequiresElevationException ex) {
                                LogExceptionToLauncher(ex);
                                AskLaunchAsAdministratorUser();
                            } catch (ArgumentException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(String.Format(Properties.Resources.AddressNotUnderstood, DownloadSourceModificationName));
                            }
                        }
                        */

                        ProgressManager.CurrentGoal.Steps++;

                        if (DownloadsBeforeModificationNames.Count > 0) {
                            try {
                                await downloadsBefore.ActivateAsync(TemplateName, DownloadsBeforeModificationNames).ConfigureAwait(true);
                            } catch (DownloadFailedException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, String.Join("\", \"", DownloadsBeforeModificationNames)));
                            } catch (ConfigurationErrorsException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.RegistryStates.Count > 0) {
                                try {
                                    registryState.Activate(TemplateName);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch (InvalidRegistryStateException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.RegistryStateUnableToCreate);
                                } catch (ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                                } catch (InvalidOperationException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ModificationsUnableToLoadImport);
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.SingleInstance.ElementInformation.IsPresent) {
                                try {
                                    singleInstance.Activate(TemplateName);
                                } catch (InvalidModificationException ex) {
                                    LogExceptionToLauncher(ex);
                                    throw;
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch (Exception ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.UnknownProcessCompatibilityConflict);
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.OldCPUSimulator.ElementInformation.IsPresent) {
                                try {
                                    oldCPUSimulator.Activate(TemplateName, ref softwareProcessStartInfo, out softwareIsOldCPUSimulator);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch (InvalidOldCPUSimulatorException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.OldCPUSimulatorProblem);
                                } catch (ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;
                        /*
                        } finally {
                            try {
                                LockActiveModificationsElement();
                            } catch (ConfigurationErrorsException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                            }

                            ProgressManager.CurrentGoal.Steps++;
                        }
                        */
                    } finally {
                        ProgressManager.CurrentGoal.Stop();
                    }
                } finally {
                    modificationsMutex.ReleaseMutex();
                }
            }
        }
        
        private void DeactivateModifications(ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modificationsMutex = new Mutex(true, MODIFICATIONS_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modificationsMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstancePreventingTemplate);
                        throw new InvalidModificationException("Another Modification is activating.");
                    }
                }

                try {
                    ProgressManager.CurrentGoal.Start(3);

                    try {
                        /*
                        try {
                            UnlockActiveModificationsElement();
                        } catch (ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        }

                        ProgressManager.CurrentGoal.Steps++;
                        */

                        // the modifications are deactivated in reverse order of how they're activated
                        try {
                            // this one really needs to work
                            // we can't continue if it does not
                            registryState.Deactivate(ModificationsRevertMethod);
                        } catch (TaskRequiresElevationException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchAsAdministratorUser();
                        } catch (InvalidRegistryStateException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.RegistryStateUnableToCreate);
                        } catch (ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                        } catch (InvalidOperationException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ModificationsUnableToLoadImport);
                        } catch (TimeoutException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.RegistryStateTimeout);
                        }

                        ProgressManager.CurrentGoal.Steps++;
                        
                        try {
                            environmentVariables.Deactivate(ModificationsRevertMethod);
                        } catch (TaskRequiresElevationException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchAsAdministratorUser();
                        } catch (CompatibilityLayersException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchWithCompatibilitySettings();
                        } catch (InvalidEnvironmentVariablesException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.EnvironmentVariablesProblem);
                        } catch (ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                        } catch (TimeoutException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.EnvironmentVariablesTimeout);
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        try {
                            TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

                            if (activeTemplateElement != null) {
                                activeTemplateElement.Active = ACTIVE_EXE_CONFIGURATION_NAME;
                                SetFlashpointSecurePlayerSection(ACTIVE_EXE_CONFIGURATION_NAME);
                            }
                        } catch (ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationUnableToLoad);
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    } finally {
                        ProgressManager.CurrentGoal.Stop();
                    }
                } finally {
                    modificationsMutex.ReleaseMutex();
                }
            }
        }

        private async Task StartSecurePlaybackAsync(TemplateElement templateElement) {
            // switch to synced process
            ProgressManager.Reset();
            ShowOutput(Properties.Resources.RequiredComponentsAreLoading);
            Refresh();

            try {
                await ActivateModificationsAsync(templateElement, delegate (string text) {
                    if (!ShowError(text)) {
                        Application.Exit();
                    }

                    throw new InvalidModificationException("An error occured while activating the Modification.");
                }).ConfigureAwait(true);
            } catch (InvalidModificationException ex) {
                // delegate handles error
                LogExceptionToLauncher(ex);
                return;
            } catch (OldCPUSimulatorRequiresApplicationRestartException ex) {
                // do this after all other modifications
                // Old CPU Simulator can't handle restarts
                LogExceptionToLauncher(ex);

                try {
                    AskLaunchWithOldCPUSimulator();
                } catch (InvalidModificationException ex2) {
                    LogExceptionToLauncher(ex2);
                    return;
                }
            }

            try {
                ActivateMode(templateElement, delegate (string text) {
                    ShowErrorFatal(text);
                    throw new InvalidModeException("An error occured while activating the Mode.");
                });
            } catch (InvalidModeException ex) {
                // delegate handles error
                LogExceptionToLauncher(ex);
                return;
            }
        }

        private void StopSecurePlayback(FormClosingEventArgs e, TemplateElement templateElement) {
            // only if closing...
            ProgressManager.Reset();
            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);
            Refresh();

            try {
                DeactivateMode(templateElement, delegate (string text) {
                    // I will assassinate the Cyrollan delegate myself...
                });
            } catch (InvalidModeException ex) {
                // delegate handles error
                LogExceptionToLauncher(ex);
                return;
            }

            try {
                DeactivateModifications(delegate (string text) {
                    // And God forbid I should fail, one touch of the button on my remote detonator...
                    // will be enough to end it all, obliterating Caldoria...
                    // and this foul infestation along with it!
                });
            } catch (InvalidModificationException ex) {
                // delegate handles error
                LogExceptionToLauncher(ex);
                return;
            }
        }

        private void ImportStart(object sender, EventArgs e) {
            ControlBox = false;
        }

        private void ImportStop(object sender, EventArgs e) {
            ControlBox = true;
        }
        
        private bool loaded = false;

        private void FlashpointSecurePlayer_Load(object sender, EventArgs e) {
            Text = Properties.Resources.FlashpointSecurePlayer + " " + typeof(FlashpointSecurePlayerGUI).Assembly.GetName().Version;

            ProgressManager.ProgressBar = securePlaybackProgressBar;
            ProgressManager.ProgressForm = this;

            if (oldWindowsVersion) {
                ShowErrorFatal(Properties.Resources.WindowsVersionTooOld);
                return;
            }

            // needed upon application restart to focus the new window
            BringToFront();
            Activate();
            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);
            Refresh();

            try {
                // Set Current Directory
                try {
                    Directory.SetCurrentDirectory(Application.StartupPath);
                } catch (SecurityException ex) {
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Setting the Current Directory requires elevation.");
                } catch {
                    // fail silently
                }

                // Get Arguments
                string[] args = Environment.GetCommandLineArgs();

                // throw on load
                if (args.Length < 3) {
                    throw new InvalidTemplateException("The Template Name and URL are required.");
                }

                TemplateName = args[1];
                URL = args[2];
                string arg = null;

                for (int i = 3; i < args.Length; i++) {
                    arg = args[i].ToLowerInvariant();

                    // instead of switch I use else if because C# is too lame for multiple case statements
                    if (arg == "--activex" || arg == "-ax") {
                        activeX = true;
                    } else if (arg == "--run-as-administrator" || arg == "-a") {
                        RunAsAdministratorModification = true;
                    } else if (arg == "--dev-force-delete-all") {
                        ModificationsRevertMethod = MODIFICATIONS_REVERT_METHOD.DELETE_ALL;
                    } else if (arg == "--dev-ignore-activex-control-install-failure") {
                        IgnoreActiveXControlInstallFailure = true;
                    } else if (arg == "--dev-use-flash-activex-control") {
                        UseFlashActiveXControl = true;
                    } else {
                        if (i < args.Length - 1) {
                            if (arg == "--arguments" || arg == "-args") {
                                Arguments = GetArgumentSliceFromCommandLine(Environment.CommandLine, i + 1);
                                break;
                            } else if (arg == "--download-before" || arg == "-dlb") {
                                if (DownloadsBeforeModificationNames == null) {
                                    DownloadsBeforeModificationNames = new List<string>();
                                }

                                DownloadsBeforeModificationNames.Add(args[i + 1]);
                                i++;
                                continue;
                            }
                        }

                        Arguments = GetArgumentSliceFromCommandLine(Environment.CommandLine, i);
                        break;
                    }
                }

                if (ModificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                    if (MessageBox.Show(Properties.Resources.ForceDeleteAllWarning, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No) {
                        ModificationsRevertMethod = MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY;
                        Application.Exit();
                        return;
                    }
                }

                if (IgnoreActiveXControlInstallFailure) {
                    MessageBox.Show(Properties.Resources.IgnoreActiveXControlInstallFailureWarning, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (UseFlashActiveXControl) {
                    MessageBox.Show(Properties.Resources.UseFlashActiveXControlWarning, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                // ignore force deleting all during crash recovery
                MODIFICATIONS_REVERT_METHOD modificationsRevertMethod = ModificationsRevertMethod;
                ModificationsRevertMethod = MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY;

                // this is where we do crash recovery
                // we attempt to deactivate whatever was in the config file first
                // it's important this succeeds
                try {
                    DeactivateModifications(delegate (string text) {
                        ShowErrorFatal(text);
                        throw new InvalidModificationException("An error occured while deactivating the Modification.");
                    });
                } catch (InvalidModificationException ex) {
                    // delegate handles error
                    LogExceptionToLauncher(ex);
                    return;
                }

                ModificationsRevertMethod = modificationsRevertMethod;
            } catch (InvalidTemplateException ex) {
                // catch on load
                LogExceptionToLauncher(ex);
                ShowNoGameSelected();
                return;
            } catch (TaskRequiresElevationException ex) {
                LogExceptionToLauncher(ex);

                try {
                    AskLaunchAsAdministratorUser();
                } catch (InvalidModificationException ex2) {
                    LogExceptionToLauncher(ex2);
                    return;
                }
            }

            loaded = true;
        }

        private async void FlashpointSecurePlayer_Shown(object sender, EventArgs e) {
            try {
                if (!loaded) {
                    return;
                }

                //Show();
                ShowOutput(Properties.Resources.GameDownloading);
                Refresh();

                // get Template Element
                TemplateElement templateElement = null;

                try {
                    if (String.IsNullOrEmpty(TemplateName)) {
                        throw new InvalidTemplateException("The Template Name may not be null or empty.");
                    }
                } catch (InvalidTemplateException ex) {
                    LogExceptionToLauncher(ex);
                    ShowNoGameSelected();
                    return;
                }
                
                if (activeX) {
                    // ActiveX Import
                    try {
                        await ImportActiveXAsync(delegate (string text) {
                            ShowErrorFatal(text);
                            throw new ActiveXImportFailedException("An error occured while activating the ActiveX Import.");
                        });
                    } catch (InvalidTemplateException ex) {
                        // no need to exit here, error shown in interface
                        LogExceptionToLauncher(ex);
                        //Application.Exit();
                    } catch (ActiveXImportFailedException ex) {
                        // no need to exit here, error shown in interface
                        LogExceptionToLauncher(ex);
                        //Application.Exit();
                    }
                    return;
                }

                await DownloadFlashpointSecurePlayerSectionAsync(TemplateName).ConfigureAwait(true);

                // get template element on start
                // throw on start
                try {
                    templateElement = GetTemplateElement(false, TemplateName);
                } catch (ConfigurationErrorsException ex) {
                    LogExceptionToLauncher(ex);
                    ShowErrorFatal(Properties.Resources.ConfigurationUnableToLoad);
                    return;
                }

                if (templateElement == null) {
                    ShowErrorFatal(Properties.Resources.ConfigurationUnableToLoad);
                    return;
                }

                // get HTDOCS File/HTDOCS File Directory (in Software Mode)
                string htdocsFile = null;
                string htdocsFileDirectory = null;

                if (templateElement.Mode.Name == ModeElement.NAME.SOFTWARE) {
                    try {
                        string htdocsFilePath = (await GetHTDOCSFilePath(URL).ConfigureAwait(true)).ToString();

                        try {
                            htdocsFile = Path.GetFileName(htdocsFilePath);
                        } catch (ArgumentException ex) {
                            // fail silently?
                            LogExceptionToLauncher(ex);
                        }

                        // empty ONLY, not null
                        if (htdocsFile == String.Empty) {
                            // path is to directory
                            if (INDEX_EXTENSIONS.Any()) {
                                htdocsFile = "index." + INDEX_EXTENSIONS.First();
                            }
                        }

                        string fullHTDOCSFilePath = null;

                        try {
                            fullHTDOCSFilePath = Path.GetFullPath(htdocsFilePath);
                        } catch (SecurityException ex) {
                            LogExceptionToLauncher(ex);
                            throw new TaskRequiresElevationException("Getting the Full Path to \"" + htdocsFilePath + "\" requires elevation.");
                        } catch (PathTooLongException ex) {
                            LogExceptionToLauncher(ex);
                            throw new ArgumentException("The path is too long to \"" + htdocsFilePath + "\".");
                        } catch (NotSupportedException ex) {
                            LogExceptionToLauncher(ex);
                            throw new ArgumentException("The path \"" + htdocsFilePath + "\" is not supported.");
                        }

                        if (fullHTDOCSFilePath == null) {
                            fullHTDOCSFilePath = String.Empty;
                        }

                        try {
                            htdocsFileDirectory = Path.GetDirectoryName(fullHTDOCSFilePath);
                        } catch (ArgumentException ex) {
                            // fail silently?
                            LogExceptionToLauncher(ex);
                        }

                        if (!String.IsNullOrEmpty(htdocsFile) && !String.IsNullOrEmpty(htdocsFileDirectory)) {
                            htdocsFile = htdocsFileDirectory + "\\" + htdocsFile;
                        }
                    } catch (DownloadFailedException ex) {
                        // fail silently
                        LogExceptionToLauncher(ex);
                    }
                }

                // set Environment Variables
                try {
                    // required
                    Environment.SetEnvironmentVariable(FP_STARTUP_PATH, Application.StartupPath, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable(FP_URL, URL, EnvironmentVariableTarget.Process);
                    // optional
                    Environment.SetEnvironmentVariable(FP_ARGUMENTS, String.IsNullOrEmpty(Arguments) ? " " : Arguments, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable(FP_HTDOCS_FILE, String.IsNullOrEmpty(htdocsFile) ? " " : htdocsFile, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable(FP_HTDOCS_FILE_DIR, String.IsNullOrEmpty(htdocsFileDirectory) ? " " : htdocsFileDirectory, EnvironmentVariableTarget.Process);
                } catch (SecurityException ex) {
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Setting the Environment Variables requires elevation.");
                }

                ProgressManager.ShowOutput();

                // Start Secure Playback
                try {
                    await StartSecurePlaybackAsync(templateElement).ConfigureAwait(false);
                } catch (InvalidModeException ex) {
                    // no need to exit here, error shown in interface
                    LogExceptionToLauncher(ex);
                    //Application.Exit();
                    return;
                } catch (InvalidModificationException ex) {
                    // no need to exit here, error shown in interface
                    LogExceptionToLauncher(ex);
                    //Application.Exit();
                    return;
                } catch (InvalidTemplateException ex) {
                    LogExceptionToLauncher(ex);
                    ShowNoGameSelected();
                    return;
                }
            } catch (TaskRequiresElevationException ex) {
                LogExceptionToLauncher(ex);

                try {
                    AskLaunchAsAdministratorUser();
                } catch (InvalidModificationException ex2) {
                    LogExceptionToLauncher(ex2);
                    return;
                }
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                Application.Exit();
            }
        }

        private void FlashpointSecurePlayer_FormClosing(object sender, FormClosingEventArgs e) {
            // don't close if there is no close button
            e.Cancel = !ControlBox;

            // do stuff, but not if restarting
            // not too important for this to work, we can reset it on restart
            if (e.Cancel) {
                return;
            }

            if (applicationMutex != null) {
                try {
                    // don't show, we don't want two windows at once on restart
                    //Show();
                    ProgressManager.ShowOutput();

                    // get template element on stop
                    TemplateElement templateElement = null;

                    try {
                        templateElement = GetTemplateElement(false, TemplateName);
                    } catch (ConfigurationErrorsException ex) {
                        LogExceptionToLauncher(ex);
                        return;
                    }

                    if (templateElement == null) {
                        return;
                    }

                    try {
                        StopSecurePlayback(e, templateElement);
                    } catch (ActiveXImportFailedException ex) {
                        // fail silently
                        LogExceptionToLauncher(ex);
                    } catch (InvalidModeException ex) {
                        // fail silently
                        LogExceptionToLauncher(ex);
                    } catch (InvalidModificationException ex) {
                        // fail silently
                        LogExceptionToLauncher(ex);
                    } catch (InvalidTemplateException ex) {
                        // fail silently
                        LogExceptionToLauncher(ex);
                    }
                } finally {
                    applicationMutex.ReleaseMutex();
                    applicationMutex.Close();
                    applicationMutex = null;
                }
            }
        }

        private void webBrowserMode_FormClosing(object sender, FormClosingEventArgs e) {
            // stop form closing recursion
            if (webBrowserMode != null) {
                webBrowserMode.FormClosing -= webBrowserMode_FormClosing;
                webBrowserMode = null;
            }

            // Set Current Directory
            try {
                Directory.SetCurrentDirectory(Application.StartupPath);
            } catch {
                // fail silently
            }

            // this should not cause an exception
            // if it does, it means there is an infinite closing loop
            // (a form is closing a form which is closing a form, and so on)
            // you can debug infinite closing loops by setting breakpoints
            // on all Form.Close functions
            try {
                Show();
                Refresh();
                Application.Exit();
            } catch (InvalidOperationException ex) {
                // IT IS VERY IMPORTANT THIS SHOULD NEVER HAPPEN!
                LogExceptionToLauncher(ex);
                Environment.Exit(-1);
            }
        }
    }
}

// "As for me and my household, we will serve the Lord." - Joshua 24:15