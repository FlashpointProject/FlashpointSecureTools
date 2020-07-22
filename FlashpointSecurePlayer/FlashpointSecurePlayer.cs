using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
    public partial class FlashpointSecurePlayer : Form {
        private const string APPLICATION_MUTEX_NAME = "Flashpoint Secure Player";
        private const string MODE_MUTEX_NAME = "Flashpoint Secure Player Mode";
        private const string MODIFICATIONS_MUTEX_NAME = "Flashpoint Secure Player Modifications";
        private const string FLASHPOINT_LAUNCHER_PARENT_PROCESS_FILE_NAME = "CMD.EXE";
        private const string FLASHPOINT_LAUNCHER_PROCESS_NAME = "FLASHPOINT";

        private Mutex applicationMutex = null;

        private readonly RunAsAdministrator runAsAdministrator;
        private readonly EnvironmentVariables environmentVariables;
        private readonly DownloadsBefore downloadsBefore;
        private readonly RegistryBackups registryBackup;
        private readonly SingleInstance singleInstance;
        private readonly OldCPUSimulator oldCPUSimulator;

        private bool activeX = false;
        Server serverForm = null;
        private ProcessStartInfo softwareProcessStartInfo = null;
        private bool softwareIsOldCPUSimulator = false;

        private string URL { get; set; } = String.Empty;
        private string TemplateName { get; set; } = ACTIVE_EXE_CONFIGURATION_NAME;
        private string Arguments { get; set; } = String.Empty;
        private bool RunAsAdministratorModification { get; set; } = false;
        private List<string> DownloadsBeforeModificationNames { get; set; } = null;

        private delegate void ErrorDelegate(string text);

        public FlashpointSecurePlayer() {
            InitializeComponent();
            runAsAdministrator = new RunAsAdministrator(this);
            environmentVariables = new EnvironmentVariables(this);
            downloadsBefore = new DownloadsBefore(this);
            registryBackup = new RegistryBackups(this);
            singleInstance = new SingleInstance(this);
            oldCPUSimulator = new OldCPUSimulator(this);
        }

        private void ShowOutput(string errorLabelText) {
            ProgressManager.ShowOutput();
            this.errorLabel.Text = errorLabelText;
        }

        private void ShowError(string errorLabelText) {
            ProgressManager.ShowError();
            this.errorLabel.Text = errorLabelText;
        }

        private void ShowNoGameSelected() {
            // detect if this application was started by Flashpoint Launcher
            // none of this is strictly necessary, I'm just trying
            // to reduce the amount of stupid in the #help-me-please channel
            //ShowError(Properties.Resources.GameNotCuratedCorrectly);
            string text = Properties.Resources.NoGameSelected;
            Process parentProcess = GetParentProcess();
            string parentProcessFileName = null;

            if (parentProcess != null) {
                try {
                    parentProcessFileName = Path.GetFileName(GetProcessName(parentProcess)).ToUpperInvariant();
                } catch {
                    // Fail silently.
                }
            }

            if (parentProcessFileName != FLASHPOINT_LAUNCHER_PARENT_PROCESS_FILE_NAME) {
                text += " " + Properties.Resources.UseFlashpointLauncher;
                Process[] processesByName;

                // detect if Flashpoint Launcher is open
                // we only show this message if it isn't open yet
                // because we don't want to confuse n00bs into
                // opening two instances of it
                try {
                    processesByName = Process.GetProcessesByName(FLASHPOINT_LAUNCHER_PROCESS_NAME);
                } catch (InvalidOperationException) {
                    // only occurs Windows XP which is unsupported
                    ProgressManager.ShowError();
                    MessageBox.Show(Properties.Resources.WindowsVersionTooOld, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }

                if (processesByName.Length <= 0) {
                    text += " " + Properties.Resources.OpenFlashpointLauncher;
                }
            }

            ProgressManager.ShowError();
            MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        private void AskLaunch(string applicationRestartMessage) {
            ProgressManager.ShowOutput();
            DialogResult dialogResult = MessageBox.Show(String.Format(Properties.Resources.LaunchGame, applicationRestartMessage), Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.YesNo, MessageBoxIcon.None);

            if (dialogResult == DialogResult.No) {
                Application.Exit();
                throw new InvalidModificationException("The operation was aborted by the user.");
            }
        }

        private void AskLaunchAsAdministratorUser() {
            if (!TestLaunchedAsAdministratorUser()) {
                // popup message box and restart program here
                // https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.messagebox?view=netframework-4.8
                /*
                 this dialog is not purely here for aesthetic/politeness reasons
                 it's a stopgap to prevent the program from reloading infinitely
                 in case the TestProcessRunningAsAdministrator function somehow fails
                 you might say "but the UAC dialog would prevent it reloading unstoppably"
                 to which I say "yes, but some very stupid people turn UAC off"
                 then there'd be no dialog except this one - and I don't want
                 the program to enter an infinite restart loop
                 */
                AskLaunch(Properties.Resources.AsAdministratorUser);

                try {
                    RestartApplication(true, this, ref applicationMutex);
                    throw new InvalidModificationException("The Modification does not work unless run as Administrator User.");
                } catch (ApplicationRestartRequiredException ex) {
                    LogExceptionToLauncher(ex);
                    throw new InvalidModificationException("The Modification does not work unless run as Administrator User and the application failed to restart.");
                }
            }

            // we're already running as admin?
            ShowError(String.Format(Properties.Resources.GameFailedLaunch, Properties.Resources.AsAdministratorUser));
            throw new InvalidModificationException("The Modification failed to run as Administrator User.");
        }

        private void AskLaunchWithCompatibilitySettings() {
            AskLaunch(Properties.Resources.WithCompatibilitySettings);

            try {
                RestartApplication(false, this, ref applicationMutex);
                throw new InvalidModificationException("The Modification does not work unless run with Compatibility Settings.");
            } catch (ApplicationRestartRequiredException ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidModificationException("The Modification does not work unless run with Compatibility Settings and the application failed to restart.");
            }
        }

        private void AskLaunchWithOldCPUSimulator() {
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

            Process parentProcess = GetParentProcess();
            string parentProcessFileName = null;

            if (parentProcess != null) {
                try {
                    parentProcessFileName = Path.GetFileName(GetProcessName(parentProcess)).ToUpperInvariant();
                } catch {
                    ProgressManager.ShowError();
                    MessageBox.Show(Properties.Resources.ProcessFailedStart, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    throw new InvalidModificationException("The Modification does not work unless run with Old CPU Simulator which failed to get the parent process EXE name.");
                }
            }

            if (parentProcessFileName == OLD_CPU_SIMULATOR_PARENT_PROCESS_FILE_NAME) {
                return;
            }

            AskLaunch(Properties.Resources.WithOldCPUSimulator);

            // Old CPU Simulator needs to be on top, not us
            string fullPath = Path.GetFullPath(OLD_CPU_SIMULATOR_PATH);

            ProcessStartInfo processStartInfo = new ProcessStartInfo {
                FileName = fullPath,
                Arguments = GetOldCPUSimulatorProcessStartInfoArguments(modificationsElement.OldCPUSimulator, Environment.CommandLine),
                WorkingDirectory = Environment.CurrentDirectory
            };

            HideWindow(ref processStartInfo);

            try {
                RestartApplication(false, this, ref applicationMutex, processStartInfo);
                throw new InvalidModificationException("The Modification does not work unless run with Old CPU Simulator.");
            } catch (ApplicationRestartRequiredException ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidModificationException("The Modification does not work unless run with Old CPU Simulator and the application failed to restart.");
            }
        }

        private async Task ImportActiveX(ErrorDelegate errorDelegate) {
            if (String.IsNullOrEmpty(TemplateName)) {
                errorDelegate(Properties.Resources.CurationMissingTemplateName);
                throw new InvalidTemplateException("The Template Name may not be the Active Template Name.");
            }

            // this requires admin
            if (!TestLaunchedAsAdministratorUser()) {
                AskLaunchAsAdministratorUser();
            }

            ProgressManager.Reset();
            ShowOutput(Properties.Resources.RegistryBackupInProgress);
            ProgressManager.CurrentGoal.Start(6);

            try {
                ActiveXControl activeXControl = null;

                try {
                    activeXControl = new ActiveXControl(TemplateName);
                } catch (DllNotFoundException ex) {
                    LogExceptionToLauncher(ex);
                    errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, TemplateName));
                    throw new ActiveXImportFailedException("The ActiveX Import failed because the DLL was not found.");
                } catch (InvalidActiveXControlException ex) {
                    LogExceptionToLauncher(ex);
                    errorDelegate(Properties.Resources.GameNotActiveXControl);
                    throw new ActiveXImportFailedException("The ActiveX Import failed because the DLL is not an ActiveX Control.");
                }

                GetBinaryType(TemplateName, out BINARY_TYPE binaryType);

                // first, we install the control without a registry backup running
                // this is so we can be sure we can uninstall the control
                try {
                    activeXControl.Install();
                } catch (Win32Exception ex) {
                    LogExceptionToLauncher(ex);
                    errorDelegate(Properties.Resources.ActiveXControlInstallFailed);
                    throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to install.");
                }

                ProgressManager.CurrentGoal.Steps++;

                // next, uninstall the control
                // in case it was already installed before this whole process
                // this is to ensure an existing install
                // doesn't interfere with our registry backup results
                try {
                    activeXControl.Uninstall();
                } catch (Win32Exception ex) {
                    LogExceptionToLauncher(ex);
                    errorDelegate(Properties.Resources.ActiveXControlUninstallFailed);
                    throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to uninstall.");
                }

                ProgressManager.CurrentGoal.Steps++;

                try {
                    try {
                        await registryBackup.StartImportAsync(TemplateName, binaryType).ConfigureAwait(true);
                    } catch (RegistryBackupFailedException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.RegistryBackupFailed);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the Registry Backup failed when the ActiveX Import was started.");
                    } catch (System.Configuration.ConfigurationErrorsException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the configuration failed to load when the ActiveX Import was started.");
                    } catch (InvalidModificationException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.GameNotCuratedCorrectly);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the Modification is invalid.");
                    } catch (TaskRequiresElevationException ex) {
                        LogExceptionToLauncher(ex);
                        // we're already running as admin?
                        errorDelegate(String.Format(Properties.Resources.GameFailedLaunch, Properties.Resources.AsAdministratorUser));
                        throw new ActiveXImportFailedException("The ActiveX Import failed running as Administrator User.");
                    } catch (InvalidOperationException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.RegistryBackupAlreadyInProgress);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because a Registry Backup is already in progress.");
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    // a registry backup is running, install the control
                    try {
                        activeXControl.Install();
                    } catch (Win32Exception ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.ActiveXControlInstallFailed);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to install.");
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    try {
                        await registryBackup.StopImportAsync().ConfigureAwait(true);
                    } catch (RegistryBackupFailedException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.RegistryBackupFailed);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the Registry Backup failed when the ActiveX Import was stopped.");
                    } catch (System.Configuration.ConfigurationErrorsException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the configuration failed to load when the ActiveX Import was stopped.");
                    } catch (InvalidOperationException ex) {
                        LogExceptionToLauncher(ex);
                        errorDelegate(Properties.Resources.RegistryBackupNotInProgress);
                        throw new ActiveXImportFailedException("The ActiveX Import failed because the Registry Backup never started.");
                    }
                } finally {
                    // we do this to ensure the user can exit in the case of an error
                    ControlBox = true;
                }

                ProgressManager.CurrentGoal.Steps++;

                // the registry backup is stopped, uninstall the control
                // this will leave the control uninstalled on the system
                // there is no way to tell if it was installed before
                // (which is the point of creating the backup so we can)
                try {
                    activeXControl.Uninstall();
                } catch (Win32Exception ex) {
                    LogExceptionToLauncher(ex);
                    errorDelegate(Properties.Resources.ActiveXControlUninstallFailed);
                    throw new ActiveXImportFailedException("The ActiveX Import failed because the ActiveX Control failed to uninstall.");
                }

                ProgressManager.CurrentGoal.Steps++;
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }

            ShowOutput(Properties.Resources.RegistryBackupWasSuccessful);
        }

        private void ActivateMode(TemplateElement templateElement, ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modeMutex = new Mutex(true, MODE_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modeMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstanceCausingInterference);
                        throw new InvalidModeException("Another Mode is activating.");
                    }
                }

                try {
                    ModeElement modeElement = templateElement.Mode;

                    switch (modeElement.Name) {
                        case ModeElement.NAME.WEB_BROWSER:
                        Uri webBrowserURL = null;

                        try {
                            webBrowserURL = new Uri(URL);
                        } catch {
                            errorDelegate(String.Format(Properties.Resources.AddressNotUnderstood, URL));
                            throw new InvalidModeException("The address (" + URL + ") was not understood by the Mode.");
                        }

                        serverForm = new Server(webBrowserURL) {
                            WindowState = FormWindowState.Maximized
                        };

                        serverForm.FormClosing += serverForm_FormClosing;
                        Hide();
                        serverForm.Show();
                        return;
                        case ModeElement.NAME.SOFTWARE:
                        try {
                            string commandLineExpanded = Environment.ExpandEnvironmentVariables(modeElement.CommandLine);
                            string[] argv = CommandLineToArgv(commandLineExpanded, out int argc);

                            if (argc <= 0) {
                                throw new IndexOutOfRangeException("The command line argument is out of range.");
                            }

                            string fullPath = Path.GetFullPath(argv[0]);

                            if (softwareProcessStartInfo == null) {
                                softwareProcessStartInfo = new ProcessStartInfo();
                            }

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.FileName)) {
                                softwareProcessStartInfo.FileName = fullPath;
                            }

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.Arguments)) {
                                softwareProcessStartInfo.Arguments = GetCommandLineArgumentRange(commandLineExpanded, 1, -1);
                            }

                            softwareProcessStartInfo.ErrorDialog = false;

                            if (modeElement.HideWindow.GetValueOrDefault()) {
                                HideWindow(ref softwareProcessStartInfo);
                            }

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.WorkingDirectory)) {
                                if (String.IsNullOrEmpty(modeElement.WorkingDirectory)) {
                                    softwareProcessStartInfo.WorkingDirectory = Path.GetDirectoryName(fullPath);
                                } else {
                                    try {
                                        SetWorkingDirectory(ref softwareProcessStartInfo, Environment.ExpandEnvironmentVariables(modeElement.WorkingDirectory));
                                    } catch (ArgumentNullException) {
                                        throw new InvalidModeException("The Mode failed because the Working Directory cannot be null.");
                                    }
                                }
                            }

                            Process softwareProcess = Process.Start(softwareProcessStartInfo);

                            try {
                                ProcessSync.Start(softwareProcess);
                            } catch (JobObjectException ex) {
                                LogExceptionToLauncher(ex);
                                // popup message box and blow up
                                errorDelegate(Properties.Resources.JobObjectNotCreated);
                                softwareProcess.Kill();
                                Environment.Exit(-1);
                                throw new InvalidModeException("The Mode failed to create a Job Object.");
                            }

                            Hide();

                            if (!softwareProcess.HasExited) {
                                softwareProcess.WaitForExit();
                            }

                            Show();

                            string softwareProcessStandardError = null;
                            string softwareProcessStandardOutput = null;

                            if (softwareProcessStartInfo.RedirectStandardError) {
                                softwareProcessStandardError = softwareProcess.StandardError.ReadToEnd();
                            }

                            if (softwareProcessStartInfo.RedirectStandardOutput) {
                                softwareProcessStandardOutput = softwareProcess.StandardOutput.ReadToEnd();
                            }

                            if (softwareIsOldCPUSimulator) {
                                switch (softwareProcess.ExitCode) {
                                    case 0:
                                    break;
                                    case -1:
                                    if (!String.IsNullOrEmpty(softwareProcessStandardError)) {
                                        string[] lastSoftwareProcessStandardErrors = softwareProcessStandardError.Split('\n');
                                        string lastSoftwareProcessStandardError = null;

                                        if (lastSoftwareProcessStandardErrors.Length > 1) {
                                            lastSoftwareProcessStandardError = lastSoftwareProcessStandardErrors[lastSoftwareProcessStandardErrors.Length - 2];
                                        }

                                        if (!String.IsNullOrEmpty(lastSoftwareProcessStandardError)) {
                                            MessageBox.Show(lastSoftwareProcessStandardError);
                                        }
                                    }
                                    break;
                                    case -2:
                                    MessageBox.Show("You cannot run multiple instances of Old CPU Simulator.");
                                    break;
                                    case -3:
                                    MessageBox.Show("Failed to Create New String");
                                    break;
                                    case -4:
                                    MessageBox.Show("Failed to Set String");
                                    break;
                                    default:
                                    MessageBox.Show("Failed to Simulate Old CPU");
                                    break;
                                }
                            }
                        } catch {
                            Show();
                            errorDelegate(Properties.Resources.ProcessFailedStart);
                            throw new InvalidModeException("The Mode failed to start the Process.");
                        }

                        Application.Exit();
                        return;
                    }

                    ProgressManager.ShowError();
                    MessageBox.Show(Properties.Resources.NoModeSelected, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        errorDelegate(Properties.Resources.AnotherInstanceCausingInterference);
                        throw new InvalidModeException("Another Mode is activating.");
                    }
                }

                try {
                    if (serverForm != null) {
                        serverForm.FormClosing -= serverForm_FormClosing;
                        serverForm.Close();
                        serverForm = null;
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
                        errorDelegate(Properties.Resources.AnotherInstanceCausingInterference);
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
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            // Fail silently.
                        }

                        if (activeTemplateElement != null) {
                            if (!String.IsNullOrEmpty(activeTemplateElement.Active)) {
                                throw new InvalidModificationException("The Modifications Element (" + activeTemplateElement.Active + ") is active.");
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        // throw on activation
                        if (templateElement == null) {
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                            throw new InvalidTemplateException("The Template Element " + TemplateName + " is null.");
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

                                    for (int i = 0;i < modificationsElement.DownloadsBefore.Count;i++) {
                                        downloadsBeforeElement = modificationsElement.DownloadsBefore.Get(i) as ModificationsElement.DownloadBeforeElementCollection.DownloadBeforeElement;

                                        if (downloadsBeforeElement == null) {
                                            throw new System.Configuration.ConfigurationErrorsException("The Downloads Before Element (" + i + ") is null.");
                                        }

                                        DownloadsBeforeModificationNames.Add(downloadsBeforeElement.Name);
                                    }

                                    //SetModificationsElement(modificationsElement, Name);
                                }
                            }
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        try {
                            runAsAdministrator.Activate(TemplateName, RunAsAdministratorModification);
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        } catch (TaskRequiresElevationException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchAsAdministratorUser();
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.EnvironmentVariables.Count > 0) {
                                try {
                                    environmentVariables.Activate(TemplateName);
                                } catch (EnvironmentVariablesFailedException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.EnvironmentVariablesFailed);
                                } catch (System.Configuration.ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch (CompatibilityLayersException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchWithCompatibilitySettings();
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
                                } catch (System.Configuration.ConfigurationErrorsException ex) {
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
                                errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, DownloadSourceModificationName));
                            } catch (System.Configuration.ConfigurationErrorsException ex) {
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
                                errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, String.Join(", ", DownloadsBeforeModificationNames)));
                            } catch (System.Configuration.ConfigurationErrorsException ex) {
                                LogExceptionToLauncher(ex);
                                errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.RegistryBackups.Count > 0) {
                                try {
                                    registryBackup.Activate(TemplateName);
                                } catch (RegistryBackupFailedException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.RegistryBackupFailed);
                                } catch (System.Configuration.ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch (ArgumentException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.GameIsMissingFiles);
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
                                    throw ex;
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                } catch {
                                    errorDelegate(Properties.Resources.UnknownProcessCompatibilityConflict);
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        if (modificationsElement.ElementInformation.IsPresent) {
                            if (modificationsElement.OldCPUSimulator.ElementInformation.IsPresent) {
                                try {
                                    oldCPUSimulator.Activate(TemplateName, ref softwareProcessStartInfo, out softwareIsOldCPUSimulator);
                                } catch (OldCPUSimulatorFailedException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.OldCPUSimulatorFailed);
                                } catch (System.Configuration.ConfigurationErrorsException ex) {
                                    LogExceptionToLauncher(ex);
                                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                                } catch (TaskRequiresElevationException ex) {
                                    LogExceptionToLauncher(ex);
                                    AskLaunchAsAdministratorUser();
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;
                        /*
                        } finally {
                            try {
                                LockActiveModificationsElement();
                            } catch (System.Configuration.ConfigurationErrorsException ex) {
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

        private async Task DeactivateModificationsAsync(ErrorDelegate errorDelegate) {
            bool createdNew = false;

            using (Mutex modificationsMutex = new Mutex(true, MODIFICATIONS_MUTEX_NAME, out createdNew)) {
                if (!createdNew) {
                    if (!modificationsMutex.WaitOne()) {
                        errorDelegate(Properties.Resources.AnotherInstanceCausingInterference);
                        throw new InvalidModificationException("Another Modification is activating.");
                    }
                }

                try {
                    ProgressManager.CurrentGoal.Start(2);

                    try {
                        /*
                        try {
                            UnlockActiveModificationsElement();
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        }

                        ProgressManager.CurrentGoal.Steps++;
                        */

                        // the modifications are deactivated in reverse order of how they're activated
                        try {
                            // this one really needs to work
                            // we can't continue if it does not
                            registryBackup.Deactivate();
                        } catch (RegistryBackupFailedException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.RegistryBackupFailed);
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        } catch (TaskRequiresElevationException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchAsAdministratorUser();
                        } catch (ArgumentException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.GameIsMissingFiles);
                        }

                        ProgressManager.CurrentGoal.Steps++;
                        
                        try {
                            environmentVariables.Deactivate();
                        } catch (EnvironmentVariablesFailedException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.EnvironmentVariablesFailed);
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        } catch (TaskRequiresElevationException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchAsAdministratorUser();
                        } catch (CompatibilityLayersException ex) {
                            LogExceptionToLauncher(ex);
                            AskLaunchWithCompatibilitySettings();
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        try {
                            TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

                            if (activeTemplateElement != null) {
                                activeTemplateElement.Active = ACTIVE_EXE_CONFIGURATION_NAME;
                                SetFlashpointSecurePlayerSection(ACTIVE_EXE_CONFIGURATION_NAME);
                            }
                        } catch (System.Configuration.ConfigurationErrorsException ex) {
                            LogExceptionToLauncher(ex);
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
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

        private async Task StartSecurePlayback(TemplateElement templateElement) {
            if (activeX) {
                // ActiveX Import
                await ImportActiveX(delegate (string text) {
                    if (text.IndexOf("\n") == -1) {
                        ShowError(text);
                    } else {
                        ProgressManager.ShowError();
                        MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    }
                    throw new ActiveXImportFailedException("An error occured while activating the ActiveX Import.");
                });
                return;
            }

            // switch to synced process
            ProgressManager.Reset();
            ShowOutput(Properties.Resources.RequiredComponentsAreLoading);

            try {
                await ActivateModificationsAsync(templateElement, delegate (string text) {
                    if (text.IndexOf("\n") == -1) {
                        ShowError(text);
                    } else {
                        ProgressManager.ShowError();
                        MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                    }
                    throw new InvalidModificationException("An error occured while activating the Modification.");
                }).ConfigureAwait(true);
            } catch (InvalidModificationException ex) {
                LogExceptionToLauncher(ex);
                // delegate handles error
                return;
            } catch (OldCPUSimulatorRequiresApplicationRestartException ex) {
                LogExceptionToLauncher(ex);
                // do this after all other modifications
                // Old CPU Simulator can't handle restarts
                try {
                    AskLaunchWithOldCPUSimulator();
                } catch (InvalidModificationException) {
                    return;
                }
            }

            try {
                ActivateMode(templateElement, delegate (string text) {
                    ProgressManager.ShowError();
                    MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    throw new InvalidModeException("An error occured while activating the Mode.");
                });
            } catch (InvalidModeException ex) {
                LogExceptionToLauncher(ex);
                // delegate handles error
                return;
            }
        }

        private async Task StopSecurePlayback(FormClosingEventArgs e, TemplateElement templateElement) {
            // only if closing...
            ProgressManager.Reset();
            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);
            
            try {
                DeactivateMode(templateElement, delegate (string text) {
                    // I will assassinate the Cyrollan delegate myself...
                });
            } catch (InvalidModeException ex) {
                LogExceptionToLauncher(ex);
                // delegate handles error
                return;
            }

            try {
                await DeactivateModificationsAsync(delegate (string text) {
                    // And God forbid I should fail, one touch of the button on my remote detonator...
                    // will be enough to end it all, obliterating Caldoria...
                    // and this foul infestation along with it!
                }).ConfigureAwait(false);
            } catch (InvalidModificationException ex) {
                LogExceptionToLauncher(ex);
                // delegate handles error
                return;
            }
        }

        private async void FlashpointSecurePlayer_Load(object sender, EventArgs e) {
            // default to false in case of error
            bool createdNew = false;

            // test multiple instances
            try {
                // signals the Mutex if it has not been
                applicationMutex = new Mutex(true, APPLICATION_MUTEX_NAME, out createdNew);

                if (!createdNew) {
                    // multiple instances open, blow up immediately
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

            ProgressManager.ProgressBar = securePlaybackProgressBar;
            string windowsVersionName = GetWindowsVersionName(false, false, false);

            if (windowsVersionName != "Windows 7" &&
                windowsVersionName != "Windows Server 2008 R2" &&
                windowsVersionName != "Windows 8" &&
                windowsVersionName != "Windows Server 2012" &&
                windowsVersionName != "Windows 8.1" &&
                windowsVersionName != "Windows Server 2012 R2" &&
                windowsVersionName != "Windows 10" &&
                windowsVersionName != "Windows Server 2016" &&
                windowsVersionName != "Windows Server 2019") {
                ProgressManager.ShowError();
                MessageBox.Show(Properties.Resources.WindowsVersionTooOld, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            // needed upon application restart to focus the new window
            BringToFront();
            Activate();
            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);

            try {
                // Set Current Directory
                try {
                    Directory.SetCurrentDirectory(Application.StartupPath);
                } catch (System.Security.SecurityException ex) {
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Setting the Current Directory requires elevation.");
                } catch {
                    // Fail silently.
                }

                // Get Arguments
                string[] args = Environment.GetCommandLineArgs();

                // throw on load
                if (args.Length < 3) {
                    throw new InvalidTemplateException("No Template was used.");
                }

                URL = args[1];
                TemplateName = args[2];
                string arg = null;

                for (int i = 3;i < args.Length;i++) {
                    arg = args[i].ToLowerInvariant();

                    // instead of switch I use else if because C# is too lame for multiple case statements
                    if (arg == "--activex" || arg == "-ax") {
                        activeX = true;
                    } else if (arg == "--run-as-administrator" || arg == "-a") {
                        RunAsAdministratorModification = true;
                    } else {
                        if (i < args.Length - 1) {
                            if (arg == "--arguments" || arg == "-args") {
                                Arguments = GetCommandLineArgumentRange(Environment.CommandLine, i + 1, -1);
                                break;
                            } else if (arg == "--download-before" || arg == "-dlb") {
                                if (DownloadsBeforeModificationNames == null) {
                                    DownloadsBeforeModificationNames = new List<string>();
                                }

                                DownloadsBeforeModificationNames.Add(args[i + 1]);
                                i++;
                            }
                        }
                    }
                }

                // this is where we do crash recovery
                // we attempt to deactivate whatever was in the config file first
                // it's important this succeeds
                try {
                    await DeactivateModificationsAsync(delegate (string text) {
                        ProgressManager.ShowError();
                        MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                        throw new InvalidModificationException("An error occured while deactivating the Modification.");
                    }).ConfigureAwait(false);
                } catch (InvalidModificationException ex) {
                    LogExceptionToLauncher(ex);
                    // can't proceed since we can't activate without deactivating first
                    Application.Exit();
                    return;
                }
            } catch (InvalidTemplateException ex) {
                LogExceptionToLauncher(ex);
                // catch on load
                ShowNoGameSelected();
                return;
            } catch (TaskRequiresElevationException ex) {
                LogExceptionToLauncher(ex);

                try {
                    AskLaunchAsAdministratorUser();
                } catch (InvalidModificationException) {
                    Application.Exit();
                    return;
                }
            }
        }

        private async void FlashpointSecurePlayer_Shown(object sender, EventArgs e) {
            try {
                //Show();
                ShowOutput(Properties.Resources.GameDownloading);
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

                await DownloadFlashpointSecurePlayerSectionAsync(TemplateName).ConfigureAwait(true);
                // get template element on start
                // throw on start
                try {
                    templateElement = GetTemplateElement(false, TemplateName);
                } catch (System.Configuration.ConfigurationErrorsException ex) {
                    LogExceptionToLauncher(ex);
                    ProgressManager.ShowError();
                    MessageBox.Show(Properties.Resources.ConfigurationFailedLoad, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }

                if (templateElement == null) {
                    ProgressManager.ShowError();
                    MessageBox.Show(Properties.Resources.ConfigurationFailedLoad, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }

                // get HTDOCS File/HTDOCS File Directory (in Software Mode)
                string htdocsFile = null;
                string htdocsFileDirectory = null;

                if (templateElement.Mode.Name == ModeElement.NAME.SOFTWARE) {
                    try {
                        Uri requestUri = await DownloadAsync(URL).ConfigureAwait(true);
                        StringBuilder htdocsFilePath = new StringBuilder(HTDOCS);

                        try {
                            // ignore host if going through localhost (no proxy)
                            if (requestUri.Host.ToLowerInvariant() != "localhost") {
                                htdocsFilePath.Append("\\");
                                htdocsFilePath.Append(requestUri.Host);
                            }

                            htdocsFilePath.Append(requestUri.LocalPath);
                        } catch (UriFormatException) {
                            throw new ArgumentException("The URL " + URL + " is malformed.");
                        } catch (NullReferenceException) {
                            throw new ArgumentNullException("The URL is null.");
                        } catch (InvalidOperationException) {
                            throw new ArgumentException("The URL " + URL + " is invalid.");
                        }

                        try {
                            htdocsFile = Path.GetFileName(htdocsFilePath.ToString());
                        } catch (ArgumentException ex) {
                            LogExceptionToLauncher(ex);
                            // Fail silently?
                        }

                        // empty ONLY, not null
                        if (htdocsFile == String.Empty) {
                            // path is to directory
                            if (INDEX_EXTENSIONS.Length > 0) {
                                htdocsFile = "index." + INDEX_EXTENSIONS[0];
                            }
                        }

                        try {
                            htdocsFileDirectory = Path.GetDirectoryName(htdocsFilePath.ToString());
                        } catch (ArgumentException ex) {
                            LogExceptionToLauncher(ex);
                            // Fail silently?
                        }

                        if (!String.IsNullOrEmpty(htdocsFile) && !String.IsNullOrEmpty(htdocsFileDirectory)) {
                            htdocsFile = htdocsFileDirectory + "\\" + htdocsFile;
                        }
                    } catch (DownloadFailedException ex) {
                        LogExceptionToLauncher(ex);
                        // Fail silently.
                    }
                }

                if (htdocsFile == null) {
                    // still create environment variable
                    htdocsFile = String.Empty;
                }

                if (htdocsFileDirectory == null) {
                    // still create environment variable
                    htdocsFileDirectory = String.Empty;
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
                } catch (ArgumentException ex) {
                    LogExceptionToLauncher(ex);
                    Application.Exit();
                    return;
                } catch (System.Security.SecurityException ex) {
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Setting the Environment Variables requires elevation.");
                }

                ProgressManager.ShowOutput();

                // Start Secure Playback
                try {
                    await StartSecurePlayback(templateElement).ConfigureAwait(false);
                } catch (ActiveXImportFailedException ex) {
                    LogExceptionToLauncher(ex);
                    // no need to exit here, error shown in interface
                    //Application.Exit();
                    return;
                } catch (InvalidModeException ex) {
                    LogExceptionToLauncher(ex);
                    // no need to exit here, error shown in interface
                    //Application.Exit();
                    return;
                } catch (InvalidModificationException ex) {
                    LogExceptionToLauncher(ex);
                    // no need to exit here, error shown in interface
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
                } catch (InvalidModificationException) {
                    Application.Exit();
                    return;
                }
            }
        }

        private async void FlashpointSecurePlayer_FormClosing(object sender, FormClosingEventArgs e) {
            // don't close if there is no close button
            e.Cancel = !ControlBox;

            // do stuff, but not if restarting
            // not too important for this to work, we can reset it on restart
            if (e.Cancel) {
                return;
            }

            // don't show, we don't want two windows at once on restart
            //Show();
            ProgressManager.ShowOutput();

            // get template element on stop
            TemplateElement templateElement = null;

            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch (System.Configuration.ConfigurationErrorsException ex) {
                LogExceptionToLauncher(ex);
                return;
            }

            if (templateElement == null) {
                return;
            }

            try {
                await StopSecurePlayback(e, templateElement).ConfigureAwait(false);
            } catch (ActiveXImportFailedException ex) {
                LogExceptionToLauncher(ex);
                // Fail silently.
            } catch (InvalidModeException ex) {
                LogExceptionToLauncher(ex);
                // Fail silently.
            } catch (InvalidModificationException ex) {
                LogExceptionToLauncher(ex);
                // Fail silently.
            } catch (InvalidTemplateException ex) {
                LogExceptionToLauncher(ex);
                // Fail silently.
            }
            
            if (applicationMutex != null) {
                applicationMutex.ReleaseMutex();
                applicationMutex = null;
            }
        }

        private void serverForm_FormClosing(object sender, FormClosingEventArgs e) {
            // stop form closing recursion
            if (serverForm != null) {
                serverForm.FormClosing -= serverForm_FormClosing;
                serverForm = null;
            }

            Application.Exit();
        }
    }
}

// "As for me and my household, we will serve the Lord." - Joshua 24:15