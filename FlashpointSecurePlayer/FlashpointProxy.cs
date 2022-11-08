using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    // https://blogs.msdn.microsoft.com/jpsanders/2011/04/26/how-to-set-the-proxy-for-the-webbrowser-control-in-net/
    public static class FlashpointProxy {
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr InternetOpen(string lpszAgent, int dwAccessType, IntPtr lpszProxy, IntPtr lpszProxyBypass, int dwFlags);

        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetCloseHandle(IntPtr hInternet);

        [StructLayout(LayoutKind.Sequential)]
        private struct INTERNET_PER_CONN_OPTION_LIST {
            public int dwSize;
            public IntPtr pszConnection;
            public int dwOptionCount;
            public int dwOptionError;
            public IntPtr pOptions;
        }

        private enum INTERNET_OPTION {
            INTERNET_OPTION_PER_CONNECTION_OPTION = 75,
            INTERNET_OPTION_SETTINGS_CHANGED = 39,
            INTERNET_OPTION_REFRESH = 37

        }

        private enum INTERNET_PER_CONN_OptionEnum {
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

        private const int INTERNET_OPEN_TYPE_DIRECT = 1;
        private const int INTERNET_OPEN_TYPE_PRECONFIG = 0;

        private enum INTERNET_OPTION_PER_CONN_FLAGS {
            PROXY_TYPE_DIRECT = 0x00000001,
            PROXY_TYPE_PROXY = 0x00000002,
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,
            PROXY_TYPE_AUTO_DETECT = 0x00000008
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct INTERNET_PER_CONN_OPTION_OptionUnion {
            [FieldOffset(0)]
            public int dwValue;
            [FieldOffset(0)]
            public IntPtr pszValue;
            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INTERNET_PER_CONN_OPTION {
            public int dwOption;
            public INTERNET_PER_CONN_OPTION_OptionUnion Value;
        }
        
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetSetOption(IntPtr hInternet, INTERNET_OPTION dwOption, IntPtr lpBuffer, int dwBufferLength);
        
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetQueryOption(IntPtr hInternet, INTERNET_OPTION dwOption, ref INTERNET_PER_CONN_OPTION_LIST lpBuffer, ref int lpdwBufferLength);
        
        // in enabling the proxy we need to set the Agent to use
        private const string AGENT = "Flashpoint Proxy";
        
        private static void GetSystemProxy(ref INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList, ref INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions) {
            int internetPerConnOptionListSize = Marshal.SizeOf(internetPerConnOptionList);

            if (internetPerConnOptionListOptions.Length < 2) {
                throw new ArgumentException("The Internet Per Connection Option List Options must not have a Length of less than two.");
            }

            // set flags
            internetPerConnOptionListOptions[0] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[0].dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS;

            // set proxy name
            internetPerConnOptionListOptions[1] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[1].dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER;

            // allocate a block of memory of the options
            internetPerConnOptionList.pOptions = Marshal.AllocCoTaskMem(Marshal.SizeOf(internetPerConnOptionListOptions[0]) + Marshal.SizeOf(internetPerConnOptionListOptions[1]));
            IntPtr internetPerConnOptionPointer = internetPerConnOptionList.pOptions;

            // marshal data from a managed object to unmanaged memory
            for (int i = 0;i < internetPerConnOptionListOptions.Length;i++) {
                Marshal.StructureToPtr(internetPerConnOptionListOptions[i], internetPerConnOptionPointer, false);
                internetPerConnOptionPointer = (IntPtr)((int)internetPerConnOptionPointer + Marshal.SizeOf(internetPerConnOptionListOptions[i]));
            }

            // fill the internetPerConnOptionList structure
            internetPerConnOptionList.dwSize = internetPerConnOptionListSize;

            // NULL == LAN, otherwise connectoid name
            internetPerConnOptionList.pszConnection = IntPtr.Zero;

            // set two options
            internetPerConnOptionList.dwOptionCount = internetPerConnOptionListOptions.Length;
            internetPerConnOptionList.dwOptionError = 0;

            // query internet options
            bool result = InternetQueryOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, ref internetPerConnOptionList, ref internetPerConnOptionListSize);

            if (!result) {
                throw new FlashpointProxyException("Could not query the Internet Options.");
            }
        }
        
        public static void Enable(string proxyServer) {
            IntPtr internetHandle = IntPtr.Zero;
            internetHandle = InternetOpen(AGENT, INTERNET_OPEN_TYPE_DIRECT, IntPtr.Zero, IntPtr.Zero, 0);

            if (internetHandle == IntPtr.Zero) {
                throw new FlashpointProxyException("Could not open the Internet Handle.");
            }

            // initialize a INTERNET_PER_CONN_OPTION_LIST instance
            INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList = new INTERNET_PER_CONN_OPTION_LIST();
            int internetPerConnOptionListSize = Marshal.SizeOf(internetPerConnOptionList);

            // create two options
            INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions = new INTERNET_PER_CONN_OPTION[2];

            // set PROXY flags
            internetPerConnOptionListOptions[0] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[0].dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS;
            internetPerConnOptionListOptions[0].Value.dwValue = (int)INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_PROXY;

            // set proxy name
            internetPerConnOptionListOptions[1] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[1].dwOption = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER;
            internetPerConnOptionListOptions[1].Value.pszValue = Marshal.StringToHGlobalAnsi(proxyServer);

            // allocate memory for the INTERNET_PER_CONN_OPTION_LIST Options
            internetPerConnOptionList.pOptions = Marshal.AllocCoTaskMem(Marshal.SizeOf(internetPerConnOptionListOptions[0]) + Marshal.SizeOf(internetPerConnOptionListOptions[1]));
            IntPtr internetPerConnOptionListOptionPointer = internetPerConnOptionList.pOptions;

            // marshal data from a managed object to unmanaged memory
            for (int i = 0;i < internetPerConnOptionListOptions.Length;i++) {
                Marshal.StructureToPtr(internetPerConnOptionListOptions[i], internetPerConnOptionListOptionPointer, false);
                internetPerConnOptionListOptionPointer = (IntPtr)((long)internetPerConnOptionListOptionPointer + Marshal.SizeOf(internetPerConnOptionListOptions[i]));
            }

            // fill the internetPerConnOptionList structure
            internetPerConnOptionList.dwSize = Marshal.SizeOf(internetPerConnOptionList);

            // NULL == LAN, otherwise connectoid name
            internetPerConnOptionList.pszConnection = IntPtr.Zero;

            // set two options
            internetPerConnOptionList.dwOptionCount = internetPerConnOptionListOptions.Length;
            internetPerConnOptionList.dwOptionError = 0;

            // allocate memory for the INTERNET_PER_CONN_OPTION_LIST
            IntPtr internetPerConnOptionListPointer = Marshal.AllocCoTaskMem(internetPerConnOptionListSize);

            // marshal data from a managed object to unmanaged memory
            Marshal.StructureToPtr(internetPerConnOptionList, internetPerConnOptionListPointer, true);

            // set the options on the connection
            bool result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, internetPerConnOptionListPointer, internetPerConnOptionListSize);

            // free the allocated memory
            Marshal.FreeCoTaskMem(internetPerConnOptionList.pOptions);
            Marshal.FreeCoTaskMem(internetPerConnOptionListPointer);

            if (!InternetCloseHandle(internetHandle)) {
                throw new FlashpointProxyException("Could not close the Internet Handle.");
            }

            // throw an exception if this operation failed
            if (!result) {
                throw new FlashpointProxyException("Could not set the Internet Options.");
            }
        }
        
        public static void Disable() {
            IntPtr internetHandle = IntPtr.Zero;
            internetHandle = InternetOpen(AGENT, INTERNET_OPEN_TYPE_DIRECT, IntPtr.Zero, IntPtr.Zero, 0);

            if (internetHandle == IntPtr.Zero) {
                throw new FlashpointProxyException("Could not open the Internet Handle.");
            }

            // initialize a INTERNET_PER_CONN_OPTION_LIST instance
            INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList = new INTERNET_PER_CONN_OPTION_LIST();
            int internetPerConnOptionListSize = Marshal.SizeOf(internetPerConnOptionList);

            // create two options
            INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions = new INTERNET_PER_CONN_OPTION[2];

            GetSystemProxy(ref internetPerConnOptionList, ref internetPerConnOptionListOptions);

            // allocate memory
            IntPtr internetPerConnOptionListPointer = Marshal.AllocCoTaskMem(internetPerConnOptionListSize);

            // convert structure to IntPtr
            Marshal.StructureToPtr(internetPerConnOptionList, internetPerConnOptionListPointer, true);

            // set internet options
            bool result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, internetPerConnOptionListPointer, internetPerConnOptionListSize);

            // free the allocated memory
            Marshal.FreeCoTaskMem(internetPerConnOptionList.pOptions);
            Marshal.FreeCoTaskMem(internetPerConnOptionListPointer);

            // notify the system that the registry settings have been changed and cause
            // the proxy data to be reread from the registry for a handle
            if (result) {
                result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            }

            if (result) {
                result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }

            if (!InternetCloseHandle(internetHandle)) {
                throw new FlashpointProxyException("Could not close the Internet Handle.");
            }

            if (!result) {
                throw new FlashpointProxyException("Could not set the Internet Options.");
            }
        }
    }
}