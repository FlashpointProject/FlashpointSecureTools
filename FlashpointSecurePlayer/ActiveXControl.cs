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
        // All COM DLLs must export the DllRegisterServer()
        // and the DllUnregisterServer() APIs for self-registration/unregistration.
        // They both have the same signature and so only one
        // delegate is required.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate UInt32 DllRegisterServerDelegate();

        [DllImport("KERNEL32.DLL", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string libFileName);

        [DllImport("KERNEL32.DLL", CallingConvention = CallingConvention.StdCall)]
        private static extern Int32 FreeLibrary(IntPtr libModuleHandle);

        [DllImport("KERNEL32.DLL", CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetProcAddress(IntPtr moduleHandle, [MarshalAs(UnmanagedType.LPStr)] string procName);

        private IntPtr moduleHandle = IntPtr.Zero;

        private readonly DllRegisterServerDelegate DllRegisterServer;
        private readonly DllRegisterServerDelegate DllUnregisterServer;

        public ActiveXControl(string libFileName) {
            moduleHandle = LoadLibrary(libFileName);

            if (moduleHandle == IntPtr.Zero) {
                throw new DllNotFoundException("The library \"" + libFileName + "\" could not be found.");
            }

            IntPtr dllRegisterServerProcAddress = IntPtr.Zero;
            dllRegisterServerProcAddress = GetProcAddress(moduleHandle, "DllRegisterServer");

            if (dllRegisterServerProcAddress == IntPtr.Zero) {
                throw new InvalidActiveXControlException("The library does not have a DllRegisterServer export.");
            }

            IntPtr dllUnregisterServerProcAddress = IntPtr.Zero;
            dllUnregisterServerProcAddress = GetProcAddress(moduleHandle, "DllUnregisterServer");

            if (dllUnregisterServerProcAddress == IntPtr.Zero) {
                throw new InvalidActiveXControlException("The library does not have a DllUnregisterServer export.");
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
            uint errorCode = DllRegisterServer();

            if (errorCode != 0) {
                throw new Win32Exception("Failed to register the DLL Server (" + errorCode + ").");
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