using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Win32;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement.RegistryBackupElementCollection;

namespace FlashpointSecurePlayer {
    public class RegistryBackups : Modifications {
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

            public WOW64Key(string Name = null, EFFECT Effect = EFFECT.SHARED, List<string> EffectExceptionValueNames = null) {
                this.Name = Name;
                this.Effect = Effect;
                this.EffectExceptionValueNames = EffectExceptionValueNames;
            }
        }
        
        public enum TYPE {
            KEY,
            VALUE
        };

        private const int IMPORT_TIMEOUT = 5;
        private const string IMPORT_RESUME = "FLASHPOINTSECUREPLAYERREGISTRYBACKUPIMPORTRESUME";
        private const string IMPORT_PAUSE = "FLASHPOINTSECUREPLAYERREGISTRYBACKUPIMPORTPAUSE";
        private const string KEY_DELETED = "";
        private string fullPath = null;
        private EventWaitHandle resumeEventWaitHandle = new ManualResetEvent(false);
        private Dictionary<ulong, SortedList<DateTime, RegistryBackupElement>> modificationsQueue = null;
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
        
        public RegistryBackups(Form form) : base(form) { }

        ~RegistryBackups() {
            if (ImportStarted) {
                try {
                    StopImport();
                } catch (RegistryBackupFailedException) {
                    // Fail silently.
                } catch (System.Configuration.ConfigurationErrorsException) {
                    // Fail silently.
                } catch (InvalidOperationException) {
                    // Fail silently.
                }
            }

            try {
                Deactivate();
            } catch (RegistryBackupFailedException) {
                // Fail silently.
            } catch (System.Configuration.ConfigurationErrorsException) {
                // Fail silently.
            } catch (TaskRequiresElevationException) {
                // Fail silently.
            }
        }

        private string GetUserKeyValueName(string keyValueName) {
            // can be empty, but not null
            if (TestLaunchedAsAdministratorUser() || keyValueName == null) {
                return keyValueName;
            }

            keyValueName = keyValueName.ToUpper() + "\\";

            if (keyValueName.IndexOf("HKEY_LOCAL_MACHINE\\") == 0) {
                keyValueName = "HKEY_CURRENT_USER\\" + keyValueName.Substring(19);
            }

            keyValueName = RemoveTrailingSlash(keyValueName);
            return keyValueName;
        }

        private string GetKeyValueNameFromKernelRegistryString(string kernelRegistryString) {
            // can be empty, but not null
            if (kernelRegistryString == null) {
                return kernelRegistryString;
            }

            kernelRegistryString = kernelRegistryString.ToUpper() + "\\";
            string keyValueName = "";

            if (kernelRegistryString.IndexOf("\\REGISTRY\\MACHINE\\") == 0) {
                keyValueName = "HKEY_LOCAL_MACHINE\\" + kernelRegistryString.Substring(18);
            } else {
                if (kernelRegistryString.IndexOf("\\REGISTRY\\USER\\") == 0) {
                    keyValueName = "HKEY_USERS\\" + kernelRegistryString.Substring(15);
                    string currentUser = WindowsIdentity.GetCurrent().User.Value.ToUpper();

                    if (keyValueName.IndexOf("HKEY_USERS\\" + currentUser + "_CLASSES\\") == 0) {
                        keyValueName = "HKEY_CURRENT_USER\\SOFTWARE\\CLASSES\\" + keyValueName.Substring(20 + currentUser.Length);
                    } else {
                        if (keyValueName.IndexOf("HKEY_USERS\\" + currentUser + "\\") == 0) {
                            keyValueName = "HKEY_CURRENT_USER\\" + keyValueName.Substring(12 + currentUser.Length);
                        }
                    }
                }
            }

            keyValueName = RemoveTrailingSlash(keyValueName);
            return keyValueName;
        }

