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

        private readonly IntPtr moduleHandle = IntPtr.Zero;

        private readonly DllRegisterServerDelegate DllRegisterServer;
        private readonly DllRegisterServerDelegate DllUnregisterServer;

        public ActiveXControl(string libFileName) {
            moduleHandle = LoadLibrary(libFileName);

            if (moduleHandle == IntPtr.Zero) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                return;
            }

            IntPtr dllRegisterServerProcAddress = GetProcAddress(moduleHandle, "DllRegisterServer");

            if (dllRegisterServerProcAddress == IntPtr.Zero) {
                throw new InvalidActiveXControlException("The library does not have a DllRegisterServer export.");
            }

            IntPtr dllUnregisterServerProcAddress = GetProcAddress(moduleHandle, "DllUnregisterServer");

            if (dllUnregisterServerProcAddress == IntPtr.Zero) {
                throw new InvalidActiveXControlException("The library does not have a DllUnregisterServer export.");
            }

            DllRegisterServer = Marshal.GetDelegateForFunctionPointer(dllRegisterServerProcAddress, typeof(DllRegisterServerDelegate)) as DllRegisterServerDelegate;
            DllUnregisterServer = Marshal.GetDelegateForFunctionPointer(dllUnregisterServerProcAddress, typeof(DllRegisterServerDelegate)) as DllRegisterServerDelegate;
        }

        ~ActiveXControl() {
            if (moduleHandle != IntPtr.Zero) {
                try {
                    if (!FreeLibrary(moduleHandle)) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return;
                    }
                } catch (Exception ex) {
                    LogExceptionToLauncher(ex);
                }
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