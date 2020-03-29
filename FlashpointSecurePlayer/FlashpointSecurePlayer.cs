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
        private const string FLASHPOINT_LAUNCHER_PARENT_PROCESS_EXE_FILE_NAME = "cmd.exe";
        private const string FLASHPOINT_LAUNCHER_PROCESS_NAME = "flashpoint";
        private static SemaphoreSlim ModificationsSemaphoreSlim = new SemaphoreSlim(1, 1);
        private readonly RunAsAdministrator RunAsAdministrator;
        private readonly ModeTemplates ModeTemplates;
        private readonly EnvironmentVariables EnvironmentVariables;
        private readonly DownloadsBefore DownloadsBefore;
        private readonly RegistryBackup RegistryBackup;
        private readonly SingleInstance SingleInstance;
        string ModificationsName = ACTIVE_EXE_CONFIGURATION_NAME;
        bool RunAsAdministratorModification = false;
        List<string> DownloadsBeforeModificationNames = null;
        bool ActiveX = false;
        string Server = null;
        string Software = null;
        ProcessStartInfo SoftwareProcessStartInfo = null;
        public delegate void ErrorDelegate(string text);

        public FlashpointSecurePlayer() {
            InitializeComponent();
            RunAsAdministrator = new RunAsAdministrator(this);
            ModeTemplates = new ModeTemplates(this);
            EnvironmentVariables = new EnvironmentVariables(this);
            DownloadsBefore = new DownloadsBefore(this);
            RegistryBackup = new RegistryBackup(this);
            SingleInstance = new SingleInstance(this);
        }

        private void ResetProgressBar() {
            securePlaybackProgressBar.Style = ProgressBarStyle.Blocks;
            securePlaybackProgressBar.Value = 0;
            securePlaybackProgressBar.Style = ProgressBarStyle.Continuous;
            SetProgressBarState(securePlaybackProgressBar, PBST_NORMAL);
        }

        private void ShowOutput() {
            securePlaybackProgressBar.Style = ProgressBarStyle.Continuous;
            SetProgressBarState(securePlaybackProgressBar, PBST_NORMAL);
        }

        private void ShowOutput(string errorLabelText) {
            ShowOutput();
            this.errorLabel.Text = errorLabelText;
        }

        private void ShowError() {
            // TODO: why won't the progress bar change colour
            securePlaybackProgressBar.Style = ProgressBarStyle.Continuous;
            securePlaybackProgressBar.Value = 100;
            SetProgressBarState(securePlaybackProgressBar, PBST_ERROR);
        }

        private void ShowError(string errorLabelText) {
            ShowError();
            this.errorLabel.Text = errorLabelText;
        }

        private void AskLaunchInAdministratorMode() {
            if (!TestProcessRunningAsAdministrator()) {
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
                ShowOutput();
                DialogResult dialogResult = MessageBox.Show(Properties.Resources.LaunchInAdministratorMode, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.YesNo, MessageBoxIcon.None);

                if (dialogResult == DialogResult.No) {
                    Application.Exit();
                    throw new InvalidModificationException();
                }

                RestartApplication(true, this, APPLICATION_MUTEX_NAME);
                throw new InvalidModificationException();
            }

            // we're already running as admin?
            ShowError(Properties.Resources.GameFailedAdministratorMode);
            throw new InvalidModificationException();
        }

        private async Task ActivateModificationsAsync(string commandLine, ErrorDelegate errorDelegate) {
            await ModificationsSemaphoreSlim.WaitAsync().ConfigureAwait(true);

            try {
                if (String.IsNullOrEmpty(ModificationsName)) {
                    //errorDelegate(Properties.Resources.CurationMissingModificationName);
                    //throw new InvalidModificationException();
                    return;
                }

                await DownloadEXEConfiguration(ModificationsName).ConfigureAwait(true);
                ModificationsElement modificationsElement = null;

                try {
                    modificationsElement = GetModificationsElement(false, ModificationsName);
                } catch (System.Configuration.ConfigurationErrorsException) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    // we really need modificationsElement to exist
                    throw new InvalidModificationException();
                }

                if (modificationsElement == null) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    throw new InvalidModificationException();
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
                                throw new System.Configuration.ConfigurationErrorsException();
                            }

                            DownloadsBeforeModificationNames.Add(downloadsBeforeElement.Name);
                        }

                        //SetModificationsElement(modificationsElement, Name);
                    }
                } catch (System.Configuration.ConfigurationErrorsException) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                }
                
                try {
                    RunAsAdministrator.Activate(ModificationsName, RunAsAdministratorModification);
                } catch (System.Configuration.ConfigurationErrorsException) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                } catch (TaskRequiresElevationException) {
                    AskLaunchInAdministratorMode();
                }

                if (modificationsElement.ModeTemplates.ServerModeTemplate.ElementInformation.IsPresent || modificationsElement.ModeTemplates.SoftwareModeTemplate.ElementInformation.IsPresent) {
                    try {
                        ModeTemplates.Activate(ModificationsName, ref Server, ref Software, ref SoftwareProcessStartInfo);
                    } catch (ModeTemplatesFailedException) {
                        errorDelegate(Properties.Resources.ModeTemplatesFailed);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    } catch (TaskRequiresElevationException) {
                        AskLaunchInAdministratorMode();
                    }
                }

                if (modificationsElement.EnvironmentVariables.Count > 0) {
                    try {
                        EnvironmentVariables.Activate(ModificationsName);
                    } catch (EnvironmentVariablesFailedException) {
                        errorDelegate(Properties.Resources.EnvironmentVariablesFailed);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    } catch (TaskRequiresElevationException) {
                        AskLaunchInAdministratorMode();
                    }
                }

                if (DownloadsBeforeModificationNames.Count > 0) {
                    try {
                        await DownloadsBefore.ActivateAsync(ModificationsName, DownloadsBeforeModificationNames).ConfigureAwait(true);
                    } catch (DownloadFailedException) {
                        errorDelegate(String.Format(Properties.Resources.GameIsMissingFiles, String.Join(", ", DownloadsBeforeModificationNames)));
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    }
                }

                if (modificationsElement.RegistryBackups.Count > 0) {
                    try {
                        RegistryBackup.Activate(ModificationsName);
                    } catch (RegistryBackupFailedException) {
                        errorDelegate(Properties.Resources.RegistryBackupFailed);
                    } catch (System.Configuration.ConfigurationErrorsException) {
                        errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                    } catch (TaskRequiresElevationException) {
                        AskLaunchInAdministratorMode();
                    }
                }

                if (modificationsElement.SingleInstance.ElementInformation.IsPresent) {
                    try {
                        SingleInstance.Activate(ModificationsName, commandLine);
                    } catch (InvalidModificationException ex) {
                        throw ex;
                    } catch (TaskRequiresElevationException) {
                        AskLaunchInAdministratorMode();
                    } catch {
                        errorDelegate(Properties.Resources.UnknownProcessCompatibilityConflict);
                    }
                }
            } finally {
                ModificationsSemaphoreSlim.Release();
            }
        }

        private async Task DeactivateModificationsAsync(ErrorDelegate errorDelegate) {
            await ModificationsSemaphoreSlim.WaitAsync().ConfigureAwait(true);

            try {
                try {
                    // this one really needs to work
                    // we can't continue if it does not
                    RegistryBackup.Deactivate();
                } catch (RegistryBackupFailedException) {
                    errorDelegate(Properties.Resources.RegistryBackupFailed);
                } catch (System.Configuration.ConfigurationErrorsException) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                } catch (TaskRequiresElevationException) {
                    AskLaunchInAdministratorMode();
                }

                try {
                    EnvironmentVariables.Deactivate();
                } catch (EnvironmentVariablesFailedException) {
                    errorDelegate(Properties.Resources.EnvironmentVariablesFailed);
                } catch (System.Configuration.ConfigurationErrorsException) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                } catch (TaskRequiresElevationException) {
                    AskLaunchInAdministratorMode();
                }

                try {
                    ModificationsElement activeModificationsElement = GetActiveModificationsElement(false);
                
                    if (activeModificationsElement != null) {
                        activeModificationsElement.Active = ACTIVE_EXE_CONFIGURATION_NAME;
                        SetFlashpointSecurePlayerSection(ACTIVE_EXE_CONFIGURATION_NAME);
                    }
                } catch (System.Configuration.ConfigurationErrorsException) {
                    errorDelegate(Properties.Resources.ConfigurationFailedLoad);
                }
            } finally {
                ModificationsSemaphoreSlim.Release();
            }
        }

        private async Task StartSecurePlayback() {
            if (ActiveX) {
                // ActiveX Mode
                if (String.IsNullOrEmpty(ModificationsName)) {
                    ShowError(Properties.Resources.CurationMissingModificationName);
                    throw new InvalidModificationException();
                }

                //this.ShowInTaskbar = true;
                //this.WindowState = FormWindowState.Normal;

                // this requires admin
                if (!TestProcessRunningAsAdministrator()) {
                    AskLaunchInAdministratorMode();
                }

                ResetProgressBar();
                ShowOutput(Properties.Resources.RegistryBackupInProgress);
                ActiveXControl activeXControl;

                try {
                    activeXControl = new ActiveXControl(ModificationsName);
                } catch (DllNotFoundException) {
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

                securePlaybackProgressBar.Value = 17;

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

                securePlaybackProgressBar.Value = 33;

                try {
                    await RegistryBackup.StartImportAsync(ModificationsName, binaryType).ConfigureAwait(true);
                } catch (RegistryBackupFailedException) {
                    ShowError(Properties.Resources.RegistryBackupFailed);
                    return;
                } catch (System.Configuration.ConfigurationErrorsException) {
                    MessageBox.Show(Properties.Resources.ConfigurationFailedLoad, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                } catch (InvalidModificationException) {
                    ShowError(Properties.Resources.GameNotCuratedCorrectly);
                    return;
                } catch (TaskRequiresElevationException) {
                    // we're already running as admin?
                    ShowError(Properties.Resources.GameFailedAdministratorMode);
                    return;
                } catch (InvalidOperationException) {
                    ShowError(Properties.Resources.RegistryBackupAlreadyRunning);
                    return;
                }

                securePlaybackProgressBar.Value = 50;

                // a registry backup is running, install the control
                try {
                    activeXControl.Install();
                } catch (Win32Exception) {
                    ShowError(Properties.Resources.ActiveXControlInstallFailed);
                    return;
                }

                securePlaybackProgressBar.Value = 67;

                try {
                    await RegistryBackup.StopImportAsync().ConfigureAwait(true);
                } catch (RegistryBackupFailedException) {
                    ShowError(Properties.Resources.RegistryBackupFailed);
                    return;
                } catch (System.Configuration.ConfigurationErrorsException) {
                    MessageBox.Show(Properties.Resources.ConfigurationFailedLoad, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                } catch (InvalidOperationException) {
                    ShowError(Properties.Resources.RegistryBackupNotRunning);
                    return;
                }

                securePlaybackProgressBar.Value = 83;

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

                securePlaybackProgressBar.Value = 100;
                ShowOutput(Properties.Resources.RegistryBackupWasSuccessful);
                return;
            } else if (!String.IsNullOrEmpty(Server)) {
                // switch to server form
                ResetProgressBar();
                ShowOutput(Properties.Resources.RequiredComponentsAreLoading);
                
                try {
                    await ActivateModificationsAsync(null, delegate (string text) {
                        if (text.IndexOf("\n") == -1) {
                            ShowError(text);
                        } else {
                            ShowError();
                            MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        throw new InvalidModificationException();
                    }).ConfigureAwait(true);
                } catch (InvalidModificationException) {
                    return;
                }

                Server serverForm = new Server(new Uri(Server));

                securePlaybackProgressBar.Value = 100;
                Hide();
                serverForm.Show();
                return;
            } else if (!String.IsNullOrEmpty(Software)) {
                // switch to synced process
                ResetProgressBar();
                ShowOutput(Properties.Resources.RequiredComponentsAreLoading);

                try {
                    await ActivateModificationsAsync(Software, delegate (string text) {
                        if (text.IndexOf("\n") == -1) {
                            ShowError(text);
                        } else {
                            ShowError();
                            MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        throw new InvalidModificationException();
                    }).ConfigureAwait(true);
                } catch (InvalidModificationException) {
                    return;
                }

                securePlaybackProgressBar.Value = 50;

                try {
                    // default to zero in case of error
                    int argc = 0;
                    string[] argv = CommandLineToArgv(Software, out argc);

                    if (SoftwareProcessStartInfo == null) {
                        SoftwareProcessStartInfo = new ProcessStartInfo();
                    }

                    SoftwareProcessStartInfo.FileName = Path.GetFullPath(argv[0]);
                    SoftwareProcessStartInfo.Arguments = GetCommandLineArgumentRange(Software, 1, -1);
                    SoftwareProcessStartInfo.ErrorDialog = false;
                    SoftwareProcessStartInfo.WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(argv[0]));

                    Process process = Process.Start(SoftwareProcessStartInfo);

                    try {
                        ProcessSync.Start(process);
                    } catch (JobObjectException) {
                        // popup message box and blow up
                        ShowError();
                        MessageBox.Show(Properties.Resources.JobObjectNotCreated, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        process.Kill();
                        Environment.Exit(-1);
                        return;
                    }

                    securePlaybackProgressBar.Value = 100;
                    Hide();

                    if (!process.HasExited) {
                        process.WaitForExit();
                    }

                    Application.Exit();
                } catch {
                    Show();
                    ShowError();
                    MessageBox.Show(Properties.Resources.ProcessFailedStart, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
                return;
            }
            throw new InvalidCurationException();
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
            Mutex flashpointSecurePlayerMutex = new Mutex(true, APPLICATION_MUTEX_NAME, out createdNew);

            if (!createdNew) {
                // multiple instances open, blow up immediately
                Environment.Exit(-2);
                return;
            }

            string windowsVersionName = GetWindowsVersionName(false, false, false);

            if (windowsVersionName != "Windows 7" &&
                windowsVersionName != "Windows Server 2008 R2" &&
                windowsVersionName != "Windows 8" &&
                windowsVersionName != "Windows Server 2012" &&
                windowsVersionName != "Windows 8.1" &&
                windowsVersionName != "Windows Server 2012 R2" &&
                windowsVersionName != "Windows 10" &&
                windowsVersionName != "Windows Server 2016") {
                ShowError();
                MessageBox.Show(Properties.Resources.WindowsVersionTooOld, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            try {
                try {
                    Directory.SetCurrentDirectory(Application.StartupPath);
                } catch (System.Security.SecurityException) {
                    throw new TaskRequiresElevationException();
                } catch {
                    // Fail silently.
                }
            } catch (TaskRequiresElevationException) {
                try {
                    AskLaunchInAdministratorMode();
                } catch (InvalidModificationException) {
                    Application.Exit();
                    return;
                }
            }

            ShowOutput(Properties.Resources.RequiredComponentsAreUnloading);

            string arg = null;
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 1;i < args.Length;i++) {
                arg = args[i].ToLower();

                // instead of switch I use else if because C# is too lame for multiple case statements
                if (arg == "--run-as-administrator" || arg == "-a") {
                    RunAsAdministratorModification = true;
                } else if (arg == "--activex" || arg == "-ax") {
                    ActiveX = true;
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
                            Server = args[i + 1];
                            i++;
                        } else if (arg == "--software" || arg == "-sw") {
                            Software = GetCommandLineArgumentRange(Environment.CommandLine, i + 1, -1);
                            break;
                        }
                    }
                }
            }

            try {
                await DeactivateModificationsAsync(delegate (string text) {
                    ShowError();
                    MessageBox.Show(text, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new InvalidModificationException();
                }).ConfigureAwait(false);
            } catch (InvalidModificationException) {
                // can't proceed since we can't activate without deactivating first
                Application.Exit();
                return;
            }
        }

        private async void FlashpointSecurePlayer_Shown(object sender, EventArgs e) {
            ShowOutput();

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
                        MessageBox.Show(Properties.Resources.WindowsVersionTooOld, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                        return;
                    }

                    if (processesByName.Length <= 0) {
                        text += " " + Properties.Resources.OpenFlashpointLauncher;
                    }
                }

                ShowError();
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