        private RegistryKey OpenBaseKeyInRegistryView(string keyName, RegistryView registryView) {
            keyName = keyName.ToUpper() + "\\";
            RegistryHive? registryHive = null;

            if (keyName.IndexOf("HKEY_CURRENT_USER\\") == 0) {
                registryHive = RegistryHive.CurrentUser;
            } else if (keyName.IndexOf("HKEY_LOCAL_MACHINE\\") == 0) {
                registryHive = RegistryHive.LocalMachine;
            } else if (keyName.IndexOf("HKEY_CLASSES_ROOT\\") == 0) {
                registryHive = RegistryHive.ClassesRoot;
            } else if (keyName.IndexOf("HKEY_USERS\\") == 0) {
                registryHive = RegistryHive.Users;
            } else if (keyName.IndexOf("HKEY_PERFORMANCE_DATA\\") == 0) {
                registryHive = RegistryHive.PerformanceData;
            } else if (keyName.IndexOf("HKEY_CURRENT_CONFIG\\") == 0) {
                registryHive = RegistryHive.CurrentConfig;
            } else if (keyName.IndexOf("HKEY_DYN_DATA\\") == 0) {
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
                throw new SecurityException("Access to the base key " + keyName + " is denied.");
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
                return KEY_DELETED;
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
                    throw new ArgumentException("The key " + keyName + " is closed.");
                } catch (IOException) {
                    // the key is marked for deletion
                    return null;
                } catch (UnauthorizedAccessException) {
                    // we don't have write rights to the key
                    throw new SecurityException("The user cannot write to the key " + keyName + ".");
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
                    } catch (NullReferenceException) {
                        // registry key is null
                        return;
                    } catch (ObjectDisposedException) {
                        // key is closed (could not be opened)
                        return;
                    } catch (UnauthorizedAccessException) {
                        // we don't have write rights to the key
                        throw new SecurityException("The user cannot write to the key " + keyName + ".");
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
                    throw new ArgumentException("The key " + keyName + " is invalid.");
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
                    throw new SecurityException("The user cannot read from the value " + valueName + ".");
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
                    throw new ArgumentException("The key " + keyName + " is null.");
                } catch (ObjectDisposedException) {
                    // key is closed (could not be opened)
                    throw new ArgumentException("The key " + keyName + " is closed.");
                } catch (IOException) {
                    // key represents a root node
                    throw new ArgumentException("The key " + keyName + " represents a root node.");
                } catch (UnauthorizedAccessException) {
                    throw new SecurityException("The value " + valueName + " cannot be accessed by the user.");
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
                windowsVersionName != "Windows Server 2019 64-bit") {
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

            keyValueName = keyValueName.ToUpper();

            // remove Wow6432Node and WowAA32Node after affected keys
            List<string> keyValueNameSplit = keyValueName.Split(new string[] { "\\WOW6432NODE\\", "\\WOW64AANODE\\" }, StringSplitOptions.None).ToList();

            if (keyValueNameSplit.Count < 2) {
                return keyValueName;
            }

            if (WOW64KeyLists.ContainsKey(keyValueNameSplit[0])) {
                List<WOW64Key> wow64KeyList = WOW64KeyLists[keyValueNameSplit[0]];
                WOW64Key.EFFECT effect = WOW64Key.EFFECT.SHARED;
                List<string> effectExceptionValueNames = new List<string>();
                windowsVersionName = GetWindowsVersionName(false, false, false);
                bool removeWOW64Subkey = false;

                for (int i = 0;i < wow64KeyList.Count;i++) {
                    if ((keyValueNameSplit[1] + "\\").IndexOf(wow64KeyList[i].Name + "\\") == 0) {
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

        public async Task StartImportAsync(string name, BINARY_TYPE binaryType) {
            base.StartImport(name);

            ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            // this happens here since this check doesn't need to occur to activate
            if (modificationsElement.RegistryBackups.Get(Name) != null) {
                // preset already exists with this name
                // prevent a registry backup from running for a non-curator
                throw new InvalidModificationException("A Modification with the name " + Name + " exists.");
            }

            try {
                fullPath = Path.GetFullPath(Name);
            } catch (PathTooLongException) {
                throw new ArgumentException("The path is too long to " + Name + ".");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the Full Path to " + Name + " requires elevation.");
            } catch (NotSupportedException) {
                throw new ArgumentException("The path to " + Name + " is not supported.");
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

            if (form != null) {
                form.ControlBox = !ImportStarted;
            }

            modificationsElement.RegistryBackups.BinaryType = binaryType;
            resumeEventWaitHandle.Reset();
            modificationsQueue = new Dictionary<ulong, SortedList<DateTime, RegistryBackupElement>>();
            kcbModificationKeyNames = new Dictionary<ulong, string>();

            this.kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            this.kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Registry);

            this.kernelSession.Source.Kernel.RegistryQueryValue += GotValue;

            this.kernelSession.Source.Kernel.RegistryCreate += ModificationAdded;
            this.kernelSession.Source.Kernel.RegistrySetValue += ModificationAdded;
            this.kernelSession.Source.Kernel.RegistrySetInformation += ModificationAdded;

            this.kernelSession.Source.Kernel.RegistryDelete += ModificationRemoved;
            this.kernelSession.Source.Kernel.RegistryDeleteValue += ModificationRemoved;

            //this.KernelSession.Source.Kernel.RegistryFlush += RegistryModified;

            // https://social.msdn.microsoft.com/Forums/en-US/ff07fc25-31e3-4b6f-810e-7a1ee458084b/etw-registry-monitoring?forum=etw
            this.kernelSession.Source.Kernel.RegistryKCBCreate += KCBStarted;
            this.kernelSession.Source.Kernel.RegistryKCBRundownBegin += KCBStarted;

            this.kernelSession.Source.Kernel.RegistryKCBDelete += KCBStopped;
            this.kernelSession.Source.Kernel.RegistryKCBRundownEnd += KCBStopped;

            Thread processThread = new Thread(delegate () {
                kernelSession.Source.Process();
            });

            processThread.Start();

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
                    await Task.Delay(1000).ConfigureAwait(false);
                //}
            }

            if (ImportPaused) {
                throw new RegistryBackupFailedException("A timeout occured while starting the Import.");
            }
        }

        private async Task StopImportAsync(bool sync) {
            base.StopImport();
            resumeEventWaitHandle.Set();

            // stop this.kernelSession
            // we give the registry backup a ten second
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
                throw new RegistryBackupFailedException("A timeout occured while stopping the Import.");
            }

            this.kernelSession.Source.Kernel.RegistryQueryValue -= GotValue;

            this.kernelSession.Source.Kernel.RegistryCreate -= ModificationAdded;
            this.kernelSession.Source.Kernel.RegistrySetValue -= ModificationAdded;
            this.kernelSession.Source.Kernel.RegistrySetInformation -= ModificationAdded;

            this.kernelSession.Source.Kernel.RegistryDelete -= ModificationRemoved;
            this.kernelSession.Source.Kernel.RegistryDeleteValue -= ModificationRemoved;

            //this.KernelSession.Source.Kernel.RegistryFlush -= RegistryModified;

            this.kernelSession.Source.Kernel.RegistryKCBCreate -= KCBStarted;
            this.kernelSession.Source.Kernel.RegistryKCBRundownBegin -= KCBStarted;

            this.kernelSession.Source.Kernel.RegistryKCBDelete -= KCBStopped;
            this.kernelSession.Source.Kernel.RegistryKCBRundownEnd -= KCBStopped;

            this.kernelSession.Source.Dispose();
            this.kernelSession.Dispose();

            SetFlashpointSecurePlayerSection(Name);
            ImportStarted = false;

            if (form != null) {
                form.ControlBox = !ImportStarted;
            }
        }

        new public void StopImport() {
            // do not await this, bool hack
            StopImportAsync(true);
        }

        public async Task StopImportAsync() {
            await StopImportAsync(false).ConfigureAwait(false);
        }

        new public void Activate(string name) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);
            ModificationsElement activeModificationsElement = GetActiveModificationsElement(true, Name);
            RegistryBackupElement registryBackupElement = null;
            RegistryBackupElement activeRegistryBackupElement = null;
            string keyDeleted = null;
            string keyName = null;
            object value = null;

            try {
                fullPath = Path.GetFullPath(Name);
            } catch (PathTooLongException) {
                throw new ArgumentException("The path is too long to " + Name + ".");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the Full Path to " + Name + " requires elevation.");
            } catch (NotSupportedException) {
                throw new ArgumentException("The path to " + Name + " is not supported.");
            }

            // to prevent issues with HKEY_LOCAL_MACHINE and crash recovery
            activeModificationsElement.RegistryBackups._Administrator = TestLaunchedAsAdministratorUser();
            RegistryView registryView = RegistryView.Registry32;

            if (modificationsElement.RegistryBackups.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                registryView = RegistryView.Registry64;
            }

            ProgressManager.CurrentGoal.Start(modificationsElement.RegistryBackups.Count * 2);

            try {
                // populate active modifications
                for (int i = 0;i < modificationsElement.RegistryBackups.Count;i++) {
                    // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                    registryBackupElement = modificationsElement.RegistryBackups.Get(i) as RegistryBackupElement;

                    if (registryBackupElement == null) {
                        Deactivate();
                        throw new System.Configuration.ConfigurationErrorsException("The Registry Backup Element (" + i + ") is null.");
                    }

                    // GOAL: find the CURRENT value in the REAL REGISTRY
                    // ACTIVE REGISTRY ELEMENT should reflect real registry
                    // DELETED = DELETED in REAL REGISTRY
                    // this keyName variable is a temp variable specific to this user
                    // it should not get saved
                    keyName = GetUserKeyValueName(registryBackupElement.KeyName);
                    value = null;

                    activeRegistryBackupElement = new RegistryBackupElement {
                        Type = registryBackupElement.Type,
                        KeyName = registryBackupElement.KeyName,
                        ValueName = registryBackupElement.ValueName,
                        ValueKind = GetValueKindInRegistryView(keyName, registryBackupElement.ValueName, registryView),
                        _Deleted = KEY_DELETED
                    };

                    switch (activeRegistryBackupElement.Type) {
                        case TYPE.KEY:
                        activeRegistryBackupElement._Deleted = TestKeyDeletedInRegistryView(keyName, registryView);
                        break;
                        case TYPE.VALUE:
                        try {
                            value = AddVariablesToLengthenedValue(LengthenValue(GetValueInRegistryView(keyName, registryBackupElement.ValueName, registryView), fullPath));
                        } catch (ArgumentException) {
                            // value doesn't exist
                            value = null;
                        } catch (SecurityException) {
                            // value exists but we can't get it
                            throw new TaskRequiresElevationException("The value " + registryBackupElement.ValueName + " cannot be accessed by the user.");
                        }

                        if (value == null) {
                            keyDeleted = TestKeyDeletedInRegistryView(keyName, registryView);

                            // if not just the value, but the entire key, is deleted, treat this as a key type
                            if (String.IsNullOrEmpty(keyDeleted)) {
                                activeRegistryBackupElement.Type = TYPE.VALUE;
                                activeRegistryBackupElement._Deleted = registryBackupElement.ValueName;
                            } else {
                                activeRegistryBackupElement.Type = TYPE.KEY;
                                activeRegistryBackupElement._Deleted = keyDeleted;
                            }
                        } else {
                            activeRegistryBackupElement.Type = TYPE.VALUE;
                            activeRegistryBackupElement.Value = value.ToString();
                        }
                        break;
                    }

                    // we do this and save in the loop so we can safely deactivate if needed partway through the process
                    activeModificationsElement.RegistryBackups.Set(activeRegistryBackupElement);
                    SetFlashpointSecurePlayerSection(Name);
                    ProgressManager.CurrentGoal.Steps++;
                }

                for (int i = 0;i < modificationsElement.RegistryBackups.Count;i++) {
                    // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                    registryBackupElement = modificationsElement.RegistryBackups.Get(i) as RegistryBackupElement;

                    if (registryBackupElement == null) {
                        Deactivate();
                        throw new System.Configuration.ConfigurationErrorsException("The Registry Backup Element (" + i + ") is null.");
                    }

                    keyName = GetUserKeyValueName(registryBackupElement.KeyName);

                    // we don't delete existing keys/values, since the program just won't use deleted keys/values
                    // therefore, _Deleted is ignored on all but the active registry backup
                    switch (registryBackupElement.Type) {
                        case TYPE.KEY:
                        try {
                            SetKeyInRegistryView(keyName, registryView);
                        } catch (InvalidOperationException) {
                            // key marked for deletion
                            Deactivate();
                            throw new RegistryBackupFailedException("The key " + keyName + " is marked for deletion.");
                        } catch (ArgumentException) {
                            // key doesn't exist and can't be created
                            Deactivate();
                            throw new TaskRequiresElevationException("Creating the key " + keyName + " requires elevation.");
                        } catch (SecurityException) {
                            // key exists and we can't modify it
                        }
                        break;
                        case TYPE.VALUE:
                        try {
                            SetValueInRegistryView(keyName, registryBackupElement.ValueName, RemoveVariablesFromLengthenedValue(registryBackupElement.Value), registryBackupElement.ValueKind.GetValueOrDefault(), registryView);
                        } catch (FormatException) {
                            // value marked for deletion
                            Deactivate();
                            throw new RegistryBackupFailedException("The value " + registryBackupElement.ValueName + " must be Base64.");
                        } catch (InvalidOperationException) {
                            // value marked for deletion
                            Deactivate();
                            throw new RegistryBackupFailedException("The value " + registryBackupElement.ValueName + " is marked for deletion.");
                        } catch (ArgumentException) {
                            // value doesn't exist and can't be created
                            Deactivate();
                            throw new TaskRequiresElevationException("Creating the value " + registryBackupElement.ValueName + " requires elevation.");
                        } catch (SecurityException) {
                            // value exists and we can't modify it
                            Deactivate();
                            throw new TaskRequiresElevationException("Modifying the value " + registryBackupElement.ValueName + " requires elevation.");
                        }
                        break;
                    }

                    ProgressManager.CurrentGoal.Steps++;
                }
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }

        new public void Deactivate() {
            base.Deactivate();
            ModificationsElement activeModificationsElement = GetActiveModificationsElement(false);

            // if the activation backup doesn't exist, we don't need to do stuff
            if (activeModificationsElement == null) {
                return;
            }

            // if the activation backup exists, but no key is marked as active...
            // we assume the registry has changed, and don't revert the changes, to be safe
            // (it should never happen unless the user tampered with the config file)
            string modificationsElementName = activeModificationsElement.Active;

            // don't allow infinite recursion!
            if (String.IsNullOrEmpty(modificationsElementName)) {
                activeModificationsElement.RegistryBackups.Clear();
                SetFlashpointSecurePlayerSection(Name);
                return;
            }

            ModificationsElement modificationsElement = GetModificationsElement(false, modificationsElementName);

            // if the active element pointed to doesn't exist... same assumption
            // and another safeguard against recursion
            if (modificationsElement == null || modificationsElement == activeModificationsElement) {
                activeModificationsElement.RegistryBackups.Clear();
                SetFlashpointSecurePlayerSection(Name);
                return;
            }

            RegistryBackupElement registryBackupElement = null;
            RegistryBackupElement activeRegistryBackupElement = null;
            object value = null;
            bool clear = false;

            if (activeModificationsElement.RegistryBackups._Administrator != TestLaunchedAsAdministratorUser()) {
                // TODO: lame
                throw new TaskRequiresElevationException("Deactivating the Registry Backup requires elevation.");
            }

            RegistryView registryView = RegistryView.Registry32;

            if (modificationsElement.RegistryBackups.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                // Super Registry 64 DS
                registryView = RegistryView.Registry64;
            }

            ProgressManager.CurrentGoal.Start(activeModificationsElement.RegistryBackups.Count * 2);

            try {
                // check if any key has been modified from the modification element
                for (int i = 0;i < activeModificationsElement.RegistryBackups.Count;i++) {
                    // the "active" one is the one that doesn't have a name (it has the "active" attribute)
                    activeRegistryBackupElement = activeModificationsElement.RegistryBackups.Get(i) as RegistryBackupElement;

                    if (activeRegistryBackupElement != null) {
                        registryBackupElement = modificationsElement.RegistryBackups.Get(activeRegistryBackupElement.Name) as RegistryBackupElement;

                        // registryBackupElement represents the value the key SHOULD have *right now*
                        // what to do if modification was recorded, but not in config?
                        // ignore... it's not part of our record
                        if (registryBackupElement != null) {
                            value = null;
                            clear = false;

                            // yes this is supposed to be active, it's PC specific
                            switch (activeRegistryBackupElement.Type) {
                                case TYPE.KEY:
                                // check the key is set (like how in registryBackupElement it must be set)
                                if (!String.IsNullOrEmpty(TestKeyDeletedInRegistryView(GetUserKeyValueName(registryBackupElement.KeyName), registryView))) {
                                    clear = true;
                                }
                                break;
                                case TYPE.VALUE:
                                try {
                                    value = GetValueInRegistryView(GetUserKeyValueName(registryBackupElement.KeyName), registryBackupElement.ValueName, registryView);
                                } catch (ArgumentException) {
                                    // value doesn't exist
                                    value = null;
                                } catch (SecurityException) {
                                    // value exists but we can't get it
                                    throw new TaskRequiresElevationException("Getting the value " + registryBackupElement.ValueName + " requires elevation.");
                                }

                                // check the value is the same as in registryBackupElement (can't be deleted, only active deletes)
                                if (value == null) {
                                    clear = true;
                                } else {
                                    if (value.ToString() != RemoveVariablesFromLengthenedValue(registryBackupElement.Value).ToString()) {
                                        clear = true;
                                    }
                                }

                                // check the ValueKind is the same as in registryBackupElement
                                if (GetValueKindInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView) != registryBackupElement.ValueKind) {
                                    clear = true;
                                }
                                break;
                            }

                            if (clear) {
                                activeModificationsElement.RegistryBackups.Clear();
                                SetFlashpointSecurePlayerSection(Name);
                                return;
                            }
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;
                }

                // our records match, revert keys
                while (activeModificationsElement.RegistryBackups.Count > 0) {
                    activeRegistryBackupElement = activeModificationsElement.RegistryBackups.Get(0) as RegistryBackupElement;

                    // how can it be deleted already?? just paranoia
                    if (activeRegistryBackupElement != null) {
                        switch (activeRegistryBackupElement.Type) {
                            case TYPE.KEY:
                            if (!String.IsNullOrEmpty(activeRegistryBackupElement._Deleted)) {
                                try {
                                    // key didn't exist before
                                    DeleteKeyInRegistryView(GetUserKeyValueName(activeRegistryBackupElement._Deleted), registryView);
                                } catch (SecurityException) {
                                    // value exists and we can't modify it
                                    throw new TaskRequiresElevationException("Deleting the key " + activeRegistryBackupElement._Deleted + " requires elevation.");
                                }
                            }
                            break;
                            case TYPE.VALUE:
                            if (String.IsNullOrEmpty(activeRegistryBackupElement._Deleted)) {
                                try {
                                    // value was different before
                                    SetValueInRegistryView(GetUserKeyValueName(activeRegistryBackupElement.KeyName), activeRegistryBackupElement.ValueName, RemoveVariablesFromLengthenedValue(activeRegistryBackupElement.Value), activeRegistryBackupElement.ValueKind.GetValueOrDefault(), registryView);
                                } catch (InvalidOperationException) {
                                    // value doesn't exist and can't be created
                                    throw new RegistryBackupFailedException("The value " + activeRegistryBackupElement.ValueName + " cannot be created.");
                                } catch (ArgumentException) {
                                    // value doesn't exist and can't be created
                                    throw new RegistryBackupFailedException("The value " + activeRegistryBackupElement.ValueName + " cannot be created.");
                                } catch (SecurityException) {
                                    // value exists and we can't modify it
                                    throw new TaskRequiresElevationException("Setting the value " + activeRegistryBackupElement.ValueName + " requires elevation.");
                                }
                            } else {
                                try {
                                    // value didn't exist before
                                    DeleteValueInRegistryView(GetUserKeyValueName(activeRegistryBackupElement.KeyName), activeRegistryBackupElement.ValueName, registryView);
                                } catch (SecurityException) {
                                    // value exists and we can't modify it
                                    throw new TaskRequiresElevationException("Deleting the value " + activeRegistryBackupElement.ValueName + " requires elevation.");
                                }
                            }
                            break;
                        }

                        // save as we go along to handle failure
                        activeModificationsElement.RegistryBackups.RemoveAt(0);
                        SetFlashpointSecurePlayerSection(Name);
                    }

                    ProgressManager.CurrentGoal.Steps++;
                }
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }

        private void GotValue(RegistryTraceData registryTraceData) {
            if (registryTraceData.ProcessID == Process.GetCurrentProcess().Id || registryTraceData.ProcessID == -1) {
                if (ImportPaused) {
                    if (registryTraceData.ValueName.ToUpper() == IMPORT_RESUME) {
                        ResumeImport();
                        // hold here until after the control has installed
                        // that way we can recieve registry messages as they come in
                        // with reassurance the control has installed already
                        // therefore, key names will be redirected properly
                        resumeEventWaitHandle.WaitOne();
                    }
                } else {
                    if (registryTraceData.ValueName.ToUpper() == IMPORT_PAUSE) {
                        PauseImport();
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
            ModificationsElement modificationsElement = null;

            // must catch exceptions here for thread safety
            try {
                modificationsElement = GetModificationsElement(false, Name);
            } catch (System.Configuration.ConfigurationErrorsException) {
                return;
            }

            if (modificationsElement == null) {
                return;
            }

            RegistryBackupElement registryBackupElement = new RegistryBackupElement {
                KeyName = registryTraceData.KeyName,
                ValueName = registryTraceData.ValueName
            };

            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;
            object value = null;
            RegistryView registryView = RegistryView.Registry32;

            if (modificationsElement.RegistryBackups.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                registryView = RegistryView.Registry64;
            }

            if (safeKeyHandle == 0) {
                // we don't need to queue it, we can just add the key right here
                registryBackupElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(registryBackupElement.KeyName), modificationsElement.RegistryBackups.BinaryType);
                
                registryBackupElement.ValueKind = GetValueKindInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView);
                value = null;

                try {
                    value = AddVariablesToLengthenedValue(LengthenValue(GetValueInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView), fullPath));
                } catch (ArgumentException) {
                    // value doesn't exist
                    value = null;
                } catch (SecurityException) {
                    // value exists but we can't get it
                    // this shouldn't happen because this task requires elevation
                    value = "";
                }
                
                if (value == null) {
                    if (String.IsNullOrEmpty(registryBackupElement.ValueName) && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryBackupElement.KeyName, registryView))) {
                        registryBackupElement.Type = TYPE.KEY;
                    } else {
                        registryBackupElement.Type = TYPE.VALUE;
                    }

                    if (registryBackupElement.Type == TYPE.VALUE) {
                        ModificationRemoved(registryTraceData);
                        return;
                    }
                } else {
                    registryBackupElement.Type = TYPE.VALUE;
                    registryBackupElement.Value = value.ToString();
                }

                modificationsElement.RegistryBackups.Set(registryBackupElement);
                SetFlashpointSecurePlayerSection(Name);
                return;
            }

            // need to deal with KCB
            // well, we already know the base key name from before, so we can wrap this up now
            if (kcbModificationKeyNames.ContainsKey(safeKeyHandle)) {
                registryBackupElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(kcbModificationKeyNames[safeKeyHandle] + "\\" + registryBackupElement.KeyName), modificationsElement.RegistryBackups.BinaryType);

                registryBackupElement.ValueKind = GetValueKindInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView);
                value = null;

                try {
                    value = AddVariablesToLengthenedValue(LengthenValue(GetValueInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView), fullPath));
                } catch (ArgumentException) {
                } catch (SecurityException) {
                    // we have permission to access the key at this point so this must not be important
                }

                if (value == null) {
                    // if not just the value, but the entire key, is deleted, treat this as a key type
                    if (String.IsNullOrEmpty(registryBackupElement.ValueName) && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryBackupElement.KeyName, registryView))) {
                        registryBackupElement.Type = TYPE.KEY;
                    } else {
                        registryBackupElement.Type = TYPE.VALUE;
                    }

