using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    public class ActiveXControl {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DllRegisterServerDelegate();

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpLibFileName
        );

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        private static extern int FreeLibrary(IntPtr hLibModule);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        private static extern IntPtr GetProcAddress(
            IntPtr hModule,
            
            [MarshalAs(UnmanagedType.LPStr)]
            string lpProcName
        );

        private IntPtr moduleHandle = IntPtr.Zero;

        private readonly DllRegisterServerDelegate DllRegisterServer;
        private readonly DllRegisterServerDelegate DllUnregisterServer;

        public ActiveXControl(string libFileName) {
            try {
                moduleHandle = LoadLibrary(libFileName);

                if (moduleHandle == IntPtr.Zero) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    return;
                }
            } catch (Win32Exception ex) {
                LogExceptionToLauncher(ex);
                throw new DllNotFoundException("The library \"" + libFileName + "\" could not be found.");
            }

            IntPtr dllRegisterServerProcAddress = IntPtr.Zero;

            try {
                dllRegisterServerProcAddress = GetProcAddress(moduleHandle, "DllRegisterServer");

                if (dllRegisterServerProcAddress == IntPtr.Zero) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    return;
                }
            } catch (Win32Exception ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidActiveXControlException("The library does not have a DllRegisterServer export.");
            }

            IntPtr dllUnregisterServerProcAddress = IntPtr.Zero;

            try {
                dllUnregisterServerProcAddress = GetProcAddress(moduleHandle, "DllUnregisterServer");

                if (dllUnregisterServerProcAddress == IntPtr.Zero) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    return;
                }
            } catch (Win32Exception ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidActiveXControlException("The library does not have a DllRegisterServer export.");
            }

            DllRegisterServer = Marshal.GetDelegateForFunctionPointer(dllRegisterServerProcAddress, typeof(DllRegisterServerDelegate)) as DllRegisterServerDelegate;
            DllUnregisterServer = Marshal.GetDelegateForFunctionPointer(dllUnregisterServerProcAddress, typeof(DllRegisterServerDelegate)) as DllRegisterServerDelegate;
        }

        ~ActiveXControl() {
            if (moduleHandle != IntPtr.Zero) {
                FreeLibrary(moduleHandle);
                moduleHandle = IntPtr.Zero;
            }
        }

        private void RegisterServer(DllRegisterServerDelegate DllRegisterServer) {
            int err = DllRegisterServer();

            if (err != 0) {
                Marshal.ThrowExceptionForHR(err);
            }
        }

        public void Install() {
            RegisterServer(DllRegisterServer);
        }

        public void Uninstall() {
            RegisterServer(DllUnregisterServer);
        }
    }
}