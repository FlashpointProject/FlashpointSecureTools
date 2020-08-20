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
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr InternetOpen(string agent, int accessType, string proxyName, string proxyBypass, int flags);

        [DllImport("WinInet.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetCloseHandle(IntPtr internetHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct INTERNET_PER_CONN_OPTION_LIST {
            public int Size;

            // The connection to be set. NULL means LAN.
            public System.IntPtr Connection;

            public int OptionCount;
            public int OptionError;

            // List of INTERNET_PER_CONN_OPTIONs.
            public System.IntPtr OptionsPointer;
        }

        private enum INTERNET_OPTION {
            // Sets or retrieves an INTERNET_PER_CONN_OPTION_LIST structure that specifies
            // a list of options for a particular connection.
            INTERNET_OPTION_PER_CONNECTION_OPTION = 75,

            // Notify the system that the registry settings have been changed so that
            // it verifies the settings on the next call to InternetConnect.
            INTERNET_OPTION_SETTINGS_CHANGED = 39,

            // Causes the proxy data to be reread from the registry for a handle.
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

        private const int INTERNET_OPEN_TYPE_DIRECT = 1;  // direct to net
        private const int INTERNET_OPEN_TYPE_PRECONFIG = 0; // read registry
                                                            /// <summary>
                                                            /// Constants used in INTERNET_PER_CONN_OPTON struct.
                                                            /// </summary>

        private enum INTERNET_OPTION_PER_CONN_FLAGS {
            PROXY_TYPE_DIRECT = 0x00000001,   // direct to net
            PROXY_TYPE_PROXY = 0x00000002,   // via named proxy
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,   // autoproxy URL
            PROXY_TYPE_AUTO_DETECT = 0x00000008   // use autoproxy detection
        }

        /// <summary>
        /// Used in INTERNET_PER_CONN_OPTION.
        /// When create a instance of OptionUnion, only one filed will be used.
        /// The StructLayout and FieldOffset attributes could help to decrease the struct size.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct INTERNET_PER_CONN_OPTION_OptionUnion {
            // A value in INTERNET_OPTION_PER_CONN_FLAGS.
            [FieldOffset(0)]
            public int Value;
            [FieldOffset(0)]
            public System.IntPtr ValuePointer;
            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ValueFiletime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INTERNET_PER_CONN_OPTION {
            // A value in INTERNET_PER_CONN_OptionEnum.
            public int Option;
            public INTERNET_PER_CONN_OPTION_OptionUnion Value;
        }

        /// <summary>
        /// Sets an Internet option.
        /// </summary>
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool InternetSetOption(IntPtr internetHandle, INTERNET_OPTION option, IntPtr bufferPointer, int bufferLength);

        /// <summary>
        /// Queries an Internet option on the specified handle. The Handle will be always 0.
        /// </summary>
        [DllImport("WinInet.dll", SetLastError = true, CharSet = CharSet.Ansi, EntryPoint = "InternetQueryOption")]
        private extern static bool InternetQueryOption(IntPtr handle, INTERNET_OPTION optionFlag, ref INTERNET_PER_CONN_OPTION_LIST optionList, ref int size);
        
        // in enabling the proxy we need to set the Agent to use
        private const string AGENT = "Flashpoint Proxy";

        /// <summary>
        /// Backup the current options for LAN connection.
        /// Make sure free the memory after restoration.
        /// </summary>
        private static void GetSystemProxy(ref INTERNET_PER_CONN_OPTION_LIST internetPerConnOptionList, ref INTERNET_PER_CONN_OPTION[] internetPerConnOptionListOptions) {
            int internetPerConnOptionListSize = Marshal.SizeOf(internetPerConnOptionList);

            if (internetPerConnOptionListOptions.Length < 2) {
                throw new ArgumentException("The Internet Per Connection Option List Options cannot have a Length of less than two.");
            }

            // set flags
            internetPerConnOptionListOptions[0] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[0].Option = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS;

            // set proxy name
            internetPerConnOptionListOptions[1] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[1].Option = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER;

            // allocate a block of memory of the options
            internetPerConnOptionList.OptionsPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf(internetPerConnOptionListOptions[0]) + Marshal.SizeOf(internetPerConnOptionListOptions[1]));
            System.IntPtr internetPerConnOptionPointer = internetPerConnOptionList.OptionsPointer;

            // marshal data from a managed object to unmanaged memory
            for (int i = 0;i < internetPerConnOptionListOptions.Length;i++) {
                Marshal.StructureToPtr(internetPerConnOptionListOptions[i], internetPerConnOptionPointer, false);
                internetPerConnOptionPointer = (System.IntPtr)((int)internetPerConnOptionPointer + Marshal.SizeOf(internetPerConnOptionListOptions[i]));
            }

            // fill the internetPerConnOptionList structure
            internetPerConnOptionList.Size = internetPerConnOptionListSize;

            // NULL == LAN, otherwise connectoid name
            internetPerConnOptionList.Connection = IntPtr.Zero;

            // set two options
            internetPerConnOptionList.OptionCount = internetPerConnOptionListOptions.Length;
            internetPerConnOptionList.OptionError = 0;

            // query internet options
            bool result = InternetQueryOption(IntPtr.Zero, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, ref internetPerConnOptionList, ref internetPerConnOptionListSize);

            if (!result) {
                throw new FlashpointProxyException("Could not query the Internet Options.");
            }
        }

        /// <summary>
        /// Set the proxy server for LAN connection.
        /// </summary>
        public static void Enable(string proxyServer) {
            IntPtr internetHandle = IntPtr.Zero;
            internetHandle = InternetOpen(AGENT, INTERNET_OPEN_TYPE_DIRECT, null, null, 0);

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
            internetPerConnOptionListOptions[0].Option = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_FLAGS;
            internetPerConnOptionListOptions[0].Value.Value = (int)INTERNET_OPTION_PER_CONN_FLAGS.PROXY_TYPE_PROXY;

            // set proxy name
            internetPerConnOptionListOptions[1] = new INTERNET_PER_CONN_OPTION();
            internetPerConnOptionListOptions[1].Option = (int)INTERNET_PER_CONN_OptionEnum.INTERNET_PER_CONN_PROXY_SERVER;
            internetPerConnOptionListOptions[1].Value.ValuePointer = Marshal.StringToHGlobalAnsi(proxyServer);

            // allocate memory for the INTERNET_PER_CONN_OPTION_LIST Options
            internetPerConnOptionList.OptionsPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf(internetPerConnOptionListOptions[0]) + Marshal.SizeOf(internetPerConnOptionListOptions[1]));
            System.IntPtr internetPerConnOptionListOptionPointer = internetPerConnOptionList.OptionsPointer;

            // marshal data from a managed object to unmanaged memory
            for (int i = 0;i < internetPerConnOptionListOptions.Length;i++) {
                Marshal.StructureToPtr(internetPerConnOptionListOptions[i], internetPerConnOptionListOptionPointer, false);
                internetPerConnOptionListOptionPointer = (System.IntPtr)((long)internetPerConnOptionListOptionPointer + Marshal.SizeOf(internetPerConnOptionListOptions[i]));
            }

            // fill the internetPerConnOptionList structure
            internetPerConnOptionList.Size = Marshal.SizeOf(internetPerConnOptionList);

            // NULL == LAN, otherwise connectoid name
            internetPerConnOptionList.Connection = IntPtr.Zero;

            // set two options
            internetPerConnOptionList.OptionCount = internetPerConnOptionListOptions.Length;
            internetPerConnOptionList.OptionError = 0;

            // allocate memory for the INTERNET_PER_CONN_OPTION_LIST
            IntPtr internetPerConnOptionListPointer = Marshal.AllocCoTaskMem(internetPerConnOptionListSize);

            // marshal data from a managed object to unmanaged memory
            Marshal.StructureToPtr(internetPerConnOptionList, internetPerConnOptionListPointer, true);

            // set the options on the connection
            bool result = InternetSetOption(internetHandle, INTERNET_OPTION.INTERNET_OPTION_PER_CONNECTION_OPTION, internetPerConnOptionListPointer, internetPerConnOptionListSize);

            // free the allocated memory
            Marshal.FreeCoTaskMem(internetPerConnOptionList.OptionsPointer);
            Marshal.FreeCoTaskMem(internetPerConnOptionListPointer);

            if (!InternetCloseHandle(internetHandle)) {
                throw new FlashpointProxyException("Could not close the Internet Handle.");
            }

            // throw an exception if this operation failed
            if (!result) {
                throw new FlashpointProxyException("Could not set the Internet Options.");
            }
        }

        /// <summary>
        /// Restore the options for LAN connection.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static void Disable() {
            IntPtr internetHandle = IntPtr.Zero;
            internetHandle = InternetOpen(AGENT, INTERNET_OPEN_TYPE_DIRECT, null, null, 0);

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
            Marshal.FreeCoTaskMem(internetPerConnOptionList.OptionsPointer);
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