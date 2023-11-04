using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    // http://blogs.msdn.microsoft.com/jpsanders/2011/04/26/how-to-set-the-proxy-for-the-webbrowser-control-in-net/
    public static class FlashpointProxy {
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr InternetOpen(string lpszAgent, uint dwAccessType, IntPtr lpszProxy, IntPtr lpszProxyBypass, uint dwFlags);

        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetCloseHandle(IntPtr hInternet);

        private enum INTERNET_OPTION : uint {
            INTERNET_OPTION_PER_CONNECTION_OPTION = 75,
            INTERNET_OPTION_REFRESH = 37,
            INTERNET_OPTION_SETTINGS_CHANGED = 39
        }

        private enum INTERNET_PER_CONN_OPTION_OPTION : uint {
            INTERNET_PER_CONN_FLAGS = 1,
            INTERNET_PER_CONN_PROXY_SERVER = 2,
            INTERNET_PER_CONN_PROXY_BYPASS = 3,
            INTERNET_PER_CONN_AUTOCONFIG_URL = 4,
            INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5,
            INTERNET_PER_CONN_AUTOCONFIG_SECONDARY_URL = 6,
            INTERNET_PER_CONN_AUTOCONFIG_RELOAD_DELAY_MINS = 7,
            INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_TIME = 8,
            INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_URL = 9,
            INTERNET_PER_CONN_FLAGS_UI = 10
        }

        [Flags]
        private enum INTERNET_PER_CONN_FLAGS_VALUEFlags : uint {
            PROXY_TYPE_DIRECT = 0x00000001,
            PROXY_TYPE_PROXY = 0x00000002,
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,
            PROXY_TYPE_AUTO_DETECT = 0x00000008
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct INTERNET_PER_CONN_OPTION_VALUE {
            [FieldOffset(0)]
            public uint dwValue;
            [FieldOffset(0)]
            public IntPtr pszValue;
            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INTERNET_PER_CONN_OPTION {
            public INTERNET_PER_CONN_OPTION_OPTION dwOption;
            public INTERNET_PER_CONN_OPTION_VALUE Value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INTERNET_PER_CONN_OPTION_LIST {
            public uint dwSize;
            public IntPtr pszConnection;
            public uint dwOptionCount;
            public uint dwOptionError;
            public IntPtr pOptions;
        }

        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetQueryOption(IntPtr hInternet, INTERNET_OPTION dwOption, ref INTERNET_PER_CONN_OPTION_LIST lpBuffer, ref uint lpdwBufferLength);

        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetSetOption(IntPtr hInternet, INTERNET_OPTION dwOption, IntPtr lpBuffer, uint dwBufferLength);
        
        // in enabling the proxy we need to set the Agent to use
        private const string AGENT = "Flashpoint Proxy";

        private const uint INTERNET_OPEN_TYPE_DIRECT = 1;

        private const bool FP_PROXY_DEFAULT = true;
        private const int FP_PROXY_PORT_DEFAULT = 22500;

        private static bool Proxy { get; set; } = FP_PROXY_DEFAULT;
        private static int Port { get; set; } = FP_PROXY_PORT_DEFAULT;

        private static bool gotPreferences = false;

        private static void GetSystemProxy(ref INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList, ref INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions) {
            if (internetPerConnOptionListOptions.Length < 2) {
                throw new ArgumentException("The Internet Per Connection Option List Options must not have a Length of less than two.");
            }

            // set flags
            internetPerConnOptionListOptions[0] = new INTERNET_PER_CONN_OPTION {
                dwOption = INTERNET_PER_CONN_OPTION_OPTION.INTERNET_PER_CONN_FLAGS
            };

            // set proxy name
            internetPerConnOptionListOptions[1] = new INTERNET_PER_CONN_OPTION {
                dwOption = INTERNET_PER_CONN_OPTION_OPTION.INTERNET_PER_CONN_PROXY_SERVER
            };

            // allocate a block of memory of the options
            internetPerConnOptionList.pOptions = Marshal.AllocCoTaskMem(Marshal.SizeOf(internetPerConnOptionListOptions[0]) + Marshal.SizeOf(internetPerConnOptionListOptions[1]));

            try {
                IntPtr internetPerConnOptionPointer = internetPerConnOptionList.pOptions;

                // marshal data from a managed object to unmanaged memory
                for (int i = 0; i < internetPerConnOptionListOptions.Length; i++) {
                    Marshal.StructureToPtr(internetPerConnOptionListOptions[i], internetPerConnOptionPointer, false);
                    internetPerConnOptionPointer = (IntPtr)((int)internetPerConnOptionPointer + Marshal.SizeOf(internetPerConnOptionListOptions[i]));
                }

                uint internetPerConnOptionListSize = (uint)Marshal.SizeOf(internetPerConnOptionList);

                // fill the internetPerConnOptionList structure
                internetPerConnOptionList.dwSize = internetPerConnOptionListSize;

                // NULL == LAN, otherwise connectoid name
                internetPerConnOptionList.pszConnection = IntPtr.Zero;

                // set two options
                internetPerConnOptionList.dwOptionCount = (uint)internetPerConnOptionListOptions.Length;
                internetPerConnOptionList.dwOptionError = 0;

                if (!InternetQueryOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, ref internetPerConnOptionList, ref internetPerConnOptionListSize)) {
                    throw new FlashpointProxyException("Could not query the Internet Options.");
                }
            } catch {
                Marshal.FreeCoTaskMem(internetPerConnOptionList.pOptions);
                throw;
            }
        }

        public const string FP_PROXY = nameof(FP_PROXY);
        public const string FP_PROXY_PORT = nameof(FP_PROXY_PORT);

        public const string FLASHPOINT_SECURE_PLAYER_PROXY = nameof(FLASHPOINT_SECURE_PLAYER_PROXY);
        public const string FLASHPOINT_SECURE_PLAYER_PROXY_PORT = nameof(FLASHPOINT_SECURE_PLAYER_PROXY_PORT);

        public static void GetPreferences(out bool proxy, out int port) {
            proxy = Proxy;
            port = Port;
            
            if (gotPreferences) {
                return;
            }

            FlashpointSecurePlayerSection.FlashpointProxyElement flashpointProxyElement = null;

            try {
                FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(false, ACTIVE_EXE_CONFIGURATION_NAME);
                flashpointProxyElement = flashpointSecurePlayerSection.FlashpointProxy;
            } catch {
                // fail silently
            }

            bool? proxyFilePreference = null;
            int? portFilePreference = null;

            if (flashpointProxyElement != null
                && flashpointProxyElement.ElementInformation.IsPresent) {
                proxyFilePreference = flashpointProxyElement.Proxy;
                portFilePreference = flashpointProxyElement.Port;
            }

            string environmentVariablePreferenceString = null;
            long preference = PREFERENCE_DEFAULT;

            // try getting from the proxy element
            // if that fails, try from the environment variables
            // if that fails, use the default
            if (proxyFilePreference == null) {
                environmentVariablePreferenceString = GetEnvironmentVariablePreference(new List<string> { FLASHPOINT_SECURE_PLAYER_PROXY, FP_PROXY });

                if (long.TryParse(environmentVariablePreferenceString, out preference)
                    && preference != PREFERENCE_DEFAULT) {
                    proxy = preference != 0;
                }
            } else {
                proxy = proxyFilePreference.GetValueOrDefault();
            }

            if (portFilePreference == null) {
                environmentVariablePreferenceString = GetEnvironmentVariablePreference(new List<string> { FLASHPOINT_SECURE_PLAYER_PROXY_PORT, FP_PROXY_PORT });

                if (long.TryParse(environmentVariablePreferenceString, out preference)
                    && preference != PREFERENCE_DEFAULT) {
                    port = (int)preference;
                }
            } else {
                port = portFilePreference.GetValueOrDefault();
            }

            Proxy = proxy;
            Port = port;

            gotPreferences = true;
        }

        public static void Enable() {
            GetPreferences(out bool proxy, out int port);

            if (!proxy) {
                return;
            }

            string proxyServer = "http=127.0.0.1:" + port + ";https=127.0.0.1:" + port + ";ftp=127.0.0.1:" + port;

            IntPtr internetHandle = InternetOpen(AGENT, INTERNET_OPEN_TYPE_DIRECT, IntPtr.Zero, IntPtr.Zero, 0);

            if (internetHandle == IntPtr.Zero) {
                throw new FlashpointProxyException("Could not open the Internet Handle.");
            }

            try {
                // initialize a INTERNET_PER_CONN_OPTION_LIST instance
                INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList = new INTERNET_PER_CONN_OPTION_LIST();
                uint internetPerConnOptionListSize = (uint)Marshal.SizeOf(internetPerConnOptionList);

                // create two options
                INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions = new INTERNET_PER_CONN_OPTION[2];

                // set PROXY flags
                internetPerConnOptionListOptions[0] = new INTERNET_PER_CONN_OPTION {
                    dwOption = INTERNET_PER_CONN_OPTION_OPTION.INTERNET_PER_CONN_FLAGS
                };

                internetPerConnOptionListOptions[0].Value.dwValue = (uint)INTERNET_PER_CONN_FLAGS_VALUEFlags.PROXY_TYPE_PROXY;

                // set proxy name
                internetPerConnOptionListOptions[1] = new INTERNET_PER_CONN_OPTION {
                    dwOption = INTERNET_PER_CONN_OPTION_OPTION.INTERNET_PER_CONN_PROXY_SERVER
                };

                internetPerConnOptionListOptions[1].Value.pszValue = Marshal.StringToHGlobalAnsi(proxyServer);

                // allocate memory for the INTERNET_PER_CONN_OPTION_LIST Options
                internetPerConnOptionList.pOptions = Marshal.AllocCoTaskMem(Marshal.SizeOf(internetPerConnOptionListOptions[0]) + Marshal.SizeOf(internetPerConnOptionListOptions[1]));

                try {
                    IntPtr internetPerConnOptionListOptionPointer = internetPerConnOptionList.pOptions;

                    // marshal data from a managed object to unmanaged memory
                    for (int i = 0; i < internetPerConnOptionListOptions.Length; i++) {
                        Marshal.StructureToPtr(internetPerConnOptionListOptions[i], internetPerConnOptionListOptionPointer, false);
                        internetPerConnOptionListOptionPointer = (IntPtr)((long)internetPerConnOptionListOptionPointer + Marshal.SizeOf(internetPerConnOptionListOptions[i]));
                    }

                    // fill the internetPerConnOptionList structure
                    internetPerConnOptionList.dwSize = (uint)Marshal.SizeOf(internetPerConnOptionList);

                    // NULL == LAN, otherwise connectoid name
                    internetPerConnOptionList.pszConnection = IntPtr.Zero;

                    // set two options
                    internetPerConnOptionList.dwOptionCount = (uint)internetPerConnOptionListOptions.Length;
                    internetPerConnOptionList.dwOptionError = 0;

                    // allocate memory for the INTERNET_PER_CONN_OPTION_LIST
                    IntPtr internetPerConnOptionListPointer = Marshal.AllocCoTaskMem((int)internetPerConnOptionListSize);

                    try {
                        // marshal data from a managed object to unmanaged memory
                        Marshal.StructureToPtr(internetPerConnOptionList, internetPerConnOptionListPointer, true);

                        // set the options on the connection
                        if (!InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, internetPerConnOptionListPointer, internetPerConnOptionListSize)) {
                            throw new FlashpointProxyException("Could not set the Internet Options.");
                        }
                    } finally {
                        // free the allocated memory
                        Marshal.FreeCoTaskMem(internetPerConnOptionListPointer);
                    }
                } finally {
                    Marshal.FreeCoTaskMem(internetPerConnOptionList.pOptions);
                }
            } finally {
                if (!InternetCloseHandle(internetHandle)) {
                    throw new FlashpointProxyException("Could not close the Internet Handle.");
                }
            }
        }
        
        public static void Disable() {
            IntPtr internetHandle = InternetOpen(AGENT, INTERNET_OPEN_TYPE_DIRECT, IntPtr.Zero, IntPtr.Zero, 0);

            if (internetHandle == IntPtr.Zero) {
                throw new FlashpointProxyException("Could not open the Internet Handle.");
            }

            try {
                // initialize a INTERNET_PER_CONN_OPTION_LIST instance
                INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList = new INTERNET_PER_CONN_OPTION_LIST();
                uint internetPerConnOptionListSize = (uint)Marshal.SizeOf(internetPerConnOptionList);

                // create two options
                INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions = new INTERNET_PER_CONN_OPTION[2];

                GetSystemProxy(ref internetPerConnOptionList, ref internetPerConnOptionListOptions);

                try {
                    // allocate memory
                    IntPtr internetPerConnOptionListPointer = Marshal.AllocCoTaskMem((int)internetPerConnOptionListSize);

                    try {
                        // convert structure to IntPtr
                        Marshal.StructureToPtr(internetPerConnOptionList, internetPerConnOptionListPointer, true);

                        // set internet options
                        bool result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, internetPerConnOptionListPointer, internetPerConnOptionListSize);

                        // notify the system that the registry settings have been changed and cause
                        // the proxy data to be reread from the registry for a handle
                        if (result) {
                            result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);

                            if (result) {
                                result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                            }
                        }

                        if (!result) {
                            throw new FlashpointProxyException("Could not set the Internet Options.");
                        }
                    } finally {
                        // free the allocated memory
                        Marshal.FreeCoTaskMem(internetPerConnOptionListPointer);
                    }
                } finally {
                    Marshal.FreeCoTaskMem(internetPerConnOptionList.pOptions);
                }
            } finally {
                if (!InternetCloseHandle(internetHandle)) {
                    throw new FlashpointProxyException("Could not close the Internet Handle.");
                }
            }
        }
    }
}