using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.RegistryStateElementCollection;

namespace FlashpointSecurePlayer {
    public class RegistryStates : Modifications {
        public enum TYPE {
            KEY,
            VALUE
        };

        //private const int IMPORT_TIMEOUT = 5;
        private const int IMPORT_TIMEOUT = 60;
        private const int IMPORT_MILLISECONDS = 1000;
        private const string IMPORT_RESUME = "FLASHPOINTSECUREPLAYERREGISTRYSTATEIMPORTRESUME";
        private const string IMPORT_PAUSE = "FLASHPOINTSECUREPLAYERREGISTRYSTATEIMPORTPAUSE";

        private string fullPath = null;
        private PathNames pathNames = null;
        private EventWaitHandle resumeEventWaitHandle = new ManualResetEvent(false);
        private Dictionary<ulong, SortedList<DateTime, List<RegistryStateElement>>> queuedModifications = null;
        private Dictionary<ulong, string> kcbModificationKeyNames = null;
        private TraceEventSession kernelSession = null;

        // http://docs.microsoft.com/en-us/windows/win32/winprog64/shared-registry-keys#redirected-shared-and-reflected-keys-under-wow64

        // the docs list of redirected/shared keys is pretty unintuitive here
        // but to summarize the upshot of the redirection rules...

        // there are only two locations where (legitimate) WOW64XXNODE subkeys appear
        // under HKEY_LOCAL_MACHINE\SOFTWARE
        // and under HKEY_LOCAL_MACHINE\SOFTWARE\CLASSES or HKEY_CURRENT_USER\SOFTWARE\CLASSES

        // for HKEY_LOCAL_MACHINE\SOFTWARE, WOW64XXNODE is always used in the 32-bit view
        // *even for shared keys, the WOW64XXNODE equivalent still exists
        // **this is also true on Vista, all the keys the docs says are redirected are handled by this,
        // it's just that on later versions they're shared, but we don't care about that

        // for HKEY_LOCAL_MACHINE\SOFTWARE\CLASSES or HKEY_CURRENT_USER\SOFTWARE\CLASSES
        // WOW64XXNODE is only used for specific subkeys (so not every class ends up there)
        // for example, HKEY_LOCAL_MACHINE\SOFTWARE\CLASSES\WOW64XXNODE\HELLO is not redirected
        // and it's inaccessible in the 32-bit view under HKEY_LOCAL_MACHINE\SOFTWARE\CLASSES\HELLO
        // but HKEY_LOCAL_MACHINE\SOFTWARE\CLASSES\WOW64XXNODE\CLSID is redirected
        // and is accessible in the 32-bit view as HKEY_LOCAL_MACHINE\SOFTWARE\CLASSES\CLSID

        // APPID, PROTOCOLS, and TYPELIB subkeys are symlinks, applying only to HKEY_LOCAL_MACHINE
        // HKEY_LOCAL_MACHINE\SOFTWARE\WOW64XXNODE\CLASSES also has a symlink
        // (but that's already handled, so it doesn't matter)

