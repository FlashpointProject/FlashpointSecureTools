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
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;

namespace FlashpointSecurePlayer {
    public partial class FlashpointSecurePlayer : Form {
        private const string APPLICATION_MUTEX_NAME = "Flashpoint Secure Player";
        private const string EMPTY_MODE_NAME = "";
        private const string FLASHPOINT_LAUNCHER_PARENT_PROCESS_EXE_FILE_NAME = "cmd.exe";
        private const string FLASHPOINT_LAUNCHER_PROCESS_NAME = "flashpoint";
        private Mutex applicationMutex = null;
        private static SemaphoreSlim modificationsSemaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly RunAsAdministrator runAsAdministrator;
        private readonly ModeTemplates modeTemplates;
        private readonly EnvironmentVariables environmentVariables;
        private readonly DownloadsBefore downloadsBefore;
        private readonly RegistryBackups registryBackup;
        private readonly SingleInstance singleInstance;
        private bool activeX = false;
        private string server = EMPTY_MODE_NAME;
        private string software = EMPTY_MODE_NAME;
        private ProcessStartInfo softwareProcessStartInfo = null;

        private string ModificationsName { get; set; } = ACTIVE_EXE_CONFIGURATION_NAME;
        private bool RunAsAdministratorModification { get; set; } = false;
        private List<string> DownloadsBeforeModificationNames { get; set; } = null;

        private delegate void ErrorDelegate(string text);

        public FlashpointSecurePlayer() {
            InitializeComponent();
            runAsAdministrator = new RunAsAdministrator(this);
            modeTemplates = new ModeTemplates(this);
            environmentVariables = new EnvironmentVariables(this);
            downloadsBefore = new DownloadsBefore(this);
            registryBackup = new RegistryBackups(this);
            singleInstance = new SingleInstance(this);
        }

        private void ShowOutput(string errorLabelText) {
            ProgressManager.ShowOutput();
            this.errorLabel.Text = errorLabelText;
        }

        private void ShowError(string errorLabelText) {
            ProgressManager.ShowError();
            this.errorLabel.Text = errorLabelText;
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
                ProgressManager.ShowOutput();
                DialogResult dialogResult = MessageBox.Show(String.Format(Properties.Resources.LaunchGame, Properties.Resources.AsAdministratorUser), Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.YesNo, MessageBoxIcon.None);

                if (dialogResult == DialogResult.No) {
                    Application.Exit();
                    throw new InvalidModificationException("The operation was aborted by the user.");
                }

                RestartApplication(true, this, APPLICATION_MUTEX_NAME);
                throw new InvalidModificationException("The Modification does not work unless run as Administrator User.");
            }

            // we're already running as admin?
            ShowError(String.Format(Properties.Resources.GameFailedLaunch, Properties.Resources.AsAdministratorUser));
            throw new InvalidModificationException("The Modification failed to run as Administrator User.");
        }

        private void AskLaunchWithCompatibilitySettings() {
            ProgressManager.ShowOutput();
            DialogResult dialogResult = MessageBox.Show(String.Format(Properties.Resources.LaunchGame, Properties.Resources.WithCompatibilitySettings), Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.YesNo, MessageBoxIcon.None);

            if (dialogResult == DialogResult.No) {
                Application.Exit();
                throw new InvalidModificationException("The operation was aborted by the user.");
            }

            RestartApplication(false, this, APPLICATION_MUTEX_NAME);
            throw new InvalidModificationException("The Modification does not work unless run with Compatibility Settings.");
        }

