using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlashpointSecurePlayer {
    public static class Shared {
        public class Exceptions {
            public class TaskRequiresElevationException : InvalidOperationException {
                public TaskRequiresElevationException() : base() { }
                public TaskRequiresElevationException(string message) : base(message) { }
                public TaskRequiresElevationException(string message, Exception inner) : base(message, inner) { }
            }

            public class CompatibilityLayersException : InvalidOperationException {
                public CompatibilityLayersException() : base() { }
                public CompatibilityLayersException(string message) : base(message) { }
                public CompatibilityLayersException(string message, Exception inner) : base(message, inner) { }
            }

            public class DownloadFailedException : InvalidOperationException {
                public DownloadFailedException() : base() { }
                public DownloadFailedException(string message) : base(message) { }
                public DownloadFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class JobObjectException : Win32Exception {
                public JobObjectException() { }
                public JobObjectException(string message) : base(message) { }
                public JobObjectException(string message, Exception inner) : base(message, inner) { }
            }

            public class FlashpointProxyException : Win32Exception {
                public FlashpointProxyException() { }
                public FlashpointProxyException(string message) : base(message) { }
                public FlashpointProxyException(string message, Exception inner) : base(message, inner) { }
            }

            public class InvalidActiveXControlException : InvalidOperationException {
                public InvalidActiveXControlException() { }
                public InvalidActiveXControlException(string message) : base(message) { }
                public InvalidActiveXControlException(string message, Exception inner) : base(message, inner) { }
            }

            public class InvalidCurationException : InvalidOperationException {
                public InvalidCurationException() { }
                public InvalidCurationException(string message) : base(message) { }
                public InvalidCurationException(string message, Exception inner) : base(message, inner) { }
            }

            public class InvalidModificationException : InvalidCurationException {
                public InvalidModificationException() { }
                public InvalidModificationException(string message) : base(message) { }
                public InvalidModificationException(string message, Exception inner) : base(message, inner) { }
            }

            public class ModeTemplatesFailedException : InvalidModificationException {
                public ModeTemplatesFailedException() { }
                public ModeTemplatesFailedException(string message) : base(message) { }
                public ModeTemplatesFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class EnvironmentVariablesFailedException : InvalidModificationException {
                public EnvironmentVariablesFailedException() { }
                public EnvironmentVariablesFailedException(string message) : base(message) { }
                public EnvironmentVariablesFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class RegistryBackupFailedException : InvalidModificationException {
                public RegistryBackupFailedException() { }
                public RegistryBackupFailedException(string message) : base(message) { }
                public RegistryBackupFailedException(string message, Exception inner) : base(message, inner) { }
            }
        }

        public const int S_OK = unchecked((int)0x0000);
        public const int S_FALSE = unchecked((int)0x0001);
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_NOINTERFACE = unchecked((int)0x80004002);
        public const int E_POINTER = unchecked((int)0x80004003);
        public const int E_ABORT = unchecked((int)0x80004004);
        public const int E_FAIL = unchecked((int)0x80004005);
        public const int E_UNEXPECTED = unchecked((int)0x8000FFFF);
        public const int E_ACCESSDENIED = unchecked((int)0x80070005);
        public const int E_HANDLE = unchecked((int)0x80070006);
        public const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        public const int E_INVALIDARG = unchecked((int)0x80070057);

        public const int INET_E_DEFAULT_ACTION = unchecked((int)0x800C0011);

        public const int MAX_PATH = 260;

        public enum BINARY_TYPE : uint {
            SCS_32BIT_BINARY = 0, // A 32-bit Windows-based application
            SCS_64BIT_BINARY = 6, // A 64-bit Windows-based application.
            SCS_DOS_BINARY = 1, // An MS-DOS – based application
            SCS_OS216_BINARY = 5, // A 16-bit OS/2-based application
            SCS_PIF_BINARY = 3, // A PIF file that executes an MS-DOS – based application
            SCS_POSIX_BINARY = 4, // A POSIX – based application
            SCS_WOW_BINARY = 2 // A 16-bit Windows-based application
        }

        [DllImport("KERNEL32.DLL")]
        public static extern bool GetBinaryType(string applicationNamePointer, out BINARY_TYPE binaryTypePointer);

        public const uint PBM_SETSTATE = 0x0410;
        public static readonly IntPtr PBST_NORMAL = (IntPtr)1;
        public static readonly IntPtr PBST_ERROR = (IntPtr)2;
        public static readonly IntPtr PBST_PAUSED = (IntPtr)3;

        [DllImport("USER32.DLL", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("KERNEL32.DLL")]
        public static extern IntPtr LocalFree(IntPtr memoryHandle);

        [DllImport("SHELL32.DLL", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int argc);

        private enum OS_TYPE : uint {
            OS_WINDOWS = 0,
            OS_NT = 1,
            OS_WIN95ORGREATER = 2,
            OS_NT4ORGREATER = 3,
            OS_WIN98ORGREATER = 5,
            OS_WIN98_GOLD = 6,
            OS_WIN2000ORGREATER = 7,
            OS_WIN2000PRO = 8,
            OS_WIN2000SERVER = 9,
            OS_WIN2000ADVSERVER = 10,
            OS_WIN2000DATACENTER = 11,
            OS_WIN2000TERMINAL = 12,
            OS_EMBEDDED = 13,
            OS_TERMINALCLIENT = 14,
            OS_TERMINALREMOTEADMIN = 15,
            OS_WIN95_GOLD = 16,
            OS_MEORGREATER = 17,
            OS_XPORGREATER = 18,
            OS_HOME = 19,
            OS_PROFESSIONAL = 20,
            OS_DATACENTER = 21,
            OS_ADVSERVER = 22,
            OS_SERVER = 23,
            OS_TERMINALSERVER = 24,
            OS_PERSONALTERMINALSERVER = 25,
            OS_FASTUSERSWITCHING = 26,
            OS_WELCOMELOGONUI = 27,
            OS_DOMAINMEMBER = 28,
            OS_ANYSERVER = 29,
            OS_WOW6432 = 30,
            OS_WEBSERVER = 31,
            OS_SMALLBUSINESSSERVER = 32,
            OS_TABLETPC = 33,
            OS_SERVERADMINUI = 34,
            OS_MEDIACENTER = 35,
            OS_APPLIANCE = 36
        }

        [DllImport("Shlwapi.dll", SetLastError = true, EntryPoint = "#437")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsOS(OS_TYPE os);

        const uint TOOLHELP32CS_INHERIT = 0x80000000;
        const uint TOOLHELP32CS_SNAPALL = TOOLHELP32CS_SNAPHEAPLIST | TOOLHELP32CS_SNAPMODULE | TOOLHELP32CS_SNAPPROCESS | TOOLHELP32CS_SNAPTHREAD;
        const uint TOOLHELP32CS_SNAPHEAPLIST = 0x00000001;
        const uint TOOLHELP32CS_SNAPMODULE = 0x00000008;
        const uint TOOLHELP32CS_SNAPMODULE32 = 0x00000010;
        const uint TOOLHELP32CS_SNAPPROCESS = 0x00000002;
        const uint TOOLHELP32CS_SNAPTHREAD = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSENTRY32 {
            public uint size;
            public uint countUsage;
            public uint toolHelp32ProcessID;
            public IntPtr toolHelp32DefaultHeapID;
            public uint toolHelp32ModuleID;
            public uint countThreads;
            public uint toolHelp32ParentProcessID;
            public int pcPriorityClassBase;
            public uint flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string exeFile;
        };

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint toolhelp32ProcessID);

        [DllImport("KERNEL32.DLL")]
        static extern bool Process32First(IntPtr snapshotHandle, ref PROCESSENTRY32 processEntryPointer);

        [DllImport("KERNEL32.DLL")]
        static extern bool Process32Next(IntPtr snapshotHandle, ref PROCESSENTRY32 processEntryPointer);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr processHandle, int flags, StringBuilder exeName, ref int sizePointer);

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Auto)]
        public static extern int GetLongPathName(
            [MarshalAs(UnmanagedType.LPTStr)]
            string shortPath,
            [MarshalAs(UnmanagedType.LPTStr)]
            StringBuilder longPath,
            int longPathLength
        );

        [DllImport("KERNEL32.DLL", CharSet = CharSet.Auto)]
        public static extern int GetShortPathName(
            [MarshalAs(UnmanagedType.LPTStr)]
            string longPath,
            [MarshalAs(UnmanagedType.LPTStr)]
            StringBuilder shortPath,
            int bufferSize
        );

        public const string HTDOCS = "..\\Server\\htdocs";
        // there should be only one HTTP Client per application
        // (as of right now though this is exclusively used by DownloadsBefore class)
        private static readonly HttpClientHandler HTTPClientHandler = new HttpClientHandler {
            Proxy = new WebProxy("127.0.0.1", 22500),
            UseProxy = true
        };

        public static readonly HttpClient HTTPClient = new HttpClient(HTTPClientHandler);
        // for best results, this should match
        // the value of FILE_READ_LENGTH constant
        // in the Flashpoint Router
        private const int STREAM_READ_LENGTH = 8192;
        // no parallel downloads
        // if parallel downloads are ever supported, the max value should
        // be changed to the maximum number of parallel downloads allowed
        // (preferably, no more than eight at a time)
        private static SemaphoreSlim DownloadSemaphoreSlim = new SemaphoreSlim(1, 1);

        private const string CONFIGURATION_FOLDER_NAME = "FlashpointSecurePlayerConfigs";
        private const string CONFIGURATION_DOWNLOAD_NAME = "flashpointsecureplayerconfigs";
        private static FlashpointSecurePlayerSection flashpointSecurePlayerSection = null;
        private static FlashpointSecurePlayerSection activeFlashpointSecurePlayerSection = null;

        public abstract class ModificationsConfigurationElementCollection : ConfigurationElementCollection {
            public override ConfigurationElementCollectionType CollectionType {
                get {
                    return ConfigurationElementCollectionType.AddRemoveClearMapAlternate;
                }
            }

            /*
            protected override ConfigurationElement CreateNewElement() {
                return new ConfigurationElement();
            }
            */

            protected override void BaseAdd(System.Configuration.ConfigurationElement configurationElement) {
                BaseAdd(configurationElement, false);
            }

            public ConfigurationElement Get(int index) {
                return BaseGet(index) as ConfigurationElement;
            }

            public ConfigurationElement Get(string name) {
                //BaseAdd(modificationsElement);
                return BaseGet(name) as ConfigurationElement;
            }

            public void Set(ConfigurationElement configurationElement) {
                Remove(GetElementKey(configurationElement) as string);
                BaseAdd(configurationElement);
            }

            public void Remove(string name) {
                if (BaseGet(name) != null) {
                    BaseRemove(name);
                }
            }

            public void Remove(ConfigurationElement configurationElement) {
                Remove(GetElementKey(configurationElement) as string);
            }

            public void RemoveAt(int index) {
                if (BaseGet(index) != null) {
                    BaseRemoveAt(index);
                }
            }

            public void Clear() {
                BaseClear();
            }

            public int IndexOf(ConfigurationElement configurationElement) {
                return BaseIndexOf(configurationElement);
            }
        }

        public class FlashpointSecurePlayerSection : ConfigurationSection {
            public class ModificationsElementCollection : ModificationsConfigurationElementCollection {
                public class ModificationsElement : ConfigurationElement {
                    [ConfigurationProperty("name", IsRequired = true)]
                    public string Name {
                        get {
                            if (String.IsNullOrEmpty(base["name"] as string)) {
                                return base["name"] as string;
                            }
                            return (base["name"] as string).ToLower();
                        }

                        set {
                            if (String.IsNullOrEmpty(value)) {
                                base["name"] = value;
                                return;
                            }

                            base["name"] = value.ToLower();
                        }
                    }

                    [ConfigurationProperty("active", IsRequired = false)]
                    public string Active {
                        get {
                            if (String.IsNullOrEmpty(base["active"] as string)) {
                                return base["active"] as string;
                            }
                            return (base["active"] as string).ToLower();
                        }

                        set {
                            if (String.IsNullOrEmpty(value)) {
                                base["active"] = value;
                                return;
                            }

                            base["active"] = value.ToLower();
                        }
                    }

                    [ConfigurationProperty("runAsAdministrator", DefaultValue = false, IsRequired = false)]
                    public bool RunAsAdministrator {
                        get {
                            return (bool)base["runAsAdministrator"];
                        }

                        set {
                            base["runAsAdministrator"] = value;
                        }
                    }

                    public class ModeTemplatesElement : ConfigurationElement {
                        public class ModeTemplateElement : ConfigurationElement {
                            public class RegexElementCollection : ModificationsConfigurationElementCollection {
                                public class RegexElement : ConfigurationElement {
                                    [ConfigurationProperty("name", IsRequired = true)]
                                    public string Name {
                                        get {
                                            return base["name"] as string;
                                        }

                                        set {
                                            base["name"] = value;
                                        }
                                    }

                                    [ConfigurationProperty("replace", IsRequired = true)]
                                    public string Replace {
                                        get {
                                            return base["replace"] as string;
                                        }

                                        set {
                                            base["replace"] = value;
                                        }
                                    }
                                }

                                protected override object GetElementKey(ConfigurationElement configurationElement) {
                                    RegexElement nameRegexElement = configurationElement as RegexElement;
                                    return nameRegexElement.Name;
                                }

                                protected override ConfigurationElement CreateNewElement() {
                                    return new RegexElement();
                                }
                            }

                            [ConfigurationProperty("regexes", IsRequired = false)]
                            [ConfigurationCollection(typeof(RegexElementCollection), AddItemName = "regex")]
                            public RegexElementCollection Regexes {
                                get {
                                    return (RegexElementCollection)base["regexes"];
                                }

                                set {
                                    base["regexes"] = value;
                                }
                            }
                        }

                        public class SoftwareModeTemplateElement : ModeTemplateElement {
                            [ConfigurationProperty("hideWindow", DefaultValue = false, IsRequired = false)]
                            public bool HideWindow {
                                get {
                                    return (bool)base["hideWindow"];
                                }

                                set {
                                    base["hideWindow"] = value;
                                }
                            }
                        }


                        [ConfigurationProperty("serverModeTemplate", IsRequired = false)]
                        public ModeTemplateElement ServerModeTemplate {
                            get {
                                return (ModeTemplateElement)base["serverModeTemplate"];
                            }

                            set {
                                base["serverModeTemplate"] = value;
                            }
                        }


                        [ConfigurationProperty("softwareModeTemplate", IsRequired = false)]
                        public SoftwareModeTemplateElement SoftwareModeTemplate {
                            get {
                                return (SoftwareModeTemplateElement)base["softwareModeTemplate"];
                            }

                            set {
                                base["softwareModeTemplate"] = value;
                            }
                        }
                    }

                    [ConfigurationProperty("modeTemplates", IsRequired = false)]
                    public ModeTemplatesElement ModeTemplates {
                        get {
                            return (ModeTemplatesElement)base["modeTemplates"];
                        }

                        set {
                            base["modeTemplates"] = value;
                        }
                    }

                    public class EnvironmentVariablesElementCollection : ModificationsConfigurationElementCollection {
                        public class EnvironmentVariablesElement : ConfigurationElement {
                            [ConfigurationProperty("name", IsRequired = true)]
                            public string Name {
                                get {
                                    if (String.IsNullOrEmpty(base["name"] as string)) {
                                        return base["name"] as string;
                                    }
                                    return (base["name"] as string).ToUpper();
                                }

                                set {
                                    if (String.IsNullOrEmpty(value)) {
                                        base["name"] = value;
                                        return;
                                    }

                                    base["name"] = value.ToUpper();
                                }
                            }

                            [ConfigurationProperty("value", IsRequired = true)]
                            public string Value {
                                get {
                                    return base["value"] as string;
                                }

                                set {
                                    base["value"] = value;
                                }
                            }
                        }

                        protected override object GetElementKey(ConfigurationElement configurationElement) {
                            EnvironmentVariablesElement environmentVariablesElement = configurationElement as EnvironmentVariablesElement;
                            return environmentVariablesElement.Name;
                        }

                        protected override ConfigurationElement CreateNewElement() {
                            return new EnvironmentVariablesElement();
                        }

                        new public ConfigurationElement Get(string name) {
                            name = name.ToUpper();
                            return base.Get(name);
                        }

                        new public void Remove(string name) {
                            name = name.ToUpper();
                            base.Remove(name);
                        }
                    }

                    [ConfigurationProperty("environmentVariables", IsRequired = false)]
                    [ConfigurationCollection(typeof(EnvironmentVariablesElementCollection), AddItemName = "environmentVariable")]
                    public EnvironmentVariablesElementCollection EnvironmentVariables {
                        get {
                            return (EnvironmentVariablesElementCollection)base["environmentVariables"];
                        }

                        set {
                            base["environmentVariables"] = value;
                        }
                    }

                    public class DownloadBeforeElementCollection : ModificationsConfigurationElementCollection {
                        public class DownloadBeforeElement : ConfigurationElement {
                            [ConfigurationProperty("name", IsRequired = true)]
                            public string Name {
                                get {
                                    return base["name"] as string;
                                }

                                set {
                                    base["name"] = value;
                                }
                            }
                        }

                        protected override object GetElementKey(ConfigurationElement configurationElement) {
                            DownloadBeforeElement downloadBeforeElement = configurationElement as DownloadBeforeElement;
                            return downloadBeforeElement.Name;
                        }

                        protected override ConfigurationElement CreateNewElement() {
                            return new DownloadBeforeElement();
                        }
                    }

                    [ConfigurationProperty("downloadsBefore", IsRequired = false)]
                    [ConfigurationCollection(typeof(DownloadBeforeElementCollection), AddItemName = "downloadBefore")]
                    public DownloadBeforeElementCollection DownloadsBefore {
                        get {
                            return (DownloadBeforeElementCollection)base["downloadsBefore"];
                        }

                        set {
                            base["downloadsBefore"] = value;
                        }
                    }

                    public class RegistryBackupElementCollection : ModificationsConfigurationElementCollection {
                        public class RegistryBackupElement : ConfigurationElement {
                            [ConfigurationProperty("type", DefaultValue = global::FlashpointSecurePlayer.RegistryBackups.TYPE.KEY, IsRequired = false)]
                            public RegistryBackups.TYPE Type {
                                get {
                                    return (RegistryBackups.TYPE)base["type"];
                                }

                                set {
                                    base["type"] = value;
                                }
                            }

                            [ConfigurationProperty("keyName", IsRequired = true)]
                            public string KeyName {
                                get {
                                    if (String.IsNullOrEmpty(base["keyName"] as string)) {
                                        return base["keyName"] as string;
                                    }
                                    return (base["keyName"] as string).ToUpper();
                                }

                                set {
                                    if (String.IsNullOrEmpty(value)) {
                                        base["keyName"] = value;
                                        return;
                                    }

                                    base["keyName"] = value.ToUpper();
                                }
                            }

                            [ConfigurationProperty("valueName", IsRequired = false)]
                            public string ValueName {
                                get {
                                    if (String.IsNullOrEmpty(base["valueName"] as string)) {
                                        return base["valueName"] as string;
                                    }
                                    return (base["valueName"] as string).ToUpper();
                                }

                                set {
                                    if (String.IsNullOrEmpty(value)) {
                                        base["valueName"] = value;
                                        return;
                                    }

                                    base["valueName"] = value.ToUpper();
                                }
                            }

                            [ConfigurationProperty("value", IsRequired = false)]
                            public string Value {
                                get {
                                    return base["value"] as string;
                                }

                                set {
                                    base["value"] = value;
                                }
                            }

                            [ConfigurationProperty("valueKind", IsRequired = false)]
                            public RegistryValueKind? ValueKind {
                                get {
                                    return base["valueKind"] as RegistryValueKind?;
                                }

                                set {
                                    base["valueKind"] = value;
                                }
                            }

                            [ConfigurationProperty("_deleted", IsRequired = false)]
                            public string _Deleted {
                                get {
                                    return base["_deleted"] as string;
                                }

                                set {
                                    base["_deleted"] = value;
                                }
                            }

                            public string Name {
                                get {
                                    return this.KeyName + "\\" + this.ValueName;
                                }
                            }
                        }

                        protected override object GetElementKey(ConfigurationElement configurationElement) {
                            RegistryBackupElement registryBackupElement = configurationElement as RegistryBackupElement;
                            return registryBackupElement.Name;
                        }

                        protected override ConfigurationElement CreateNewElement() {
                            return new RegistryBackupElement();
                        }

                        new public ConfigurationElement Get(string name) {
                            name = name.ToUpper();
                            return base.Get(name);
                        }

                        new public void Remove(string name) {
                            name = name.ToUpper();
                            base.Remove(name);
                        }

                        [ConfigurationProperty("binaryType", IsRequired = true)]
                        public BINARY_TYPE BinaryType {
                            get {
                                return (BINARY_TYPE)base["binaryType"];
                            }

                            set {
                                base["binaryType"] = value;
                            }
                        }
                    }

                    [ConfigurationProperty("registryBackups", IsRequired = false)]
                    [ConfigurationCollection(typeof(RegistryBackupElementCollection), AddItemName = "registryBackup")]
                    public RegistryBackupElementCollection RegistryBackups {
                        get {
                            return (RegistryBackupElementCollection)base["registryBackups"];
                        }

                        set {
                            base["registryBackups"] = value;
                        }
                    }

                    public class SingleInstanceElement : ConfigurationElement {
                        [ConfigurationProperty("strict", DefaultValue = false, IsRequired = false)]
                        public bool Strict {
                            get {
                                return (bool)base["strict"];
                            }

                            set {
                                base["strict"] = value;
                            }
                        }

                        [ConfigurationProperty("commandLine", IsRequired = false)]
                        public string CommandLine {
                            get {
                                return base["commandLine"] as string;
                            }

                            set {
                                base["commandLine"] = value;
                            }
                        }
                    }

                    [ConfigurationProperty("singleInstance", IsRequired = false)]
                    public SingleInstanceElement SingleInstance {
                        get {
                            return (SingleInstanceElement)base["singleInstance"];
                        }

                        set {
                            base["singleInstance"] = value;
                        }
                    }
                }

                protected override object GetElementKey(ConfigurationElement configurationElement) {
                    ModificationsElement modificationsElement = configurationElement as ModificationsElement;
                    return modificationsElement.Name;
                }

                protected override ConfigurationElement CreateNewElement() {
                    return new ModificationsElement();
                }

                new public ModificationsElement Get(string name) {
                    name = name.ToLower();
                    return base.Get(name) as ModificationsElement;
                }

                new public void Remove(string name) {
                    name = name.ToLower();
                    base.Remove(name);
                }
            }

            [ConfigurationProperty("modifications", IsRequired = true)]
            [ConfigurationCollection(typeof(ModificationsElementCollection), AddItemName = "modification")]
            public ModificationsElementCollection Modifications {
                get {
                    return (ModificationsElementCollection)base["modifications"];
                }

                set {
                    base["modifications"] = value;
                }
            }
        }

        private static readonly object exeConfigurationNameLock = new object();
        private static string exeConfigurationName = null;
        public const string ACTIVE_EXE_CONFIGURATION_NAME = "";
        private static readonly object exeConfigurationLock = new object();
        private static Configuration exeConfiguration = null;
        private static readonly object activeEXEConfigurationLock = new object();
        private static Configuration activeEXEConfiguration = null;

        private static string EXEConfigurationName {
            get {
                lock (exeConfigurationNameLock) {
                    return exeConfigurationName;
                }
            }

            set {
                lock (exeConfigurationNameLock) {
                    exeConfigurationName = value;
                }
            }
        }

        private static Configuration EXEConfiguration {
            get {
                lock (exeConfigurationLock) {
                    return exeConfiguration;
                }
            }

            set {
                lock (exeConfigurationLock) {
                    exeConfiguration = value;
                }
            }
        }

        private static Configuration ActiveEXEConfiguration {
            get {
                lock (activeEXEConfigurationLock) {
                    return activeEXEConfiguration;
                }
            }

            set {
                lock (activeEXEConfigurationLock) {
                    activeEXEConfiguration = value;
                }
            }
        }

        public static bool TestLaunchedAsAdministratorUser() {
            AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);

            try {
                return (Thread.CurrentPrincipal as WindowsPrincipal).IsInRole(WindowsBuiltInRole.Administrator);
            } catch (NullReferenceException) {
                return false;
            }
        }

        public static async Task DownloadAsync(string name) {
            await DownloadSemaphoreSlim.WaitAsync().ConfigureAwait(false);

            try {
                using (HttpResponseMessage httpResponseMessage = await HTTPClient.GetAsync(name, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false)) {
                    // now we loop through the stream and read it
                    // to the end in chunks
                    // we can't use StreamReader.ReadToEnd because
                    // the result of the function could use too much memory
                    // for large files
                    // temporary buffer so we don't always reallocate this
                    byte[] streamReadBuffer = new byte[STREAM_READ_LENGTH];
                    int characterNumber = 0;

                    do {
                        // if for whatever reason there's a problem
                        // just ignore this download
                        try {
                            characterNumber = await stream.ReadAsync(streamReadBuffer, 0, STREAM_READ_LENGTH).ConfigureAwait(false);
                        } catch (ArgumentNullException) {
                            break;
                        } catch (ArgumentOutOfRangeException) {
                            break;
                        } catch (ArgumentException) {
                            break;
                        } catch (NotSupportedException) {
                            break;
                        } catch (ObjectDisposedException) {
                            break;
                        } catch (InvalidOperationException) {
                            break;
                        }
                    } while (characterNumber > 0);
                }
            } catch (ArgumentNullException) {
                throw new Exceptions.DownloadFailedException();
            } catch (HttpRequestException) {
                throw new Exceptions.DownloadFailedException();
            } finally {
                DownloadSemaphoreSlim.Release();
            }
        }

        private static string GetValidEXEConfigurationName(string name) {
            if (String.IsNullOrEmpty(name)) {
                throw new ConfigurationErrorsException();
            }

            string invalidNameCharacters = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex invalidNameCharactersRegex = new Regex("[" + Regex.Escape(invalidNameCharacters) + "]+");
            return invalidNameCharactersRegex.Replace(name, ".").ToLower();
        }

        public static async Task DownloadEXEConfiguration(string name) {
            try {
                name = GetValidEXEConfigurationName(name);
                await DownloadAsync("http://" + CONFIGURATION_DOWNLOAD_NAME + "/" + name + ".config").ConfigureAwait(false);
            } catch (Exceptions.DownloadFailedException) {
                // Fail silently.
            } catch (ConfigurationErrorsException) {
                // Fail silently.
            }
        }

        private static Configuration GetActiveEXEConfiguration() {
            // get cached if exists
            if (ActiveEXEConfiguration != null) {
                return ActiveEXEConfiguration;
            }

            try {
                // open from configuration folder
                ActiveEXEConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                if (ActiveEXEConfiguration == null) {
                    throw new ConfigurationErrorsException();
                }

                if (!ActiveEXEConfiguration.HasFile) {
                    throw new ConfigurationErrorsException();
                }
                // success!
                return ActiveEXEConfiguration;
            } catch (ConfigurationErrorsException) {
                // Fail silently.
            }

            if (ActiveEXEConfiguration == null) {
                throw new ConfigurationErrorsException();
            }

            // create anew
            ActiveEXEConfiguration.Save(ConfigurationSaveMode.Modified);
            // open the new one
            ActiveEXEConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None) ?? throw new ConfigurationErrorsException();
            return ActiveEXEConfiguration;
        }
        
        // where should ConfigurationErrorsExceptions all be caught? Review this
        private static Configuration GetEXEConfiguration(bool create, string name) {
            // active
            if (String.IsNullOrEmpty(name)) {
                return GetActiveEXEConfiguration();
            }
            
            name = GetValidEXEConfigurationName(name);

            // caching...
            if (name == EXEConfigurationName) {
                return EXEConfiguration;
            }

            // now goal is to set EXEConfiguration...
            ExeConfigurationFileMap exeConfigurationFileMap = new ExeConfigurationFileMap {
                ExeConfigFilename = Application.StartupPath + "\\" + CONFIGURATION_FOLDER_NAME + "\\" + name + ".config"
            };

            Configuration exeConfiguration = null;

            try {
                // open from configuration folder
                exeConfiguration = ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, ConfigurationUserLevel.None);

                if (exeConfiguration == null) {
                    throw new ConfigurationErrorsException();
                }

                if (!exeConfiguration.HasFile) {
                    throw new ConfigurationErrorsException();
                }
            } catch (ConfigurationErrorsException) {
                try {
                    // nope, so open from configuration download
                    EXEConfiguration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap {
                        ExeConfigFilename = Application.StartupPath + "\\" + HTDOCS + "\\" + CONFIGURATION_DOWNLOAD_NAME + "\\" + name + ".config"
                    }, ConfigurationUserLevel.None);

                    if (EXEConfiguration == null) {
                        throw new ConfigurationErrorsException();
                    }

                    if (!EXEConfiguration.HasFile) {
                        throw new ConfigurationErrorsException();
                    }

                    // we got here so success
                    EXEConfigurationName = name;
                    return EXEConfiguration;
                } catch (Exceptions.DownloadFailedException) {
                    // Fail silently.
                } catch (ConfigurationErrorsException) {
                    // Fail silently.
                }
            }

            if (exeConfiguration == null) {
                throw new ConfigurationErrorsException();
            }

            if (!create) {
                if (!exeConfiguration.HasFile) {
                    throw new ConfigurationErrorsException();
                }
            }

            EXEConfiguration = exeConfiguration;
            EXEConfigurationName = name;
            return EXEConfiguration;
        }

        public static FlashpointSecurePlayerSection GetFlashpointSecurePlayerSection(bool create, string exeConfigurationName) {
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = null;
            Configuration exeConfiguration = null;

            if (String.IsNullOrEmpty(exeConfigurationName)) {
                if (Shared.activeFlashpointSecurePlayerSection != null) {
                    return Shared.activeFlashpointSecurePlayerSection;
                }

                exeConfiguration = GetActiveEXEConfiguration();
            } else {
                if (GetValidEXEConfigurationName(exeConfigurationName) == EXEConfigurationName && Shared.flashpointSecurePlayerSection != null) {
                    return Shared.flashpointSecurePlayerSection;
                }
                
                exeConfiguration = GetEXEConfiguration(create, exeConfigurationName);
            }

            try {
                // initial attempt
                flashpointSecurePlayerSection = exeConfiguration.GetSection("flashpointSecurePlayer") as FlashpointSecurePlayerSection;
            } catch (ConfigurationErrorsException ex) {
                // Fail silently.
            }

            if (flashpointSecurePlayerSection == null) {
                // create the section
                try {
                    exeConfiguration.Sections.Add("flashpointSecurePlayer", new FlashpointSecurePlayerSection());
                } catch (ArgumentException) {
                    throw new ConfigurationErrorsException();
                }

                // reload it into the configuration
                // edit: prevent download lockout
                //exeConfiguration.Save(ConfigurationSaveMode.Modified);
                //ConfigurationManager.RefreshSection("flashpointSecurePlayer");
                // get the section
                flashpointSecurePlayerSection = exeConfiguration.GetSection("flashpointSecurePlayer") as FlashpointSecurePlayerSection;
            }

            if (flashpointSecurePlayerSection == null) {
                // section was not created?
                throw new ConfigurationErrorsException();
            }

            if (String.IsNullOrEmpty(exeConfigurationName)) {
                Shared.activeFlashpointSecurePlayerSection = flashpointSecurePlayerSection;
            } else {
                Shared.flashpointSecurePlayerSection = flashpointSecurePlayerSection;
            }
            return flashpointSecurePlayerSection;
        }

        public static void SetFlashpointSecurePlayerSection(string exeConfigurationName) {
            Configuration activeEXEConfiguration = GetActiveEXEConfiguration();
            activeEXEConfiguration.Save(ConfigurationSaveMode.Modified);

            if (!String.IsNullOrEmpty(exeConfigurationName)) {
                Configuration exeConfiguration = GetEXEConfiguration(true, exeConfigurationName);
                exeConfiguration.Save(ConfigurationSaveMode.Modified);
            }

            ConfigurationManager.RefreshSection("flashpointSecurePlayer");
        }

        // does not save!
        public static FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement GetModificationsElement(bool createModificationsElement, string exeConfigurationName) {
            // need the section to operate on
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(createModificationsElement, exeConfigurationName);
            // get the element
            FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement modificationsElement = flashpointSecurePlayerSection.Modifications.Get(exeConfigurationName) as FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement;

            // create it if it doesn't exist...
            if (createModificationsElement && modificationsElement == null) {
                // ...by new'ing it...
                modificationsElement = new FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement() {
                    Name = exeConfigurationName
                };
                
                // and setting it (which does not save!)
                SetModificationsElement(modificationsElement, exeConfigurationName);
            }
            // exit scene
            return modificationsElement;
        }

        // does not save!
        public static FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement GetActiveModificationsElement(bool createModificationsElement, string exeConfigurationName = ACTIVE_EXE_CONFIGURATION_NAME) {
            FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement modificationsElement = GetModificationsElement(createModificationsElement, ACTIVE_EXE_CONFIGURATION_NAME);

            if (!String.IsNullOrEmpty(exeConfigurationName) && modificationsElement != null) {
                modificationsElement.Active = exeConfigurationName;
            }

            return modificationsElement;
        }

        // does not save!
        public static void SetModificationsElement(FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement modificationsElement, string exeConfigurationName) {
            // need the section to operate on
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(true, exeConfigurationName);
            // set it, and forget it
            // it gets replaced if it existed already
            flashpointSecurePlayerSection.Modifications.Set(modificationsElement);
        }

        // find path in registry value
        // string must begin with path
        // string cannot exceed MAX_PATH*2+15 characters
        public static object AddVariablesToValue(object value) {
            // since it's a value we'll just check it exists
            if (!(value is string valueString)) {
                return value;
            }

            if (valueString.Length <= MAX_PATH * 2 + 15) {
                StringBuilder path = new StringBuilder(MAX_PATH);
                GetShortPathName(Application.StartupPath, path, path.Capacity);

                if (path.Length > 0) {
                    if (valueString.ToUpper().IndexOf(path.ToString().ToUpper()) == 0) {
                        valueString = "%FLASHPOINTSECUREPLAYERSTARTUPPATH%\\" + valueString.Substring(path.Length);
                    }
                }

                path = new StringBuilder(MAX_PATH);
                GetLongPathName(Application.StartupPath, path, path.Capacity);

                if (path.Length > 0) {
                    if (valueString.ToUpper().IndexOf(path.ToString().ToUpper()) == 0) {
                        valueString = "%FLASHPOINTSECUREPLAYERSTARTUPPATH%\\" + valueString.Substring(path.Length);
                    }
                }
            }
            return valueString;
        }

        public static object RemoveVariablesFromValue(object value) {
            // TODO: multistrings?
            if (!(value is string valueString)) {
                return value;
            }

            if (valueString.ToUpper().IndexOf("%FLASHPOINTSECUREPLAYERSTARTUPPATH%") == 0) {
                valueString = Application.StartupPath + "\\" + valueString.Substring(35);
            }
            return valueString;
        }

        public static void SetProgressBarState(ProgressBar progessBar, IntPtr progressBarState) {
            SendMessage(progessBar.Handle, PBM_SETSTATE, (IntPtr)progressBarState, IntPtr.Zero);
        }

        public static bool GetCommandLineArgument(string commandLine, out string commandLineArgument) {
            commandLineArgument = "";
            Regex commandLineQuotes = new Regex("^\\s*\"[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*\"? ?");
            Regex commandLineWords = new Regex("^\\s*\\S+ ?");
            MatchCollection matchResults = commandLineQuotes.Matches(commandLine);

            if (matchResults.Count > 0) {
                commandLineArgument = matchResults[0].Value;
                return true;
            } else {
                matchResults = commandLineWords.Matches(commandLine);

                if (matchResults.Count > 0) {
                    commandLineArgument = matchResults[0].Value;
                    return true;
                }
            }
            return false;
        }

        public static string GetCommandLineArgumentRange(string commandLine, int begin, int end) {
            List<string> commandLineArguments = new List<string>();
            string commandLineArgument = "";
            string commandLineArgumentRange = "";

            while (GetCommandLineArgument(commandLine, out commandLineArgument)) {
                commandLineArguments.Add(commandLineArgument);
                commandLine = commandLine.Substring(commandLineArgument.Length);
            }

            if (end < 0) {
                end += commandLineArguments.Count + 1;
            }

            for (int i = 0;i < commandLineArguments.Count;i++) {
                if (i >= end) {
                    break;
                }

                if (i >= begin) {
                    commandLineArgumentRange += commandLineArguments[i];
                }
            }
            return commandLineArgumentRange;
        }

        public static string[] CommandLineToArgv(string commandLine, out int argc) {
            argc = 0;
            string[] argv;
            IntPtr argvPointer = IntPtr.Zero;
            argvPointer = CommandLineToArgvW(commandLine, out argc);

            if (argvPointer == IntPtr.Zero) {
                throw new Win32Exception();
            }

            try {
                argv = new string[argc];

                for (int i = 0;i < argc;i++) {
                    argv[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argvPointer, i * IntPtr.Size));
                }
            } finally {
                LocalFree(argvPointer);
            }
            return argv;
        }

        public static void RestartApplication(bool runAsAdministrator, Form form, string applicationMutexName = null) {
            ProcessStartInfo processStartInfo = new ProcessStartInfo {
                FileName = Application.ExecutablePath,
                // can't use GetCommandLineArgs() and String.Join because arguments that were in quotes will lose their quotes
                // need to use Environment.CommandLine and find arguments

                Arguments = GetCommandLineArgumentRange(Environment.CommandLine, 1, -1)
            };

            if (runAsAdministrator) {
                processStartInfo.UseShellExecute = true;
                processStartInfo.Verb = "runas";
            }

            if (!String.IsNullOrEmpty(applicationMutexName)) {
                // default to false in case of error
                bool createdNew = false;
                // will not signal the Mutex if it has not already been
                Mutex applicationMutex = new Mutex(false, applicationMutexName, out createdNew);

                if (!createdNew) {
                    applicationMutex.ReleaseMutex();
                }
            }

            // hide the current form so two windows are not open at once
            form.Hide();
            form.ControlBox = true;
            // no this is not a race condition
            // https://stackoverflow.com/questions/33042010/in-what-cases-does-the-process-start-method-return-false
            Process.Start(processStartInfo);
            Application.Exit();
        }

        public static string GetWindowsVersionName(bool edition, bool servicePack, bool architecture) {
            OperatingSystem operatingSystem = Environment.OSVersion;
            string versionName = "Windows ";

            if (operatingSystem.Platform == PlatformID.Win32Windows) {
                switch (operatingSystem.Version.Minor) {
                    case 0:
                    versionName += "95";
                    break;
                    case 10:
                    if (operatingSystem.Version.Revision.ToString() == "2222A") {
                        versionName += "98 SE";
                    } else {
                        versionName += "98";
                    }
                    break;
                    default:
                    // Windows ME is the last version of Windows before Windows NT
                    versionName += "ME";
                    break;
                }
            } else {
                switch (operatingSystem.Version.Major) {
                    case 3:
                    versionName += "NT 3.51";
                    break;
                    case 4:
                    versionName += "NT 4.0";
                    break;
                    case 5:
                    if (operatingSystem.Version.Minor == 0) {
                        versionName += "2000";
                    } else {
                        if (IsOS(OS_TYPE.OS_ANYSERVER)) {
                            versionName += "Server 2003";
                        } else {
                            versionName += "XP";
                        }
                    }
                    break;
                    case 6:
                    switch (operatingSystem.Version.Minor) {
                        case 0:
                        if (IsOS(OS_TYPE.OS_ANYSERVER)) {
                            versionName += "Server 2008";
                        } else {
                            versionName += "Vista";
                        }
                        break;
                        case 1:
                        if (IsOS(OS_TYPE.OS_ANYSERVER)) {
                            versionName += "Server 2008 R2";
                        } else {
                            versionName += "7";
                        }
                        break;
                        case 2:
                        if (IsOS(OS_TYPE.OS_ANYSERVER)) {
                            versionName += "Server 2012";
                        } else {
                            versionName += "8";
                        }
                        break;
                        default:
                        if (IsOS(OS_TYPE.OS_ANYSERVER)) {
                            versionName += "Server 2012 R2";
                        } else {
                            versionName += "8.1";
                        }
                        break;
                    }
                    break;
                    default:
                    // Windows 10 will be the last version of Windows
                    if (IsOS(OS_TYPE.OS_ANYSERVER)) {
                        versionName += "Server 2016";
                    } else {
                        versionName += "10";
                    }
                    break;
                }
            }

            if (edition) {
                string editionID = null;

                try {
                    editionID = Microsoft.Win32.Registry.GetValue("HKEY_LOCAL_MACHINE/SOFTWARE/Microsoft/Windows NT/CurrentVersion", "EditionID", null) as string;
                } catch (SecurityException) {
                    // value exists but we can't get it
                    editionID = "";
                } catch (IOException) {
                    // value marked for deletion
                    editionID = null;
                } catch (ArgumentException) {
                    // value doesn't exist
                    editionID = null;
                }
                
                // no way to get the edition before Windows 7
                if (!String.IsNullOrEmpty(editionID)) {
                    versionName += " " + editionID;
                }
            }

            if (servicePack) {
                // can be empty if no service pack is installed
                if (!String.IsNullOrEmpty(operatingSystem.ServicePack)) {
                    versionName += " " + operatingSystem.ServicePack;
                }
            }

            if (architecture) {
                versionName += " " + (Environment.Is64BitOperatingSystem ? "64" : "32") + "-bit";
            }
            return versionName;
        }

        public static Process GetParentProcess() {
            Process parentProcess = null;
            int currentProcessID = Process.GetCurrentProcess().Id;
            int parentProcessID = -1;

            IntPtr parentProcessSnapshotHandle = IntPtr.Zero;
            parentProcessSnapshotHandle = CreateToolhelp32Snapshot(TOOLHELP32CS_SNAPPROCESS, 0);

            if (parentProcessSnapshotHandle == IntPtr.Zero) {
                return parentProcess;
            }

            PROCESSENTRY32 parentProcessEntry = new PROCESSENTRY32 {
                size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(PROCESSENTRY32))
            };

            if (!Process32First(parentProcessSnapshotHandle, ref parentProcessEntry)) {
                return parentProcess;
            }

            do {
                if (currentProcessID == parentProcessEntry.toolHelp32ProcessID) {
                    parentProcessID = (int)parentProcessEntry.toolHelp32ParentProcessID;
                }
            } while (parentProcessID <= 0 && Process32Next(parentProcessSnapshotHandle, ref parentProcessEntry));

            if (parentProcessID > 0) {
                try {
                    parentProcess = Process.GetProcessById(parentProcessID);
                } catch (ArgumentException) {
                    // Fail silently.
                } catch (InvalidOperationException) {
                    // Fail silently.
                }
            }
            return parentProcess;
        }

        public static string GetProcessEXEName(Process process) {
            bool queryResult = false;
            int size = MAX_PATH;
            StringBuilder processEXEName = new StringBuilder(size);

            try {
                string processMainModuleFileName = process.MainModule.FileName;
                queryResult = true;
                processEXEName = new StringBuilder(processMainModuleFileName);
            } catch (NotSupportedException) {
                queryResult = QueryFullProcessImageName(process.Handle, 0, processEXEName, ref size);
            } catch (Win32Exception) {
                queryResult = QueryFullProcessImageName(process.Handle, 0, processEXEName, ref size);
            } catch (InvalidOperationException) {
                queryResult = QueryFullProcessImageName(process.Handle, 0, processEXEName, ref size);
            }

            if (!queryResult) {
                throw new Win32Exception();
            }

            return processEXEName.ToString();
        }
    }

    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FlashpointSecurePlayer());
        }
    }
}