        // note the trailing slashes on keys to make lookups easier
        private Dictionary<string, List<string>> WOW64KeyList = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) {
            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\", null},
            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\CLASSES\\", new List<string>() {
                "APPID",
                "CLSID",
                "DIRECTSHOW",
                "INTERFACE",
                "MEDIA TYPE",
                "MEDIAFOUNDATION",
                "PROTOCOLS",
                "TYPELIB"
            }},
            {"HKEY_CURRENT_USER\\SOFTWARE\\CLASSES\\", new List<string>() {
                "CLSID",
                "DIRECTSHOW",
                "INTERFACE",
                "MEDIA TYPE",
                "MEDIAFOUNDATION"
            }}
        };

        public RegistryStates(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        ~RegistryStates() {
            if (resumeEventWaitHandle != null) {
                resumeEventWaitHandle.Dispose();
                resumeEventWaitHandle = null;
            }
        }

        private string GetUserKeyValueName(string keyValueName, string activeCurrentUser = null, bool activeAdministrator = true) {
            // can be empty, but not null
            if (keyValueName == null) {
                return keyValueName;
            }

            keyValueName = AddTrailingSlash(keyValueName);

            const string HKEY_CLASSES_ROOT = "HKEY_CLASSES_ROOT\\";
            const string HKEY_LOCAL_MACHINE_SOFTWARE_CLASSES = "HKEY_LOCAL_MACHINE\\SOFTWARE\\CLASSES\\";

            // make this explicitly the machine
            // need this so reverting works across users on the same machine
            // we use HKEY_LOCAL_MACHINE not HKEY_CURRENT_USER because
            // current user settings may be ignored as admin
            // (but this might get changed again down below if not admin)
            if (keyValueName.StartsWith(HKEY_CLASSES_ROOT, StringComparison.OrdinalIgnoreCase)) {
                keyValueName = HKEY_LOCAL_MACHINE_SOFTWARE_CLASSES + keyValueName.Substring(HKEY_CLASSES_ROOT.Length);
            }

            const string HKEY_CURRENT_USER = "HKEY_CURRENT_USER\\";
            const string HKEY_LOCAL_MACHINE = "HKEY_LOCAL_MACHINE\\";

            string keyValueNameCurrentUser = "HKEY_USERS\\" + (String.IsNullOrEmpty(activeCurrentUser)
                ? WindowsIdentity.GetCurrent().User.Value
                : activeCurrentUser) + "\\";

            if (keyValueName.StartsWith(HKEY_CURRENT_USER, StringComparison.OrdinalIgnoreCase)) {
                // make this explicit in case this is a shared computer
                keyValueName = keyValueNameCurrentUser + keyValueName.Substring(HKEY_CURRENT_USER.Length);
            } else if (keyValueName.StartsWith(HKEY_LOCAL_MACHINE, StringComparison.OrdinalIgnoreCase)) {
                if (!activeAdministrator || !TestLaunchedAsAdministratorUser()) {
                    // if activeAdministrator is false, we use HKEY_USERS anyway
                    // because the registry state was created as a non-admin
                    keyValueName = keyValueNameCurrentUser + keyValueName.Substring(HKEY_LOCAL_MACHINE.Length);
                }
            }

            keyValueName = RemoveTrailingSlash(keyValueName);
            return keyValueName;
        }

        private string GetKeyValueNameFromKernelRegistryString(string kernelRegistryString) {
            // can be empty, but not null
            if (kernelRegistryString == null) {
                return kernelRegistryString;
            }

            const string REGISTRY_MACHINE = "\\REGISTRY\\MACHINE\\";

            string keyValueName = String.Empty;
            kernelRegistryString = AddTrailingSlash(kernelRegistryString);

            if (kernelRegistryString.StartsWith(REGISTRY_MACHINE, StringComparison.OrdinalIgnoreCase)) {
                keyValueName = "HKEY_LOCAL_MACHINE\\" + kernelRegistryString.Substring(REGISTRY_MACHINE.Length);
            } else {
                const string REGISTRY_USER = "\\REGISTRY\\USER\\";

                if (kernelRegistryString.StartsWith(REGISTRY_USER, StringComparison.OrdinalIgnoreCase)) {
                    const string HKEY_USERS = "HKEY_USERS\\";

                    keyValueName = HKEY_USERS + kernelRegistryString.Substring(REGISTRY_USER.Length);
                    string currentUser = WindowsIdentity.GetCurrent().User.Value;
                    string keyValueNameCurrentUser = HKEY_USERS + currentUser + "_CLASSES\\";

                    if (keyValueName.StartsWith(keyValueNameCurrentUser, StringComparison.OrdinalIgnoreCase)) {
                        keyValueName = "HKEY_CURRENT_USER\\SOFTWARE\\CLASSES\\" + keyValueName.Substring(keyValueNameCurrentUser.Length);
                    } else {
                        keyValueNameCurrentUser = HKEY_USERS + currentUser + "\\";

                        if (keyValueName.StartsWith(keyValueNameCurrentUser, StringComparison.OrdinalIgnoreCase)) {
                            keyValueName = "HKEY_CURRENT_USER\\" + keyValueName.Substring(keyValueNameCurrentUser.Length);
                        }
                    }
                }
            }

            keyValueName = RemoveTrailingSlash(keyValueName);
            return keyValueName;
        }

        private RegistryKey OpenBaseKeyInRegistryView(string keyName, RegistryView registryView) {
            if (keyName == null) {
                return null;
            }

            keyName = AddTrailingSlash(keyName);

            RegistryHive? registryHive = null;

            if (keyName.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.CurrentUser;
            } else if (keyName.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.LocalMachine;
            } else if (keyName.StartsWith("HKEY_CLASSES_ROOT\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.ClassesRoot;
            } else if (keyName.StartsWith("HKEY_USERS\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.Users;
            } else if (keyName.StartsWith("HKEY_PERFORMANCE_DATA\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.PerformanceData;
            } else if (keyName.StartsWith("HKEY_CURRENT_CONFIG\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.CurrentConfig;
            } else if (keyName.StartsWith("HKEY_DYN_DATA\\", StringComparison.OrdinalIgnoreCase)) {
                registryHive = RegistryHive.DynData;
            }
            
            keyName = RemoveTrailingSlash(keyName);

            if (registryHive == null) {
                return null;
            }
            return RegistryKey.OpenBaseKey(registryHive.GetValueOrDefault(), registryView);
        }

        private RegistryKey OpenKeyInRegistryView(string keyName, bool writable, RegistryView registryView) {
            RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView);

            if (registryKey == null) {
                // base key does not exist
                return null;
            }

            int subKeyNameIndex = keyName.IndexOf("\\") + 1;

            if (subKeyNameIndex > 0) {
                registryKey = registryKey.OpenSubKey(keyName.Substring(subKeyNameIndex), writable);
            }
            return registryKey;
        }

        private RegistryKey CreateKeyInRegistryView(string keyName, RegistryKeyPermissionCheck registryKeyPermissionCheck, RegistryView registryView) {
            RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView);

            if (registryKey == null) {
                // base key does not exist
                return null;
            }

            int subKeyNameIndex = keyName.IndexOf("\\") + 1;

            if (subKeyNameIndex > 0) {
                registryKey = registryKey.CreateSubKey(keyName.Substring(subKeyNameIndex), registryKeyPermissionCheck);
            }
            return registryKey;
        }

        private void SetKeyInRegistryView(string keyName, RegistryView registryView) {
            try {
                using (RegistryKey registryKey = CreateKeyInRegistryView(keyName, RegistryKeyPermissionCheck.Default, registryView)) {
                    if (registryKey == null) {
                        // key is invalid
                        throw new ArgumentException("The key \"" + keyName + "\" is invalid.");
                    }
                }
            } catch (UnauthorizedAccessException) {
                // if the key already exists and can be opened, don't worry about it
                // (this is mainly to deal with permissions issues)
                try {
                    using (RegistryKey registryKey = OpenKeyInRegistryView(keyName, false, registryView)) {
                        if (registryKey != null) {
                            return;
                        }
                    }
                } catch {
                    // fail silently
                }

                throw;
            }
        }

        private void DeleteKeyInRegistryView(string keyName, RegistryView registryView) {
            using (RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView)) {
                if (registryKey == null) {
                    // base key does not exist (good!)
                    return;
                }

                int subKeyNameIndex = keyName.IndexOf("\\") + 1;

                if (subKeyNameIndex > 0) {
                    registryKey.DeleteSubKeyTree(keyName.Substring(subKeyNameIndex), false);
                }
            }
        }

        private object GetValueInRegistryView(string keyName, string valueName, out RegistryValueKind? valueKind, RegistryView registryView) {
            using (RegistryKey registryKey = OpenKeyInRegistryView(keyName, false, registryView)) {
                valueKind = null;

                if (registryKey == null) {
                    // key does not exist
                    return null;
                }

                object value = registryKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

                try {
                    valueKind = GetValueKindInRegistryView(keyName, valueName, registryView);
                } catch (SecurityException ex) {
                    // value exists but we can't get it
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Accessing the value \"" + valueName + "\" in key \"" + keyName + "\" requires elevation.");
                } catch (UnauthorizedAccessException ex) {
                    // value exists but we can't get it
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Accessing the value \"" + valueName + "\" in key \"" + keyName + "\" requires elevation.");
                } catch {
                    // value doesn't exist
                    valueKind = null;
                }

                switch (valueKind) {
                    case RegistryValueKind.Binary:
                    if (value is byte[] binaryValue) {
                        value = Convert.ToBase64String(binaryValue);
                    }
                    break;
                    case RegistryValueKind.MultiString:
                    if (value is string[] multiStringValue) {
                        value = String.Join("\0", multiStringValue);
                    }
                    break;
                }
                return value;
            }
        }

        private void SetValueInRegistryView(string keyName, string valueName, object value, RegistryValueKind valueKind, RegistryView registryView) {
            // save this before converting to Base64 or Array
            object _value = value;

            switch (valueKind) {
                case RegistryValueKind.Binary:
                if (value is string binaryValue) {
                    value = Convert.FromBase64String(binaryValue);
                }
                break;
                case RegistryValueKind.MultiString:
                if (value is string multiStringValue) {
                    value = multiStringValue.Split('\0');
                }
                break;
            }

            try {
                using (RegistryKey registryKey = CreateKeyInRegistryView(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree, registryView)) {
                    if (registryKey == null) {
                        // key is invalid
                        throw new ArgumentException("The key \"" + keyName + "\" is invalid.");
                    }

                    registryKey.SetValue(valueName, value, valueKind);
                }
            } catch (UnauthorizedAccessException) {
                // if the value already exists and is an exact string match, don't worry about it
                // (this is mainly to deal with permissions issues)
                try {
                    if (_value is string valueString) {
                        _value = GetValueInRegistryView(keyName, valueName, out RegistryValueKind? _valueKind, registryView);

                        if (valueKind == _valueKind) {
                            if (_value is string _valueString) {
                                if (valueString.Equals(_valueString, StringComparison.Ordinal)) {
                                    return;
                                }
                            }
                        }
                    }
                } catch {
                    // fail silently
                }

                throw;
            }
        }

        private void DeleteValueInRegistryView(string keyName, string valueName, RegistryView registryView) {
            using (RegistryKey registryKey = OpenKeyInRegistryView(keyName, true, registryView)) {
                if (registryKey == null) {
                    // key does not exist (good!)
                    return;
                }

                registryKey.DeleteValue(valueName, false);
            }
        }

        private RegistryValueKind? GetValueKindInRegistryView(string keyName, string valueName, RegistryView registryView) {
            using (RegistryKey registryKey = OpenKeyInRegistryView(keyName, false, registryView)) {
                if (registryKey == null) {
                    // key does not exist
                    return null;
                }
                return registryKey.GetValueKind(valueName);
            }
        }

        private string TestKeyDeletedInRegistryView(string keyName, RegistryView registryView) {
            if (keyName == null) {
                return keyName;
            }

            List<string> keyNames = keyName.Split('\\').ToList();

            if (!keyNames.Any()) {
                return keyName;
            }

            RegistryKey registryKey = null;

            try {
                registryKey = OpenBaseKeyInRegistryView(keyName, registryView);
            } catch (SecurityException) {
                throw;
            } catch (UnauthorizedAccessException) {
                throw;
            } catch {
                // base key doesn't exist
            }
            
            if (registryKey == null) {
                return keyNames.First();
            }

            List<RegistryKey> registryKeys = new List<RegistryKey>() { registryKey };

            try {
                for (int i = 1; i < keyNames.Count; i++) {
                    try {
                        registryKey = registryKey.OpenSubKey(keyNames[i]);
                    } catch (SecurityException) {
                        throw;
                    } catch (UnauthorizedAccessException) {
                        throw;
                    } catch {
                        // sub key doesn't exist
                        registryKey = null;
                    }

                    if (registryKey == null) {
                        return String.Join("\\", keyNames.Take(i + 1).ToArray());
                    }

                    registryKeys.Insert(i, registryKey);
                }
                return String.Empty;
            } finally {
                for (int i = 0; i < registryKeys.Count; i++) {
                    registryKeys[i].Dispose();
                    registryKeys[i] = null;
                }

                registryKeys = null;
            }
        }

        // older than Windows XP 64-bit
        private readonly bool notWOW64Version = !Environment.Is64BitOperatingSystem
            || Environment.OSVersion.Version < new Version(5, 1);
        
        private string GetRedirectedKeyValueName(string keyValueName, BINARY_TYPE binaryType) {
            if (notWOW64Version) {
                // the version isn't WOW64
                return keyValueName;
            }
            
            if (binaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                // the binary isn't 32-bit
                return keyValueName;
            }

            // can be empty for default value, but not null
            if (keyValueName == null) {
                return keyValueName;
            }

            // for testing: all WOW64XXNODE's should be gone except the last two
            //keyValueName = "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432NODE\\WOW64AANODE\\WOW6432NODE\\CLASSES\\WOW6432NODE\\WOW6432NODE\\CLSID\\WOW6432NODE\\HELLO\\WOW64AANODE";

            string[] keyValueNameSplit = keyValueName.Split('\\');
            int keyValueNameSplitEndIndex = keyValueNameSplit.Length - 1;

            // redirect WOW64XXNODE subkeys
            bool wow64Node = false;
            string subkey = null;
            StringBuilder wow64NodeSubkeys = new StringBuilder();

            bool redirected = false;
            StringBuilder redirectedKeyValueName = new StringBuilder();

            for (int i = 0; i <= keyValueNameSplitEndIndex; i++) {
                wow64Node = false;
                subkey = keyValueNameSplit[i];

                if (subkey.Equals("WOW6432NODE", StringComparison.OrdinalIgnoreCase)
                    || subkey.Equals("WOW64AANODE", StringComparison.OrdinalIgnoreCase)) {
                    wow64Node = true;
                }

                if (wow64Node) {
                    wow64NodeSubkeys.Append(subkey);
                    wow64NodeSubkeys.Append("\\");
                }

                // if this is not a WOW64XXNODE, or we are at the end
                if ((!wow64Node || (wow64Node && i == keyValueNameSplitEndIndex))
                    && wow64NodeSubkeys.Length > 0) {
                    redirected = false;

                    if (WOW64KeyList.TryGetValue(redirectedKeyValueName.ToString(), out List<string> wow64SubkeyList)) {
                        // if there's no subkey list or we are at the end
                        if (wow64SubkeyList == null || wow64Node) {
                            redirected = true;
                        } else {
                            // must equal a subkey from the list
                            for (int j = 0; j < wow64SubkeyList.Count; j++) {
                                if (subkey.Equals(wow64SubkeyList[j], StringComparison.OrdinalIgnoreCase)) {
                                    redirected = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!redirected) {
                        redirectedKeyValueName.Append(wow64NodeSubkeys);
                    }

                    wow64NodeSubkeys.Clear();
                }

                if (!wow64Node) {
                    redirectedKeyValueName.Append(subkey);
                    redirectedKeyValueName.Append("\\");
                }
            }
            return RemoveTrailingSlash(redirectedKeyValueName.ToString());
        }

        private bool CompareKeys(RegistryView registryView, RegistryStateElement registryStateElement, RegistryStateElement activeRegistryStateElement, string activeCurrentUser = null, bool activeAdministrator = true) {
            if (registryStateElement == null || activeRegistryStateElement == null) {
                return true;
            }

            // empty = key existed before
            // null = key ignored
            if (activeRegistryStateElement._Deleted == String.Empty) {
                // key did exist before
                // that means it should still exist
                try {
                    if (!String.IsNullOrEmpty(
                        TestKeyDeletedInRegistryView(
                            GetUserKeyValueName(
                                registryStateElement.KeyName,
                                activeCurrentUser,
                                activeAdministrator
                            ),
                        
                            registryView
                        )
                    )) {
                        // key no longer exists, bad state
                        return false;
                    }
                } catch (SecurityException ex) {
                    // key exists but we can't get it
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Accessing the key \"" + registryStateElement.KeyName + "\" requires elevation.");
                } catch (UnauthorizedAccessException ex) {
                    // key exists but we can't get it
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Accessing the key \"" + registryStateElement.KeyName + "\" requires elevation.");
                }
            }
            return true;
        }

        private bool CompareValues(string value, RegistryValueKind? valueKind, RegistryView registryView, RegistryStateElement registryStateElement, RegistryStateElement activeRegistryStateElement, string activeCurrentUser = null, bool activeAdministrator = true) {
            // caller needs to decide what to do if value is null
            if (value == null) {
                throw new ArgumentNullException("The value is null.");
            }

            if (registryStateElement == null) {
                registryStateElement = activeRegistryStateElement;

                if (registryStateElement == null) {
                    throw new ArgumentNullException("The registryStateElement is null.");
                }
            }

            string registryStateElementValue = registryStateElement.Value;

            if (registryStateElementValue != null) {
                // if value kind is the same as current value kind
                if (valueKind == registryStateElement.ValueKind) {
                    if (activeRegistryStateElement != null) {
                        // account for expanded values
                        registryStateElementValue = String.IsNullOrEmpty(activeRegistryStateElement._ValueExpanded)
                            ? registryStateElementValue
                            : activeRegistryStateElement._ValueExpanded;
                    }

                    // check value matches current value/current expanded value
                    if (value.Equals(registryStateElementValue, StringComparison.Ordinal)) {
                        return true;
                    }

                    // for ActiveX: check if it matches as a path
                    try {
                        if (ComparePaths(value, registryStateElementValue)) {
                            return true;
                        }
                    } catch {
                        // fail silently
                    }
                }
            }

            if (activeRegistryStateElement != null
                && activeRegistryStateElement != registryStateElement) {
                // get value before
                registryStateElementValue = activeRegistryStateElement.Value;

                // if value existed before
                if (registryStateElementValue != null) {
                    // value kind before also matters
                    if (valueKind == activeRegistryStateElement.ValueKind) {
                        // check value matches current value
                        if (value.Equals(registryStateElementValue, StringComparison.Ordinal)) {
                            return true;
                        }

                        // for ActiveX: check if it matches as a path
                        try {
                            if (ComparePaths(value, registryStateElementValue)) {
                                return true;
                            }
                        } catch {
                            // fail silently
                        }
                    }
                }
            }
            return false;
        }

        public async Task StartImportAsync(string templateName, BINARY_TYPE binaryType) {
            base.StartImport(templateName);

            TemplateElement templateElement = GetTemplateElement(true, TemplateName);
            ModificationsElement modificationsElement = templateElement.Modifications;

            // this happens here since this check doesn't need to occur to activate
            if (modificationsElement.RegistryStates.Get(TemplateName) != null) {
                // preset already exists with this name
                // prevent a registry state from running for a non-curator
                throw new InvalidTemplateException("A Template with the name \"" + TemplateName + "\" exists.");
            }

            if (resumeEventWaitHandle == null) {
                throw new InvalidOperationException("resumeEventWaitHandle is null.");
            }

            try {
                fullPath = Path.GetFullPath(TemplateName);
            } catch (SecurityException ex) {
                LogExceptionToLauncher(ex);
                throw new TaskRequiresElevationException("Getting the Full Path to \"" + TemplateName + "\" requires elevation.");
            } catch (PathTooLongException ex) {
                LogExceptionToLauncher(ex);
                throw new ArgumentException("The path is too long to \"" + TemplateName + "\".");
            } catch (NotSupportedException ex) {
                LogExceptionToLauncher(ex);
                throw new ArgumentException("The path to \"" + TemplateName + "\" is not supported.");
            }

            // check permission to run
            if (!TraceEventSession.IsElevated().GetValueOrDefault()) {
                throw new TaskRequiresElevationException("The Trace Event Session requires elevation.");
            }

            if (!TestLaunchedAsAdministratorUser()) {
                throw new TaskRequiresElevationException("The Import requires elevation.");
            }

            // lock close button
            ImportStarted = true;

            try {
                modificationsElement.RegistryStates.BinaryType = binaryType;

                pathNames = new PathNames();

                resumeEventWaitHandle.Reset();

                queuedModifications = new Dictionary<ulong, SortedList<DateTime, List<RegistryStateElement>>>();
                kcbModificationKeyNames = new Dictionary<ulong, string>();

                if (kernelSession != null) {
                    kernelSession.Dispose();
                }

                kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);

                try {
                    try {
                        kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Registry);
                    } catch (Win32Exception ex) {
                        LogExceptionToLauncher(ex);
                        throw new InvalidRegistryStateException("The Kernel Trace Control could not be found.");
                    }

                    kernelSession.Source.Kernel.RegistryQueryValue += GotValue;

                    kernelSession.Source.Kernel.RegistryCreate += ModificationAdded;
                    kernelSession.Source.Kernel.RegistrySetValue += ModificationAdded;
                    kernelSession.Source.Kernel.RegistrySetInformation += ModificationAdded;

                    kernelSession.Source.Kernel.RegistryDelete += ModificationRemoved;
                    kernelSession.Source.Kernel.RegistryDeleteValue += ModificationRemoved;

                    //kernelSession.Source.Kernel.RegistryFlush += RegistryModified;

                    // http://social.msdn.microsoft.com/Forums/en-US/ff07fc25-31e3-4b6f-810e-7a1ee458084b/etw-registry-monitoring?forum=etw
                    kernelSession.Source.Kernel.RegistryKCBCreate += KCBStarted;
                    kernelSession.Source.Kernel.RegistryKCBRundownBegin += KCBStarted;

                    kernelSession.Source.Kernel.RegistryKCBDelete += KCBStopped;
                    kernelSession.Source.Kernel.RegistryKCBRundownEnd += KCBStopped;

                    Thread processThread = new Thread(delegate () {
                        if (kernelSession == null) {
                            return;
                        }

                        kernelSession.Source.Process();
                    });

                    processThread.Start();

                    // ensure the kernel session is actually processing
                    for (int i = 0; i < IMPORT_TIMEOUT; i++) {
                        // we just ping this value so it gets detected we tried to read it
                        Registry.GetValue("HKEY_LOCAL_MACHINE", IMPORT_RESUME, null);

                        if (!ImportPaused) {
                            break;
                        }

                        //if (sync) {
                        //Thread.Sleep(IMPORT_MILLISECONDS);
                        //} else {
                        await Task.Delay(IMPORT_MILLISECONDS).ConfigureAwait(true);
                        //}
                    }

                    if (ImportPaused) {
                        throw new InvalidRegistryStateException("A timeout occured while starting the Import.");
                    }
                } catch {
                    kernelSession.Dispose();
                    kernelSession = null;
                    throw;
                }
            } catch {
                pathNames = null;
                ImportStarted = false;
                throw;
            }
        }

        private async Task StopImportAsync(bool sync) {
            try {
                base.StopImport();
                
                if (resumeEventWaitHandle == null) {
                    throw new InvalidOperationException("resumeEventWaitHandle is null.");
                }

                resumeEventWaitHandle.Set();

                // stop kernelSession
                // we give the registry state a ten second
                // timeout, which should be enough
                for (int i = 0; i < IMPORT_TIMEOUT; i++) {
                    Registry.GetValue("HKEY_LOCAL_MACHINE", IMPORT_PAUSE, null);

                    if (ImportPaused) {
                        break;
                    }

                    if (sync) {
                        Thread.Sleep(IMPORT_MILLISECONDS);
                    } else {
                        await Task.Delay(IMPORT_MILLISECONDS).ConfigureAwait(true);
                    }
                }

                if (!ImportPaused) {
                    throw new InvalidRegistryStateException("A timeout occured while stopping the Import.");
                }

                if (kernelSession != null) {
                    kernelSession.Stop();
                    kernelSession.Source.Kernel.RegistryQueryValue -= GotValue;

                    kernelSession.Source.Kernel.RegistryCreate -= ModificationAdded;
                    kernelSession.Source.Kernel.RegistrySetValue -= ModificationAdded;
                    kernelSession.Source.Kernel.RegistrySetInformation -= ModificationAdded;

                    kernelSession.Source.Kernel.RegistryDelete -= ModificationRemoved;
                    kernelSession.Source.Kernel.RegistryDeleteValue -= ModificationRemoved;

                    //kernelSession.Source.Kernel.RegistryFlush -= RegistryModified;

                    kernelSession.Source.Kernel.RegistryKCBCreate -= KCBStarted;
                    kernelSession.Source.Kernel.RegistryKCBRundownBegin -= KCBStarted;

                    kernelSession.Source.Kernel.RegistryKCBDelete -= KCBStopped;
                    kernelSession.Source.Kernel.RegistryKCBRundownEnd -= KCBStopped;
                }

                SetFlashpointSecurePlayerSection(TemplateName);
            } finally {
                if (kernelSession != null) {
                    kernelSession.Dispose();
                    kernelSession = null;
                }

                pathNames = null;
                ImportStarted = false;
            }
        }

        public override void StopImport() {
            // do not await this, bool hack
#pragma warning disable CS4014
            StopImportAsync(true);
#pragma warning restore CS4014
        }

        public async Task StopImportAsync() {
            await StopImportAsync(false).ConfigureAwait(true);
        }

        public override void Activate(string templateName) {
            lock (activationLock) {
                base.Activate(templateName);

                if (String.IsNullOrEmpty(TemplateName)) {
                    // no argument
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

                TemplateElement activeTemplateElement = GetActiveTemplateElement(true, TemplateName);
                ModificationsElement activeModificationsElement = activeTemplateElement.Modifications;
                int registryStateIndex = 0;
                RegistryStateElement registryStateElement = null;
                RegistryStateElement activeRegistryStateElement = null;
                string keyName = null;
                string value = null;
                RegistryValueKind? valueKind = null;
                string keyDeleted = null;
                string valueExpanded = null;

                /*
                try {
                    fullPath = Path.GetFullPath(TemplateName);
                } catch (PathTooLongException) {
                    throw new ArgumentException("The path is too long to \"" + TemplateName + "\".");
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException("Getting the Full Path to \"" + TemplateName + "\" requires elevation.");
                } catch (NotSupportedException) {
                    throw new ArgumentException("The path to \"" + TemplateName + "\" is not supported.");
                }
                */

                // to prevent issues with HKEY_LOCAL_MACHINE and crash recovery
                activeModificationsElement.RegistryStates.BinaryType = Environment.Is64BitOperatingSystem ? BINARY_TYPE.SCS_64BIT_BINARY : BINARY_TYPE.SCS_32BIT_BINARY;
                activeModificationsElement.RegistryStates._CurrentUser = WindowsIdentity.GetCurrent().User.Value;
                activeModificationsElement.RegistryStates._Administrator = TestLaunchedAsAdministratorUser();
                RegistryView registryView = modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY ? RegistryView.Registry64 : RegistryView.Registry32;

                ProgressManager.CurrentGoal.Start(modificationsElement.RegistryStates.Count + modificationsElement.RegistryStates.Count);

                try {
                    // populate active modifications
                    for (int i = 0; i < modificationsElement.RegistryStates.Count; i++) {
                        // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                        registryStateElement = modificationsElement.RegistryStates.Get(i) as RegistryStateElement;

                        if (registryStateElement == null) {
                            throw new ConfigurationErrorsException("The Registry State Element (" + i + ") is null.");
                        }

                        // GOAL: find the CURRENT value in the REAL REGISTRY
                        // ACTIVE REGISTRY ELEMENT should reflect real registry
                        // DELETED = DELETED in REAL REGISTRY
                        // this keyName variable is a temp variable specific to this user
                        // it should not get saved
                        keyName = GetUserKeyValueName(registryStateElement.KeyName);
                        value = null;
                        valueKind = null;

                        activeRegistryStateElement = new RegistryStateElement {
                            Type = registryStateElement.Type,
                            KeyName = registryStateElement.KeyName,
                            ValueName = registryStateElement.ValueName
                        };

                        if (registryStateElement.Type == TYPE.KEY) {
                            // we create a key
                            activeRegistryStateElement.Type = TYPE.KEY;
                            activeRegistryStateElement.Value = null;
                            activeRegistryStateElement.ValueKind = null;

                            try {
                                activeRegistryStateElement._Deleted = TestKeyDeletedInRegistryView(keyName, registryView);
                            } catch (SecurityException ex) {
                                // key exists but we can't get it
                                LogExceptionToLauncher(ex);
                                throw new TaskRequiresElevationException("Accessing the key \"" + keyName + "\" requires elevation.");
                            } catch (UnauthorizedAccessException ex) {
                                // key exists but we can't get it
                                LogExceptionToLauncher(ex);
                                throw new TaskRequiresElevationException("Accessing the key \"" + keyName + "\" requires elevation.");
                            }
                        } else {
                            try {
                                valueExpanded = Environment.ExpandEnvironmentVariables(registryStateElement.Value);

                                if (!valueExpanded.Equals(registryStateElement.Value, StringComparison.Ordinal)) {
                                    activeRegistryStateElement._ValueExpanded = valueExpanded;
                                }
                            } catch {
                                // fail silently
                            }

                            try {
                                value = GetValueInRegistryView(keyName, registryStateElement.ValueName, out valueKind, registryView) as string;
                            } catch (SecurityException ex) {
                                // value exists but we can't get it
                                LogExceptionToLauncher(ex);
                                throw new TaskRequiresElevationException("Accessing the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                            } catch (UnauthorizedAccessException ex) {
                                // value exists but we can't get it
                                LogExceptionToLauncher(ex);
                                throw new TaskRequiresElevationException("Accessing the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                            } catch {
                                // value doesn't exist
                                value = null;
                            }

                            if (value == null) {
                                try {
                                    keyDeleted = TestKeyDeletedInRegistryView(keyName, registryView);
                                } catch (SecurityException ex) {
                                    // key exists but we can't get it
                                    LogExceptionToLauncher(ex);
                                    throw new TaskRequiresElevationException("Accessing the key \"" + keyName + "\" requires elevation.");
                                } catch (UnauthorizedAccessException ex) {
                                    // key exists but we can't get it
                                    LogExceptionToLauncher(ex);
                                    throw new TaskRequiresElevationException("Accessing the key \"" + keyName + "\" requires elevation.");
                                }
                            } else {
                                keyDeleted = null;
                            }
                            
                            if (String.IsNullOrEmpty(keyDeleted)) {
                                // we create a value
                                // the value does not exist
                                // or, we edit a value that exists
                                activeRegistryStateElement.Type = TYPE.VALUE;
                                activeRegistryStateElement.Value = value;
                                activeRegistryStateElement.ValueKind = valueKind;
                                activeRegistryStateElement._Deleted = null;
                            } else {
                                // we create a value
                                // the value, and the key it belonged to, does not exist
                                activeRegistryStateElement.Type = TYPE.KEY;
                                activeRegistryStateElement.Value = null;
                                activeRegistryStateElement.ValueKind = null;
                                activeRegistryStateElement._Deleted = keyDeleted;
                            }
                        }

                        activeModificationsElement.RegistryStates.Set(activeRegistryStateElement);
                        ProgressManager.CurrentGoal.Steps++;
                    }

                    SetFlashpointSecurePlayerSection(TemplateName);

                    for (registryStateIndex = 0; registryStateIndex < modificationsElement.RegistryStates.Count; registryStateIndex++) {
                        // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                        registryStateElement = modificationsElement.RegistryStates.Get(registryStateIndex) as RegistryStateElement;

                        if (registryStateElement == null) {
                            throw new ConfigurationErrorsException("The Registry State Element (" + registryStateIndex + ") is null.");
                        }

                        activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(registryStateElement.Name) as RegistryStateElement;

                        if (activeRegistryStateElement == null) {
                            throw new ConfigurationErrorsException("The Active Registry State Element \"" + registryStateElement.Name + "\" is null.");
                        }

                        keyName = GetUserKeyValueName(registryStateElement.KeyName);

                        // we don't delete existing keys/values, since the program just won't use deleted keys/values
                        // therefore, _Deleted is ignored on all but the active registry state
                        if (keyName != null) {
                            switch (registryStateElement.Type) {
                                case TYPE.KEY:
                                try {
                                    SetKeyInRegistryView(keyName, registryView);
                                } catch (SecurityException ex) {
                                    // key doesn't exist and we can't set it
                                    LogExceptionToLauncher(ex);
                                    throw new TaskRequiresElevationException("Setting the key \"" + keyName + "\" requires elevation.");
                                } catch (UnauthorizedAccessException ex) {
                                    // key exists and we can't set it
                                    LogExceptionToLauncher(ex);
                                    throw new TaskRequiresElevationException("Setting the key \"" + keyName + "\" requires elevation.");
                                } catch (InvalidOperationException ex) {
                                    // key marked for deletion
                                    LogExceptionToLauncher(ex);
                                    throw new InvalidRegistryStateException("The key \"" + keyName + "\" is marked for deletion.");
                                } catch (Exception ex) {
                                    // key doesn't exist and can't be created
                                    LogExceptionToLauncher(ex);
                                    throw new InvalidRegistryStateException("The key \"" + keyName + "\" could not be set.");
                                }
                                break;
                                case TYPE.VALUE:
                                value = String.IsNullOrEmpty(activeRegistryStateElement._ValueExpanded)
                                        ? registryStateElement.Value
                                        : activeRegistryStateElement._ValueExpanded;

                                if (value != null) {
                                    try {
                                        SetValueInRegistryView(
                                            keyName,
                                            registryStateElement.ValueName,
                                            value,
                                            registryStateElement.ValueKind.GetValueOrDefault(),
                                            registryView
                                        );
                                    } catch (SecurityException ex) {
                                        // value exists and we can't set it
                                        LogExceptionToLauncher(ex);
                                        throw new TaskRequiresElevationException("Setting the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                    } catch (UnauthorizedAccessException ex) {
                                        // value exists and we can't set it
                                        LogExceptionToLauncher(ex);
                                        throw new TaskRequiresElevationException("Setting the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                    } catch (FormatException ex) {
                                        // value must be Base64
                                        LogExceptionToLauncher(ex);
                                        throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" must be Base64.");
                                    } catch (InvalidOperationException ex) {
                                        // value marked for deletion
                                        LogExceptionToLauncher(ex);
                                        throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" is marked for deletion.");
                                    } catch (Exception ex) {
                                        // value doesn't exist and can't be created
                                        LogExceptionToLauncher(ex);
                                        throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" could not be set.");
                                    }
                                }
                                break;
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }
                } catch {
                    // remove registry states we didn't modify
                    try {
                        while (registryStateIndex < activeModificationsElement.RegistryStates.Count) {
                            activeModificationsElement.RegistryStates.RemoveAt(registryStateIndex);
                        }

                        if (activeModificationsElement.RegistryStates.Count <= 0) {
                            activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                            activeModificationsElement.RegistryStates._Administrator = false;
                            activeModificationsElement.RegistryStates._CurrentUser = String.Empty;
                        }

                        SetFlashpointSecurePlayerSection(TemplateName);
                    } catch {
                        // fail silently
                    }

                    Deactivate();
                    throw;
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }

        public void Deactivate(MODIFICATIONS_REVERT_METHOD modificationsRevertMethod) {
            lock (deactivationLock) {
                base.Deactivate();

                TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

                // if the activation state doesn't exist, we don't need to do stuff
                if (activeTemplateElement == null) {
                    return;
                }

                ModificationsElement activeModificationsElement = activeTemplateElement.Modifications;

                if (activeModificationsElement.RegistryStates.Count <= 0) {
                    return;
                }

                // if the activation state exists, but no key is marked as active...
                // we assume the registry has changed, and don't revert the changes, to be safe
                // (it should never happen unless the user tampered with the config file)
                string templateElementName = activeTemplateElement.Active;

                // don't allow infinite recursion!
                if (String.IsNullOrEmpty(templateElementName)) {
                    activeModificationsElement.RegistryStates.Clear();
                    activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                    activeModificationsElement.RegistryStates._Administrator = false;
                    activeModificationsElement.RegistryStates._CurrentUser = String.Empty;
                    SetFlashpointSecurePlayerSection(TemplateName);
                    return;
                }

                TemplateElement templateElement = GetTemplateElement(false, templateElementName);
                ModificationsElement modificationsElement = null;

                // if the active element pointed to doesn't exist... same assumption
                // and another safeguard against recursion
                if (templateElement != null && templateElement != activeTemplateElement) {
                    if (templateElement.Modifications.ElementInformation.IsPresent) {
                        modificationsElement = templateElement.Modifications;
                    }
                }

                if (modificationsElement == null) {
                    activeModificationsElement.RegistryStates.Clear();
                    activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                    activeModificationsElement.RegistryStates._Administrator = false;
                    activeModificationsElement.RegistryStates._CurrentUser = String.Empty;
                    SetFlashpointSecurePlayerSection(TemplateName);
                    return;
                }

                RegistryStateElement registryStateElement = null;
                RegistryStateElement activeRegistryStateElement = null;
                string activeCurrentUser = activeModificationsElement.RegistryStates._CurrentUser;
                bool activeAdministrator = activeModificationsElement.RegistryStates._Administrator.GetValueOrDefault();
                string keyName = null;
                string value = null;
                RegistryValueKind? valueKind = null;
                bool clear = false;
                
                if (activeAdministrator && !TestLaunchedAsAdministratorUser()) {
                    throw new TaskRequiresElevationException("Deactivating the Registry State requires elevation.");
                }

                RegistryView registryView = modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY ? RegistryView.Registry64 : RegistryView.Registry32;

                ProgressManager.CurrentGoal.Start(activeModificationsElement.RegistryStates.Count + activeModificationsElement.RegistryStates.Count);

                try {
                    if (modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY) {
                        // check if any key has been modified from the modification element
                        for (int i = 0; i < activeModificationsElement.RegistryStates.Count; i++) {
                            // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                            activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(i) as RegistryStateElement;

                            if (activeRegistryStateElement != null) {
                                registryStateElement = modificationsElement.RegistryStates.Get(activeRegistryStateElement.Name) as RegistryStateElement;

                                // registryStateElement represents the value the key SHOULD have *right now*
                                // but if there was a partial move, it may instead be the active value
                                if (registryStateElement != null) {
                                    keyName = null;
                                    value = null;
                                    clear = false;

                                    if (activeRegistryStateElement.Type == TYPE.KEY
                                        && registryStateElement.Type == TYPE.KEY) {
                                        // we previously created a key
                                        // it may or may not have existed before
                                        // so it may or may not need to exist
                                        if (!CompareKeys(
                                            registryView,
                                            registryStateElement,
                                            activeRegistryStateElement,
                                            activeCurrentUser,
                                            activeAdministrator
                                        )) {
                                            clear = true;
                                        }
                                    } else {
                                        keyName = GetUserKeyValueName(registryStateElement.KeyName, activeCurrentUser, activeAdministrator);

                                        try {
                                            value = GetValueInRegistryView(keyName, registryStateElement.ValueName, out valueKind, registryView) as string;
                                        } catch (SecurityException ex) {
                                            // value exists but we can't get it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Accessing the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                        } catch (UnauthorizedAccessException ex) {
                                            // value exists but we can't get it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Accessing the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                        } catch {
                                            // value doesn't exist
                                            value = null;
                                        }
                                        
                                        if ((activeRegistryStateElement.Type == TYPE.KEY
                                            && registryStateElement.Type == TYPE.VALUE)
                                            || (activeRegistryStateElement.Type == TYPE.VALUE
                                            && activeRegistryStateElement.Value == null)) {
                                            // we previously created a value
                                            // the value, (and potentially the key it belonged to) did not exist before
                                            // the value may or may not exist now
                                            // if the value still exists, we need to check it's not edited
                                            if (value != null) {
                                                // value still exists
                                                if (!CompareValues(
                                                    value,
                                                    valueKind,
                                                    registryView,
                                                    registryStateElement,
                                                    activeRegistryStateElement,
                                                    activeCurrentUser,
                                                    activeAdministrator
                                                )) {
                                                    clear = true;
                                                }
                                            }
                                        } else if (activeRegistryStateElement.Type == TYPE.VALUE
                                            && activeRegistryStateElement.Value != null) {
                                            // we previously edited a value that existed before
                                            // we need to check it still exists in one of the two valid states
                                            if (value == null) {
                                                clear = true;
                                            } else {
                                                if (!CompareValues(
                                                    value,
                                                    valueKind,
                                                    registryView,
                                                    registryStateElement,
                                                    activeRegistryStateElement,
                                                    activeCurrentUser,
                                                    activeAdministrator
                                                )) {
                                                    clear = true;
                                                }
                                            }
                                        }
                                    }
                                    
                                    if (clear) {
                                        activeModificationsElement.RegistryStates.Clear();
                                        activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                                        activeModificationsElement.RegistryStates._Administrator = false;
                                        activeModificationsElement.RegistryStates._CurrentUser = String.Empty;
                                        SetFlashpointSecurePlayerSection(TemplateName);
                                        return;
                                    }
                                }
                            }

                            ProgressManager.CurrentGoal.Steps++;
                        }
                    }

                    TaskRequiresElevationException taskRequiresElevationException = null;
                    Exception exception = null;

                    for (int i = 0; i < activeModificationsElement.RegistryStates.Count; i++) {
                        try {
                            activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(i) as RegistryStateElement;

                            // how can it be deleted already?? just paranoia
                            if (activeRegistryStateElement == null) {
                                throw new ConfigurationErrorsException("The Active Modifications Element is null.");
                            }

                            switch (activeRegistryStateElement.Type) {
                                case TYPE.KEY:
                                if (!String.IsNullOrEmpty(activeRegistryStateElement._Deleted)
                                    || modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                    keyName = GetUserKeyValueName(
                                        String.IsNullOrEmpty(activeRegistryStateElement._Deleted)
                                        ? activeRegistryStateElement.KeyName
                                        : activeRegistryStateElement._Deleted,
                                        activeCurrentUser,
                                        activeAdministrator
                                    );

                                    if (keyName != null) {
                                        try {
                                            // key didn't exist before
                                            DeleteKeyInRegistryView(
                                                keyName,
                                                registryView
                                            );
                                        } catch (SecurityException ex) {
                                            // key exists and we can't modify it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Deleting the key \"" + activeRegistryStateElement._Deleted + "\" requires elevation.");
                                        } catch (UnauthorizedAccessException ex) {
                                            // key exists and we can't modify it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Deleting the key \"" + activeRegistryStateElement._Deleted + "\" requires elevation.");
                                        } catch {
                                            // key doesn't exist
                                        }
                                    }
                                }
                                break;
                                case TYPE.VALUE:
                                if (activeRegistryStateElement.Value == null
                                    || modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                    keyName = GetUserKeyValueName(activeRegistryStateElement.KeyName, activeCurrentUser, activeAdministrator);

                                    if (keyName != null) {
                                        try {
                                            // value didn't exist before
                                            DeleteValueInRegistryView(
                                                keyName,
                                                activeRegistryStateElement.ValueName,
                                                registryView
                                            );
                                        } catch (SecurityException ex) {
                                            // value exists and we can't modify it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Deleting the value \"" + activeRegistryStateElement.ValueName + "\" requires elevation.");
                                        } catch (UnauthorizedAccessException ex) {
                                            // value exists and we can't modify it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Deleting the value \"" + activeRegistryStateElement.ValueName + "\" requires elevation.");
                                        } catch {
                                            // value doesn't exist
                                        }
                                    }
                                } else {
                                    keyName = GetUserKeyValueName(activeRegistryStateElement.KeyName, activeCurrentUser, activeAdministrator);

                                    if (keyName != null) {
                                        try {
                                            // value was different before
                                            SetValueInRegistryView(
                                                keyName,
                                                activeRegistryStateElement.ValueName,
                                                activeRegistryStateElement.Value,
                                                activeRegistryStateElement.ValueKind.GetValueOrDefault(),
                                                registryView
                                            );
                                        } catch (SecurityException ex) {
                                            // value exists and we can't modify it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Setting the value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                        } catch (UnauthorizedAccessException ex) {
                                            // value exists and we can't modify it
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Setting the value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                        } catch (FormatException ex) {
                                            // value must be Base64
                                            LogExceptionToLauncher(ex);
                                            throw new InvalidRegistryStateException("The value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" must be Base64.");
                                        } catch (InvalidOperationException ex) {
                                            // value marked for deletion
                                            LogExceptionToLauncher(ex);
                                            throw new InvalidRegistryStateException("The value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" is marked for deletion.");
                                        } catch (Exception ex) {
                                            // value doesn't exist and can't be created
                                            LogExceptionToLauncher(ex);
                                            throw new InvalidRegistryStateException("The value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" could not be set.");
                                        }
                                    }
                                }
                                break;
                            }

                            activeModificationsElement.RegistryStates.RemoveAt(i);
                            i--;
                        } catch (TaskRequiresElevationException ex) {
                            taskRequiresElevationException = ex;
                        } catch (Exception ex) {
                            exception = ex;
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }

                    if (taskRequiresElevationException != null) {
                        SetFlashpointSecurePlayerSection(TemplateName);
                        throw taskRequiresElevationException;
                    }

                    if (exception != null) {
                        SetFlashpointSecurePlayerSection(TemplateName);
                        throw exception;
                    }

                    activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                    activeModificationsElement.RegistryStates._Administrator = false;
                    activeModificationsElement.RegistryStates._CurrentUser = String.Empty;
                    SetFlashpointSecurePlayerSection(TemplateName);
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }

        public override void Deactivate() {
            Deactivate(MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY);
        }

        private void QueueModification(ulong safeKeyHandle, DateTime timeStamp, RegistryStateElement registryStateElement) {
            if (queuedModifications == null) {
                queuedModifications = new Dictionary<ulong, SortedList<DateTime, List<RegistryStateElement>>>();
            }

            queuedModifications.TryGetValue(safeKeyHandle, out SortedList<DateTime, List<RegistryStateElement>> timeStamps);

            // worst case scenario: now we need to watch for when we get info on that handle
            if (timeStamps == null) {
                // create queue if does not exist (might be multiple keys waiting on it)
                timeStamps = new SortedList<DateTime, List<RegistryStateElement>>();
            }

            timeStamps.TryGetValue(timeStamp, out List<RegistryStateElement> registryStateElements);

            if (registryStateElements == null) {
                registryStateElements = new List<RegistryStateElement>();
            }

            registryStateElements.Add(registryStateElement);
            
            timeStamps[timeStamp] = registryStateElements;
            queuedModifications[safeKeyHandle] = timeStamps;
        }

        private void GotValue(RegistryTraceData registryTraceData) {
            try {
                if (registryTraceData.ProcessID != CurrentProcessId && registryTraceData.ProcessID != -1) {
                    return;
                }
            } catch {
                return;
            }

            if (registryTraceData.ValueName == null) {
                return;
            }

            if (ImportPaused) {
                if (registryTraceData.ValueName.Equals(IMPORT_RESUME, StringComparison.OrdinalIgnoreCase)) {
                    ImportPaused = false;

                    // hold here until after the control has installed
                    // that way we can recieve registry messages as they come in
                    // with reassurance the control has installed already
                    // therefore, key names will be redirected properly
                    if (resumeEventWaitHandle == null) {
                        throw new InvalidOperationException("resumeEventWaitHandle is null.");
                    }

                    resumeEventWaitHandle.WaitOne();
                }
            } else {
                if (registryTraceData.ValueName.Equals(IMPORT_PAUSE, StringComparison.OrdinalIgnoreCase)) {
                    ImportPaused = true;
                }
            }
        }

        private void ModificationAdded(RegistryTraceData registryTraceData) {
            // self explanatory
            if (ImportPaused) {
                return;
            }

            // if KCBModificationKeyNames has KeyHandle, add it to RegistryStates
            // otherwise queue the modification
            // check the registry change was made by this process (or an unknown process - might be ours)
            try {
                if (registryTraceData.ProcessID != CurrentProcessId && registryTraceData.ProcessID != -1) {
                    return;
                }
            } catch {
                return;
            }

            // if there isn't any KCB to deal with...
            TemplateElement templateElement = null;

            // must catch exceptions here for thread safety
            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch {
                return;
            }

            if (templateElement == null) {
                return;
            }

            ModificationsElement modificationsElement = templateElement.Modifications;

            //if (!modificationsElement.ElementInformation.IsPresent) {
                //return;
            //}

            RegistryStateElement registryStateElement = new RegistryStateElement {
                KeyName = registryTraceData.KeyName,
                ValueName = registryTraceData.ValueName
            };

            // KeyHandle is meant to be a uint32, so we discard the rest
            // http://learn.microsoft.com/en-us/windows/win32/etw/registry-typegroup1
            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;
            string value = null;
            RegistryValueKind? valueKind = null;
            RegistryView registryView = modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY ? RegistryView.Registry64 : RegistryView.Registry32;

            if (safeKeyHandle == 0) {
                // we don't need to queue it, we can just add the key right here
                registryStateElement.KeyName = GetRedirectedKeyValueName(
                    GetKeyValueNameFromKernelRegistryString(registryStateElement.KeyName),
                    modificationsElement.RegistryStates.BinaryType
                );

                valueKind = null;

                try {
                    value = ReplaceStartupPathEnvironmentVariable(
                        LengthenValue(
                            GetValueInRegistryView(
                                registryStateElement.KeyName,
                                registryStateElement.ValueName,
                                out valueKind,
                                registryView
                            ) as string,

                            fullPath,
                            pathNames
                        ),

                        pathNames
                    );
                } catch (SecurityException ex) {
                    // value exists but we can't get it
                    // this shouldn't happen because this task requires elevation
                    LogExceptionToLauncher(ex);
                    value = String.Empty;
                } catch (UnauthorizedAccessException ex) {
                    // value exists but we can't get it
                    // this shouldn't happen because this task requires elevation
                    LogExceptionToLauncher(ex);
                    value = String.Empty;
                } catch {
                    // value doesn't exist
                    value = null;
                }

                registryStateElement.Type = TYPE.VALUE;
                registryStateElement.ValueKind = valueKind;

                if (value == null) {
                    try {
                        if (String.IsNullOrEmpty(registryStateElement.ValueName)
                            && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryStateElement.KeyName, registryView))) {
                            registryStateElement.Type = TYPE.KEY;
                        }
                    } catch {
                        // fail silently
                    }

                    if (registryStateElement.Type == TYPE.VALUE) {
                        ModificationRemoved(registryTraceData);
                        return;
                    }
                } else {
                    registryStateElement.Value = value;
                }

                modificationsElement.RegistryStates.Set(registryStateElement);
                //SetFlashpointSecurePlayerSection(TemplateName);
                return;
            }

            // need to deal with KCB
            // well, we already know the base key name from before, so we can wrap this up now
            if (kcbModificationKeyNames != null) {
                kcbModificationKeyNames.TryGetValue(safeKeyHandle, out string kcbModificationKeyName);

                if (!String.IsNullOrEmpty(kcbModificationKeyName)) {
                    registryStateElement.KeyName = GetRedirectedKeyValueName(
                        GetKeyValueNameFromKernelRegistryString(kcbModificationKeyName + "\\" + registryStateElement.KeyName),
                        modificationsElement.RegistryStates.BinaryType
                    );

                    valueKind = null;

                    try {
                        value = ReplaceStartupPathEnvironmentVariable(
                            LengthenValue(
                                GetValueInRegistryView(
                                    registryStateElement.KeyName,
                                    registryStateElement.ValueName,
                                    out valueKind,
                                    registryView
                                ) as string,

                                fullPath,
                                pathNames),

                            pathNames
                        );
                    } catch {
                        // we have permission to access the key at this point so this must not be important
                    }

                    registryStateElement.Type = TYPE.VALUE;
                    registryStateElement.ValueKind = valueKind;

                    if (value == null) {
                        try {
                            // if not just the value, but the entire key, is deleted, treat this as a key type
                            if (String.IsNullOrEmpty(registryStateElement.ValueName)
                                && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryStateElement.KeyName, registryView))) {
                                registryStateElement.Type = TYPE.KEY;
                            }
                        } catch {
                            // fail silently
                        }

                        if (registryStateElement.Type == TYPE.VALUE) {
                            ModificationRemoved(registryTraceData);
                            return;
                        }
                    } else {
                        registryStateElement.Value = value;
                    }

                    modificationsElement.RegistryStates.Set(registryStateElement);
                    //SetFlashpointSecurePlayerSection(TemplateName);
                    return;
                }
            }

            // add this to the modifications queue
            // (we'll test if it was deleted on KCBStopped)
            QueueModification(safeKeyHandle, registryTraceData.TimeStamp, registryStateElement);
        }

        private void ModificationRemoved(RegistryTraceData registryTraceData) {
            if (ImportPaused) {
                return;
            }

            // if KCBModificationKeyNames has KeyHandle, remove it from RegistryStates
            // otherwise queue the modification
            // check the registry change was made by this process (or an unknown process - might be ours)
            try {
                if (registryTraceData.ProcessID != CurrentProcessId && registryTraceData.ProcessID != -1) {
                    return;
                }
            } catch {
                return;
            }

            TemplateElement templateElement = null;

            // must catch exceptions here for thread safety
            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch {
                return;
            }

            if (templateElement == null) {
                return;
            }

            ModificationsElement modificationsElement = templateElement.Modifications;

            //if (!modificationsElement.ElementInformation.IsPresent) {
                //return;
            //}

            // create filler element to get name
            RegistryStateElement registryStateElement = new RegistryStateElement {
                KeyName = registryTraceData.KeyName,
                ValueName = registryTraceData.ValueName
            };

            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;

            if (safeKeyHandle == 0) {
                registryStateElement.KeyName = GetRedirectedKeyValueName(
                    GetKeyValueNameFromKernelRegistryString(registryStateElement.KeyName),
                    modificationsElement.RegistryStates.BinaryType
                );

                // key was deleted - don't need to find its value, just enough info for the name
                modificationsElement.RegistryStates.Remove(registryStateElement.Name);
                //SetModificationsElement(modificationsElement, Name);
                return;
            }

            if (kcbModificationKeyNames != null) {
                kcbModificationKeyNames.TryGetValue(safeKeyHandle, out string kcbModificationKeyName);

                if (!String.IsNullOrEmpty(kcbModificationKeyName)) {
                    // we have info from the handle already to get the name
                        registryStateElement.KeyName = GetRedirectedKeyValueName(
                        GetKeyValueNameFromKernelRegistryString(kcbModificationKeyName + "\\" + registryStateElement.KeyName),
                        modificationsElement.RegistryStates.BinaryType
                    );

                    modificationsElement.RegistryStates.Remove(registryStateElement.Name);
                    //SetModificationsElement(modificationsElement, Name);
                    return;
                }
            }

            // add this to the modifications queue
            // (we'll test if it was deleted on KCBStopped)
            QueueModification(safeKeyHandle, registryTraceData.TimeStamp, registryStateElement);
        }

        private void KCBStarted(RegistryTraceData registryTraceData) {
            if (ImportPaused) {
                return;
            }

            // clear out the queue, since we started now, so this handle refers to something else
            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;

            if (queuedModifications != null) {
                queuedModifications.Remove(safeKeyHandle);
            }

            if (kcbModificationKeyNames == null) {
                kcbModificationKeyNames = new Dictionary<ulong, string>();
            }

            kcbModificationKeyNames[safeKeyHandle] = registryTraceData.KeyName;
        }

        private void KCBStopped(RegistryTraceData registryTraceData) {
            if (ImportPaused) {
                return;
            }

            TemplateElement templateElement = null;

            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch {
                return;
            }

            if (templateElement == null) {
                return;
            }

            ModificationsElement modificationsElement = templateElement.Modifications;

            //if (!modificationsElement.ElementInformation.IsPresent) {
                //return;
            //}

            // it's stopped, remove it from the list of active key names
            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x0000000FFFFFFFF;

            if (kcbModificationKeyNames != null) {
                kcbModificationKeyNames.Remove(safeKeyHandle);
            }

            // we'll be finding these in a second
            RegistryStateElement registryStateElement = null;
            string value = null;
            RegistryValueKind? valueKind = null;

            RegistryView registryView = modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY ? RegistryView.Registry64 : RegistryView.Registry32;

            // we want to take care of any queued registry timeline events
            // an event entails the date and time of the registry modification
            if (queuedModifications == null) {
                return;
            }

            queuedModifications.TryGetValue(safeKeyHandle, out SortedList<DateTime, List<RegistryStateElement>> timeStamps);

            if (timeStamps == null) {
                return;
            }

            foreach (List<RegistryStateElement> registryStateElements in timeStamps.Values) {
                // add its BaseKeyName
                for (int j = 0; j < registryStateElements.Count; j++) {
                    registryStateElement = registryStateElements[j];

                    registryStateElement.KeyName = GetRedirectedKeyValueName(
                        GetKeyValueNameFromKernelRegistryString(registryTraceData.KeyName + "\\" + registryStateElement.KeyName),
                        modificationsElement.RegistryStates.BinaryType
                    );

                    valueKind = null;

                    try {
                        value = ReplaceStartupPathEnvironmentVariable(
                            LengthenValue(
                                GetValueInRegistryView(
                                    registryStateElement.KeyName,
                                    registryStateElement.ValueName,
                                    out valueKind,
                                    registryView
                                ) as string,

                                fullPath,
                                pathNames
                            ),

                            pathNames
                        );
                    } catch (SecurityException ex) {
                        // value exists but we can't get it
                        // this shouldn't happen because this task requires elevation
                        LogExceptionToLauncher(ex);
                        value = String.Empty;
                    } catch (UnauthorizedAccessException ex) {
                        // value exists but we can't get it
                        // this shouldn't happen because this task requires elevation
                        LogExceptionToLauncher(ex);
                        value = String.Empty;
                    } catch {
                        // value doesn't exist
                        value = null;
                    }

                    registryStateElement.Type = TYPE.VALUE;
                    registryStateElement.ValueKind = valueKind;

                    if (value == null) {
                        try {
                            if (String.IsNullOrEmpty(registryStateElement.ValueName)
                                && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryStateElement.KeyName, registryView))) {
                                registryStateElement.Type = TYPE.KEY;
                                modificationsElement.RegistryStates.Set(registryStateElement);
                            }
                        } catch {
                            // fail silently
                        }

                        if (registryStateElement.Type == TYPE.VALUE) {
                            modificationsElement.RegistryStates.Remove(registryStateElement.Name);
                        }
                    } else {
                        registryStateElement.Value = value;
                        modificationsElement.RegistryStates.Set(registryStateElement);
                    }
                }
            }

            // and out of the queue
            // (the Key is the TimeStamp)
            //SetFlashpointSecurePlayerSection(TemplateName);
            queuedModifications[safeKeyHandle].Clear();
        }
    }
}