                    if (registryBackupElement.Type == TYPE.VALUE) {
                        ModificationRemoved(registryTraceData);
                        return;
                    }
                } else {
                    registryBackupElement.Type = TYPE.VALUE;
                    registryBackupElement.Value = value.ToString();
                }

                modificationsElement.RegistryBackups.Set(registryBackupElement);
                SetFlashpointSecurePlayerSection(Name);
                return;
            }

            // worst case scenario: now we need to watch for when we get info on that handle
            if (!modificationsQueue.ContainsKey(safeKeyHandle)) {
                // create queue if does not exist (might be multiple keys waiting on it)
                modificationsQueue[safeKeyHandle] = new SortedList<DateTime, RegistryBackupElement>();
            }

            // add key to the queue for that handle
            modificationsQueue[safeKeyHandle][registryTraceData.TimeStamp] = registryBackupElement;
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

            ModificationsElement modificationsElement = null;

            // must catch exceptions here for thread safety
            try {
                modificationsElement = GetModificationsElement(false, Name);
            } catch (System.Configuration.ConfigurationErrorsException) {
                return;
            }

            if (modificationsElement == null) {
                return;
            }

            // create filler element to get name
            RegistryBackupElement registryBackupElement = new RegistryBackupElement {
                KeyName = registryTraceData.KeyName,
                ValueName = registryTraceData.ValueName
            };

            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x00000000FFFFFFFF;

            if (safeKeyHandle == 0) {
                registryBackupElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(registryBackupElement.KeyName), modificationsElement.RegistryBackups.BinaryType);

                // key was deleted - don't need to find its value, just enough info for the name
                modificationsElement.RegistryBackups.Remove(registryBackupElement.Name);
                //SetModificationsElement(modificationsElement, Name);
                return;
            }

            if (kcbModificationKeyNames.ContainsKey(safeKeyHandle)) {
                // we have info from the handle already to get the name
                registryBackupElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(kcbModificationKeyNames[safeKeyHandle] + "\\" + registryBackupElement.KeyName), modificationsElement.RegistryBackups.BinaryType);

                modificationsElement.RegistryBackups.Remove(registryBackupElement.Name);
                //SetModificationsElement(modificationsElement, Name);
                return;
            }

            // worst case scenario: now we need to watch for when we get info on that handle
            if (!modificationsQueue.ContainsKey(safeKeyHandle)) {
                // create queue if does not exist (might be multiple keys waiting on it)
                modificationsQueue[safeKeyHandle] = new SortedList<DateTime, RegistryBackupElement>();
            }

            // TODO: how do we handle this for deletion? (see also KCBStopped)
            modificationsQueue[safeKeyHandle][registryTraceData.TimeStamp] = registryBackupElement;
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

            ModificationsElement modificationsElement = null;

            try {
                modificationsElement = GetModificationsElement(false, Name);
            } catch (System.Configuration.ConfigurationErrorsException) {
                return;
            }

            if (modificationsElement == null) {
                return;
            }

            // it's stopped, remove it from the list of active key names
            ulong safeKeyHandle = registryTraceData.KeyHandle & 0x0000000FFFFFFFF;
            kcbModificationKeyNames.Remove(safeKeyHandle);

            // we'll be finding these in a second
            KeyValuePair<DateTime, RegistryBackupElement> queuedModification;
            RegistryBackupElement registryBackupElement;
            object value = null;

            RegistryView registryView = RegistryView.Registry32;

            if (modificationsElement.RegistryBackups.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY) {
                registryView = RegistryView.Registry64;
            }

            // we want to take care of any queued registry timeline events
            // an event entails the date and time of the registry modification
            if (modificationsQueue.ContainsKey(safeKeyHandle)) {
                while (modificationsQueue[safeKeyHandle].Any()) {
                    // get the first event
                    queuedModification = modificationsQueue[safeKeyHandle].First();

                    // add its BaseKeyName
                    registryBackupElement = queuedModification.Value;
                    registryBackupElement.KeyName = GetRedirectedKeyValueName(GetKeyValueNameFromKernelRegistryString(registryTraceData.KeyName + "\\" + registryBackupElement.KeyName), modificationsElement.RegistryBackups.BinaryType);
                    registryBackupElement.ValueKind = GetValueKindInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView);
                    value = null;

                    // value
                    try {
                        value = AddVariablesToLengthenedValue(LengthenValue(GetValueInRegistryView(registryBackupElement.KeyName, registryBackupElement.ValueName, registryView), fullPath));
                    } catch (ArgumentException) {
                        // value doesn't exist
                        value = null;
                    } catch (SecurityException) {
                        // value exists but we can't get it
                        value = "";
                    }

                    // TODO: is this the best way to handle deletion?
                    // move it into the normal registry modifications
                    if (value == null) {
                        if (String.IsNullOrEmpty(registryBackupElement.ValueName) && String.IsNullOrEmpty(TestKeyDeletedInRegistryView(registryBackupElement.KeyName, registryView))) {
                            registryBackupElement.Type = TYPE.KEY;
                            modificationsElement.RegistryBackups.Set(registryBackupElement);
                        } else {
                            registryBackupElement.Type = TYPE.VALUE;
                            modificationsElement.RegistryBackups.Remove(registryBackupElement.Name);
                        }
                    } else {
                        registryBackupElement.Type = TYPE.VALUE;
                        registryBackupElement.Value = value.ToString();
                        modificationsElement.RegistryBackups.Set(registryBackupElement);
                    }

                    // and out of the queue
                    // (the Key is the TimeStamp)
                    SetFlashpointSecurePlayerSection(Name);
                    modificationsQueue[safeKeyHandle].Remove(queuedModification.Key);
                }
            }
        }
    }
}