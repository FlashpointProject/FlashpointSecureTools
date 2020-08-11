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
        public static class Exceptions {
            public class ApplicationRestartRequiredException : InvalidOperationException {
                public ApplicationRestartRequiredException() : base() { }
                public ApplicationRestartRequiredException(string message) : base(message) { }
                public ApplicationRestartRequiredException(string message, Exception inner) : base(message, inner) { }
            }

            public class TaskRequiresElevationException : ApplicationRestartRequiredException {
                public TaskRequiresElevationException() : base() { }
                public TaskRequiresElevationException(string message) : base(message) { }
                public TaskRequiresElevationException(string message, Exception inner) : base(message, inner) { }
            }

            public class CompatibilityLayersException : ApplicationRestartRequiredException {
                public CompatibilityLayersException() : base() { }
                public CompatibilityLayersException(string message) : base(message) { }
                public CompatibilityLayersException(string message, Exception inner) : base(message, inner) { }
            }

            public class OldCPUSimulatorRequiresApplicationRestartException : ApplicationRestartRequiredException {
                public OldCPUSimulatorRequiresApplicationRestartException() { }
                public OldCPUSimulatorRequiresApplicationRestartException(string message) : base(message) { }
                public OldCPUSimulatorRequiresApplicationRestartException(string message, Exception inner) : base(message, inner) { }
            }

            public class DownloadFailedException : InvalidOperationException {
                public DownloadFailedException() : base() { }
                public DownloadFailedException(string message) : base(message) { }
                public DownloadFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class ImportFailedException : InvalidOperationException {
                public ImportFailedException() { }
                public ImportFailedException(string message) : base(message) { }
                public ImportFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class ActiveXImportFailedException : ImportFailedException {
                public ActiveXImportFailedException() { }
                public ActiveXImportFailedException(string message) : base(message) { }
                public ActiveXImportFailedException(string message, Exception inner) : base(message, inner) { }
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

            public class InvalidTemplateException : InvalidOperationException {
                public InvalidTemplateException() { }
                public InvalidTemplateException(string message) : base(message) { }
                public InvalidTemplateException(string message, Exception inner) : base(message, inner) { }
            }

            public class InvalidModeException : InvalidTemplateException {
                public InvalidModeException() { }
                public InvalidModeException(string message) : base(message) { }
                public InvalidModeException(string message, Exception inner) : base(message, inner) { }
            }

            public class InvalidModificationException : InvalidTemplateException {
                public InvalidModificationException() { }
                public InvalidModificationException(string message) : base(message) { }
                public InvalidModificationException(string message, Exception inner) : base(message, inner) { }
            }

            public class OldCPUSimulatorFailedException : InvalidModificationException {
                public OldCPUSimulatorFailedException() { }
                public OldCPUSimulatorFailedException(string message) : base(message) { }
                public OldCPUSimulatorFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class EnvironmentVariablesFailedException : InvalidModificationException {
                public EnvironmentVariablesFailedException() { }
                public EnvironmentVariablesFailedException(string message) : base(message) { }
                public EnvironmentVariablesFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public class RegistryStateFailedException : InvalidModificationException {
                public RegistryStateFailedException() { }
                public RegistryStateFailedException(string message) : base(message) { }
                public RegistryStateFailedException(string message, Exception inner) : base(message, inner) { }
            }

            public static void LogExceptionToLauncher(Exception ex) {
                try {
                    Console.Error.WriteLine(ex.Message);
                } catch {
                    // Fail silently.
                }
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

        public const int WM_DESTROY = 0x00000002;
        public const int WM_PAINT = 0x0000000F;
        public const int WM_PARENTNOTIFY = 0x00000210;

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

        public static readonly Task CompletedTask = Task.FromResult(false);

        public const string HTDOCS = "..\\Server\\htdocs";
        public static readonly string[] INDEX_EXTENSIONS = new string[2] { "html", "htm" };
        // there should be only one HTTP Client per application
        // (as of right now though this is exclusively used by DownloadsBefore class)
        private static readonly HttpClientHandler httpClientHandler = new HttpClientHandler {
            Proxy = new WebProxy("127.0.0.1", 22500),
            UseProxy = true
        };

        public static readonly HttpClient httpClient = new HttpClient(httpClientHandler);
        // for best results, this should match
        // the value of FILE_READ_LENGTH constant
        // in the Flashpoint Router
        private const int STREAM_READ_LENGTH = 8192;
        // no parallel downloads
        // if parallel downloads are ever supported, the max value should
        // be changed to the maximum number of parallel downloads allowed
        // (preferably, no more than eight at a time)
        private static SemaphoreSlim downloadSemaphoreSlim = new SemaphoreSlim(1, 1);

        private const string CONFIGURATION_FOLDER_NAME = "FlashpointSecurePlayerConfigs";
        private const string CONFIGURATION_DOWNLOAD_NAME = "flashpointsecureplayerconfigs";
        private static FlashpointSecurePlayerSection flashpointSecurePlayerSection = null;
        private static FlashpointSecurePlayerSection activeFlashpointSecurePlayerSection = null;

        public const string FP_STARTUP_PATH = nameof(FP_STARTUP_PATH);
        public const string FP_URL = nameof(FP_URL);
        public const string FP_ARGUMENTS = nameof(FP_ARGUMENTS);
        public const string FP_HTDOCS_FILE = nameof(FP_HTDOCS_FILE);
        public const string FP_HTDOCS_FILE_DIR = nameof(FP_HTDOCS_FILE_DIR);

        public const string OLD_CPU_SIMULATOR_PATH = "OldCPUSimulator\\OldCPUSimulator.exe";
        public const string OLD_CPU_SIMULATOR_PARENT_PROCESS_FILE_NAME = "OLDCPUSIMULATOR.EXE";

        public abstract class TemplatesConfigurationElementCollection : ConfigurationElementCollection {
            public override ConfigurationElementCollectionType CollectionType {
                get {
                    return ConfigurationElementCollectionType.AddRemoveClearMapAlternate;
                }
            }

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
            public class TemplatesElementCollection : TemplatesConfigurationElementCollection {
                public class TemplateElement : ConfigurationElement {
                    protected ConfigurationPropertyCollection _properties = null;
                    protected ConfigurationProperty _name = null;
                    protected ConfigurationProperty _active = null;
                    protected ConfigurationProperty _mode = null;
                    protected ConfigurationProperty _modifications = null;

                    public TemplateElement() {
                        _properties = new ConfigurationPropertyCollection();

                        _name = new ConfigurationProperty("name", typeof(string), null,
                            ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);
                        _properties.Add(_name);

                        /*
                        if (String.IsNullOrEmpty(Name)) {
                            _active = new ConfigurationProperty("active", typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                            _properties.Add(_active);
                        } else {
                            _mode = new ConfigurationProperty("mode", typeof(ModeElement), null, ConfigurationPropertyOptions.IsRequired);
                            _properties.Add(_mode);
                        }
                        */
                        
                        _active = new ConfigurationProperty("active", typeof(string), null, ConfigurationPropertyOptions.None);
                        _properties.Add(_active);

                        _mode = new ConfigurationProperty("mode", typeof(ModeElement), null, ConfigurationPropertyOptions.None);
                        _properties.Add(_mode);

                        _modifications = new ConfigurationProperty("modifications", typeof(ModificationsElement), null, ConfigurationPropertyOptions.None);
                        _properties.Add(_modifications);
                    }
                    
                    public string Name {
                        get {
                            if (String.IsNullOrEmpty(base[_name] as string)) {
                                return base[_name] as string;
                            }
                            return (base[_name] as string).ToLowerInvariant();
                        }

                        set {
                            if (String.IsNullOrEmpty(value)) {
                                base[_mode] = null;
                                base[_name] = value;
                                return;
                            }

                            base[_active] = null;
                            base[_name] = value.ToLowerInvariant();
                        }
                    }
                    
                    public string Active {
                        get {
                            if (!String.IsNullOrEmpty(Name) || _active == null) {
                                return null;
                            }

                            if (String.IsNullOrEmpty(base[_active] as string)) {
                                return base[_active] as string;
                            }
                            return (base[_active] as string).ToLowerInvariant();
                        }

                        set {
                            if (!String.IsNullOrEmpty(Name) || _active == null) {
                                return;
                            }

                            if (String.IsNullOrEmpty(value)) {
                                base[_active] = value;
                                return;
                            }

                            base[_active] = value.ToLowerInvariant();
                        }
                    }

                    public class ModeElement : ConfigurationElement {
                        public enum NAME {
                            WEB_BROWSER,
                            SOFTWARE
                        }

                        public enum WEB_BROWSER_NAME {
                            INTERNET_EXPLORER
                        }

                        protected ConfigurationPropertyCollection _properties = null;
                        protected ConfigurationProperty _name = null;
                        protected ConfigurationProperty _webBrowserName = null;
                        protected ConfigurationProperty _commandLine = null;
                        protected ConfigurationProperty _workingDirectory = null;
                        protected ConfigurationProperty _hideWindow = null;

                        public ModeElement() {
                            _properties = new ConfigurationPropertyCollection();

                            _name = new ConfigurationProperty("name", typeof(NAME), NAME.WEB_BROWSER,
                                ConfigurationPropertyOptions.IsKey/* | ConfigurationPropertyOptions.IsRequired*/);
                            _properties.Add(_name);

                            /*
                            switch (Name) {
                                case NAME.WEB_BROWSER:
                                _webBrowserName = new ConfigurationProperty("webBrowserName",
                                    typeof(WEB_BROWSER_NAME), WEB_BROWSER_NAME.INTERNET_EXPLORER, ConfigurationPropertyOptions.IsRequired);
                                _properties.Add(_webBrowserName);
                                break;
                                case NAME.SOFTWARE:
                                _commandLine = new ConfigurationProperty("commandLine",
                                    typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                _properties.Add(_commandLine);

                                _hideWindow = new ConfigurationProperty("hideWindow",
                                    typeof(string), null, ConfigurationPropertyOptions.None);
                                _properties.Add(_hideWindow);
                                break;
                            }
                            */
                            
                            _webBrowserName = new ConfigurationProperty("webBrowserName",
                                typeof(WEB_BROWSER_NAME?), WEB_BROWSER_NAME.INTERNET_EXPLORER, ConfigurationPropertyOptions.None);
                            _properties.Add(_webBrowserName);

                            _commandLine = new ConfigurationProperty("commandLine",
                                typeof(string), null, ConfigurationPropertyOptions.None);
                            _properties.Add(_commandLine);

                            _workingDirectory = new ConfigurationProperty("workingDirectory", typeof(string), null, ConfigurationPropertyOptions.None);
                            _properties.Add(_workingDirectory);

                            _hideWindow = new ConfigurationProperty("hideWindow",
                                typeof(bool), false, ConfigurationPropertyOptions.None);
                            _properties.Add(_hideWindow);
                        }
                        
                        public NAME Name {
                            get {
                                return (NAME)base[_name];
                            }

                            set {
                                switch (value) {
                                    case NAME.WEB_BROWSER:
                                    base[_commandLine] = null;
                                    base[_hideWindow] = null;
                                    break;
                                    case NAME.SOFTWARE:
                                    base[_webBrowserName] = null;
                                    break;
                                }

                                base[_name] = value;
                            }
                        }
                        
                        public WEB_BROWSER_NAME? WebBrowserName {
                            get {
                                if (Name != NAME.WEB_BROWSER || _webBrowserName == null) {
                                    return null;
                                }
                                return base[_webBrowserName] as WEB_BROWSER_NAME?;
                            }

                            set {
                                if (Name != NAME.WEB_BROWSER || _webBrowserName == null) {
                                    return;
                                }
                                base[_webBrowserName] = value;
                            }
                        }
                        
                        public string CommandLine {
                            get {
                                if (Name != NAME.SOFTWARE || _commandLine == null) {
                                    return null;
                                }
                                return base[_commandLine] as string;
                            }

                            set {
                                if (Name != NAME.SOFTWARE || _commandLine == null) {
                                    return;
                                }
                                base[_commandLine] = value;
                            }
                        }
                        
                        public string WorkingDirectory {
                            get {
                                if (Name != NAME.SOFTWARE || _workingDirectory == null) {
                                    return null;
                                }
                                return base[_workingDirectory] as string;
                            }

                            set {
                                if (Name != NAME.SOFTWARE || _workingDirectory == null) {
                                    return;
                                }

                                base[_workingDirectory] = value;
                            }
                        }
                        
                        public bool? HideWindow {
                            get {
                                if (Name != NAME.SOFTWARE || _hideWindow == null) {
                                    return null;
                                }
                                return base[_hideWindow] as bool?;
                            }

                            set {
                                if (Name != NAME.SOFTWARE || _hideWindow == null) {
                                    return;
                                }
                                base[_hideWindow] = value;
                            }
                        }

                        protected override ConfigurationPropertyCollection Properties {
                            get {
                                return _properties;
                            }
                        }
                    }
                    
                    public ModeElement Mode {
                        get {
                            if (String.IsNullOrEmpty(Name) || _mode == null) {
                                return null;
                            }
                            return base[_mode] as ModeElement;
                        }

                        set {
                            if (String.IsNullOrEmpty(Name) || _mode == null) {
                                return;
                            }

                            base[_mode] = value;
                        }
                    }

                    public class ModificationsElement : ConfigurationElement {
                        [ConfigurationProperty("runAsAdministrator", DefaultValue = false, IsRequired = false)]
                        public bool RunAsAdministrator {
                            get {
                                return (bool)base["runAsAdministrator"];
                            }

                            set {
                                base["runAsAdministrator"] = value;
                            }
                        }

                        public class EnvironmentVariablesElementCollection : TemplatesConfigurationElementCollection {
                            public class EnvironmentVariablesElement : ConfigurationElement {
                                protected ConfigurationPropertyCollection _properties = null;
                                protected ConfigurationProperty _name = null;
                                protected ConfigurationProperty _find = null;
                                protected ConfigurationProperty _replace = null;
                                protected ConfigurationProperty _value = null;

                                public EnvironmentVariablesElement() {
                                    _properties = new ConfigurationPropertyCollection();

                                    // name not key for multiple find replace operations if so desired
                                    _name = new ConfigurationProperty("name", typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                    _properties.Add(_name);

                                    _find = new ConfigurationProperty("find", typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_find);

                                    /*
                                    _value = new ConfigurationProperty(String.IsNullOrEmpty(Find) ? "value" : "replace",
                                        typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                    _properties.Add(_value);
                                    */

                                    _replace = new ConfigurationProperty("replace",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_replace);

                                    _value = new ConfigurationProperty("value",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_value);
                                }

                                public string Name {
                                    get {
                                        if (String.IsNullOrEmpty(base[_name] as string)) {
                                            return base[_name] as string;
                                        }
                                        return (base[_name] as string).ToUpperInvariant();
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(value)) {
                                            base[_name] = value;
                                            return;
                                        }

                                        base[_name] = value.ToUpperInvariant();
                                    }
                                }
                                
                                public string Find {
                                    get {
                                        return base[_find] as string;
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(value)) {
                                            base[_value] = Value;
                                            base[_replace] = null;
                                        } else {
                                            base[_replace] = Value;
                                            base[_value] = null;
                                        }

                                        base[_find] = value;
                                    }
                                }
                                
                                public string Replace {
                                    get {
                                        return Value;
                                    }

                                    set {
                                        Value = value;
                                    }
                                }
                                
                                public string Value {
                                    get {
                                        if (String.IsNullOrEmpty(Find)) {
                                            return base[_value] as string;
                                        }
                                        return base[_replace] as string;
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(Find)) {
                                            base[_value] = value;
                                            base[_replace] = null;
                                        } else {
                                            base[_replace] = value;
                                            base[_value] = null;
                                        }
                                    }
                                }

                                public string _Key {
                                    get {
                                        return this.Name + "\\" + this.Find;
                                    }
                                }

                                protected override ConfigurationPropertyCollection Properties {
                                    get {
                                        return _properties;
                                    }
                                }
                            }

                            protected override object GetElementKey(ConfigurationElement configurationElement) {
                                EnvironmentVariablesElement environmentVariablesElement = configurationElement as EnvironmentVariablesElement;
                                return environmentVariablesElement._Key;
                            }

                            protected override ConfigurationElement CreateNewElement() {
                                return new EnvironmentVariablesElement();
                            }

                            new public ConfigurationElement Get(string name) {
                                name = name.ToUpperInvariant();
                                return base.Get(name);
                            }

                            new public void Remove(string name) {
                                name = name.ToUpperInvariant();
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

                        public class DownloadBeforeElementCollection : TemplatesConfigurationElementCollection {
                            public class DownloadBeforeElement : ConfigurationElement {
                                [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
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

                        public class RegistryStateElementCollection : TemplatesConfigurationElementCollection {
                            protected ConfigurationPropertyCollection _properties = null;
                            protected ConfigurationProperty _binaryType = null;
                            protected ConfigurationProperty _administrator = null;

                            public RegistryStateElementCollection() {
                                _properties = new ConfigurationPropertyCollection();

                                _binaryType = new ConfigurationProperty("binaryType",
                                    typeof(BINARY_TYPE), BINARY_TYPE.SCS_64BIT_BINARY, ConfigurationPropertyOptions.IsRequired);
                                _properties.Add(_binaryType);

                                //if (String.IsNullOrEmpty(templateName)) {
                                _administrator = new ConfigurationProperty("administrator",
                                    typeof(bool?), false, ConfigurationPropertyOptions.None);
                                _properties.Add(_administrator);
                                //}
                            }

                            public class RegistryStateElement : ConfigurationElement {
                                protected ConfigurationPropertyCollection _properties = null;
                                protected ConfigurationProperty _type = null;
                                protected ConfigurationProperty _keyName = null;
                                protected ConfigurationProperty _valueName = null;
                                protected ConfigurationProperty _value = null;
                                protected ConfigurationProperty _valueKind = null;
                                protected ConfigurationProperty _deleted = null;
                                protected ConfigurationProperty _valueExpanded = null;

                                public RegistryStateElement() {
                                    _properties = new ConfigurationPropertyCollection();

                                    _type = new ConfigurationProperty("type",
                                        typeof(global::FlashpointSecurePlayer.RegistryStates.TYPE),
                                        global::FlashpointSecurePlayer.RegistryStates.TYPE.KEY, ConfigurationPropertyOptions.None);
                                    _properties.Add(_type);

                                    _keyName = new ConfigurationProperty("keyName",
                                        typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                    _properties.Add(_keyName);

                                    _value = new ConfigurationProperty("value",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_value);

                                    _valueKind = new ConfigurationProperty("valueKind",
                                        typeof(RegistryValueKind?), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_valueKind);

                                    /*
                                    if (Type == global::FlashpointSecurePlayer.RegistryStates.TYPE.VALUE) {
                                        _valueName = new ConfigurationProperty("valueName",
                                            typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                        _properties.Add(_valueName);
                                    }
                                    */

                                    _valueName = new ConfigurationProperty("valueName",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_valueName);

                                    //if (String.IsNullOrEmpty(templateName)) {
                                    _deleted = new ConfigurationProperty("deleted",
                                            typeof(string), null, ConfigurationPropertyOptions.None);
                                        _properties.Add(_deleted);

                                        _valueExpanded = new ConfigurationProperty("valueExpanded",
                                            typeof(string), null, ConfigurationPropertyOptions.None);
                                        _properties.Add(_valueExpanded);
                                    //}
                                }
                                
                                public RegistryStates.TYPE Type {
                                    get {
                                        return (RegistryStates.TYPE)base[_type];
                                    }

                                    set {
                                        /*
                                        if (value != global::FlashpointSecurePlayer.RegistryStates.TYPE.VALUE) {
                                            base[_valueName] = null;
                                        }
                                        */

                                        base[_type] = value;
                                    }
                                }
                                
                                public string KeyName {
                                    get {
                                        if (String.IsNullOrEmpty(base[_keyName] as string)) {
                                            return base[_keyName] as string;
                                        }
                                        return (base[_keyName] as string).ToUpperInvariant();
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(value)) {
                                            base[_keyName] = value;
                                            return;
                                        }

                                        base[_keyName] = value.ToUpperInvariant();
                                    }
                                }
                                
                                public string ValueName {
                                    get {
                                        /*
                                        if (Type != global::FlashpointSecurePlayer.RegistryStates.TYPE.VALUE || _valueName == null) {
                                            return null;
                                        }
                                        */

                                        if (String.IsNullOrEmpty(base[_valueName] as string)) {
                                            return base[_valueName] as string;
                                        }
                                        return (base[_valueName] as string).ToUpperInvariant();
                                    }

                                    set {
                                        /*
                                        if (Type != global::FlashpointSecurePlayer.RegistryStates.TYPE.VALUE || _valueName == null) {
                                            return;
                                        }
                                        */

                                        if (String.IsNullOrEmpty(value)) {
                                            base[_valueName] = value;
                                            return;
                                        }

                                        base[_valueName] = value.ToUpperInvariant();
                                    }
                                }
                                
                                public string Value {
                                    get {
                                        return base[_value] as string;
                                    }

                                    set {
                                        base[_value] = value;
                                    }
                                }
                                
                                public RegistryValueKind? ValueKind {
                                    get {
                                        if (_valueKind == null) {
                                            return null;
                                        }

                                        return base[_valueKind] as RegistryValueKind?;
                                    }

                                    set {
                                        if (_valueKind == null) {
                                            return;
                                        }

                                        base[_valueKind] = value;
                                    }
                                }
                                
                                public string _Deleted {
                                    get {
                                        if (_deleted == null) {
                                            return null;
                                        }

                                        return base[_deleted] as string;
                                    }

                                    set {
                                        if (_deleted == null) {
                                            return;
                                        }

                                        base[_deleted] = value;
                                    }
                                }
                                
                                public string _ValueExpanded {
                                    get {
                                        if (_valueExpanded == null) {
                                            return null;
                                        }

                                        return base[_valueExpanded] as string;
                                    }

                                    set {
                                        if (_valueExpanded == null) {
                                            return;
                                        }

                                        base[_valueExpanded] = value;
                                    }
                                }

                                public string Name {
                                    get {
                                        return this.KeyName + "\\" + this.ValueName;
                                    }
                                }

                                protected override ConfigurationPropertyCollection Properties {
                                    get {
                                        return _properties;
                                    }
                                }
                            }

                            protected override object GetElementKey(ConfigurationElement configurationElement) {
                                RegistryStateElement registryStateElement = configurationElement as RegistryStateElement;
                                return registryStateElement.Name;
                            }

                            protected override ConfigurationElement CreateNewElement() {
                                return new RegistryStateElement();
                            }

                            new public ConfigurationElement Get(string name) {
                                name = name.ToUpperInvariant();
                                return base.Get(name);
                            }

                            new public void Remove(string name) {
                                name = name.ToUpperInvariant();
                                base.Remove(name);
                            }
                            
                            public BINARY_TYPE BinaryType {
                                get {
                                    return (BINARY_TYPE)base[_binaryType];
                                }

                                set {
                                    base[_binaryType] = value;
                                }
                            }
                            
                            public bool? _Administrator {
                                get {
                                    if (_administrator == null) {
                                        return null;
                                    }

                                    return base[_administrator] as bool?;
                                }

                                set {
                                    if (_administrator == null) {
                                        return;
                                    }

                                    base[_administrator] = value;
                                }
                            }

                            protected override ConfigurationPropertyCollection Properties {
                                get {
                                    return _properties;
                                }
                            }
                        }

                        [ConfigurationProperty("registryStates", IsRequired = false)]
                        [ConfigurationCollection(typeof(RegistryStateElementCollection), AddItemName = "registryState")]
                        public RegistryStateElementCollection RegistryStates {
                            get {
                                return (RegistryStateElementCollection)base["registryStates"];
                            }

                            set {
                                base["registryStates"] = value;
                            }
                        }

                        public class SingleInstanceElement : ConfigurationElement {
                            [ConfigurationProperty("executable", IsRequired = false)]
                            public string Executable {
                                get {
                                    return base["executable"] as string;
                                }

                                set {
                                    base["executable"] = value;
                                }
                            }

                            [ConfigurationProperty("strict", DefaultValue = false, IsRequired = false)]
                            public bool Strict {
                                get {
                                    return (bool)base["strict"];
                                }

                                set {
                                    base["strict"] = value;
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

                        public class OldCPUSimulatorElement : ConfigurationElement {
                            [ConfigurationProperty("targetRate", DefaultValue = null, IsRequired = false)]
                            public int? TargetRate {
                                get {
                                    return (int?)base["targetRate"];
                                }

                                set {
                                    base["targetRate"] = value;
                                }
                            }

                            [ConfigurationProperty("refreshRate", DefaultValue = null, IsRequired = false)]
                            public int? RefreshRate {
                                get {
                                    return (int?)base["refreshRate"];
                                }

                                set {
                                    base["refreshRate"] = value;
                                }
                            }

                            [ConfigurationProperty("setProcessPriorityHigh", DefaultValue = false, IsRequired = false)]
                            public bool SetProcessPriorityHigh {
                                get {
                                    return (bool)base["setProcessPriorityHigh"];
                                }

                                set {
                                    base["setProcessPriorityHigh"] = value;
                                }
                            }

                            [ConfigurationProperty("setSyncedProcessAffinityOne", DefaultValue = true, IsRequired = false)]
                            public bool SetSyncedProcessAffinityOne {
                                get {
                                    return (bool)base["setSyncedProcessAffinityOne"];
                                }

                                set {
                                    base["setSyncedProcessAffinityOne"] = value;
                                }
                            }

                            [ConfigurationProperty("syncedProcessMainThreadOnly", DefaultValue = true, IsRequired = false)]
                            public bool SyncedProcessMainThreadOnly {
                                get {
                                    return (bool)base["syncedProcessMainThreadOnly"];
                                }

                                set {
                                    base["syncedProcessMainThreadOnly"] = value;
                                }
                            }

                            [ConfigurationProperty("refreshRateFloorFifteen", DefaultValue = true, IsRequired = false)]
                            public bool RefreshRateFloorFifteen {
                                get {
                                    return (bool)base["refreshRateFloorFifteen"];
                                }

                                set {
                                    base["refreshRateFloorFifteen"] = value;
                                }
                            }
                        }

                        [ConfigurationProperty("oldCPUSimulator", IsRequired = false)]
                        public OldCPUSimulatorElement OldCPUSimulator {
                            get {
                                return (OldCPUSimulatorElement)base["oldCPUSimulator"];
                            }

                            set {
                                base["oldCPUSimulator"] = value;
                            }
                        }
                    }
                    
                    public ModificationsElement Modifications {
                        get {
                            return base[_modifications] as ModificationsElement;
                        }

                        set {
                            base[_modifications] = value;
                        }
                    }

                    protected override ConfigurationPropertyCollection Properties {
                        get {
                            return _properties;
                        }
                    }
                }

                protected override object GetElementKey(ConfigurationElement configurationElement) {
                    TemplateElement templateElement = configurationElement as TemplateElement;
                    return templateElement.Name;
                }

                protected override ConfigurationElement CreateNewElement() {
                    return new TemplateElement();
                }

                new public TemplateElement Get(string name) {
                    name = name.ToLowerInvariant();
                    return base.Get(name) as TemplateElement;
                }

                new public void Remove(string name) {
                    name = name.ToLowerInvariant();
                    base.Remove(name);
                }
            }

            [ConfigurationProperty("templates", IsDefaultCollection = true, IsRequired = true)]
            [ConfigurationCollection(typeof(TemplatesElementCollection), AddItemName = "template")]
            public TemplatesElementCollection Templates {
                get {
                    return (TemplatesElementCollection)base["templates"];
                }

                set {
                    base["templates"] = value;
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

        private static class PathNames {
            public static Dictionary<string, StringBuilder> Short { get; set; } = new Dictionary<string, StringBuilder>();
            public static Dictionary<string, StringBuilder> Long { get; set; } = new Dictionary<string, StringBuilder>();
        }

        public static bool TestLaunchedAsAdministratorUser() {
            AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);

            try {
                return (Thread.CurrentPrincipal as WindowsPrincipal).IsInRole(WindowsBuiltInRole.Administrator);
            } catch (NullReferenceException) {
                return false;
            }
        }

        public static void HandleAntecedentTask(Task antecedentTask) {
            if (antecedentTask.IsFaulted) {
                Exception ex = antecedentTask.Exception;

                while (ex is AggregateException && ex.InnerException != null) {
                    ex = ex.InnerException;
                }

                throw ex;
            }
        }

        public static async Task<Uri> DownloadAsync(string name) {
            await downloadSemaphoreSlim.WaitAsync().ConfigureAwait(true);

            try {
                using (HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(name, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true)) {
                    if (!httpResponseMessage.IsSuccessStatusCode) {
                        throw new Exceptions.DownloadFailedException("The HTTP Response Message failed with a Status Code of " + httpResponseMessage.StatusCode + ".");
                    }

                    using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(true)) {
                        bool currentGoalStarted = false;

                        if (httpResponseMessage.Content.Headers.ContentLength != null) {
                            currentGoalStarted = true;
                            ProgressManager.CurrentGoal.Start((int)Math.Ceiling((double)httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault() / STREAM_READ_LENGTH));
                        }

                        try {
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
                                    characterNumber = await stream.ReadAsync(streamReadBuffer, 0, STREAM_READ_LENGTH).ConfigureAwait(true);
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

                                if (currentGoalStarted) {
                                    ProgressManager.CurrentGoal.Steps++;
                                }
                            } while (characterNumber > 0);
                        } finally {
                            if (currentGoalStarted) {
                                ProgressManager.CurrentGoal.Stop();
                            }
                        }
                    }
                    return httpResponseMessage.RequestMessage.RequestUri;
                }
            } catch (Exceptions.DownloadFailedException ex) {
                throw ex;
            } catch (ArgumentException) {
                throw new Exceptions.DownloadFailedException("The download failed because the download name (" + name + ") is invalid.");
            } catch (HttpRequestException) {
                throw new Exceptions.DownloadFailedException("The download failed because the HTTP Request is invalid.");
            } catch (InvalidOperationException) {
                throw new Exceptions.DownloadFailedException("The download failed because the address (" + name + ") was not understood.");
            } finally {
                downloadSemaphoreSlim.Release();
            }
        }

        private static string GetValidEXEConfigurationName(string name) {
            if (String.IsNullOrEmpty(name)) {
                throw new ConfigurationErrorsException("Cannot get Valid EXE Configuration Name.");
            }

            string invalidNameCharacters = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex invalidNameCharactersRegex = new Regex("[" + Regex.Escape(invalidNameCharacters) + "]+");
            return invalidNameCharactersRegex.Replace(name, ".").ToLowerInvariant();
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
                    throw new ConfigurationErrorsException("The Active EXE Configuration is null.");
                }

                if (!ActiveEXEConfiguration.HasFile) {
                    throw new ConfigurationErrorsException("The Active EXE Configuration has no file.");
                }
                // success!
                return ActiveEXEConfiguration;
            } catch (ConfigurationErrorsException) {
                // Fail silently.
            }

            if (ActiveEXEConfiguration == null) {
                throw new ConfigurationErrorsException("The Active EXE Configuration is null.");
            }

            // create anew
            ActiveEXEConfiguration.Save(ConfigurationSaveMode.Modified);
            // open the new one
            ActiveEXEConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None) ?? throw new ConfigurationErrorsException("The Active EXE Configuration is null.");
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

            // be careful if modifying this
            try {
                // open from configuration folder
                exeConfiguration = ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, ConfigurationUserLevel.None);

                if (exeConfiguration == null) {
                    throw new ConfigurationErrorsException("The EXE Configuration is null.");
                }

                if (!exeConfiguration.HasFile) {
                    throw new ConfigurationErrorsException("The EXE Configuration has no file.");
                }
            } catch (ConfigurationErrorsException) {
                try {
                    // nope, so open from configuration download
                    EXEConfiguration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap {
                        ExeConfigFilename = Application.StartupPath + "\\" + HTDOCS + "\\" + CONFIGURATION_DOWNLOAD_NAME + "\\" + name + ".config"
                    }, ConfigurationUserLevel.None);

                    if (EXEConfiguration == null) {
                        throw new ConfigurationErrorsException("The downloaded EXE Configuration is null.");
                    }

                    if (!EXEConfiguration.HasFile) {
                        throw new ConfigurationErrorsException("The downloaded EXE Configuration has no file.");
                    }

                    // we got here so success
                    EXEConfigurationName = name;
                    return EXEConfiguration;
                } catch (Exceptions.DownloadFailedException) {
                    // Fail silently.
                } catch (ConfigurationErrorsException) {
                    // Fail silently.
                } catch (IOException) {
                    throw new ConfigurationErrorsException("The EXE Configuration is in use.");
                }
            } catch (IOException) {
                throw new ConfigurationErrorsException("The EXE Configuration is in use.");
            }

            if (exeConfiguration == null) {
                throw new ConfigurationErrorsException("The EXE Configuration is null.");
            }

            if (!create) {
                if (!exeConfiguration.HasFile) {
                    throw new ConfigurationErrorsException("The EXE Configuration has no file.");
                }
            }

            EXEConfiguration = exeConfiguration;
            EXEConfigurationName = name;
            return EXEConfiguration;
        }

        public static FlashpointSecurePlayerSection GetFlashpointSecurePlayerSection(bool create, string exeConfigurationName) {
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = null;
            Configuration exeConfiguration = null;

            // again, be careful if modifying this
            // get the appropriate cached section if it exists
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

            ConfigurationErrorsException configurationErrorsException = new ConfigurationErrorsException("The flashpointSecurePlayer Section is null.");

            try {
                // initial attempt
                flashpointSecurePlayerSection = exeConfiguration.GetSection("flashpointSecurePlayer") as FlashpointSecurePlayerSection;
            } catch (ConfigurationErrorsException ex) {
                configurationErrorsException = ex;
            }

            if (flashpointSecurePlayerSection == null) {
                // create the section
                try {
                    exeConfiguration.Sections.Add("flashpointSecurePlayer", new FlashpointSecurePlayerSection());
                } catch (ArgumentException) {
                    throw configurationErrorsException;
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
                throw configurationErrorsException;
            }

            // caching...
            if (String.IsNullOrEmpty(exeConfigurationName)) {
                Shared.activeFlashpointSecurePlayerSection = flashpointSecurePlayerSection;
            } else {
                Shared.flashpointSecurePlayerSection = flashpointSecurePlayerSection;
            }
            return flashpointSecurePlayerSection;
        }

        public static void SetFlashpointSecurePlayerSection(string exeConfigurationName) {
            // effectively saving
            Configuration activeEXEConfiguration = GetActiveEXEConfiguration();
            activeEXEConfiguration.Save(ConfigurationSaveMode.Modified);

            if (!String.IsNullOrEmpty(exeConfigurationName)) {
                Configuration exeConfiguration = GetEXEConfiguration(true, exeConfigurationName);
                exeConfiguration.Save(ConfigurationSaveMode.Modified);
            }

            ConfigurationManager.RefreshSection("flashpointSecurePlayer");
        }

        public static async Task DownloadFlashpointSecurePlayerSectionAsync(string name) {
            try {
                // important to use this function particularly - GetEXEConfiguration is for internal use by GetModificationsElement only
                GetFlashpointSecurePlayerSection(false, name);
            } catch (ConfigurationErrorsException) {
                try {
                    name = GetValidEXEConfigurationName(name);
                    await DownloadAsync("http://" + CONFIGURATION_DOWNLOAD_NAME + "/" + name + ".config").ConfigureAwait(false);
                } catch (Exceptions.DownloadFailedException) {
                    // Fail silently.
                } catch (ConfigurationErrorsException) {
                    // Fail silently.
                }
            }
        }

        // does not save!
        public static FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement GetTemplateElement(bool createTemplateElement, string exeConfigurationName) {
            // need the section to operate on
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(createTemplateElement, exeConfigurationName);
            // get the element
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement templateElement = flashpointSecurePlayerSection.Templates.Get(exeConfigurationName) as FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;

            // create it if it doesn't exist...
            if (createTemplateElement && templateElement == null) {
                // ...by new'ing it...
                templateElement = new FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement() {
                    Name = exeConfigurationName
                };

                // and setting it (which does not save!)
                SetTemplateElement(templateElement, exeConfigurationName);
            }
            // exit scene
            return templateElement;
        }

        // does not save!
        public static FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement GetActiveTemplateElement(bool createTemplateElement, string exeConfigurationName = ACTIVE_EXE_CONFIGURATION_NAME) {
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement templateElement = GetTemplateElement(createTemplateElement, ACTIVE_EXE_CONFIGURATION_NAME);

            if (!String.IsNullOrEmpty(exeConfigurationName) && templateElement != null) {
                templateElement.Active = exeConfigurationName;
            }
            return templateElement;
        }

        // does not save!
        public static void SetTemplateElement(FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement templateElement, string exeConfigurationName) {
            // need the section to operate on
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(true, exeConfigurationName);
            // set it, and forget it
            // it gets replaced if it existed already
            flashpointSecurePlayerSection.Templates.Set(templateElement);
        }

        public static void LockActiveTemplateElement() {
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

            if (activeTemplateElement == null) {
                return;
            }

            activeTemplateElement.LockItem = true;
        }

        public static void UnlockActiveTemplateElement() {
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

            if (activeTemplateElement == null) {
                return;
            }

            activeTemplateElement.LockItem = false;
            //activeTemplateElement.RegistryStates.LockItem = false;
        }

        public static string RemoveTrailingSlash(string path) {
            // can be empty, but not null
            if (path == null) {
                return path;
            }

            while (path.Length > 0 && path.Substring(path.Length - 1) == "\\") {
                path = path.Substring(0, path.Length - 1);
            }
            return path;
        }

        public static string RemoveValueStringSlash(string valueString) {
            // can be empty, but not null
            if (valueString == null) {
                return valueString;
            }

            while (valueString.Length > 0 && valueString.Substring(0, 1) == "\\") {
                valueString = valueString.Substring(1);
            }
            return valueString;
        }

        public static object LengthenValue(object value, string path) {
            // since it's a value we'll just check it exists
            if (!(value is string valueString)) {
                return value;
            }

            if (String.IsNullOrEmpty(path)) {
                return value;
            }

            if (valueString.Length <= MAX_PATH * 2 + 15) {
                // get the short path
                StringBuilder shortPathName = null;

                // get cached SHORT path if available, less File IO
                if (!PathNames.Short.TryGetValue(path, out shortPathName) || shortPathName == null) {
                    shortPathName = new StringBuilder(MAX_PATH);
                    GetShortPathName(path, shortPathName, shortPathName.Capacity);
                    PathNames.Short[path] = shortPathName;
                }

                if (shortPathName != null) {
                    if (shortPathName.Length > 0) {
                        // if the value is a short value...
                        if (valueString.ToUpperInvariant().IndexOf(shortPathName.ToString().ToUpperInvariant()) == 0) {
                            // get the long path
                            StringBuilder longPathName = null;

                            // get cached LONG path if available, less File IO
                            if (!PathNames.Long.TryGetValue(path, out longPathName) || longPathName == null) {
                                longPathName = new StringBuilder(MAX_PATH);
                                GetLongPathName(path, longPathName, longPathName.Capacity);
                                PathNames.Long[path] = shortPathName;
                            }

                            if (longPathName != null) {
                                if (longPathName.Length > 0) {
                                    // replace the short path with the long path
                                    valueString = longPathName.ToString() + valueString.Substring(shortPathName.Length);
                                }
                            }
                        }
                    }
                }
            }
            return valueString;
        }

        // find path in registry value
        // string must begin with path
        // string cannot exceed MAX_PATH*2+15 characters
        public static object ReplaceStartupPathEnvironmentVariable(object value) {
            // since it's a value we'll just check it exists
            if (!(value is string valueString)) {
                return value;
            }

            if (valueString.Length <= MAX_PATH * 2 + 15) {
                StringBuilder pathName = null;

                if (!PathNames.Long.TryGetValue(Application.StartupPath, out pathName) || pathName == null) {
                    pathName = new StringBuilder(MAX_PATH);
                    GetLongPathName(Application.StartupPath, pathName, pathName.Capacity);
                    PathNames.Long[Application.StartupPath] = pathName;
                }

                if (pathName != null) {
                    if (pathName.Length > 0) {
                        if (valueString.ToUpperInvariant().IndexOf(RemoveTrailingSlash(pathName.ToString()).ToUpperInvariant()) == 0) {
                            valueString = "%" + FP_STARTUP_PATH + "%\\" + RemoveValueStringSlash(valueString.Substring(pathName.Length));
                        }
                    }
                }
            }
            return valueString;
        }

        /*
        public static object ReplaceStartupPathEnvironmentVariable(object value) {
            if (!(value is string valueString)) {
                return value;
            }

            if (valueString.ToUpper().IndexOf("%" + FLASHPOINT_SECURE_PLAYER_STARTUP_PATH + "%") == 0) {
                valueString = RemoveTrailingSlash(Application.StartupPath) + "\\" + RemoveValueStringSlash(valueString.Substring(35));
            }
            return valueString;
        }
        */

        public static bool GetCommandLineArgument(string commandLine, out string commandLineArgument) {
            commandLineArgument = String.Empty;
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
            string commandLineArgument = String.Empty;
            string commandLineArgumentRange = String.Empty;

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
                throw new Win32Exception("Failed to get the argv pointer.");
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

        public static string GetOldCPUSimulatorProcessStartInfoArguments(FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.OldCPUSimulatorElement oldCPUSimulatorElement, string software) {
            StringBuilder oldCPUSimulatorProcessStartInfoArguments = new StringBuilder("-t ");
            oldCPUSimulatorProcessStartInfoArguments.Append(oldCPUSimulatorElement.TargetRate.GetValueOrDefault());

            if (oldCPUSimulatorElement.RefreshRate != null) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" -r ");
                oldCPUSimulatorProcessStartInfoArguments.Append(oldCPUSimulatorElement.RefreshRate.GetValueOrDefault());
            }

            if (oldCPUSimulatorElement.SetProcessPriorityHigh) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" --set-process-priority-high");
            }

            if (oldCPUSimulatorElement.SetSyncedProcessAffinityOne) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" --set-synced-process-affinity-one");
            }

            if (oldCPUSimulatorElement.SyncedProcessMainThreadOnly) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" --synced-process-main-thread-only");
            }

            if (oldCPUSimulatorElement.RefreshRateFloorFifteen) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" --refresh-rate-floor-fifteen");
            }

            oldCPUSimulatorProcessStartInfoArguments.Append(" -sw ");
            oldCPUSimulatorProcessStartInfoArguments.Append(software);
            return oldCPUSimulatorProcessStartInfoArguments.ToString();
        }

        public static void RestartApplication(bool runAsAdministrator, Form form, ref Mutex applicationMutex, ProcessStartInfo processStartInfo = null) {
            if (processStartInfo == null) {
                processStartInfo = new ProcessStartInfo {
                    FileName = Application.ExecutablePath,
                    // can't use GetCommandLineArgs() and String.Join because arguments that were in quotes will lose their quotes
                    // need to use Environment.CommandLine and find arguments
                    Arguments = GetCommandLineArgumentRange(Environment.CommandLine, 1, -1)
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
                applicationMutex.Dispose();
                applicationMutex = null;
            }

            // hide the current form so two windows are not open at once
            try {
                form.Hide();
                form.ControlBox = true;
                // no this is not a race condition
                // https://stackoverflow.com/questions/33042010/in-what-cases-does-the-process-start-method-return-false
                Process.Start(processStartInfo);
                Application.Exit();
            } catch {
                form.Show();
                ProgressManager.ShowError();
                MessageBox.Show(Properties.Resources.ProcessFailedStart, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                throw new Exceptions.ApplicationRestartRequiredException("The application failed to restart.");
            }
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
                        if (operatingSystem.Version.Build == 14393) {
                            versionName += "Server 2016";
                        } else {
                            versionName += "Server 2019";
                        }
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
                    editionID = String.Empty;
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

        public static void SetWorkingDirectory(ref ProcessStartInfo processStartInfo, string workingDirectory) {
            if (processStartInfo == null) {
                processStartInfo = new ProcessStartInfo();
            }

            processStartInfo.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDirectory);
        }

        public static void HideWindow(ref ProcessStartInfo processStartInfo) {
            if (processStartInfo == null) {
                processStartInfo = new ProcessStartInfo();
            }

            processStartInfo.UseShellExecute = false;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.ErrorDialog = false;
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
                size = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32))
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

        public static string GetProcessName(Process process) {
            bool queryResult = false;
            int size = MAX_PATH;
            StringBuilder processName = new StringBuilder(size);

            try {
                string processMainModuleFileName = process.MainModule.FileName;
                queryResult = true;
                processName = new StringBuilder(processMainModuleFileName);
            } catch (NotSupportedException) {
                queryResult = QueryFullProcessImageName(process.Handle, 0, processName, ref size);
            } catch (Win32Exception) {
                queryResult = QueryFullProcessImageName(process.Handle, 0, processName, ref size);
            } catch (InvalidOperationException) {
                queryResult = QueryFullProcessImageName(process.Handle, 0, processName, ref size);
            }

            if (!queryResult) {
                throw new Win32Exception("Failed to query the Full Process Image Name.");
            }
            return processName.ToString();
        }
    }
}
