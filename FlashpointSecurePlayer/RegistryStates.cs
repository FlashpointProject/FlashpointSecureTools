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
        // http://social.msdn.microsoft.com/Forums/vstudio/en-US/0f3557ee-16bd-4a36-a4f3-00efbeae9b0d/app-config-multiple-sections-in-sectiongroup-with-same-name?forum=csharpgeneral
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

        private string fullPath = null;
        private PathNames pathNames = null;
        private EventWaitHandle resumeEventWaitHandle = new ManualResetEvent(false);
        private Dictionary<ulong, SortedList<DateTime, List<RegistryStateElement>>> queuedModifications = null;
        private Dictionary<ulong, string> kcbModificationKeyNames = null;
        private TraceEventSession kernelSession = null;

        // Windows XP, Windows Server 2003, Windows Vista and Windows Server 2008
        private readonly bool reflectionVersion = Environment.OSVersion.Version >= new Version(5, 1)
            && Environment.OSVersion.Version <= new Version(6, 0);

        private Dictionary<string, List<WOW64Key>> wow64KeyLists = null;

        private Dictionary<string, List<WOW64Key>> WOW64KeyLists {
            get {
                if (wow64KeyLists == null) {
                    if (reflectionVersion) {
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
            using (RegistryKey registryKey = CreateKeyInRegistryView(keyName, RegistryKeyPermissionCheck.Default, registryView)) {
                if (registryKey == null) {
                    // key is invalid
                    throw new ArgumentException("The key \"" + keyName + "\" is invalid.");
                }
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

        private object GetValueInRegistryView(string keyName, string valueName, RegistryView registryView) {
            using (RegistryKey registryKey = OpenKeyInRegistryView(keyName, false, registryView)) {
                if (registryKey == null) {
                    // key does not exist
                    return null;
                }

                object value = registryKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                RegistryValueKind? valueKind = null;

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
            using (RegistryKey registryKey = CreateKeyInRegistryView(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree, registryView)) {
                if (registryKey == null) {
                    // key is invalid
                    throw new ArgumentException("The key \"" + keyName + "\" is invalid.");
                }

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
                
                registryKey.SetValue(valueName, value, valueKind);
            }
        }

        private void DeleteValueInRegistryView(string keyName, string valueName, RegistryView registryView) {
            using (RegistryKey registryKey = OpenKeyInRegistryView(keyName, true, registryView)) {
                if (registryKey == null) {
                    // key does not exist (good!)
                    return;
                }

                registryKey.DeleteValue(valueName);
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

        // Windows XP and Windows Server 2003
        private readonly bool notSharedVistaVersion = Environment.OSVersion.Version >= new Version(5, 1)
            && Environment.OSVersion.Version < new Version(6, 0);
        
        // http://docs.microsoft.com/en-us/windows/win32/winprog64/shared-registry-keys#redirected-shared-and-reflected-keys-under-wow64
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
            
            // make these keys uppercase using a regex
            keyValueName = Regex.Replace(keyValueName, "\\\\WOW6432NODE\\\\", "\\WOW6432NODE\\", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            keyValueName = Regex.Replace(keyValueName, "\\\\WOW64AANODE\\\\", "\\WOW64AANODE\\", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            // remove Wow6432Node and WowAA32Node after affected keys
            List<string> keyValueNameSplit = keyValueName.Split(new string[] { "\\WOW6432NODE\\", "\\WOW64AANODE\\" }, StringSplitOptions.None).ToList();

            if (keyValueNameSplit.Count < 2) {
                return keyValueName;
            }

            string wow64KeyUpper = keyValueNameSplit[0].ToUpperInvariant();
            string wow64KeyName = keyValueNameSplit[1] + "\\";

            if (WOW64KeyLists.ContainsKey(wow64KeyUpper)) {
                List<WOW64Key> wow64KeyList = WOW64KeyLists[wow64KeyUpper];
                WOW64Key.EFFECT effect = WOW64Key.EFFECT.SHARED;
                List<string> effectExceptionValueNames = new List<string>();
                bool removeWOW64Subkey = false;

                for (int i = 0; i < wow64KeyList.Count; i++) {
                    if (wow64KeyName.StartsWith(wow64KeyList[i].Name + "\\", StringComparison.OrdinalIgnoreCase)) {
                        effect = wow64KeyList[i].Effect;
                        effectExceptionValueNames = wow64KeyList[i].EffectExceptionValueNames;

                        if (effect == WOW64Key.EFFECT.SHARED_VISTA) {
                            effect = notSharedVistaVersion ? WOW64Key.EFFECT.REDIRECTED : WOW64Key.EFFECT.SHARED;
                        }

                        switch (effect) {
                            case WOW64Key.EFFECT.REDIRECTED:
                            removeWOW64Subkey = true;
                            break;
                            case WOW64Key.EFFECT.REDIRECTED_EXCEPTION_VALUE_IS_DEFINED:
                            // check exceptions for value defined in registry
                            object effectExceptionValue = null;

                            for (int j = 0; j < effectExceptionValueNames.Count; j++) {
                                try {
                                    effectExceptionValue = GetValueInRegistryView(
                                        GetKeyValueNameFromKernelRegistryString(
                                            String.Join(
                                                "\\",
                                                keyValueNameSplit.GetRange(0, i)
                                            )
                                        ),
                                    
                                        effectExceptionValueNames[j],
                                        RegistryView.Registry64
                                    );
                                } catch (SecurityException ex) {
                                    // value exists but we can't get it
                                    LogExceptionToLauncher(ex);
                                    removeWOW64Subkey = true;
                                } catch (UnauthorizedAccessException ex) {
                                    // value exists but we can't get it
                                    LogExceptionToLauncher(ex);
                                    removeWOW64Subkey = true;
                                } catch {
                                    // value doesn't exist
                                    effectExceptionValue = null;
                                }

                            // just checking it exists
                            if (effectExceptionValue != null) {
                                    removeWOW64Subkey = true;
                                }
                            }
                            break;
                        }

                        if (removeWOW64Subkey) {
                            // key after WOW64XXNODE will shift into current position
                            // therefore, after the next loop...
                            // one is added to i so we effectively move two keys
                            // so we will be looking at the next potential WOW64XXNODE candidate
                            keyValueName = String.Join("\\", keyValueNameSplit);
                        }
                        break;
                    }
                }
            }
            return keyValueName;
        }

        private bool CompareKeys(RegistryView registryView, RegistryStateElement registryStateElement, RegistryStateElement activeRegistryStateElement, string activeCurrentUser = null, bool activeAdministrator = true) {
            if (registryStateElement == null || activeRegistryStateElement == null) {
                return true;
            }

            if (String.IsNullOrEmpty(activeRegistryStateElement._Deleted)) {
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

        private bool CompareValues(object value, RegistryView registryView, RegistryStateElement registryStateElement, RegistryStateElement activeRegistryStateElement, string activeCurrentUser = null, bool activeAdministrator = true) {
            // caller needs to decide what to do if value is null
            if (!(value is string comparableValue)) {
                throw new ArgumentNullException("The comparableValueString is null.");
            }

            if (registryStateElement == null) {
                throw new ArgumentNullException("The registryStateElement is null.");
            }

            RegistryValueKind? valueKind = null;

            try {
                valueKind = GetValueKindInRegistryView(
                    GetUserKeyValueName(
                        registryStateElement.KeyName,
                        activeCurrentUser,
                        activeAdministrator
                    ),

                    registryStateElement.ValueName,
                    registryView
                );
            } catch (SecurityException ex) {
                // value exists but we can't get it
                LogExceptionToLauncher(ex);
                throw new TaskRequiresElevationException("Accessing the value \"" + registryStateElement.ValueName + "\" in key \"" + registryStateElement.KeyName + "\" requires elevation.");
            } catch (UnauthorizedAccessException ex) {
                // value exists but we can't get it
                LogExceptionToLauncher(ex);
                throw new TaskRequiresElevationException("Accessing the value \"" + registryStateElement.ValueName + "\" in key \"" + registryStateElement.KeyName + "\" requires elevation.");
            } catch {
                // value doesn't exist
                valueKind = null;
            }

            string comparableRegistryStateElementValue = registryStateElement.Value;

            // if value kind is the same as current value kind
            if (valueKind == registryStateElement.ValueKind) {
                if (activeRegistryStateElement != null) {
                    // account for expanded values
                    comparableRegistryStateElementValue = String.IsNullOrEmpty(activeRegistryStateElement._ValueExpanded)
                        ? comparableRegistryStateElementValue
                        : activeRegistryStateElement._ValueExpanded;
                }

                // check value matches current value/current expanded value
                if (comparableValue.Equals(comparableRegistryStateElementValue, StringComparison.Ordinal)) {
                    return true;
                }

                // for ActiveX: check if it matches as a path
                try {
                    if (ComparePaths(comparableValue, comparableRegistryStateElementValue)) {
                        return true;
                    }
                } catch {
                    // fail silently
                }
            }

            if (activeRegistryStateElement != null) {
                // if value existed before
                if (!String.IsNullOrEmpty(activeRegistryStateElement._Deleted)) {
                    // value kind before also matters
                    if (valueKind == activeRegistryStateElement.ValueKind) {
                        // get value before
                        comparableRegistryStateElementValue = activeRegistryStateElement.Value;

                        // check value matches
                        if (comparableValue.Equals(comparableRegistryStateElementValue, StringComparison.Ordinal)) {
                            return true;
                        }

                        // check if it matches as a path
                        try {
                            if (ComparePaths(comparableValue, comparableRegistryStateElementValue)) {
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
                        Thread.Sleep(1000);
                    } else {
                        await Task.Delay(1000).ConfigureAwait(true);
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

                        try {
                            valueKind = GetValueKindInRegistryView(keyName, registryStateElement.ValueName, registryView);
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
                            valueKind = null;
                        }

                        activeRegistryStateElement = new RegistryStateElement {
                            Type = registryStateElement.Type,
                            KeyName = registryStateElement.KeyName,
                            ValueName = registryStateElement.ValueName,
                            ValueKind = valueKind,
                            _Deleted = String.Empty
                        };

                        if (registryStateElement.Type == TYPE.KEY) {
                            // we create a key
                            activeRegistryStateElement.Type = TYPE.KEY;

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

                                if (valueExpanded != registryStateElement.Value) {
                                    activeRegistryStateElement._ValueExpanded = valueExpanded;
                                }
                            } catch {
                                // fail silently
                            }

                            try {
                                value = GetValueInRegistryView(keyName, registryStateElement.ValueName, registryView) as string;
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
                                activeRegistryStateElement.Value = value;
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
                            throw new ConfigurationErrorsException("The Registry State Element (" + i + ") is null.");
                        }

                        activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(registryStateElement.Name) as RegistryStateElement;

                        if (activeRegistryStateElement == null) {
                            throw new ConfigurationErrorsException("The Active Registry State Element \"" + registryStateElement.Name + "\" is null.");
                        }

                        keyName = GetUserKeyValueName(registryStateElement.KeyName);

                        // we don't delete existing keys/values, since the program just won't use deleted keys/values
                        // therefore, _Deleted is ignored on all but the active registry state
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
                            try {
                                SetValueInRegistryView(
                                    keyName,
                                    registryStateElement.ValueName,
                                    String.IsNullOrEmpty(activeRegistryStateElement._ValueExpanded)
                                    ? registryStateElement.Value
                                    : activeRegistryStateElement._ValueExpanded,
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
                            break;
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }
                } catch {
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
                string value = null;
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
                                        string keyName = GetUserKeyValueName(registryStateElement.KeyName, activeCurrentUser, activeAdministrator);

                                        try {
                                            value = GetValueInRegistryView(keyName, registryStateElement.ValueName, registryView) as string;
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
                                            && !String.IsNullOrEmpty(activeRegistryStateElement._Deleted))) {
                                            // we previously created a value
                                            // the value, (and potentially the key it belonged to) did not exist before
                                            // the value may or may not exist now
                                            // if the value still exists, we need to check it's not edited
                                            if (value != null) {
                                                // value still exists
                                                if (!CompareValues(
                                                    value,
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
                                            && String.IsNullOrEmpty(activeRegistryStateElement._Deleted)) {
                                            // we previously edited a value that existed before
                                            // we need to check it still exists in one of the two valid states
                                            if (value == null) {
                                                clear = true;
                                            } else {
                                                if (!CompareValues(
                                                    value,
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

                    while (activeModificationsElement.RegistryStates.Count > 0) {
                        try {
                            activeRegistryStateElement = activeModificationsElement.RegistryStates.Get(0) as RegistryStateElement;

                            // how can it be deleted already?? just paranoia
                            if (activeRegistryStateElement != null) {
                                registryStateElement = modificationsElement.RegistryStates.Get(activeRegistryStateElement.Name) as RegistryStateElement;

                                if (registryStateElement != null) {
                                    switch (activeRegistryStateElement.Type) {
                                        case TYPE.KEY:
                                        if (!String.IsNullOrEmpty(activeRegistryStateElement._Deleted)
                                            || modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                            try {
                                                // key didn't exist before
                                                DeleteKeyInRegistryView(
                                                    GetUserKeyValueName(
                                                        activeRegistryStateElement._Deleted,
                                                        activeCurrentUser,
                                                        activeAdministrator
                                                    ),

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
                                        break;
                                        case TYPE.VALUE:
                                        if (String.IsNullOrEmpty(activeRegistryStateElement._Deleted)
                                            && modificationsRevertMethod != MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                            string keyName = GetUserKeyValueName(activeRegistryStateElement.KeyName, activeCurrentUser, activeAdministrator);

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
                                                throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" must be Base64.");
                                            } catch (InvalidOperationException ex) {
                                                // value marked for deletion
                                                LogExceptionToLauncher(ex);
                                                throw new InvalidRegistryStateException("The value \"" + registryStateElement.ValueName + "\" in key \"" + keyName + "\" is marked for deletion.");
                                            } catch (Exception ex) {
                                                // value doesn't exist and can't be created
                                                LogExceptionToLauncher(ex);
                                                throw new InvalidRegistryStateException("The value \"" + activeRegistryStateElement.ValueName + "\" in key \"" + keyName + "\" could not be set.");
                                            }
                                        } else {
                                            try {
                                                // value didn't exist before
                                                DeleteValueInRegistryView(
                                                    GetUserKeyValueName(
                                                        activeRegistryStateElement.KeyName,
                                                        activeCurrentUser,
                                                        activeAdministrator
                                                    ),
                                                    
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
                                        break;
                                    }
                                    
                                    activeModificationsElement.RegistryStates.RemoveAt(0);
                                }
                            }
                        } catch (TaskRequiresElevationException ex) {
                            taskRequiresElevationException = ex;
                        } catch (Exception ex) {
                            exception = ex;
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }

                    activeModificationsElement.RegistryStates.BinaryType = BINARY_TYPE.SCS_64BIT_BINARY;
                    activeModificationsElement.RegistryStates._Administrator = false;
                    activeModificationsElement.RegistryStates._CurrentUser = String.Empty;
                    SetFlashpointSecurePlayerSection(TemplateName);

                    if (taskRequiresElevationException != null) {
                        throw taskRequiresElevationException;
                    }

                    if (exception != null) {
                        throw exception;
                    }
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
            RegistryView registryView = modificationsElement.RegistryStates.BinaryType == BINARY_TYPE.SCS_64BIT_BINARY ? RegistryView.Registry64 : RegistryView.Registry32;

            if (safeKeyHandle == 0) {
                // we don't need to queue it, we can just add the key right here
                registryStateElement.KeyName = GetRedirectedKeyValueName(
                    GetKeyValueNameFromKernelRegistryString(registryStateElement.KeyName),
                    modificationsElement.RegistryStates.BinaryType
                );

                try {
                    registryStateElement.ValueKind = GetValueKindInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView);
                } catch {
                    // value doesn't exist
                    registryStateElement.ValueKind = null;
                }
                
                try {
                    value = ReplaceStartupPathEnvironmentVariable(
                        LengthenValue(
                            GetValueInRegistryView(
                                registryStateElement.KeyName,
                                registryStateElement.ValueName,
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

                if (kcbModificationKeyName != null) {
                    registryStateElement.KeyName = GetRedirectedKeyValueName(
                        GetKeyValueNameFromKernelRegistryString(kcbModificationKeyName + "\\" + registryStateElement.KeyName),
                        modificationsElement.RegistryStates.BinaryType
                    );

                    try {
                        registryStateElement.ValueKind = GetValueKindInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView);
                    } catch {
                        // value doesn't exist
                        registryStateElement.ValueKind = null;
                    }

                    try {
                        value = ReplaceStartupPathEnvironmentVariable(
                            LengthenValue(
                                GetValueInRegistryView(
                                    registryStateElement.KeyName,
                                    registryStateElement.ValueName,
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

                if (kcbModificationKeyName != null) {
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

                    try {
                        registryStateElement.ValueKind = GetValueKindInRegistryView(registryStateElement.KeyName, registryStateElement.ValueName, registryView);
                    } catch {
                        // value doesn't exist
                        registryStateElement.ValueKind = null;
                    }

                    try {
                        value = ReplaceStartupPathEnvironmentVariable(
                            LengthenValue(
                                GetValueInRegistryView(
                                    registryStateElement.KeyName,
                                    registryStateElement.ValueName,
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