        private async Task ActivateModificationsAsync(string commandLine, ErrorDelegate errorDelegate) {
            await modificationsSemaphoreSlim.WaitAsync().ConfigureAwait(true);

            try {
                if (String.IsNullOrEmpty(ModificationsName)) {
                    //errorDelegate(Properties.Resources.CurationMissingModificationName);
                    //throw new InvalidModificationException();
                    return;
                }

                ProgressManager.CurrentGoal.Start(7);

                try {
                    await DownloadFlashpointSecurePlayerSectionAsync(ModificationsName).ConfigureAwait(true);
                    ModificationsElement modificationsElement = null;

                    try {
                        modificationsElement = GetModificationsElement(false, ModificationsName);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        // we really need modificationsElement to exist
                        throw new InvalidModificationException("The Modifications Element " + ModificationsName + " does not exist.");
                    }

                    if (modificationsElement == null) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        throw new InvalidModificationException("The Modifications Element " + ModificationsName + " is null.");
                    }

                    if (DownloadsBeforeModificationNames == null) {
                        DownloadsBeforeModificationNames = new List<string>();
                    }

                    try {
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
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    try {
                        runAsAdministrator.Activate(ModificationsName, RunAsAdministratorModification);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    } catch (TaskRequiresElevationException) {
                        AskLaunchAsAdministratorUser();
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    if (modificationsElement.ModeTemplates.ServerModeTemplate.ElementInformation.IsPresent || modificationsElement.ModeTemplates.SoftwareModeTemplate.ElementInformation.IsPresent) {
                        try {
                            modeTemplates.Activate(ModificationsName, ref server, ref software, ref softwareProcessStartInfo);
                        } catch (ModeTemplatesFailedException) {
                            errorDelegate(Properties.Resources.ModeTemplatesFailed);
                        } catch (System.Configuration.ConfigurationErrorsException) {
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        } catch (TaskRequiresElevationException) {
                            AskLaunchAsAdministratorUser();
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    if (modificationsElement.EnvironmentVariables.Count > 0) {
                        try {
                            environmentVariables.Activate(ModificationsName, server, APPLICATION_MUTEX_NAME);
                        } catch (EnvironmentVariablesFailedException) {
                            errorDelegate(Properties.Resources.EnvironmentVariablesFailed);
                        } catch (System.Configuration.ConfigurationErrorsException) {
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        } catch (TaskRequiresElevationException) {
                            AskLaunchAsAdministratorUser();
                        } catch (CompatibilityLayersException) {
                            AskLaunchWithCompatibilitySettings();
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    if (DownloadsBeforeModificationNames.Count > 0) {
                        try {
                            await downloadsBefore.ActivateAsync(ModificationsName, DownloadsBeforeModificationNames).ConfigureAwait(true);
                        } catch (DownloadFailedException) {
                            errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, String.Join(", ", DownloadsBeforeModificationNames)));
                        } catch (System.Configuration.ConfigurationErrorsException) {
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    if (modificationsElement.RegistryBackups.Count > 0) {
                        try {
                            registryBackup.Activate(ModificationsName);
                        } catch (RegistryBackupFailedException) {
                            errorDelegate(Properties.Resources.RegistryBackupFailed);
                        } catch (System.Configuration.ConfigurationErrorsException) {
                            errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                        } catch (TaskRequiresElevationException) {
                            AskLaunchAsAdministratorUser();
                        } catch (ArgumentException) {
                            errorDelegate(Properties.Resources.GameIsMissingFiles);
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    if (modificationsElement.SingleInstance.ElementInformation.IsPresent) {
                        try {
                            singleInstance.Activate(ModificationsName, commandLine);
                        } catch (InvalidModificationException ex) {
                            throw ex;
                        } catch (TaskRequiresElevationException) {
                            AskLaunchAsAdministratorUser();
                        } catch {
                            errorDelegate(Properties.Resources.UnknownProcessCompatibilityConflict);
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            } finally {
                modificationsSemaphoreSlim.Release();
            }
        }

        private async Task DeactivateModificationsAsync(ErrorDelegate errorDelegate) {
            await modificationsSemaphoreSlim.WaitAsync().ConfigureAwait(true);

            try {
                ProgressManager.CurrentGoal.Start(3);

                try {
                    try {
                        // this one really needs to work
                        // we can't continue if it does not
                        registryBackup.Deactivate();
                    } catch (RegistryBackupFailedException) {
                        errorDelegate(Properties.Resources.RegistryBackupFailed);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    } catch (TaskRequiresElevationException) {
                        AskLaunchAsAdministratorUser();
                    } catch (ArgumentException) {
                        errorDelegate(Properties.Resources.GameIsMissingFiles);
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    try {
                        environmentVariables.Deactivate(server);
                    } catch (EnvironmentVariablesFailedException) {
                        errorDelegate(Properties.Resources.EnvironmentVariablesFailed);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    } catch (TaskRequiresElevationException) {
                        AskLaunchAsAdministratorUser();
                    } catch (CompatibilityLayersException) {
                        AskLaunchWithCompatibilitySettings();
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    try {
                        ModificationsElement activeModificationsElement = GetActiveModificationsElement(false);

                        if (activeModificationsElement != null) {
                            activeModificationsElement.Active = ACTIVE_EXE_CONFIGURATION_NAME;
                            SetFlashpointSecurePlayerSection(ACTIVE_EXE_CONFIGURATION_NAME);
                        }
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    }

                    ProgressManager.CurrentGoal.Steps++;
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            } finally {
                modificationsSemaphoreSlim.Release();
            }
        }

        private async Task StartSecurePlayback() {
            if (activeX) {
                // ActiveX Mode
                if (String.IsNullOrEmpty(ModificationsName)) {
                    ShowError(Properties.Resources.CurationMissingModificationName);
                    throw new InvalidModificationException("The Modification Name may not be the Active Modification Name.");
                }

                // this requires admin
                if (!TestLaunchedAsAdministratorUser()) {
                    AskLaunchAsAdministratorUser();
                }

                ProgressManager.Reset();
                ShowOutput(Properties.Resources.RegistryBackupInProgress);
                ProgressManager.CurrentGoal.Start(6);

                try {
                    ActiveXControl activeXControl;

                    try {
                        activeXControl = new ActiveXControl(ModificationsName);
                    } catch (DllNotFoundException) {
                        ProgressManager.ShowError();
                        MessageBox.Show(String.Format(Properties.Resources.GameIsMissingFiles, ModificationsName), Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                        return;
                    } catch (InvalidActiveXControlException) {
                        ShowError(Properties.Resources.GameNotActiveXControl);
                        return;
                    }

                    GetBinaryType(ModificationsName, out BINARY_TYPE binaryType);

                    // first, we install the control without a registry backup running
                    // this is so we can be sure we can uninstall the control
                    try {
                        activeXControl.Install();
                    } catch (Win32Exception) {
                        ShowError(Properties.Resources.ActiveXControlInstallFailed);
                        return;
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    // next, uninstall the control
                    // in case it was already installed before this whole process
                    // this is to ensure an existing install
                    // doesn't interfere with our registry backup results
                    try {
                        activeXControl.Uninstall();
                    } catch (Win32Exception) {
                        ShowError(Properties.Resources.ActiveXControlUninstallFailed);
                        return;
                    }

                    ProgressManager.CurrentGoal.Steps++;

                    try {
                        try {
                            await registryBackup.StartImportAsync(ModificationsName, binaryType).ConfigureAwait(true);
                        } catch (RegistryBackupFailedException) {
                            ShowError(Properties.Resources.RegistryBackupFailed);
                            return;
                        } catch (System.Configuration.ConfigurationErrorsException) {
                            ProgressManager.ShowError();
                            MessageBox.Show(Properties.Resources.ConfigurationFailedLoad, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        } catch (InvalidModificationException) {
                            ShowError(Properties.Resources.GameNotCuratedCorrectly);
                            return;
                        } catch (TaskRequiresElevationException) {
                            // we're already running as admin?
                            ShowError(String.Format(Properties.Resources.GameFailedLaunch, Properties.Resources.AsAdministratorUser));
                            return;
                        } catch (InvalidOperationException) {
                            ShowError(Properties.Resources.RegistryBackupAlreadyRunning);
                            return;
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        // a registry backup is running, install the control
                        try {
                            activeXControl.Install();
                        } catch (Win32Exception) {
                            ShowError(Properties.Resources.ActiveXControlInstallFailed);
                            return;
                        }

                        ProgressManager.CurrentGoal.Steps++;

                        try {
                            await registryBackup.StopImportAsync().ConfigureAwait(true);
                        } catch (RegistryBackupFailedException) {
                            ShowError(Properties.Resources.RegistryBackupFailed);
                            return;
                        } catch (System.Configuration.ConfigurationErrorsException) {
                            ProgressManager.ShowError();
                            MessageBox.Show(Properties.Resources.ConfigurationFailedLoad, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                            return;
                        } catch (InvalidOperationException) {
                            ShowError(Properties.Resources.RegistryBackupNotRunning);
                            return;
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
                    } catch (Win32Exception) {
                        ShowError(Properties.Resources.ActiveXControlUninstallFailed);
                        return;
                    }

                    ProgressManager.CurrentGoal.Steps++;
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }

                ShowOutput(Properties.Resources.RegistryBackupWasSuccessful);
                return;
            } else {
                // switch to synced process
                ProgressManager.Reset();
                ShowOutput(Properties.Resources.RequiredComponentsAreLoading);
                ProgressManager.CurrentGoal.Start(2);

                try {
                    try {
                        await ActivateModificationsAsync(software, delegate (string text) {
                            if (text.IndexOf("\n") == -1) {
                                ShowError(text);
                            } else {
                                ProgressManager.ShowError();
                                MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }

                            throw new InvalidModificationException("An error occured while activating.");
                        }).ConfigureAwait(true);
                    } catch (InvalidModificationException) {
                        return;
                    }

                    if (!String.IsNullOrEmpty(server)) {
                        ProgressManager.CurrentGoal.Steps++;
                        Server serverForm = new Server(new Uri(server));

                        ProgressManager.CurrentGoal.Steps++;
                        Hide();
                        serverForm.Show();
                        return;
                    } else if (!String.IsNullOrEmpty(software)) {
                        ProgressManager.CurrentGoal.Steps++;

                        try {
                            // default to zero in case of error
                            int argc = 0;
                            string[] argv = CommandLineToArgv(software, out argc);

                            if (softwareProcessStartInfo == null) {
                                softwareProcessStartInfo = new ProcessStartInfo();
                            }

                            string fullPath = Path.GetFullPath(argv[0]);
                            softwareProcessStartInfo.FileName = fullPath;
                            softwareProcessStartInfo.Arguments = GetCommandLineArgumentRange(software, 1, -1);
                            softwareProcessStartInfo.ErrorDialog = false;

                            if (String.IsNullOrEmpty(softwareProcessStartInfo.WorkingDirectory)) {
                                softwareProcessStartInfo.WorkingDirectory = Path.GetDirectoryName(fullPath);
                            }

                            Process process = Process.Start(softwareProcessStartInfo);

                            try {
                                ProcessSync.Start(process);
                            } catch (JobObjectException) {
                                // popup message box and blow up
                                ProgressManager.ShowError();
                                MessageBox.Show(Properties.Resources.JobObjectNotCreated, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                process.Kill();
                                Environment.Exit(-1);
                                return;
                            }

                            ProgressManager.CurrentGoal.Steps++;
                            Hide();

                            if (!process.HasExited) {
                                process.WaitForExit();
                            }

                            Application.Exit();
                        } catch {
                            Show();
                            ProgressManager.ShowError();
                            MessageBox.Show(Properties.Resources.ProcessFailedStart, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Application.Exit();
                        }
                        return;
                    }
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
            throw new InvalidCurationException("No Mode was used.");
        }

        private async Task StopSecurePlayback(FormClosingEventArgs e) {
            // only if closing...
            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);

            try {
                await DeactivateModificationsAsync(delegate (string text) {
                    // I will assassinate the Cyrollan delegate myself...
                }).ConfigureAwait(false);
            } catch (InvalidModificationException) {
                // Fail silently.
            }
        }

        private async void FlashpointSecurePlayer_Load(object sender, EventArgs e) {
            // default to false in case of error
            bool createdNew = false;
            // signals the Mutex if it has not been
            applicationMutex = new Mutex(true, APPLICATION_MUTEX_NAME, out createdNew);

            if (!createdNew) {
                // multiple instances open, blow up immediately
                Environment.Exit(-2);
                return;
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

            try {
                try {
                    Directory.SetCurrentDirectory(Application.StartupPath);
                } catch (System.Security.SecurityException) {
                    throw new TaskRequiresElevationException("Setting the Current Directory requires elevation.");
                } catch {
                    // Fail silently.
                }
            } catch (TaskRequiresElevationException) {
                try {
                    AskLaunchAsAdministratorUser();
                } catch (InvalidModificationException) {
                    Application.Exit();
                    return;
                }
            }

            // needed upon application restart to focus the new window
            BringToFront();
            Activate();
            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);

            string arg = null;
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 1;i < args.Length;i++) {
                arg = args[i].ToLower();

                // instead of switch I use else if because C# is too lame for multiple case statements
                if (arg == "--run-as-administrator" || arg == "-a") {
                    RunAsAdministratorModification = true;
                } else if (arg == "--activex" || arg == "-ax") {
                    activeX = true;
                } else {
                    if (i < args.Length - 1) {
                        if (arg == "--name" || arg == "-n") {
                            ModificationsName = args[i + 1];
                            i++;
                        } else if (arg == "--download-before" || arg == "-dlb") {
                            if (DownloadsBeforeModificationNames == null) {
                                DownloadsBeforeModificationNames = new List<string>();
                            }

                            DownloadsBeforeModificationNames.Add(args[i + 1]);
                            i++;
                        } else if (arg == "--server" || arg == "-sv") {
                            server = args[i + 1];
                            i++;
                        } else if (arg == "--software" || arg == "-sw") {
                            software = GetCommandLineArgumentRange(Environment.CommandLine, i + 1, -1);
                            break;
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
                    throw new InvalidModificationException("An error occured while deactivating.");
                }).ConfigureAwait(false);
            } catch (InvalidModificationException) {
                // can't proceed since we can't activate without deactivating first
                Application.Exit();
                return;
            }
        }

        private async void FlashpointSecurePlayer_Shown(object sender, EventArgs e) {
            ProgressManager.ShowOutput();

            try {
                await StartSecurePlayback().ConfigureAwait(false);
            } catch (InvalidModificationException) {
                // no need to exit here, error shown in interface
                //Application.Exit();
                return;
            } catch (InvalidCurationException) {
                // detect if this application was started by Flashpoint Launcher
                // none of this is strictly necessary, I'm just trying
                // to reduce the amount of stupid in the #help-me-please channel
                //ShowError(Properties.Resources.GameNotCuratedCorrectly);
                string text = Properties.Resources.NoGameSelected;
                Process parentProcess = GetParentProcess();
                string parentProcessEXEFileName = null;

                if (parentProcess != null) {
                    try {
                        parentProcessEXEFileName = Path.GetFileName(GetProcessEXEName(parentProcess)).ToLower();
                    } catch {
                        // Fail silently.
                    }
                }

                if (parentProcessEXEFileName != FLASHPOINT_LAUNCHER_PARENT_PROCESS_EXE_FILE_NAME) {
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
                return;
            }
        }

        private async void FlashpointSecurePlayer_FormClosing(object sender, FormClosingEventArgs e) {
            // don't close if there is no close button
            e.Cancel = !ControlBox;

            // do stuff, but not if restarting
            // not too important for this to work, we can reset it on restart
            if (!e.Cancel) {
                try {
                    await StopSecurePlayback(e).ConfigureAwait(false);
                } catch (InvalidModificationException) {
                    // Fail silently.
                } catch (InvalidCurationException) {
                    // Fail silently.
                }
            }
        }
    }
}

// "As for me and my household, we will serve the Lord." - Joshua 24:15