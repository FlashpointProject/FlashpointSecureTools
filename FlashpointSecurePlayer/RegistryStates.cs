using System;
using System.Collections.Generic;
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
        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/0f3557ee-16bd-4a36-a4f3-00efbeae9b0d/app-config-multiple-sections-in-sectiongroup-with-same-name?forum=csharpgeneral
        private class WOW64Key {
            public enum EFFECT {
                SHARED,
                SHARED_VISTA,
                REDIRECTED,
                REDIRECTED_EXCEPTION_VALUE_IS_DEFINED
            }

            public string Name = null;
            public EFFECT Effect = EFFECT.SHARED;
            public List<string> EffectExceptionValueNames = null;

            public WOW64Key(string name = null, EFFECT effect = EFFECT.SHARED, List<string> effectExceptionValueNames = null) {
                Name = name;
                Effect = effect;
                EffectExceptionValueNames = effectExceptionValueNames;
            }
        }
        
        public enum TYPE {
            KEY,
            VALUE
        };

        //private const int IMPORT_TIMEOUT = 5;
        private const int IMPORT_TIMEOUT = 60;
        private const string IMPORT_RESUME = "FLASHPOINTSECUREPLAYERREGISTRYSTATEIMPORTRESUME";
        private const string IMPORT_PAUSE = "FLASHPOINTSECUREPLAYERREGISTRYSTATEIMPORTPAUSE";
        private readonly object activationLock = new object();
        private readonly object deactivationLock = new object();
        private string fullPath = null;
        private EventWaitHandle resumeEventWaitHandle = new ManualResetEvent(false);
        private Dictionary<ulong, SortedList<DateTime, RegistryStateElement>> modificationsQueue = null;
        private Dictionary<ulong, string> kcbModificationKeyNames = null;
        private TraceEventSession kernelSession;
        private Dictionary<string, List<WOW64Key>> wow64KeyLists = null;

        private Dictionary<string, List<WOW64Key>> WOW64KeyLists {
            get {
                if (wow64KeyLists == null) {
                    string windowsVersionName = GetWindowsVersionName(false, false, false);

                    if (windowsVersionName == "Windows Server 2008" || windowsVersionName == "Windows Vista" || windowsVersionName == "Windows Server 2003" || windowsVersionName == "Windows XP") {
                        wow64KeyLists = new Dictionary<string, List<WOW64Key>>() {
                            {"HKEY_LOCAL_MACHINE", new List<WOW64Key>() {
                                { new WOW64Key("SOFTWARE", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE", new List<WOW64Key>() {
                                { new WOW64Key("CLASSES", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("CLIENTS", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\CLASSES", new List<WOW64Key>() {
                                { new WOW64Key("APPID", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("CLSID", WOW64Key.EFFECT.REDIRECTED_EXCEPTION_VALUE_IS_DEFINED)},
                                { new WOW64Key("DIRECTSHOW", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("INTERFACE", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("MEDIA TYPE", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("MEDIAFOUNDATION", WOW64Key.EFFECT.REDIRECTED)}
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT", new List<WOW64Key>() {
                                { new WOW64Key("COM3", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("EVENTSYSTEM", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("OLE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("RPC", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\NOTEPAD", new List<WOW64Key>() {
                                { new WOW64Key("DEFAULTFONTS", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\WINDOWS\\CURRENTVERSION", new List<WOW64Key>() {
                                { new WOW64Key("APP PATHS", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("PREVIEWHANDLERS", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\WINDOWS\\CURRENTVERSION\\EXPLORER", new List<WOW64Key>() {
                                { new WOW64Key("AUTOPLAYHANDLERS", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("DRIVEICONS", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("KINDMAP", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\WINDOWS NT\\CURRENTVERSION", new List<WOW64Key>() {
                                { new WOW64Key("CONSOLE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("FONTLINK", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("GRE_INITIALIZE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("IMAGE FILE EXECUTION OPTIONS", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("LANGUAGE PACK", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_CURRENT_USER\\SOFTWARE", new List<WOW64Key>() {
                                { new WOW64Key("CLASSES", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_CURRENT_USER\\SOFTWARE\\CLASSES", new List<WOW64Key>() {
                                { new WOW64Key("APPID", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("CLSID", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("DIRECTSHOW", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("INTERFACE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("MEDIA TYPE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("MEDIAFOUNDATION", WOW64Key.EFFECT.REDIRECTED) }
                            }}
                        };
                    } else {
                        wow64KeyLists = new Dictionary<string, List<WOW64Key>>() {
                            {"HKEY_LOCAL_MACHINE", new List<WOW64Key>() {
                                { new WOW64Key("SOFTWARE", WOW64Key.EFFECT.REDIRECTED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE", new List<WOW64Key>() {
                                { new WOW64Key("CLASSES", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("CLIENTS", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\CLASSES", new List<WOW64Key>() {
                                { new WOW64Key("APPID", WOW64Key.EFFECT.SHARED)},
                                { new WOW64Key("CLSID", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("DIRECTSHOW", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("INTERFACE", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("MEDIA TYPE", WOW64Key.EFFECT.REDIRECTED)},
                                { new WOW64Key("MEDIAFOUNDATION", WOW64Key.EFFECT.REDIRECTED)}
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT", new List<WOW64Key>() {
                                { new WOW64Key("COM3", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("EVENTSYSTEM", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("OLE", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("RPC", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\NOTEPAD", new List<WOW64Key>() {
                                { new WOW64Key("DEFAULTFONTS", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\WINDOWS\\CURRENTVERSION", new List<WOW64Key>() {
                                { new WOW64Key("APP PATHS", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("PREVIEWHANDLERS", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\WINDOWS\\CURRENTVERSION\\EXPLORER", new List<WOW64Key>() {
                                { new WOW64Key("AUTOPLAYHANDLERS", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("DRIVEICONS", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("KINDMAP", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_LOCAL_MACHINE\\SOFTWARE\\MICROSOFT\\WINDOWS NT\\CURRENTVERSION", new List<WOW64Key>() {
                                { new WOW64Key("CONSOLE", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("FONTLINK", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("GRE_INITIALIZE", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("IMAGE FILE EXECUTION OPTIONS", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("LANGUAGE PACK", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_CURRENT_USER\\SOFTWARE", new List<WOW64Key>() {
                                { new WOW64Key("CLASSES", WOW64Key.EFFECT.SHARED) }
                            }},
                            {"HKEY_CURRENT_USER\\SOFTWARE\\CLASSES", new List<WOW64Key>() {
                                { new WOW64Key("APPID", WOW64Key.EFFECT.SHARED) },
                                { new WOW64Key("CLSID", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("DIRECTSHOW", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("INTERFACE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("MEDIA TYPE", WOW64Key.EFFECT.REDIRECTED) },
                                { new WOW64Key("MEDIAFOUNDATION", WOW64Key.EFFECT.REDIRECTED) }
                            }}
                        };
                    }
                }
                return wow64KeyLists;
            }
        }
        
        public RegistryStates(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        ~RegistryStates() {
            if (ImportStarted) {
                try {
                    StopImport();
                } catch (InvalidRegistryStateException) {
                    // Fail silently.
                } catch (System.Configuration.ConfigurationErrorsException) {
                    // Fail silently.
                } catch (InvalidOperationException) {
                    // Fail silently.
                }
            }

            try {
                Deactivate();
            } catch (InvalidRegistryStateException) {
                // Fail silently.
            } catch (System.Configuration.ConfigurationErrorsException) {
                // Fail silently.
            } catch (TaskRequiresElevationException) {
                // Fail silently.
            } catch (InvalidOperationException) {
                // Fail silently.
            }
        }

        private string GetUserKeyValueName(string keyValueName) {
            // can be empty, but not null
            if (keyValueName == null || TestLaunchedAsAdministratorUser()) {
                return keyValueName;
            }

            const string HKEY_LOCAL_MACHINE = "HKEY_LOCAL_MACHINE\\";

            keyValueName += "\\";

            if (keyValueName.StartsWith(HKEY_LOCAL_MACHINE, StringComparison.InvariantCultureIgnoreCase)) {
                keyValueName = "HKEY_CURRENT_USER\\" + keyValueName.Substring(HKEY_LOCAL_MACHINE.Length);
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

            kernelRegistryString += "\\";
            string keyValueName = String.Empty;

            if (kernelRegistryString.StartsWith(REGISTRY_MACHINE, StringComparison.InvariantCultureIgnoreCase)) {
                keyValueName = "HKEY_LOCAL_MACHINE\\" + kernelRegistryString.Substring(REGISTRY_MACHINE.Length);
            } else {
                const string REGISTRY_USER = "\\REGISTRY\\USER\\";

                if (kernelRegistryString.StartsWith(REGISTRY_USER, StringComparison.InvariantCultureIgnoreCase)) {
                    const string HKEY_USERS = "HKEY_USERS\\";

                    keyValueName = HKEY_USERS + kernelRegistryString.Substring(REGISTRY_USER.Length);
                    string currentUser = WindowsIdentity.GetCurrent().User.Value;
                    string keyValueNameCurrentUser = HKEY_USERS + currentUser + "_CLASSES\\";

                    if (keyValueName.StartsWith(keyValueNameCurrentUser, StringComparison.InvariantCultureIgnoreCase)) {
                        keyValueName = "HKEY_CURRENT_USER\\SOFTWARE\\CLASSES\\" + keyValueName.Substring(keyValueNameCurrentUser.Length);
                    } else {
                        keyValueNameCurrentUser = HKEY_USERS + currentUser + "\\";

                        if (keyValueName.StartsWith(keyValueNameCurrentUser, StringComparison.InvariantCultureIgnoreCase)) {
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

            keyName += "\\";
            RegistryHive? registryHive = null;

            if (keyName.StartsWith("HKEY_CURRENT_USER\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.CurrentUser;
            } else if (keyName.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.LocalMachine;
            } else if (keyName.StartsWith("HKEY_CLASSES_ROOT\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.ClassesRoot;
            } else if (keyName.StartsWith("HKEY_USERS\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.Users;
            } else if (keyName.StartsWith("HKEY_PERFORMANCE_DATA\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.PerformanceData;
            } else if (keyName.StartsWith("HKEY_CURRENT_CONFIG\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.CurrentConfig;
            } else if (keyName.StartsWith("HKEY_DYN_DATA\\", StringComparison.InvariantCultureIgnoreCase)) {
                registryHive = RegistryHive.DynData;
            }
            
            keyName = RemoveTrailingSlash(keyName);

            if (registryHive == null) {
                return null;
            }

            try {
                return RegistryKey.OpenBaseKey(registryHive.GetValueOrDefault(), registryView);
            } catch (ArgumentException) {
                return null;
            } catch (UnauthorizedAccessException) {
                throw new SecurityException("Access to the base key \"" + keyName + "\" is denied.");
            }
        }

        private RegistryKey OpenKeyInRegistryView(string keyName, bool writable, RegistryView registryView) {
            RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView);

            if (registryKey == null) {
                return null;
            }
            
            int subKeyNameIndex = keyName.IndexOf("\\") + 1;

            if (subKeyNameIndex > 0) {
                try {
                    registryKey = registryKey.OpenSubKey(keyName.Substring(subKeyNameIndex), writable);
                } catch (ArgumentNullException) {
                    // key name is null
                    return null;
                } catch (NullReferenceException) {
                    // registry key is null
                    return null;
                } catch (ObjectDisposedException) {
                    // key is closed (could not be opened)
                    return null;
                }
            }
            return registryKey;
        }

        private string TestKeyDeletedInRegistryView(string keyName, RegistryView registryView) {
            List<string> keyNames = keyName.Split('\\').ToList();

            if (keyNames.Count <= 0) {
                return keyName;
            }

            RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView);

            // base key is deleted
            if (registryKey == null) {
                return keyNames[0];
            }

            try {
                for (int i = 0;i < keyNames.Count - 1;i++) {
                    try {
                        registryKey = registryKey.OpenSubKey(keyNames[i + 1]);
                    } catch (ArgumentNullException) {
                        // key name is null
                        return String.Join("\\", keyNames.Take(i + 2).ToArray());
                    } catch (NullReferenceException) {
                        // registry key is null
                        return String.Join("\\", keyNames.Take(i + 2).ToArray());
                    } catch (ObjectDisposedException) {
                        // key is closed (could not be opened)
                        return String.Join("\\", keyNames.Take(i + 2).ToArray());
                    }

                    if (registryKey == null) {
                        return String.Join("\\", keyNames.Take(i + 2).ToArray());
                    }
                }

                if (registryKey == null) {
                    return keyName;
                }
                return String.Empty;
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        private RegistryKey CreateKeyInRegistryView(string keyName, RegistryKeyPermissionCheck registryKeyPermissionCheck, RegistryView registryView) {
            RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView);

            if (registryKey == null) {
                // base key does not exist
                return null;
            }
            
            int subKeyNameIndex = keyName.IndexOf("\\") + 1;

            if (subKeyNameIndex > 0) {
                try {
                    registryKey = registryKey.CreateSubKey(keyName.Substring(subKeyNameIndex), registryKeyPermissionCheck);
                } catch (NullReferenceException) {
                    // registry key is null
                    return null;
                } catch (ObjectDisposedException) {
                    // key is closed (could not be opened)
                    throw new ArgumentException("The key \"" + keyName + "\" is closed.");
                } catch (IOException) {
                    // the key is marked for deletion
                    return null;
                } catch (UnauthorizedAccessException) {
                    // we don't have write rights to the key
                    throw new SecurityException("The user cannot write to the key \"" + keyName + "\".");
                }
            }
            return registryKey;
        }

        private void DeleteKeyInRegistryView(string keyName, RegistryView registryView) {
            RegistryKey registryKey = OpenBaseKeyInRegistryView(keyName, registryView);

            if (registryKey == null) {
                // base key does not exist (good!)
                return;
            }

            try {
                int subKeyNameIndex = keyName.IndexOf("\\") + 1;

                if (subKeyNameIndex > 0) {
                    try {
                        registryKey.DeleteSubKeyTree(keyName.Substring(subKeyNameIndex), false);
                    } catch (ArgumentNullException) {
                        // key name is null
                        return;
                    } catch (ArgumentException) {
                        // subkey doesn't exist
                        return;
                    } catch (NullReferenceException) {
                        // registry key is null
                        return;
                    } catch (ObjectDisposedException) {
                        // key is closed (could not be opened)
                        return;
                    } catch (UnauthorizedAccessException) {
                        // we don't have write rights to the key
                        throw new SecurityException("The user cannot write to the key \"" + keyName + "\".");
                    }
                }
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        private void SetKeyInRegistryView(string keyName, RegistryView registryView) {
            RegistryKey registryKey = CreateKeyInRegistryView(keyName, RegistryKeyPermissionCheck.Default, registryView);

            try {
                if (registryKey == null) {
                    // key is invalid
                    throw new ArgumentException("The key \"" + keyName + "\" is invalid.");
                }
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        private object GetValueInRegistryView(string keyName, string valueName, RegistryView registryView) {
            RegistryKey registryKey = OpenKeyInRegistryView(keyName, false, registryView);

            if (registryKey == null) {
                // key does not exist
                return null;
            }

            object value = null;

            try {
                try {
                    value = registryKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                } catch (ArgumentNullException) {
                    // value name is null
                    return null;
                } catch (NullReferenceException) {
                    // registry key is null
                    return null;
                } catch (ObjectDisposedException) {
                    // key is closed (could not be opened)
                    return null;
                } catch (IOException) {
                    // the key is marked for deletion
                    return null;
                } catch (UnauthorizedAccessException) {
                    // we don't have read rights to the key
                    throw new SecurityException("The user cannot read from the value \"" + valueName + "\" in key \"" + keyName + "\".");
                }

                RegistryValueKind? valueKind = GetValueKindInRegistryView(keyName, valueName, registryView);

                switch (valueKind) {
                    case RegistryValueKind.Binary:
                    if (value as byte[] is byte[]) {
                        value = Convert.ToBase64String(value as byte[]);
                    }
                    break;
                    case RegistryValueKind.MultiString:
                    if (value as string[] is string[]) {
                        value = String.Join("\0", value as string[]);
                    }
                    break;
                }

                return value;
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        private void SetValueInRegistryView(string keyName, string valueName, object value, RegistryValueKind valueKind, RegistryView registryView) {
            RegistryKey registryKey = CreateKeyInRegistryView(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree, registryView);

            try {
                switch (valueKind) {
                    case RegistryValueKind.Binary:
                    if (value as string is string) {
                        value = Convert.FromBase64String(value as string);
                    }
                    break;
                    case RegistryValueKind.MultiString:
                    if (value as string is string) {
                        value = (value as string).Split('\0');
                    }
                    break;
                }

                try {
                    registryKey.SetValue(valueName, value, valueKind);
                } catch (NullReferenceException) {
                    // registry key is null
                    throw new ArgumentException("The key \"" + keyName + "\" is null.");
                } catch (ObjectDisposedException) {
                    // key is closed (could not be opened)
                    throw new ArgumentException("The key \"" + keyName + "\" is closed.");
                } catch (IOException) {
                    // key represents a root node
                    throw new ArgumentException("The key \"" + keyName + "\" represents a root node.");
                } catch (UnauthorizedAccessException) {
                    throw new SecurityException("The value \"" + valueName + "\" in key \"" + keyName + "\" cannot be accessed by the user.");
                } catch (ArgumentException) {
                    throw new SecurityException("The value \"" + valueName + "\" in key \"" + keyName + "\" has the wrong type.");
                }
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        private void DeleteValueInRegistryView(string keyName, string valueName, RegistryView registryView) {
            RegistryKey registryKey = OpenKeyInRegistryView(keyName, true, registryView);

            if (registryKey == null) {
                // base key does not exist (good!)
                return;
            }
            
            try {
                try {
                    registryKey.DeleteValue(valueName);
                } catch (NullReferenceException) {
                    return;
                }
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        private RegistryValueKind? GetValueKindInRegistryView(string keyName, string valueName, RegistryView registryView) {
            RegistryKey registryKey = OpenKeyInRegistryView(keyName, false, registryView);

            if (registryKey == null) {
                return null;
            }

            try {
                try {
                    return registryKey.GetValueKind(valueName);
                } catch (ArgumentNullException) {
                    // value name is null
                    return null;
                } catch (NullReferenceException) {
                    // registry key is null
                    return null;
                } catch (IOException) {
                    // value does not exist
                    return null;
                }
            } finally {
                if (registryKey != null) {
                    registryKey.Close();
                }
            }
        }

        // this function deals with this utter trashfire
        // https://docs.microsoft.com/en-us/windows/win32/winprog64/shared-registry-keys#redirected-shared-and-reflected-keys-under-wow64
        private string GetRedirectedKeyValueName(string keyValueName, BINARY_TYPE binaryType) {
            // does our OS use WOW64?
            string windowsVersionName = GetWindowsVersionName(false, false, true);

            if (windowsVersionName != "Windows Vista 64-bit" &&
                windowsVersionName != "Windows Server 2008 64-bit" &&
                windowsVersionName != "Windows 7 64-bit" &&
                windowsVersionName != "Windows Server 2008 R2 64-bit" &&
                windowsVersionName != "Windows 8 64-bit" &&
                windowsVersionName != "Windows Server 2012 64-bit" &&
                windowsVersionName != "Windows 8.1 64-bit" &&
                windowsVersionName != "Windows Server 2012 R2 64-bit" &&
                windowsVersionName != "Windows 10 64-bit" &&
                windowsVersionName != "Windows Server 2016 64-bit" &&
                windowsVersionName != "Windows Server 2019 64-bit" &&
                windowsVersionName != "Windows Server 2022 64-bit") {
                // no
                return keyValueName;
            }

            // is the binary 32-bit?
            if (binaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                // no
                return keyValueName;
            }

            // can be empty for default value, but not null
            if (keyValueName == null) {
                return keyValueName;
            }
            
            // make these keys uppercase using a regex
            keyValueName = Regex.Replace(keyValueName, "\\\\WOW6432NODE\\\\", "\\WOW6432NODE\\", RegexOptions.IgnoreCase);
            keyValueName = Regex.Replace(keyValueName, "\\\\WOW64AANODE\\\\", "\\WOW64AANODE\\", RegexOptions.IgnoreCase);

            // remove Wow6432Node and WowAA32Node after affected keys
            List<string> keyValueNameSplit = keyValueName.Split(new string[] { "\\WOW6432NODE\\", "\\WOW64AANODE\\" }, StringSplitOptions.None).ToList();

            if (keyValueNameSplit.Count < 2) {
                return keyValueName;
            }

            string wow64KeyUpper = keyValueNameSplit[0].ToUpperInvariant();
            string wow64KeyName = (keyValueNameSplit[1] + "\\");

            if (WOW64KeyLists.ContainsKey(wow64KeyUpper)) {
                List<WOW64Key> wow64KeyList = WOW64KeyLists[wow64KeyUpper];
                WOW64Key.EFFECT effect = WOW64Key.EFFECT.SHARED;
                List<string> effectExceptionValueNames = new List<string>();
                windowsVersionName = GetWindowsVersionName(false, false, false);
                bool removeWOW64Subkey = false;

                for (int i = 0;i < wow64KeyList.Count;i++) {
                    if (wow64KeyName.StartsWith(wow64KeyList[i].Name + "\\", StringComparison.InvariantCultureIgnoreCase)) {
                        effect = wow64KeyList[i].Effect;
                        effectExceptionValueNames = wow64KeyList[i].EffectExceptionValueNames;

                        if (effect == WOW64Key.EFFECT.SHARED_VISTA) {
                            if (windowsVersionName == "Windows Server 2003" || windowsVersionName == "Windows XP") {
                                effect = WOW64Key.EFFECT.REDIRECTED;
                            } else {
                                effect = WOW64Key.EFFECT.SHARED;
                            }
                        }

                        switch (effect) {
                            case WOW64Key.EFFECT.REDIRECTED:
                            removeWOW64Subkey = true;
                            break;
                            case WOW64Key.EFFECT.REDIRECTED_EXCEPTION_VALUE_IS_DEFINED:
                            // check exceptions for value defined in registry
                            object effectExceptionValue = null;

                            for (int j = 0;j < effectExceptionValueNames.Count;j++) {
                                try {
                                    effectExceptionValue = GetValueInRegistryView(GetKeyValueNameFromKernelRegistryString(String.Join("\\", keyValueNameSplit.GetRange(0, i))), effectExceptionValueNames[j], RegistryView.Registry64);
                                } catch (ArgumentException) {
                                    // value doesn't exist
                                    effectExceptionValue = null;
                                } catch (SecurityException) {
                                    // value exists but we can't get it
                                    removeWOW64Subkey = true;
                                }

                                // just checking it exists
                                if (effectExceptionValue != null) {
                                    removeWOW64Subkey = true;
                                }
                            }
                            break;
                        }

                        if (removeWOW64Subkey) {
                            // key after wow64xxnode will shift into current position
                            // therefore, after the next loop...
                            // one is added to i so we effectively move two keys
                            // so we will be looking at the next potential wow64xxnode candidate
                            keyValueName = String.Join("\\", keyValueNameSplit);
                        }
                        break;
                    }
                }
            }
            return keyValueName;
        }

        private bool CompareKeys(RegistryView registryView, RegistryStateElement registryStateElement, RegistryStateElement activeRegistryStateElement) {
            if (registryStateElement == null || activeRegistryStateElement == null) {
                return true;
            }

            if (String.IsNullOrEmpty(activeRegistryStateElement._Deleted)) {
                // key did exist before
                // that means it should still exist
                if (!String.IsNullOrEmpty(TestKeyDeletedInRegistryView(GetUserKeyValueName(registryStateElement.KeyName), registryView))) {
                    // key no longer exists, bad state
                    return false;
                }
            }
            return true;
        }

        private bool CompareValues(object value, RegistryView registryView, RegistryStateElement registryStateElement, RegistryStateElement activeRegistryStateElement) {
            // caller needs to decide what to do if value is null
            if (value == null) {
                throw new ArgumentNullException("The value is null.");
            }

            if (registryStateElement == null) {
                throw new ArgumentNullException("The registryStateElement is null.");
            }

            RegistryValueKind? registryValueKind = GetValueKindInRegistryView(GetUserKeyValueName(registryStateElement.KeyName), registryStateElement.ValueName, registryView);
            string comparableValueString = value.ToString();
            string comparableRegistryStateElementValueString = registryStateElement.Value;

            // if value kind is the same as current value kind
            if (registryValueKind == registryStateElement.ValueKind) {
                if (activeRegistryStateElement != null) {
                    // account for expanded values
                    comparableRegistryStateElementValueString = String.IsNullOrEmpty(activeRegistryStateElement._ValueExpanded) ? comparableRegistryStateElementValueString : activeRegistryStateElement._ValueExpanded;
                }

                // check value matches current value/current expanded value
                if (comparableValueString == comparableRegistryStateElementValueString) {
                    return true;
                }

                // for ActiveX: check if it matches as a path
                try {
                    if (ComparePaths(comparableValueString, comparableRegistryStateElementValueString)) {
                        return true;
                    }
                } catch {
                    // Fail silently.
                }
            }

            if (activeRegistryStateElement != null) {
                // if value existed before
                if (!String.IsNullOrEmpty(activeRegistryStateElement._Deleted)) {
                    // value kind before also matters
                    if (registryValueKind == activeRegistryStateElement.ValueKind) {
                        // get value before
                        comparableRegistryStateElementValueString = activeRegistryStateElement.Value;

                        // check value matches
                        if (comparableValueString == comparableRegistryStateElementValueString) {
                            return true;
                        }

                        // check if it matches as a path
                        try {
                            if (ComparePaths(comparableValueString, comparableRegistryStateElementValueString)) {
                                return true;
                            }
                        } catch {
                            // Fail silently.
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

            try {
                fullPath = Path.GetFullPath(TemplateName);
            } catch (PathTooLongException) {
                throw new ArgumentException("The path is too long to \"" + TemplateName + "\".");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the Full Path to \"" + TemplateName + "\" requires elevation.");
            } catch (NotSupportedException) {
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
                resumeEventWaitHandle.Reset();
                modificationsQueue = new Dictionary<ulong, SortedList<DateTime, RegistryStateElement>>();
                kcbModificationKeyNames = new Dictionary<ulong, string>();

                kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
                kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Registry);

                kernelSession.Source.Kernel.RegistryQueryValue += GotValue;

                kernelSession.Source.Kernel.RegistryCreate += ModificationAdded;
                kernelSession.Source.Kernel.RegistrySetValue += ModificationAdded;
                kernelSession.Source.Kernel.RegistrySetInformation += ModificationAdded;

                kernelSession.Source.Kernel.RegistryDelete += ModificationRemoved;
                kernelSession.Source.Kernel.RegistryDeleteValue += ModificationRemoved;

                //kernelSession.Source.Kernel.RegistryFlush += RegistryModified;

                // https://social.msdn.microsoft.com/Forums/en-US/ff07fc25-31e3-4b6f-810e-7a1ee458084b/etw-registry-monitoring?forum=etw
                kernelSession.Source.Kernel.RegistryKCBCreate += KCBStarted;
                kernelSession.Source.Kernel.RegistryKCBRundownBegin += KCBStarted;

                kernelSession.Source.Kernel.RegistryKCBDelete += KCBStopped;
                kernelSession.Source.Kernel.RegistryKCBRundownEnd += KCBStopped;

                Thread processThread = new Thread(delegate () {
                    kernelSession.Source.Process();
                });

                processThread.Start();

                try {
                    // ensure the kernel session is actually processing
                    for (int i = 0;i < IMPORT_TIMEOUT;i++) {
                        // we just ping this value so it gets detected we tried to read it
                        Registry.GetValue("HKEY_LOCAL_MACHINE", IMPORT_RESUME, null);

                        if (!ImportPaused) {
                            break;
                        }

                        //if (sync) {
                        //Thread.Sleep(1000);
                        //} else {
                        await Task.Delay(1000).ConfigureAwait(true);
                        //}
                    }

                    if (ImportPaused) {
                        throw new InvalidRegistryStateException("A timeout occured while starting the Import.");
                    }
                } catch {
                    kernelSession.Dispose();
                    kernelSession = null;
                    ImportStarted = false;
                }
            } catch {
                ImportStarted = false;
            }
        }

        private async Task StopImportAsync(bool sync) {
            try {
                base.StopImport();
                resumeEventWaitHandle.Set();

                // stop kernelSession
                // we give the registry state a ten second
                // timeout, which should be enough
                for (int i = 0;i < IMPORT_TIMEOUT;i++) {
                    Registry.GetValue("HKEY_LOCAL_MACHINE", IMPORT_PAUSE, null);

                    if (ImportPaused) {
                        break;
                    }

                    if (sync) {
                        Thread.Sleep(1000);
                    } else {
                        await Task.Delay(1000).ConfigureAwait(true);
                    }
                }

                if (!ImportPaused) {
                    throw new InvalidRegistryStateException("A timeout occured while stopping the Import.");
                }

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
                SetFlashpointSecurePlayerSection(TemplateName);
            } finally {
                kernelSession.Dispose();
                kernelSession = null;
                ImportStarted = false;
            }
        }

        new public void StopImport() {
            // do not await this, bool hack
            StopImportAsync(true);
        }

        public async Task StopImportAsync() {
            await StopImportAsync(false).ConfigureAwait(false);
        }

        new public void Activate(string templateName) {
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
                RegistryStateElement registryStateElement = null;
                RegistryStateElement activeRegistryStateElement = null;
                string keyName = null;
                object value = null;
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
                activeModificationsElement.RegistryStates._Administrator = TestLaunchedAsAdministratorUser();
                RegistryView registryView = RegistryView.Registry32;

                if (modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                    registryView = RegistryView.Registry64;
                }

                ProgressManager.CurrentGoal.Start(modificationsElement.RegistryStates.Count + modificationsElement.RegistryStates.Count);

                try {
                    // populate active modifications
                    for (int i = 0; i < modificationsElement.RegistryStates.Count; i++) {
                        // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                        registryStateElement = modificationsElement.RegistryStates.Get(i) as RegistryStateElement;

                        if (registryStateElement == null) {
                            Deactivate();
                            throw new System.Configuration.ConfigurationErrorsException("The Registry State Element (" + i + ") is null.");
                        }

                        // GOAL: find the CURRENT value in the REAL REGISTRY
                        // ACTIVE REGISTRY ELEMENT should reflect real registry
                        // DELETED = DELETED in REAL REGISTRY
                        // this keyName variable is a temp variable specific to this user
                        // it should not get saved
                        keyName = GetUserKeyValueName(registryStateElement.KeyName);
                        value = null;

                        activeRegistryStateElement = new RegistryStateElement {
                            Type = registryStateElement.Type,
                            KeyName = registryStateElement.KeyName,
                            ValueName = registryStateElement.ValueName,
                            ValueKind = GetValueKindInRegistryView(keyName, registryStateElement.ValueName, registryView),
                            _Deleted = String.Empty
                        };

                        if (registryStateElement.Type == TYPE.KEY) {
                            // we create a key
                            activeRegistryStateElement.Type = TYPE.KEY;
                            activeRegistryStateElement._Deleted = TestKeyDeletedInRegistryView(keyName, registryView);
                        } else {
                            try {
                                valueExpanded = Environment.ExpandEnvironmentVariables(registryStateElement.Value);

                                if (valueExpanded != registryStateElement.Value) {
                                    activeRegistryStateElement._ValueExpanded = valueExpanded;
                                }
                            } catch (ArgumentNullException) {
                                // Fail silently.
                            }

                            try {
                                value = /*ReplaceStartupPathEnvironmentVariable(LengthenValue(*/GetValueInRegistryView(keyName, registryStateElement.ValueName, registryView)/*, fullPath))*/;
                            } catch (ArgumentException) {
                                // value doesn't exist
                                value = null;
                            } catch (SecurityException) {
                                // value exists but we can't get it
                                throw new TaskRequiresElevationException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" cannot be accessed by the user.");
                            }

                            if (value == null) {
                                keyDeleted = TestKeyDeletedInRegistryView(keyName, registryView);

                                if (String.IsNullOrEmpty(keyDeleted)) {
                                    // we create a value
                                    // the value does not exist
                                    activeRegistryStateElement.Type = TYPE.VALUE;
                                    activeRegistryStateElement._Deleted = registryStateElement.ValueName;
                                } else {
                                    // we create a value
                                    // the value, and the key it belonged to, does not exist
                                    activeRegistryStateElement.Type = TYPE.KEY;
                                    activeRegistryStateElement._Deleted = keyDeleted;
                                }
                            } else {
                                // we edit a value that exists
                                activeRegistryStateElement.Type = TYPE.VALUE;
                                activeRegistryStateElement.Value = value.ToString();
                            }
                        }

                        activeModificationsElement.RegistryStates.Set(activeRegistryStateElement);
                        ProgressManager.CurrentGoal.Steps++;
                    }

                    SetFlashpointSecurePlayerSection(TemplateName);

                    for (int i = 0; i < modificationsElement.RegistryStates.Count; i++) {
                        // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                        registryStateElement = modificationsElement.RegistryStates.Get(i) as RegistryStateElement;

                        if (registryStateElement == null) {
                            Deactivate();
                            throw new System.Configuration.ConfigurationErrorsException("The Registry State Element (" + i + ") is null.");
                        }

                        activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(registryStateElement.Name) as RegistryStateElement;

                        if (activeRegistryStateElement == null) {
                            Deactivate();
                            throw new System.Configuration.ConfigurationErrorsException("The Active Registry State Element (" + registryStateElement.Name + ") is null.");
                        }

                        keyName = GetUserKeyValueName(registryStateElement.KeyName);

                        // we don't delete existing keys/values, since the program just won't use deleted keys/values
                        // therefore, _Deleted is ignored on all but the active registry state
                        switch (registryStateElement.Type) {
                            case TYPE.KEY:
                                try {
                                    SetKeyInRegistryView(keyName, registryView);
                                } catch (InvalidOperationException) {
                                    // key marked for deletion
                                    Deactivate();
                                    throw new InvalidRegistryStateException("The key \"" + keyName + "\" is marked for deletion.");
                                } catch (ArgumentException) {
                                    // key doesn't exist and can't be created
                                    Deactivate();
                                    throw new TaskRequiresElevationException("Creating the key \"" + keyName + "\" requires elevation.");
                                } catch (SecurityException) {
                                    // key exists and we can't modify it
                                }
                                break;
                            case TYPE.VALUE:
                                try {
                                    SetValueInRegistryView(keyName, registryStateElement.ValueName, String.IsNullOrEmpty(activeRegistryStateElement._ValueExpanded) ? registryStateElement.Value : activeRegistryStateElement._ValueExpanded, registryStateElement.ValueKind.GetValueOrDefault(), registryView);
                                } catch (FormatException) {
                                    // value marked for deletion
                                    Deactivate();
                                    throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" must be Base64.");
                                } catch (InvalidOperationException) {
                                    // value marked for deletion
                                    Deactivate();
                                    throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" is marked for deletion.");
                                } catch (ArgumentException) {
                                    // value doesn't exist and can't be created
                                    Deactivate();
                                    throw new TaskRequiresElevationException("Creating the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                } catch (SecurityException) {
                                    // value exists and we can't modify it
                                    Deactivate();
                                    throw new TaskRequiresElevationException("Modifying the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                }
                                break;
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }

        public void Deactivate(MODIFICATIONS_REVERT_METHOD modificationsRevertMethod = MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY) {
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
                    SetFlashpointSecurePlayerSection(TemplateName);
                    return;
                }

                RegistryStateElement registryStateElement = null;
                RegistryStateElement activeRegistryStateElement = null;
                object value = null;
                bool clear = false;

                if (activeModificationsElement.RegistryStates._Administrator != TestLaunchedAsAdministratorUser()) {
                    // TODO: lame
                    throw new TaskRequiresElevationException("Deactivating the Registry State requires elevation.");
                }

                RegistryView registryView = RegistryView.Registry32;

                if (modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                    // Super Registry 64 DS
                    registryView = RegistryView.Registry64;
                }

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
                                    value = null;
                                    clear = false;

                                    if (activeRegistryStateElement.Type == TYPE.KEY && registryStateElement.Type == TYPE.KEY) {
                                        // we previously created a key
                                        // it may or may not have existed before
                                        // so it may or may not need to exist
                                        if (!CompareKeys(registryView, registryStateElement, activeRegistryStateElement)) {
                                            clear = true;
                                        }
                                    } else {
                                        string keyName = GetUserKeyValueName(registryStateElement.KeyName);

                                        try {
                                            value = GetValueInRegistryView(keyName, registryStateElement.ValueName, registryView);
                                        } catch (ArgumentException) {
                                            // value doesn't exist
                                            value = null;
                                        } catch (SecurityException) {
                                            // value exists but we can't get it
                                            throw new TaskRequiresElevationException("Getting the value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                        }

                                        if ((activeRegistryStateElement.Type == TYPE.KEY && registryStateElement.Type == TYPE.VALUE) || (activeRegistryStateElement.Type == TYPE.VALUE && !String.IsNullOrEmpty(activeRegistryStateElement._Deleted))) {
                                            // we previously created a value
                                            // the value, (and potentially the key it belonged to) did not exist before
                                            // the value may or may not exist now
                                            // if the value still exists, we need to check it's not edited
                                            if (value != null) {
                                                // value still exists
                                                if (!CompareValues(value, registryView, registryStateElement, activeRegistryStateElement)) {
                                                    clear = true;
                                                }
                                            }
                                        } else if (activeRegistryStateElement.Type == TYPE.VALUE && String.IsNullOrEmpty(activeRegistryStateElement._Deleted)) {
                                            // we previously edited a value that existed before
                                            // we need to check it still exists in one of the two valid states
                                            if (value == null) {
                                                clear = true;
                                            } else {
                                                if (!CompareValues(value, registryView, registryStateElement, activeRegistryStateElement)) {
                                                    clear = true;
                                                }
                                            }
                                        }
                                    }
                                    
                                    if (clear) {
                                        activeModificationsElement.RegistryStates.Clear();
                                        activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                                        activeModificationsElement.RegistryStates._Administrator = false;
                                        SetFlashpointSecurePlayerSection(TemplateName);
                                        return;
                                    }
                                }
                            }

                            ProgressManager.CurrentGoal.Steps++;
                        }
                    }

                    Exception exception = null;

                    while (activeModificationsElement.RegistryStates.Count > 0) {
                        try {
                            activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(0) as RegistryStateElement;

                            // how can it be deleted already?? just paranoia
                            if (activeRegistryStateElement != null) {
                                registryStateElement = modificationsElement.RegistryStates.Get(activeRegistryStateElement.Name) as RegistryStateElement;

                                if (registryStateElement != null) {
                                    switch (activeRegistryStateElement.Type) {
                                        case TYPE.KEY:
                                        if (!String.IsNullOrEmpty(activeRegistryStateElement._Deleted) || modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                            try {
                                                // key didn't exist before
                                                DeleteKeyInRegistryView(GetUserKeyValueName(activeRegistryStateElement._Deleted), registryView);
                                            } catch (SecurityException) {
                                                // value exists and we can't modify it
                                                throw new TaskRequiresElevationException("Deleting the key \"" + activeRegistryStateElement._Deleted + "\" requires elevation.");
                                            }
                                        }
                                        break;
                                        case TYPE.VALUE:
                                        if (String.IsNullOrEmpty(activeRegistryStateElement._Deleted) && modificationsRevertMethod != MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                            string keyName = GetUserKeyValueName(activeRegistryStateElement.KeyName);

                                            try {
                                                // value was different before
                                                SetValueInRegistryView(keyName, activeRegistryStateElement.ValueName, activeRegistryStateElement.Value, activeRegistryStateElement.ValueKind.GetValueOrDefault(), registryView);
                                            } catch (InvalidOperationException) {
                                                // value doesn't exist and can't be created
                                                throw new InvalidRegistryStateException("The value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" cannot be created.");
                                            } catch (ArgumentException) {
                                                // value doesn't exist and can't be created
                                                throw new InvalidRegistryStateException("The value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" cannot be created.");
                                            } catch (SecurityException) {
                                                // value exists and we can't modify it
                                                throw new TaskRequiresElevationException("Setting the value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" requires elevation.");
                                            }
                                        } else {
                                            try {
                                                // value didn't exist before
                                                DeleteValueInRegistryView(GetUserKeyValueName(activeRegistryStateElement.KeyName), activeRegistryStateElement.ValueName, registryView);
                                            } catch (SecurityException) {
                                                // value exists and we can't modify it
                                                throw new TaskRequiresElevationException("Deleting the value \"" + activeRegistryStateElement.ValueName + "\" requires elevation.");
                                            }
                                        }
                                        break;
                                    }
                                    
                                    activeModificationsElement.RegistryStates.RemoveAt(0);
                                }
                            }
                        } catch (Exception ex) {
                            exception = ex;
                            continue;
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }

                    activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                    activeModificationsElement.RegistryStates._Administrator = false;
                    SetFlashpointSecurePlayerSection(TemplateName);

                    if (exception != null) {
                        throw exception;
                    }
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }

        private void GotValue(RegistryTraceData registryTraceData) {
            if (registryTraceData.ValueName != null
                && (registryTraceData.ProcessID == Process.GetCurrentProcess().Id
                || registryTraceData.ProcessID == -1)) {
                if (ImportPaused) {
                    if (registryTraceData.ValueName.Equals(IMPORT_RESUME, StringComparison.InvariantCultureIgnoreCase)) {
                        ImportPaused = false;
                        // hold here until after the control has installed
                        // that way we can recieve registry messages as they come in
                        // with reassurance the control has installed already
                        // therefore, key names will be redirected properly
                        resumeEventWaitHandle.WaitOne();
                    }
                } else {
                    if (registryTraceData.ValueName.Equals(IMPORT_PAUSE, StringComparison.InvariantCultureIgnoreCase)) {
                        ImportPaused = true;
                    }
                }
            }
        }

        private void ModificationAdded(RegistryTraceData registryTraceData) {
            // self explanatory
            if (ImportPaused) {
                return;
            }

            // if KCBModificationKeyNames has KeyHandle, add to RegistryTimeline
            // else add to RegistryTimelineQueue
            // check the registry change was made by this process (or an unknown process - might be ours)
            if (registryTraceData.ProcessID != Process.GetCurrentProcess().Id && registryTraceData.ProcessID != -1) {
                return;
            }

            // if there isn't any KCB to deal with...
            TemplateElement templateElement = null;

            // must catch exceptions here for thread safety
            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch (System.Configuration.ConfigurationErrorsException) {
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

            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;
            object value = null;
            RegistryView registryView = RegistryView.Registry32;

            if (modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                registryView = RegistryView.Registry64;
            }

            if (safeKeyHandle == 0) {
                // we don't need to queue it, we can just add the key right here
                registryStateElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(registryStateElement.KeyName), modificationsElement.RegistryStates.BinaryType);
                
                registryStateElement.ValueKind = GetValueKindInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView);
                value = null;

                try {
                    value = ReplaceStartupPathEnvironmentVariable(LengthenValue(GetValueInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView), fullPath));
                } catch (ArgumentException) {
                    // value doesn't exist
                    value = null;
                } catch (SecurityException) {
                    // value exists but we can't get it
                    // this shouldn't happen because this task requires elevation
                    value = String.Empty;
                }
                
                if (value == null) {
                    if (String.IsNullOrEmpty(registryStateElement.ValueName) && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryStateElement.KeyName, registryView))) {
                        registryStateElement.Type = TYPE.KEY;
                    } else {
                        registryStateElement.Type = TYPE.VALUE;
                    }

                    if (registryStateElement.Type == TYPE.VALUE) {
                        ModificationRemoved(registryTraceData);
                        return;
                    }
                } else {
                    registryStateElement.Type = TYPE.VALUE;
                    registryStateElement.Value = value.ToString();
                }

                modificationsElement.RegistryStates.Set(registryStateElement);
                //SetFlashpointSecurePlayerSection(TemplateName);
                return;
            }

            // need to deal with KCB
            // well, we already know the base key name from before, so we can wrap this up now
            if (kcbModificationKeyNames.ContainsKey(safeKeyHandle)) {
                registryStateElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(kcbModificationKeyNames[safeKeyHandle] + "\\" + registryStateElement.KeyName), modificationsElement.RegistryStates.BinaryType);

                registryStateElement.ValueKind = GetValueKindInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView);
                value = null;

                try {
                    value = ReplaceStartupPathEnvironmentVariable(LengthenValue(GetValueInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView), fullPath));
                } catch (ArgumentException) {
                } catch (SecurityException) {
                    // we have permission to access the key at this point so this must not be important
                }

                if (value == null) {
                    // if not just the value, but the entire key, is deleted, treat this as a key type
                    if (String.IsNullOrEmpty(registryStateElement.ValueName) && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryStateElement.KeyName, registryView))) {
                        registryStateElement.Type = TYPE.KEY;
                    } else {
                        registryStateElement.Type = TYPE.VALUE;
                    }

                    if (registryStateElement.Type == TYPE.VALUE) {
                        ModificationRemoved(registryTraceData);
                        return;
                    }
                } else {
                    registryStateElement.Type = TYPE.VALUE;
                    registryStateElement.Value = value.ToString();
                }

                modificationsElement.RegistryStates.Set(registryStateElement);
                //SetFlashpointSecurePlayerSection(TemplateName);
                return;
            }

            // worst case scenario: now we need to watch for when we get info on that handle
            if (!modificationsQueue.ContainsKey(safeKeyHandle)) {
                // create queue if does not exist (might be multiple keys waiting on it)
                modificationsQueue[safeKeyHandle] = new SortedList<DateTime, RegistryStateElement>();
            }

            // add key to the queue for that handle
            modificationsQueue[safeKeyHandle][registryTraceData.TimeStamp] = registryStateElement;
        }

        private void ModificationRemoved(RegistryTraceData registryTraceData) {
            if (ImportPaused) {
                return;
            }

            // if KCBModificationKeyNames has KeyHandle, add to RegistryTimeline
            // else add to RegistryTimelineQueue
            // check the registry change was made by this process (or an unknown process - might be ours)
            if (registryTraceData.ProcessID != Process.GetCurrentProcess().Id && registryTraceData.ProcessID != -1) {
                return;
            }

            TemplateElement templateElement = null;

            // must catch exceptions here for thread safety
            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch (System.Configuration.ConfigurationErrorsException) {
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
                registryStateElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(registryStateElement.KeyName), modificationsElement.RegistryStates.BinaryType);

                // key was deleted - don't need to find its value, just enough info for the name
                modificationsElement.RegistryStates.Remove(registryStateElement.Name);
                //SetModificationsElement(modificationsElement, Name);
                return;
            }

            if (kcbModificationKeyNames.ContainsKey(safeKeyHandle)) {
                // we have info from the handle already to get the name
                registryStateElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(kcbModificationKeyNames[safeKeyHandle] + "\\" + registryStateElement.KeyName), modificationsElement.RegistryStates.BinaryType);

                modificationsElement.RegistryStates.Remove(registryStateElement.Name);
                //SetModificationsElement(modificationsElement, Name);
                return;
            }

            // worst case scenario: now we need to watch for when we get info on that handle
            if (!modificationsQueue.ContainsKey(safeKeyHandle)) {
                // create queue if does not exist (might be multiple keys waiting on it)
                modificationsQueue[safeKeyHandle] = new SortedList<DateTime, RegistryStateElement>();
            }

            // TODO: how do we handle this for deletion? (see also KCBStopped)
            modificationsQueue[safeKeyHandle][registryTraceData.TimeStamp] = registryStateElement;
        }

        private void KCBStarted(RegistryTraceData registryTraceData) {
            if (ImportPaused) {
                return;
            }

            // add the key to KeyNames, and clear any queued registry modifications with the same KeyHandle
            // are KCBs system-wide?
            //if (registryTraceData.ProcessID != Process.GetCurrentProcess().Id && registryTraceData.ProcessID != -1) {
            //return;
            //}

            // could be empty, if immediate subkey of hive
            //if (String.IsNullOrEmpty(registryTraceData.KeyName)) {
            //return;
            //}

            // clear out the queue, since we started now, so this handle refers to something else
            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;
            modificationsQueue.Remove(safeKeyHandle);
            kcbModificationKeyNames[safeKeyHandle] = registryTraceData.KeyName;
        }

        private void KCBStopped(RegistryTraceData registryTraceData) {
            if (ImportPaused) {
                return;
            }

            TemplateElement templateElement = null;

            try {
                templateElement = GetTemplateElement(false, TemplateName);
            } catch (System.Configuration.ConfigurationErrorsException) {
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
            kcbModificationKeyNames.Remove(safeKeyHandle);

            // we'll be finding these in a second
            KeyValuePair<DateTime, RegistryStateElement> queuedModification;
            RegistryStateElement registryStateElement;
            object value = null;

            RegistryView registryView = RegistryView.Registry32;

            if (modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                registryView = RegistryView.Registry64;
            }

            // we want to take care of any queued registry timeline events
            // an event entails the date and time of the registry modification
            if (modificationsQueue.ContainsKey(safeKeyHandle)) {
                while (modificationsQueue[safeKeyHandle].Any()) {
                    // get the first event
                    queuedModification = modificationsQueue[safeKeyHandle].First();

                    // add its BaseKeyName
                    registryStateElement = queuedModification.Value;
                    registryStateElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(registryTraceData.KeyName + "\\" + registryStateElement.KeyName), modificationsElement.RegistryStates.BinaryType);
                    registryStateElement.ValueKind = GetValueKindInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView);
                    value = null;

                    // value
                    try {
                        value = ReplaceStartupPathEnvironmentVariable(LengthenValue(GetValueInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView), fullPath));
                    } catch (ArgumentException) {
                        // value doesn't exist
                        value = null;
                    } catch (SecurityException) {
                        // value exists but we can't get it
                        value = String.Empty;
                    }

                    // TODO: is this the best way to handle deletion?
                    // move it into the normal registry modifications
                    if (value == null) {
                        if (String.IsNullOrEmpty(registryStateElement.ValueName) && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryStateElement.KeyName, registryView))) {
                            registryStateElement.Type = TYPE.KEY;
                            modificationsElement.RegistryStates.Set(registryStateElement);
                        } else {
                            registryStateElement.Type = TYPE.VALUE;
                            modificationsElement.RegistryStates.Remove(registryStateElement.Name);
                        }
                    } else {
                        registryStateElement.Type = TYPE.VALUE;
                        registryStateElement.Value = value.ToString();
                        modificationsElement.RegistryStates.Set(registryStateElement);
                    }

                    // and out of the queue
                    // (the Key is the TimeStamp)
                    //SetFlashpointSecurePlayerSection(TemplateName);
                    modificationsQueue[safeKeyHandle].Remove(queuedModification.Key);
                }
            }
        }
    }
}