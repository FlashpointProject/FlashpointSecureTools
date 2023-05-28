using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace FlashpointSecurePlayer {
    public static class Shared {
        public static class Exceptions {
            [Serializable]
            public class ApplicationRestartRequiredException : Exception {
                public ApplicationRestartRequiredException() : base() { }
                public ApplicationRestartRequiredException(string message) : base(message) { }
                public ApplicationRestartRequiredException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class TaskRequiresElevationException : ApplicationRestartRequiredException {
                public TaskRequiresElevationException() : base() { }
                public TaskRequiresElevationException(string message) : base(message) { }
                public TaskRequiresElevationException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class CompatibilityLayersException : ApplicationRestartRequiredException {
                public CompatibilityLayersException() : base() { }
                public CompatibilityLayersException(string message) : base(message) { }
                public CompatibilityLayersException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class OldCPUSimulatorRequiresApplicationRestartException : ApplicationRestartRequiredException {
                public OldCPUSimulatorRequiresApplicationRestartException() { }
                public OldCPUSimulatorRequiresApplicationRestartException(string message) : base(message) { }
                public OldCPUSimulatorRequiresApplicationRestartException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class DownloadFailedException : Exception {
                public DownloadFailedException() : base() { }
                public DownloadFailedException(string message) : base(message) { }
                public DownloadFailedException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class ImportFailedException : Exception {
                public ImportFailedException() { }
                public ImportFailedException(string message) : base(message) { }
                public ImportFailedException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class ActiveXImportFailedException : ImportFailedException {
                public ActiveXImportFailedException() { }
                public ActiveXImportFailedException(string message) : base(message) { }
                public ActiveXImportFailedException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class JobObjectException : Exception {
                public JobObjectException() { }
                public JobObjectException(string message) : base(message) { }
                public JobObjectException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class FlashpointProxyException : Exception {
                public FlashpointProxyException() { }
                public FlashpointProxyException(string message) : base(message) { }
                public FlashpointProxyException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class LibraryBinaryFormatException : FormatException {
                public LibraryBinaryFormatException() { }
                public LibraryBinaryFormatException(string message) : base(message) { }
                public LibraryBinaryFormatException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidActiveXControlException : InvalidOperationException {
                public InvalidActiveXControlException() { }
                public InvalidActiveXControlException(string message) : base(message) { }
                public InvalidActiveXControlException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidTemplateException : InvalidOperationException {
                public InvalidTemplateException() { }
                public InvalidTemplateException(string message) : base(message) { }
                public InvalidTemplateException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidModeException : InvalidTemplateException {
                public InvalidModeException() { }
                public InvalidModeException(string message) : base(message) { }
                public InvalidModeException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidModificationException : InvalidTemplateException {
                public InvalidModificationException() { }
                public InvalidModificationException(string message) : base(message) { }
                public InvalidModificationException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidOldCPUSimulatorException : InvalidModificationException {
                public InvalidOldCPUSimulatorException() { }
                public InvalidOldCPUSimulatorException(string message) : base(message) { }
                public InvalidOldCPUSimulatorException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidEnvironmentVariablesException : InvalidModificationException {
                public InvalidEnvironmentVariablesException() { }
                public InvalidEnvironmentVariablesException(string message) : base(message) { }
                public InvalidEnvironmentVariablesException(string message, Exception inner) : base(message, inner) { }
            }

            [Serializable]
            public class InvalidRegistryStateException : InvalidModificationException {
                public InvalidRegistryStateException() { }
                public InvalidRegistryStateException(string message) : base(message) { }
                public InvalidRegistryStateException(string message, Exception inner) : base(message, inner) { }
            }

            public static void LogExceptionToLauncher(Exception ex) {
                try {
                    Console.Error.WriteLine(ex.Message);
                } catch {
                    // fail silently
                }
            }
        }

        // it'd be nice to move all these native methods to their own class one day
        public const int MAX_PATH = 260;

        public const int ERROR_SUCCESS = 0x00000000;
        public const int ERROR_NO_MORE_FILES = 0x00000012;

        public const int WM_DESTROY = 0x00000002;
        public const int WM_PAINT = 0x0000000F;
        public const int WM_MOUSEMOVE = 0x00000200;
        public const int WM_XBUTTONUP = 0x0000020C;
        public const int WM_PARENTNOTIFY = 0x00000210;

        public const int MK_XBUTTON1 = 0x00010000;
        public const int MK_XBUTTON2 = 0x00020000;

        public const int S_OK = unchecked((int)0x00000000);
        public const int S_FALSE = unchecked((int)0x00000001);

        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_NOINTERFACE = unchecked((int)0x80004002);
        public const int E_POINTER = unchecked((int)0x80004003);
        public const int E_ABORT = unchecked((int)0x80004004);
        public const int E_FAIL = unchecked((int)0x80004005);
        public const int E_UNEXPECTED = unchecked((int)0x8000FFFF);
        public const int E_ACCESSDENIED = unchecked((int)0x80070005);
        public const int E_HANDLE = unchecked((int)0x80070006);
        public const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        public const int E_INVALIDARG = unchecked((int)0x80070057);

        public const int INET_E_DEFAULT_ACTION = unchecked((int)0x800C0011);

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetShortPathName(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszLongPath,

            [MarshalAs(UnmanagedType.LPTStr)]
            StringBuilder lpszShortPath,

            uint cchBuffer
        );

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetLongPathName(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpszShortPath,

            [MarshalAs(UnmanagedType.LPTStr)]
            StringBuilder lpszLongPath,

            uint cchBuffer
        );

        public const uint INTERNET_MAX_PATH_LENGTH = 2048;
        public const uint INTERNET_MAX_SCHEME_LENGTH = 32;
        public static readonly uint INTERNET_MAX_URL_LENGTH = INTERNET_MAX_SCHEME_LENGTH + (uint)"://".Length + INTERNET_MAX_PATH_LENGTH;

        [Flags]
        public enum URL_APPLYFlags : uint {
            Zero = 0x00000000,
            URL_APPLY_DEFAULT = 0x00000001,
            URL_APPLY_GUESSSCHEME = 0x00000002,
            URL_APPLY_GUESSFILE = 0x00000004,
            URL_APPLY_FORCEAPPLY = 0x00000008
        }

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        public static extern int UrlApplyScheme(
            [MarshalAs(UnmanagedType.LPTStr)]
            string pszIn,

            [MarshalAs(UnmanagedType.LPTStr)]
            StringBuilder pszOut,

            ref uint pcchOut,
            URL_APPLYFlags dwFlags);

        public enum OS : uint {
            OS_WINDOWS = 0,
            OS_NT = 1,
            OS_WIN95ORGREATER = 2,
            OS_NT4ORGREATER = 3,
            OS_WIN98ORGREATER = 5,
            OS_WIN98_GOLD = 6,
            OS_WIN2000ORGREATER = 7,
            OS_WIN2000PRO = 8,
            OS_WIN2000SERVER = 9,
            OS_WIN2000ADVSERVER = 10,
            OS_WIN2000DATACENTER = 11,
            OS_WIN2000TERMINAL = 12,
            OS_EMBEDDED = 13,
            OS_TERMINALCLIENT = 14,
            OS_TERMINALREMOTEADMIN = 15,
            OS_WIN95_GOLD = 16,
            OS_MEORGREATER = 17,
            OS_XPORGREATER = 18,
            OS_HOME = 19,
            OS_PROFESSIONAL = 20,
            OS_DATACENTER = 21,
            OS_ADVSERVER = 22,
            OS_SERVER = 23,
            OS_TERMINALSERVER = 24,
            OS_PERSONALTERMINALSERVER = 25,
            OS_FASTUSERSWITCHING = 26,
            OS_WELCOMELOGONUI = 27,
            OS_DOMAINMEMBER = 28,
            OS_ANYSERVER = 29,
            OS_WOW6432 = 30,
            OS_WEBSERVER = 31,
            OS_SMALLBUSINESSSERVER = 32,
            OS_TABLETPC = 33,
            OS_SERVERADMINUI = 34,
            OS_MEDIACENTER = 35,
            OS_APPLIANCE = 36
        }

        [DllImport("Shlwapi.dll", SetLastError = true, EntryPoint = "#437")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsOS(OS dwOS);

        [DllImport("USER32.DLL")]
        public static extern IntPtr GetForegroundWindow();

        public enum GW : uint {
            GW_CHILD = 5,
            GW_ENABLEDPOPUP = 6,
            GW_HWNDFIRST = 0,
            GW_HWNDLAST = 1,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4
        }

        [DllImport("USER32.DLL", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, GW uCmd);

        [DllImport("USER32.DLL", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int left;
            public int right;
            public int top;
            public int bottom;
        }

        public enum HookType : int {
            WH_MSGFILTER = -1,
            WH_JOURNALRECORD = 0,
            WH_JOURNALPLAYBACK = 1,
            WH_KEYBOARD = 2,
            WH_GETMESSAGE = 3,
            WH_CALLWNDPROC = 4,
            WH_CBT = 5,
            WH_SYSMSGFILTER = 6,
            WH_MOUSE = 7,
            WH_HARDWARE = 8,
            WH_DEBUG = 9,
            WH_SHELL = 10,
            WH_FOREGROUNDIDLE = 11,
            WH_CALLWNDPROCRET = 12,
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL = 14
        }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("USER32.DLL")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT {
            public uint vkCode;
            public uint scanCode;
            public KBDLLHOOKSTRUCTFlags flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [Flags]
        public enum KBDLLHOOKSTRUCTFlags : uint {
            LLKHF_EXTENDED = 0x01,
            LLKHF_INJECTED = 0x10,
            LLKHF_ALTDOWN = 0x20,
            LLKHF_UP = 0x80
        }

        [DllImport("USER32.DLL")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, KBDLLHOOKSTRUCT lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        [DllImport("USER32.DLL")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, MSLLHOOKSTRUCT lParam);

        [DllImport("USER32.DLL", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(HookType hookType, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("USER32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        public enum SW : uint {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_NORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_MAXIMIZE = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACTIVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11,
            SW_MAX = 11
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT {
            public int length;
            public int flags;
            public SW showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [DllImport("USER32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("USER32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES {
            public uint nLength;
            public IntPtr lpSecurityDescriptor;
        }

        [Flags]
        public enum CreationFlags : uint {
            Zero = 0,
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SECURE_PROCESS = 0x00400000,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUP_INFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO {
            public uint cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttributes;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdErr;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFOEX {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpApplicationName,

            [MarshalAs(UnmanagedType.LPTStr)]
            string lpCommandLine,

            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,

            [MarshalAs(UnmanagedType.Bool)]
            bool bInheritHandles,

            CreationFlags dwCreationFlags,
            IntPtr lpEnvironment,

            [MarshalAs(UnmanagedType.LPTStr)]
            string lpCurrentDirectory,

            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpApplicationName,

            [MarshalAs(UnmanagedType.LPTStr)]
            string lpCommandLine,

            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,

            [MarshalAs(UnmanagedType.Bool)]
            bool bInheritHandles,

            CreationFlags dwCreationFlags,
            IntPtr lpEnvironment,

            [MarshalAs(UnmanagedType.LPTStr)]
            string lpCurrentDirectory,

            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        public static extern int SuspendThread(IntPtr hThread);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        public static extern int ResumeThread(IntPtr hThread);

        public enum BINARY_TYPE : uint {
            SCS_32BIT_BINARY = 0, // A 32-bit Windows-based application
            SCS_64BIT_BINARY = 6, // A 64-bit Windows-based application.
            SCS_DOS_BINARY = 1, // An MS-DOS – based application
            SCS_OS216_BINARY = 5, // A 16-bit OS/2-based application
            SCS_PIF_BINARY = 3, // A PIF file that executes an MS-DOS – based application
            SCS_POSIX_BINARY = 4, // A POSIX – based application
            SCS_WOW_BINARY = 2 // A 16-bit Windows-based application
        }

        /*
        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetBinaryType(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpApplicationName,

            out BINARY_TYPE lpBinaryType
        );
        */

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibrary(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpLibFileName
        );

        [Flags]
        public enum LoadLibraryFlags : uint {
            Zero = 0,
            DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
            LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
            LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
            LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
            LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
            LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
            LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
            LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
        }

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibraryEx(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpLibFileName,

            IntPtr hFile,
            LoadLibraryFlags dwFlags
        );

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hLibModule);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        public static extern IntPtr GetProcAddress(
            IntPtr hModule,

            [MarshalAs(UnmanagedType.LPStr)]
            string lpProcName
        );

        public static readonly ushort IMAGE_DOS_SIGNATURE = 0x5A4D;
        public static readonly uint IMAGE_NT_SIGNATURE = 0x00004550;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DOS_HEADER {
            public ushort e_magic;
            public ushort e_cblp;
            public ushort e_cp;
            public ushort e_crlc;
            public ushort e_cparhdr;
            public ushort e_minalloc;
            public ushort e_maxalloc;
            public ushort e_ss;
            public ushort e_sp;
            public ushort e_csum;
            public ushort e_ip;
            public ushort e_cs;
            public ushort e_lfarlc;
            public ushort e_ovno;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] e_res1;

            public ushort e_oemid;
            public ushort e_oeminfo;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public ushort[] e_res2;

            public int e_lfanew;
        }

        public const ushort IMAGE_FILE_MACHINE_I386 = 0x014C;

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_FILE_HEADER {
            public ushort Machine;
            public ushort NumberOfSections;
            public uint TimeDateStamp;
            public uint PointerToSymbolTable;
            public uint NumberOfSymbols;
            public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY {
            public uint VirtualAddress;
            public uint Size;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct IMAGE_OPTIONAL_HEADER {
            [FieldOffset(0)]
            public ushort Magic;

            [FieldOffset(2)]
            public byte MajorLinkerVersion;

            [FieldOffset(3)]
            public byte MinorLinkerVersion;

            [FieldOffset(4)]
            public uint SizeOfCode;

            [FieldOffset(8)]
            public uint SizeOfInitializedData;

            [FieldOffset(12)]
            public uint SizeOfUninitializedData;

            [FieldOffset(16)]
            public uint AddressOfEntryPoint;

            [FieldOffset(20)]
            public uint BaseOfCode;
            
            [FieldOffset(24)]
            public uint BaseOfData;

            [FieldOffset(28)]
            public uint ImageBase;

            [FieldOffset(32)]
            public uint SectionAlignment;

            [FieldOffset(36)]
            public uint FileAlignment;

            [FieldOffset(40)]
            public ushort MajorOperatingSystemVersion;

            [FieldOffset(42)]
            public ushort MinorOperatingSystemVersion;

            [FieldOffset(44)]
            public ushort MajorImageVersion;

            [FieldOffset(46)]
            public ushort MinorImageVersion;

            [FieldOffset(48)]
            public ushort MajorSubsystemVersion;

            [FieldOffset(50)]
            public ushort MinorSubsystemVersion;

            [FieldOffset(52)]
            public uint Win32VersionValue;

            [FieldOffset(56)]
            public uint SizeOfImage;

            [FieldOffset(60)]
            public uint SizeOfHeaders;

            [FieldOffset(64)]
            public uint CheckSum;

            [FieldOffset(68)]
            public ushort Subsystem;

            [FieldOffset(70)]
            public ushort DllCharacteristics;

            [FieldOffset(72)]
            public uint SizeOfStackReserve;

            [FieldOffset(76)]
            public uint SizeOfStackCommit;

            [FieldOffset(80)]
            public uint SizeOfHeapReserve;

            [FieldOffset(84)]
            public uint SizeOfHeapCommit;

            [FieldOffset(88)]
            public uint LoaderFlags;

            [FieldOffset(92)]
            public uint NumberOfRvaAndSizes;

            [FieldOffset(96)]
            public IMAGE_DATA_DIRECTORY ExportTable;

            [FieldOffset(104)]
            public IMAGE_DATA_DIRECTORY ImportTable;

            [FieldOffset(112)]
            public IMAGE_DATA_DIRECTORY ResourceTable;

            [FieldOffset(120)]
            public IMAGE_DATA_DIRECTORY ExceptionTable;

            [FieldOffset(128)]
            public IMAGE_DATA_DIRECTORY CertificateTable;

            [FieldOffset(136)]
            public IMAGE_DATA_DIRECTORY BaseRelocationTable;

            [FieldOffset(144)]
            public IMAGE_DATA_DIRECTORY Debug;

            [FieldOffset(152)]
            public IMAGE_DATA_DIRECTORY Architecture;

            [FieldOffset(160)]
            public IMAGE_DATA_DIRECTORY GlobalPtr;

            [FieldOffset(168)]
            public IMAGE_DATA_DIRECTORY TLSTable;

            [FieldOffset(176)]
            public IMAGE_DATA_DIRECTORY LoadConfigTable;

            [FieldOffset(184)]
            public IMAGE_DATA_DIRECTORY BoundImport;

            [FieldOffset(192)]
            public IMAGE_DATA_DIRECTORY IAT;

            [FieldOffset(200)]
            public IMAGE_DATA_DIRECTORY DelayImportDescriptor;

            [FieldOffset(208)]
            public IMAGE_DATA_DIRECTORY CLRRuntimeHeader;

            [FieldOffset(216)]
            public IMAGE_DATA_DIRECTORY Reserved;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct IMAGE_NT_HEADERS {
            [FieldOffset(0)]
            public uint Signature;

            [FieldOffset(4)]
            public IMAGE_FILE_HEADER FileHeader;

            [FieldOffset(24)]
            public IMAGE_OPTIONAL_HEADER OptionalHeader;
        }

        [DllImport("dbghelp.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ImageNtHeader(IntPtr Base);

        [Flags]
        public enum FileFlagsAndAttributes : uint {
            ReadOnly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            WriteThrough = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern SafeFileHandle CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpFileName,

            [MarshalAs(UnmanagedType.U4)]
            FileAccess dwDesiredAccess,

            [MarshalAs(UnmanagedType.U4)]
            FileShare dwShareMode,

            IntPtr lpSecurityAttributes,

            [MarshalAs(UnmanagedType.U4)]
            FileMode dwCreationDisposition,

            [MarshalAs(UnmanagedType.U4)]
            FileFlagsAndAttributes dwFlagsAndAttributes,

            IntPtr hTemplateFile
        );

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BY_HANDLE_FILE_INFORMATION {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [Flags]
        public enum TH32CSFlags : uint {
            TH32CS_INHERIT = 0x80000000,
            TH32CS_SNAPALL = TH32CS_SNAPHEAPLIST | TH32CS_SNAPMODULE | TH32CS_SNAPPROCESS | TH32CS_SNAPTHREAD,
            TH32CS_SNAPHEAPLIST = 0x00000001,
            TH32CS_SNAPMODULE = 0x00000008,
            TH32CS_SNAPMODULE32 = 0x00000010,
            TH32CS_SNAPPROCESS = 0x00000002,
            TH32CS_SNAPTHREAD = 0x00000004
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSENTRY32 {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szExeFile;
        };

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(TH32CSFlags dwFlags, uint th32ProcessID);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags,

        [MarshalAs(UnmanagedType.LPTStr)]
        StringBuilder lpExeName,
            
        ref int lpdwSize);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("SHELL32.DLL", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CommandLineToArgv(
            [MarshalAs(UnmanagedType.LPWStr)]
            string lpCmdLine,

            out int pNumArgs
        );

        public enum MODIFICATIONS_REVERT_METHOD {
            CRASH_RECOVERY,
            REVERT_ALL,
            DELETE_ALL
        }

        public const string HTDOCS = "..\\Legacy\\htdocs";
        public static readonly string[] INDEX_EXTENSIONS = { "html", "htm" };

        // for best results, this should match
        // the value of FILE_READ_LENGTH constant
        // in the Flashpoint Router
        private const int STREAM_READ_LENGTH = 8192;

        private const string CONFIGURATION_FOLDER_NAME = "FlashpointSecurePlayerConfigs";
        private const string CONFIGURATION_DOWNLOAD_NAME = "flashpointsecureplayerconfigs";

        public const string OLD_CPU_SIMULATOR_PATH = "OldCPUSimulator\\OldCPUSimulator.exe";
        public const string OLD_CPU_SIMULATOR_PARENT_PROCESS_FILE_NAME = "OLDCPUSIMULATOR.EXE";

        public const string FP_STARTUP_PATH = nameof(FP_STARTUP_PATH);
        public const string FP_URL = nameof(FP_URL);
        public const string FP_ARGUMENTS = nameof(FP_ARGUMENTS);
        public const string FP_HTDOCS_FILE = nameof(FP_HTDOCS_FILE);
        public const string FP_HTDOCS_FILE_DIR = nameof(FP_HTDOCS_FILE_DIR);

        // there should be only one HTTP Client per application
        // (as of right now though this is exclusively used by DownloadsBefore class)
        private static readonly HttpClientHandler httpClientHandler = new HttpClientHandler {
            Proxy = new WebProxy("127.0.0.1", 22500),
            UseProxy = true
        };

        public static readonly HttpClient httpClient = new HttpClient(httpClientHandler);

        // no parallel downloads
        // if parallel downloads are ever supported, the max value should
        // be changed to the maximum number of parallel downloads allowed
        // (preferably, no more than eight at a time)
        private static SemaphoreSlim downloadSemaphoreSlim = new SemaphoreSlim(1, 1);

        // it'd be nice to move all the configuration, template, section stuff to their own class one day
        private static FlashpointSecurePlayerSection flashpointSecurePlayerSection = null;
        private static FlashpointSecurePlayerSection activeFlashpointSecurePlayerSection = null;

        public abstract class TemplatesConfigurationElementCollection : ConfigurationElementCollection {
            public override ConfigurationElementCollectionType CollectionType {
                get {
                    return ConfigurationElementCollectionType.AddRemoveClearMapAlternate;
                }
            }

            protected override void BaseAdd(System.Configuration.ConfigurationElement configurationElement) {
                BaseAdd(configurationElement, false);
            }

            public ConfigurationElement Get(int index) {
                return BaseGet(index) as ConfigurationElement;
            }

            public ConfigurationElement Get(string name) {
                //BaseAdd(modificationsElement);
                return BaseGet(name) as ConfigurationElement;
            }

            public void Set(ConfigurationElement configurationElement) {
                Remove(GetElementKey(configurationElement) as string);
                BaseAdd(configurationElement);
            }

            public void Remove(string name) {
                if (BaseGet(name) != null) {
                    BaseRemove(name);
                }
            }

            public void Remove(ConfigurationElement configurationElement) {
                Remove(GetElementKey(configurationElement) as string);
            }

            public void RemoveAt(int index) {
                if (BaseGet(index) != null) {
                    BaseRemoveAt(index);
                }
            }

            public void Clear() {
                BaseClear();
            }

            public int IndexOf(ConfigurationElement configurationElement) {
                return BaseIndexOf(configurationElement);
            }
        }

        public class FlashpointSecurePlayerSection : ConfigurationSection {
            public class TemplatesElementCollection : TemplatesConfigurationElementCollection {
                public class TemplateElement : ConfigurationElement {
                    protected ConfigurationPropertyCollection _properties = null;
                    protected ConfigurationProperty _name = null;
                    protected ConfigurationProperty _active = null;
                    protected ConfigurationProperty _mode = null;
                    protected ConfigurationProperty _modifications = null;

                    public TemplateElement() {
                        _properties = new ConfigurationPropertyCollection();

                        _name = new ConfigurationProperty("name", typeof(string), null,
                            ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);
                        _properties.Add(_name);

                        /*
                        if (String.IsNullOrEmpty(Name)) {
                            _active = new ConfigurationProperty("active", typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                            _properties.Add(_active);
                        } else {
                            _mode = new ConfigurationProperty("mode", typeof(ModeElement), null, ConfigurationPropertyOptions.IsRequired);
                            _properties.Add(_mode);
                        }
                        */
                        
                        _active = new ConfigurationProperty("active", typeof(string), null, ConfigurationPropertyOptions.None);
                        _properties.Add(_active);

                        _mode = new ConfigurationProperty("mode", typeof(ModeElement), null, ConfigurationPropertyOptions.None);
                        _properties.Add(_mode);

                        _modifications = new ConfigurationProperty("modifications", typeof(ModificationsElement), null, ConfigurationPropertyOptions.None);
                        _properties.Add(_modifications);
                    }
                    
                    public string Name {
                        get {
                            if (String.IsNullOrEmpty(base[_name] as string)) {
                                return base[_name] as string;
                            }
                            return (base[_name] as string).ToLowerInvariant();
                        }

                        set {
                            if (String.IsNullOrEmpty(value)) {
                                base[_mode] = null;
                                base[_name] = value;
                                return;
                            }

                            base[_active] = null;
                            base[_name] = value.ToLowerInvariant();
                        }
                    }
                    
                    public string Active {
                        get {
                            if (!String.IsNullOrEmpty(Name) || _active == null) {
                                return null;
                            }

                            if (String.IsNullOrEmpty(base[_active] as string)) {
                                return base[_active] as string;
                            }
                            return (base[_active] as string).ToLowerInvariant();
                        }

                        set {
                            if (!String.IsNullOrEmpty(Name) || _active == null) {
                                return;
                            }

                            if (String.IsNullOrEmpty(value)) {
                                base[_active] = value;
                                return;
                            }

                            base[_active] = value.ToLowerInvariant();
                        }
                    }

                    public class ModeElement : ConfigurationElement {
                        public enum NAME {
                            WEB_BROWSER,
                            SOFTWARE
                        }

                        public enum WEB_BROWSER_NAME {
                            INTERNET_EXPLORER
                        }

                        protected ConfigurationPropertyCollection _properties = null;
                        protected ConfigurationProperty _name = null;
                        protected ConfigurationProperty _webBrowserName = null;
                        protected ConfigurationProperty _commandLine = null;
                        protected ConfigurationProperty _workingDirectory = null;
                        protected ConfigurationProperty _hideWindow = null;

                        public ModeElement() {
                            _properties = new ConfigurationPropertyCollection();

                            _name = new ConfigurationProperty("name", typeof(NAME), NAME.WEB_BROWSER,
                                ConfigurationPropertyOptions.IsKey/* | ConfigurationPropertyOptions.IsRequired*/);
                            _properties.Add(_name);

                            /*
                            switch (Name) {
                                case NAME.WEB_BROWSER:
                                _webBrowserName = new ConfigurationProperty("webBrowserName",
                                    typeof(WEB_BROWSER_NAME), WEB_BROWSER_NAME.INTERNET_EXPLORER, ConfigurationPropertyOptions.IsRequired);
                                _properties.Add(_webBrowserName);
                                break;
                                case NAME.SOFTWARE:
                                _commandLine = new ConfigurationProperty("commandLine",
                                    typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                _properties.Add(_commandLine);

                                _hideWindow = new ConfigurationProperty("hideWindow",
                                    typeof(string), null, ConfigurationPropertyOptions.None);
                                _properties.Add(_hideWindow);
                                break;
                            }
                            */
                            
                            _webBrowserName = new ConfigurationProperty("webBrowserName",
                                typeof(WEB_BROWSER_NAME?), WEB_BROWSER_NAME.INTERNET_EXPLORER, ConfigurationPropertyOptions.None);
                            _properties.Add(_webBrowserName);

                            _commandLine = new ConfigurationProperty("commandLine",
                                typeof(string), null, ConfigurationPropertyOptions.None);
                            _properties.Add(_commandLine);

                            _workingDirectory = new ConfigurationProperty("workingDirectory", typeof(string), null, ConfigurationPropertyOptions.None);
                            _properties.Add(_workingDirectory);

                            _hideWindow = new ConfigurationProperty("hideWindow",
                                typeof(bool), false, ConfigurationPropertyOptions.None);
                            _properties.Add(_hideWindow);
                        }
                        
                        public NAME Name {
                            get {
                                return (NAME)base[_name];
                            }

                            set {
                                switch (value) {
                                    case NAME.WEB_BROWSER:
                                    base[_commandLine] = null;
                                    base[_hideWindow] = null;
                                    break;
                                    case NAME.SOFTWARE:
                                    base[_webBrowserName] = null;
                                    break;
                                }

                                base[_name] = value;
                            }
                        }
                        
                        public WEB_BROWSER_NAME? WebBrowserName {
                            get {
                                if (Name != NAME.WEB_BROWSER || _webBrowserName == null) {
                                    return null;
                                }
                                return base[_webBrowserName] as WEB_BROWSER_NAME?;
                            }

                            set {
                                if (Name != NAME.WEB_BROWSER || _webBrowserName == null) {
                                    return;
                                }
                                base[_webBrowserName] = value;
                            }
                        }
                        
                        public string CommandLine {
                            get {
                                if (Name != NAME.SOFTWARE || _commandLine == null) {
                                    return null;
                                }
                                return base[_commandLine] as string;
                            }

                            set {
                                if (Name != NAME.SOFTWARE || _commandLine == null) {
                                    return;
                                }
                                base[_commandLine] = value;
                            }
                        }
                        
                        public string WorkingDirectory {
                            get {
                                if (/*Name != NAME.SOFTWARE || */_workingDirectory == null) {
                                    return null;
                                }
                                return base[_workingDirectory] as string;
                            }

                            set {
                                if (Name != NAME.SOFTWARE || _workingDirectory == null) {
                                    return;
                                }

                                base[_workingDirectory] = value;
                            }
                        }
                        
                        public bool? HideWindow {
                            get {
                                if (Name != NAME.SOFTWARE || _hideWindow == null) {
                                    return null;
                                }
                                return base[_hideWindow] as bool?;
                            }

                            set {
                                if (Name != NAME.SOFTWARE || _hideWindow == null) {
                                    return;
                                }
                                base[_hideWindow] = value;
                            }
                        }

                        protected override ConfigurationPropertyCollection Properties {
                            get {
                                return _properties;
                            }
                        }
                    }
                    
                    public ModeElement Mode {
                        get {
                            if (String.IsNullOrEmpty(Name) || _mode == null) {
                                return null;
                            }
                            return base[_mode] as ModeElement;
                        }

                        set {
                            if (String.IsNullOrEmpty(Name) || _mode == null) {
                                return;
                            }

                            base[_mode] = value;
                        }
                    }

                    public class ModificationsElement : ConfigurationElement {
                        [ConfigurationProperty("runAsAdministrator", DefaultValue = false, IsRequired = false)]
                        public bool RunAsAdministrator {
                            get {
                                return (bool)base["runAsAdministrator"];
                            }

                            set {
                                base["runAsAdministrator"] = value;
                            }
                        }

                        public class EnvironmentVariablesElementCollection : TemplatesConfigurationElementCollection {
                            public class EnvironmentVariablesElement : ConfigurationElement {
                                protected ConfigurationPropertyCollection _properties = null;
                                protected ConfigurationProperty _name = null;
                                protected ConfigurationProperty _find = null;
                                protected ConfigurationProperty _replace = null;
                                protected ConfigurationProperty _value = null;

                                public EnvironmentVariablesElement() {
                                    _properties = new ConfigurationPropertyCollection();

                                    // name not key for multiple find replace operations if so desired
                                    _name = new ConfigurationProperty("name", typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                    _properties.Add(_name);

                                    _find = new ConfigurationProperty("find", typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_find);

                                    /*
                                    _value = new ConfigurationProperty(String.IsNullOrEmpty(Find) ? "value" : "replace",
                                        typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                    _properties.Add(_value);
                                    */

                                    _replace = new ConfigurationProperty("replace",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_replace);

                                    _value = new ConfigurationProperty("value",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_value);
                                }

                                public string Name {
                                    get {
                                        if (String.IsNullOrEmpty(base[_name] as string)) {
                                            return base[_name] as string;
                                        }
                                        return (base[_name] as string).ToUpperInvariant();
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(value)) {
                                            base[_name] = value;
                                            return;
                                        }

                                        base[_name] = value.ToUpperInvariant();
                                    }
                                }
                                
                                public string Find {
                                    get {
                                        return base[_find] as string;
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(value)) {
                                            base[_value] = Value;
                                            base[_replace] = null;
                                        } else {
                                            base[_replace] = Value;
                                            base[_value] = null;
                                        }

                                        base[_find] = value;
                                    }
                                }
                                
                                public string Replace {
                                    get {
                                        return Value;
                                    }

                                    set {
                                        Value = value;
                                    }
                                }
                                
                                public string Value {
                                    get {
                                        if (String.IsNullOrEmpty(Find)) {
                                            return base[_value] as string;
                                        }
                                        return base[_replace] as string;
                                    }

                                    set {
                                        if (String.IsNullOrEmpty(Find)) {
                                            base[_value] = value;
                                            base[_replace] = null;
                                        } else {
                                            base[_replace] = value;
                                            base[_value] = null;
                                        }
                                    }
                                }

                                public string _Key {
                                    get {
                                        return Name + "\\" + Find;
                                    }
                                }

                                protected override ConfigurationPropertyCollection Properties {
                                    get {
                                        return _properties;
                                    }
                                }
                            }

                            protected override object GetElementKey(ConfigurationElement configurationElement) {
                                EnvironmentVariablesElement environmentVariablesElement = configurationElement as EnvironmentVariablesElement;
                                return environmentVariablesElement._Key;
                            }

                            protected override ConfigurationElement CreateNewElement() {
                                return new EnvironmentVariablesElement();
                            }

                            new public ConfigurationElement Get(string name) {
                                name = name.ToUpperInvariant();
                                return base.Get(name);
                            }

                            new public void Remove(string name) {
                                name = name.ToUpperInvariant();
                                base.Remove(name);
                            }
                        }

                        [ConfigurationProperty("environmentVariables", IsRequired = false)]
                        [ConfigurationCollection(typeof(EnvironmentVariablesElementCollection), AddItemName = "environmentVariable")]
                        public EnvironmentVariablesElementCollection EnvironmentVariables {
                            get {
                                return (EnvironmentVariablesElementCollection)base["environmentVariables"];
                            }

                            set {
                                base["environmentVariables"] = value;
                            }
                        }

                        public class DownloadBeforeElementCollection : TemplatesConfigurationElementCollection {
                            public class DownloadBeforeElement : ConfigurationElement {
                                [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
                                public string Name {
                                    get {
                                        return base["name"] as string;
                                    }

                                    set {
                                        base["name"] = value;
                                    }
                                }
                            }

                            protected override object GetElementKey(ConfigurationElement configurationElement) {
                                DownloadBeforeElement downloadBeforeElement = configurationElement as DownloadBeforeElement;
                                return downloadBeforeElement.Name;
                            }

                            protected override ConfigurationElement CreateNewElement() {
                                return new DownloadBeforeElement();
                            }
                        }

                        [ConfigurationProperty("downloadsBefore", IsRequired = false)]
                        [ConfigurationCollection(typeof(DownloadBeforeElementCollection), AddItemName = "downloadBefore")]
                        public DownloadBeforeElementCollection DownloadsBefore {
                            get {
                                return (DownloadBeforeElementCollection)base["downloadsBefore"];
                            }

                            set {
                                base["downloadsBefore"] = value;
                            }
                        }

                        public class RegistryStateElementCollection : TemplatesConfigurationElementCollection {
                            protected ConfigurationPropertyCollection _properties = null;
                            protected ConfigurationProperty _binaryType = null;
                            protected ConfigurationProperty _currentUser = null;
                            protected ConfigurationProperty _administrator = null;

                            public RegistryStateElementCollection() {
                                _properties = new ConfigurationPropertyCollection();

                                _binaryType = new ConfigurationProperty("binaryType",
                                    typeof(BINARY_TYPE), BINARY_TYPE.SCS_64BIT_BINARY, ConfigurationPropertyOptions.IsRequired);
                                _properties.Add(_binaryType);

                                //if (String.IsNullOrEmpty(templateName)) {
                                _currentUser = new ConfigurationProperty("currentUser",
                                    typeof(string), null, ConfigurationPropertyOptions.None);
                                _properties.Add(_currentUser);

                                _administrator = new ConfigurationProperty("administrator",
                                    typeof(bool?), false, ConfigurationPropertyOptions.None);
                                _properties.Add(_administrator);
                                //}
                            }

                            public class RegistryStateElement : ConfigurationElement {
                                protected ConfigurationPropertyCollection _properties = null;
                                protected ConfigurationProperty _type = null;
                                protected ConfigurationProperty _keyName = null;
                                protected ConfigurationProperty _valueName = null;
                                protected ConfigurationProperty _value = null;
                                protected ConfigurationProperty _valueKind = null;
                                protected ConfigurationProperty _deleted = null;
                                protected ConfigurationProperty _valueExpanded = null;

                                public RegistryStateElement() {
                                    _properties = new ConfigurationPropertyCollection();

                                    _type = new ConfigurationProperty("type",
                                        typeof(FlashpointSecurePlayer.RegistryStates.TYPE),
                                        FlashpointSecurePlayer.RegistryStates.TYPE.KEY, ConfigurationPropertyOptions.None);
                                    _properties.Add(_type);

                                    _keyName = new ConfigurationProperty("keyName",
                                        typeof(string), null, ConfigurationPropertyOptions.IsRequired);
                                    _properties.Add(_keyName);

                                    _value = new ConfigurationProperty("value",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_value);

                                    _valueKind = new ConfigurationProperty("valueKind",
                                        typeof(RegistryValueKind?), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_valueKind);

                                    _valueName = new ConfigurationProperty("valueName",
                                        typeof(string), null, ConfigurationPropertyOptions.None);
                                    _properties.Add(_valueName);

                                    //if (String.IsNullOrEmpty(templateName)) {
                                    _deleted = new ConfigurationProperty("deleted",
                                            typeof(string), null, ConfigurationPropertyOptions.None);
                                        _properties.Add(_deleted);

                                        _valueExpanded = new ConfigurationProperty("valueExpanded",
                                            typeof(string), null, ConfigurationPropertyOptions.None);
                                        _properties.Add(_valueExpanded);
                                    //}
                                }
                                
                                public RegistryStates.TYPE Type {
                                    get {
                                        return (RegistryStates.TYPE)base[_type];
                                    }

                                    set {
                                        base[_type] = value;
                                    }
                                }
                                
                                public string KeyName {
                                    get {
                                        return base[_keyName] as string;
                                    }

                                    set {
                                        base[_keyName] = value;
                                    }
                                }
                                
                                public string ValueName {
                                    get {
                                        return base[_valueName] as string;
                                    }

                                    set {
                                        base[_valueName] = value;
                                    }
                                }
                                
                                public string Value {
                                    get {
                                        return base[_value] as string;
                                    }

                                    set {
                                        base[_value] = value;
                                    }
                                }
                                
                                public RegistryValueKind? ValueKind {
                                    get {
                                        if (_valueKind == null) {
                                            return null;
                                        }

                                        return base[_valueKind] as RegistryValueKind?;
                                    }

                                    set {
                                        if (_valueKind == null) {
                                            return;
                                        }

                                        base[_valueKind] = value;
                                    }
                                }
                                
                                public string _Deleted {
                                    get {
                                        if (_deleted == null) {
                                            return null;
                                        }

                                        return base[_deleted] as string;
                                    }

                                    set {
                                        if (_deleted == null) {
                                            return;
                                        }

                                        base[_deleted] = value;
                                    }
                                }
                                
                                public string _ValueExpanded {
                                    get {
                                        if (_valueExpanded == null) {
                                            return null;
                                        }

                                        return base[_valueExpanded] as string;
                                    }

                                    set {
                                        if (_valueExpanded == null) {
                                            return;
                                        }

                                        base[_valueExpanded] = value;
                                    }
                                }

                                public string Name {
                                    get {
                                        string keyName = KeyName;

                                        if (!String.IsNullOrEmpty(keyName)) {
                                            keyName = keyName.ToUpperInvariant();
                                        }

                                        string valueName = ValueName;

                                        if (!String.IsNullOrEmpty(valueName)) {
                                            valueName = valueName.ToUpperInvariant();
                                        }

                                        return keyName + "\\" + valueName;
                                    }
                                }

                                protected override ConfigurationPropertyCollection Properties {
                                    get {
                                        return _properties;
                                    }
                                }
                            }

                            protected override object GetElementKey(ConfigurationElement configurationElement) {
                                RegistryStateElement registryStateElement = configurationElement as RegistryStateElement;
                                return registryStateElement.Name;
                            }

                            protected override ConfigurationElement CreateNewElement() {
                                return new RegistryStateElement();
                            }

                            new public ConfigurationElement Get(string name) {
                                name = name.ToUpperInvariant();
                                return base.Get(name);
                            }

                            new public void Remove(string name) {
                                name = name.ToUpperInvariant();
                                base.Remove(name);
                            }
                            
                            public BINARY_TYPE BinaryType {
                                get {
                                    return (BINARY_TYPE)base[_binaryType];
                                }

                                set {
                                    base[_binaryType] = value;
                                }
                            }

                            public string _CurrentUser {
                                get {
                                    if (_currentUser == null) {
                                        return null;
                                    }

                                    return base[_currentUser] as string;
                                }

                                set {
                                    if (_currentUser == null) {
                                        return;
                                    }

                                    base[_currentUser] = value;
                                }
                            }

                            public bool? _Administrator {
                                get {
                                    if (_administrator == null) {
                                        return null;
                                    }

                                    return base[_administrator] as bool?;
                                }

                                set {
                                    if (_administrator == null) {
                                        return;
                                    }

                                    base[_administrator] = value;
                                }
                            }

                            protected override ConfigurationPropertyCollection Properties {
                                get {
                                    return _properties;
                                }
                            }
                        }

                        [ConfigurationProperty("registryStates", IsRequired = false)]
                        [ConfigurationCollection(typeof(RegistryStateElementCollection), AddItemName = "registryState")]
                        public RegistryStateElementCollection RegistryStates {
                            get {
                                return (RegistryStateElementCollection)base["registryStates"];
                            }

                            set {
                                base["registryStates"] = value;
                            }
                        }

                        public class SingleInstanceElement : ConfigurationElement {
                            [ConfigurationProperty("executable", IsRequired = false)]
                            public string Executable {
                                get {
                                    return base["executable"] as string;
                                }

                                set {
                                    base["executable"] = value;
                                }
                            }

                            [ConfigurationProperty("strict", DefaultValue = false, IsRequired = false)]
                            public bool Strict {
                                get {
                                    return (bool)base["strict"];
                                }

                                set {
                                    base["strict"] = value;
                                }
                            }
                        }

                        [ConfigurationProperty("singleInstance", IsRequired = false)]
                        public SingleInstanceElement SingleInstance {
                            get {
                                return (SingleInstanceElement)base["singleInstance"];
                            }

                            set {
                                base["singleInstance"] = value;
                            }
                        }

                        public class OldCPUSimulatorElement : ConfigurationElement {
                            [ConfigurationProperty("targetRate", IsRequired = false)]
                            public string TargetRate {
                                get {
                                    return base["targetRate"] as string;
                                }

                                set {
                                    base["targetRate"] = value;
                                }
                            }

                            [ConfigurationProperty("refreshRate", IsRequired = false)]
                            public string RefreshRate {
                                get {
                                    return base["refreshRate"] as string;
                                }

                                set {
                                    base["refreshRate"] = value;
                                }
                            }

                            [ConfigurationProperty("setProcessPriorityHigh", DefaultValue = true, IsRequired = false)]
                            public bool SetProcessPriorityHigh {
                                get {
                                    return (bool)base["setProcessPriorityHigh"];
                                }

                                set {
                                    base["setProcessPriorityHigh"] = value;
                                }
                            }

                            [ConfigurationProperty("setSyncedProcessAffinityOne", DefaultValue = true, IsRequired = false)]
                            public bool SetSyncedProcessAffinityOne {
                                get {
                                    return (bool)base["setSyncedProcessAffinityOne"];
                                }

                                set {
                                    base["setSyncedProcessAffinityOne"] = value;
                                }
                            }

                            [ConfigurationProperty("syncedProcessMainThreadOnly", DefaultValue = true, IsRequired = false)]
                            public bool SyncedProcessMainThreadOnly {
                                get {
                                    return (bool)base["syncedProcessMainThreadOnly"];
                                }

                                set {
                                    base["syncedProcessMainThreadOnly"] = value;
                                }
                            }

                            [ConfigurationProperty("refreshRateFloorFifteen", DefaultValue = true, IsRequired = false)]
                            public bool RefreshRateFloorFifteen {
                                get {
                                    return (bool)base["refreshRateFloorFifteen"];
                                }

                                set {
                                    base["refreshRateFloorFifteen"] = value;
                                }
                            }
                        }

                        [ConfigurationProperty("oldCPUSimulator", IsRequired = false)]
                        public OldCPUSimulatorElement OldCPUSimulator {
                            get {
                                return (OldCPUSimulatorElement)base["oldCPUSimulator"];
                            }

                            set {
                                base["oldCPUSimulator"] = value;
                            }
                        }
                    }
                    
                    public ModificationsElement Modifications {
                        get {
                            return base[_modifications] as ModificationsElement;
                        }

                        set {
                            base[_modifications] = value;
                        }
                    }

                    protected override ConfigurationPropertyCollection Properties {
                        get {
                            return _properties;
                        }
                    }
                }

                protected override object GetElementKey(ConfigurationElement configurationElement) {
                    TemplateElement templateElement = configurationElement as TemplateElement;
                    return templateElement.Name;
                }

                protected override ConfigurationElement CreateNewElement() {
                    return new TemplateElement();
                }

                new public TemplateElement Get(string name) {
                    name = name.ToLowerInvariant();
                    return base.Get(name) as TemplateElement;
                }

                new public void Remove(string name) {
                    name = name.ToLowerInvariant();
                    base.Remove(name);
                }
            }

            [ConfigurationProperty("templates", IsDefaultCollection = true, IsRequired = true)]
            [ConfigurationCollection(typeof(TemplatesElementCollection), AddItemName = "template")]
            public TemplatesElementCollection Templates {
                get {
                    return (TemplatesElementCollection)base["templates"];
                }

                set {
                    base["templates"] = value;
                }
            }
        }

        public const string ACTIVE_EXE_CONFIGURATION_NAME = "";

        private static readonly object exeConfigurationNameLock = new object();
        private static string exeConfigurationName = null;

        private static readonly object exeConfigurationLock = new object();
        private static Configuration exeConfiguration = null;

        private static readonly object activeEXEConfigurationLock = new object();
        private static Configuration activeEXEConfiguration = null;

        private static string EXEConfigurationName {
            get {
                lock (exeConfigurationNameLock) {
                    return exeConfigurationName;
                }
            }

            set {
                lock (exeConfigurationNameLock) {
                    exeConfigurationName = value;
                }
            }
        }

        private static Configuration EXEConfiguration {
            get {
                lock (exeConfigurationLock) {
                    return exeConfiguration;
                }
            }

            set {
                lock (exeConfigurationLock) {
                    exeConfiguration = value;
                }
            }
        }

        private static Configuration ActiveEXEConfiguration {
            get {
                lock (activeEXEConfigurationLock) {
                    return activeEXEConfiguration;
                }
            }

            set {
                lock (activeEXEConfigurationLock) {
                    activeEXEConfiguration = value;
                }
            }
        }

        private static int currentProcessId = 0;

        public static int CurrentProcessId {
            get {
                if (currentProcessId == 0) {
                    try {
                        using (Process currentProcess = Process.GetCurrentProcess()) {
                            currentProcessId = currentProcess.Id;
                        }
                    } catch (Exception ex) {
                        Exceptions.LogExceptionToLauncher(ex);
                        throw new InvalidOperationException("Failed to Get Current Process.");
                    }
                }
                return currentProcessId;
            }
        }

        public class PathNames {
            public class PathNamesShort {
                private readonly Dictionary<string, string> pathNamesShort = new Dictionary<string, string>();

                public string this[string longPath] {
                    get {
                        if (!pathNamesShort.ContainsKey(longPath)) {
                            StringBuilder shortPath = new StringBuilder(MAX_PATH);

                            if (GetShortPathName(longPath, shortPath, (uint)shortPath.Capacity) >= shortPath.Capacity) {
                                return null;
                            }

                            string pathNameShort = shortPath.ToString();
                            pathNamesShort[longPath] = pathNameShort;
                            return pathNameShort;
                        }
                        return pathNamesShort[longPath];
                    }
                }
            }

            public class PathNamesLong {
                private readonly Dictionary<string, string> pathNamesLong = new Dictionary<string, string>();

                public string this[string shortPath] {
                    get {
                        if (!pathNamesLong.ContainsKey(shortPath)) {
                            StringBuilder longPath = new StringBuilder(MAX_PATH);

                            if (GetLongPathName(shortPath, longPath, (uint)longPath.Capacity) >= longPath.Capacity) {
                                return null;
                            }

                            string pathNameLong = longPath.ToString();
                            pathNamesLong[shortPath] = pathNameLong;
                            return pathNameLong;
                        }
                        return pathNamesLong[shortPath];
                    }
                }
            }

            public PathNamesShort Short { get; } = new PathNamesShort();
            public PathNamesLong Long { get; } = new PathNamesLong();
        }

        private const string URI_SCHEME_HTTP = "http";
        private const string URI_SCHEME_HTTPS = "https";
        private const string URI_SCHEME_FTP = "ftp";

        public static string GetValidatedURL(string url) {
            if (String.IsNullOrWhiteSpace(url)) {
                return String.Empty;
            }

            url = url.Trim();

            // first try a guessed scheme
            // (for example, guess HTTP for www subdomain, FTP for ftp subdomain...)
            StringBuilder validatedURL = new StringBuilder((int)INTERNET_MAX_URL_LENGTH);
            uint validatedURLCapacity = (uint)validatedURL.Capacity;

            int err = UrlApplyScheme(url, validatedURL, ref validatedURLCapacity, URL_APPLYFlags.URL_APPLY_GUESSSCHEME);

            if (err == S_OK) {
                if (validatedURLCapacity < validatedURL.Capacity) {
                    return validatedURL.ToString();
                }
            }

            // second try the default scheme
            // we only do this to see if it returns S_FALSE, because
            // if so, we know there is already a scheme and don't add our own
            // we do not use the validated URL given by UrlApplyScheme here
            validatedURL.Clear();
            validatedURLCapacity = (uint)validatedURL.Capacity;

            err = UrlApplyScheme(url, validatedURL, ref validatedURLCapacity, URL_APPLYFlags.URL_APPLY_DEFAULT);

            if (err == S_FALSE) {
                return url;
            }

            // workaround: we always want to use HTTP, regardless of the default
            // (in case the default is HTTPS)
            // skip leading slashes for protocol-less URLs
            const string LEADING_SLASHES = "//";

            if (url.StartsWith(LEADING_SLASHES, StringComparison.Ordinal)) {
                url = url.Substring(LEADING_SLASHES.Length);
            }
            return URI_SCHEME_HTTP + "://" + url;
        }

        public static bool TestInternetURI(Uri uri) {
            if (uri.IsFile) {
                return false;
            }

            // the URI Scheme is always lowercase
            string scheme = uri.Scheme;
            return scheme == URI_SCHEME_HTTP || scheme == URI_SCHEME_HTTPS || scheme == URI_SCHEME_FTP;
        }

        /*
        public static StringBuilder GetWindowsVersionName(bool edition = false, bool servicePack = false, bool architecture = false) {
            OperatingSystem operatingSystem = Environment.OSVersion;
            StringBuilder versionName = new StringBuilder("Windows ");

            if (operatingSystem.Platform == PlatformID.Win32Windows) {
                switch (operatingSystem.Version.Minor) {
                    case 0:
                    versionName.Append("95");
                    break;
                    case 10:
                    if (operatingSystem.Version.Revision.ToString() == "2222A") {
                        versionName.Append("98 SE");
                    } else {
                        versionName.Append("98");
                    }
                    break;
                    default:
                    // Windows ME is the last version of Windows before Windows NT
                    versionName.Append("ME");
                    break;
                }
            } else {
                switch (operatingSystem.Version.Major) {
                    case 3:
                    versionName.Append("NT 3.51");
                    break;
                    case 4:
                    versionName.Append("NT 4.0");
                    break;
                    case 5:
                    if (operatingSystem.Version.Minor == 0) {
                        versionName.Append("2000");
                    } else {
                        if (IsOS(OS.OS_ANYSERVER)) {
                            versionName.Append("Server 2003");
                        } else {
                            versionName.Append("XP");
                        }
                    }
                    break;
                    case 6:
                    switch (operatingSystem.Version.Minor) {
                        case 0:
                        if (IsOS(OS.OS_ANYSERVER)) {
                            versionName.Append("Server 2008");
                        } else {
                            versionName.Append("Vista");
                        }
                        break;
                        case 1:
                        if (IsOS(OS.OS_ANYSERVER)) {
                            versionName.Append("Server 2008 R2");
                        } else {
                            versionName.Append("7");
                        }
                        break;
                        case 2:
                        if (IsOS(OS.OS_ANYSERVER)) {
                            versionName.Append("Server 2012");
                        } else {
                            versionName.Append("8");
                        }
                        break;
                        default:
                        if (IsOS(OS.OS_ANYSERVER)) {
                            versionName.Append("Server 2012 R2");
                        } else {
                            versionName.Append("8.1");
                        }
                        break;
                    }
                    break;
                    default:
                    // Windows 10 will be the last version of Windows
                    if (IsOS(OS.OS_ANYSERVER)) {
                        if (operatingSystem.Version.Build < 17763) {
                            versionName.Append("Server 2016");
                        } else if (operatingSystem.Version.Build < 20348) {
                            versionName.Append("Server 2019");
                        } else {
                            versionName.Append("Server 2022");
                        }
                    } else {
                        versionName.Append("10");
                    }
                    break;
                }
            }

            if (edition) {
                string editionID = null;

                try {
                    editionID = Registry.GetValue("HKEY_LOCAL_MACHINE/SOFTWARE/Microsoft/Windows NT/CurrentVersion", "EditionID", null) as string;
                } catch {
                    editionID = null;
                }

                // no way to get the edition before Windows 7
                if (!String.IsNullOrWhiteSpace(editionID)) {
                    versionName.Append(" " + editionID);
                }
            }

            if (servicePack) {
                // can be empty if no service pack is installed
                if (!String.IsNullOrWhiteSpace(operatingSystem.ServicePack)) {
                    versionName.Append(" " + operatingSystem.ServicePack);
                }
            }

            if (architecture) {
                versionName.Append(" " + (Environment.Is64BitOperatingSystem ? "64" : "32") + "-bit");
            }
            return versionName;
        }
        */

        public static void HandleAntecedentTask(Task antecedentTask) {
            if (antecedentTask.IsFaulted) {
                Exception ex = antecedentTask.Exception;

                while (ex is AggregateException && ex.InnerException != null) {
                    ex = ex.InnerException;
                }

                throw ex;
            }
        }

        public static async Task<Uri> DownloadAsync(string name) {
            await downloadSemaphoreSlim.WaitAsync().ConfigureAwait(true);

            try {
                using (HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(name, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(true)) {
                    if (!httpResponseMessage.IsSuccessStatusCode) {
                        throw new Exceptions.DownloadFailedException("The HTTP Response Message failed with a Status Code of \"" + httpResponseMessage.StatusCode + "\".");
                    }

                    using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(true)) {
                        bool currentGoalStarted = false;

                        if (httpResponseMessage.Content.Headers.ContentLength != null) {
                            currentGoalStarted = true;
                            ProgressManager.CurrentGoal.Start((int)Math.Ceiling((double)httpResponseMessage.Content.Headers.ContentLength.GetValueOrDefault() / STREAM_READ_LENGTH));
                        }

                        try {
                            // now we loop through the stream and read it
                            // to the end in chunks
                            // we can't use StreamReader.ReadToEnd because
                            // the result of the function could use too much memory
                            // for large files
                            // temporary buffer so we don't always reallocate this
                            byte[] streamReadBuffer = new byte[STREAM_READ_LENGTH];
                            int characterNumber = 0;

                            do {
                                // if for whatever reason there's a problem
                                // just ignore this download
                                try {
                                    characterNumber = await stream.ReadAsync(streamReadBuffer, 0, STREAM_READ_LENGTH).ConfigureAwait(true);
                                } catch {
                                    break;
                                }

                                if (currentGoalStarted) {
                                    ProgressManager.CurrentGoal.Steps++;
                                }
                            } while (characterNumber > 0);
                        } finally {
                            if (currentGoalStarted) {
                                ProgressManager.CurrentGoal.Stop();
                            }
                        }
                    }
                    return httpResponseMessage.RequestMessage.RequestUri;
                }
            } catch (Exceptions.DownloadFailedException) {
                throw;
            } catch (HttpRequestException ex) {
                Exceptions.LogExceptionToLauncher(ex);
                throw new Exceptions.DownloadFailedException("The download failed because the HTTP Request is invalid.");
            } catch (InvalidOperationException ex) {
                Exceptions.LogExceptionToLauncher(ex);
                throw new Exceptions.DownloadFailedException("The download failed because the address \"" + name + "\" was not understood.");
            } catch (Exception ex) {
                Exceptions.LogExceptionToLauncher(ex);
                throw new Exceptions.DownloadFailedException("The download failed because the download name \"" + name + "\" is invalid.");
            } finally {
                downloadSemaphoreSlim.Release();
            }
        }

        private static string GetValidEXEConfigurationName(string name) {
            if (String.IsNullOrEmpty(name)) {
                throw new ConfigurationErrorsException("Cannot get Valid EXE Configuration Name.");
            }

            string invalidNameCharacters = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex invalidNameCharactersRegex = new Regex("[" + Regex.Escape(invalidNameCharacters) + "]+");
            return invalidNameCharactersRegex.Replace(name, ".").ToLowerInvariant();
        }

        private static Configuration GetActiveEXEConfiguration() {
            // get cached if exists
            if (ActiveEXEConfiguration != null) {
                return ActiveEXEConfiguration;
            }

            try {
                // open from configuration folder
                ActiveEXEConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                if (ActiveEXEConfiguration == null) {
                    throw new ConfigurationErrorsException("The Active EXE Configuration is null.");
                }

                if (!ActiveEXEConfiguration.HasFile) {
                    throw new ConfigurationErrorsException("The Active EXE Configuration has no file.");
                }
                // success!
                return ActiveEXEConfiguration;
            } catch (ConfigurationErrorsException) {
                // fail silently
            }

            if (ActiveEXEConfiguration == null) {
                throw new ConfigurationErrorsException("The Active EXE Configuration is null.");
            }

            // create anew
            ActiveEXEConfiguration.Save(ConfigurationSaveMode.Modified);
            // open the new one
            ActiveEXEConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None) ?? throw new ConfigurationErrorsException("The Active EXE Configuration is null.");
            return ActiveEXEConfiguration;
        }

        // where should ConfigurationErrorsExceptions all be caught? Review this
        private static Configuration GetEXEConfiguration(bool create, string name) {
            // active
            if (String.IsNullOrEmpty(name)) {
                return GetActiveEXEConfiguration();
            }

            name = GetValidEXEConfigurationName(name);

            // caching...
            if (!String.IsNullOrEmpty(name)) {
                if (name.Equals(EXEConfigurationName, StringComparison.OrdinalIgnoreCase)) {
                    return EXEConfiguration;
                }
            }

            // now goal is to set EXEConfiguration...
            ExeConfigurationFileMap exeConfigurationFileMap = new ExeConfigurationFileMap {
                ExeConfigFilename = Application.StartupPath + "\\" + CONFIGURATION_FOLDER_NAME + "\\" + name + ".config"
            };

            Configuration exeConfiguration = null;

            // be careful if modifying this
            try {
                // open from configuration folder
                exeConfiguration = ConfigurationManager.OpenMappedExeConfiguration(exeConfigurationFileMap, ConfigurationUserLevel.None);

                if (exeConfiguration == null) {
                    throw new ConfigurationErrorsException("The EXE Configuration is null.");
                }

                if (!exeConfiguration.HasFile) {
                    throw new ConfigurationErrorsException("The EXE Configuration has no file.");
                }
            } catch (ConfigurationErrorsException ex) {
                Exceptions.LogExceptionToLauncher(ex);

                try {
                    // nope, so open from configuration download
                    EXEConfiguration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap {
                        ExeConfigFilename = Application.StartupPath + "\\" + HTDOCS + "\\" + CONFIGURATION_DOWNLOAD_NAME + "\\" + name + ".config"
                    }, ConfigurationUserLevel.None);

                    if (EXEConfiguration == null) {
                        throw new ConfigurationErrorsException("The downloaded EXE Configuration is null.");
                    }

                    if (!EXEConfiguration.HasFile) {
                        throw new ConfigurationErrorsException("The downloaded EXE Configuration has no file.");
                    }

                    // we got here so success
                    EXEConfigurationName = name;
                    return EXEConfiguration;
                } catch (Exceptions.DownloadFailedException) {
                    // fail silently
                    //throw new ConfigurationErrorsException("The EXE Configuration failed to download.");
                } catch (ConfigurationErrorsException) {
                    // fail silently
                } catch (IOException ex2) {
                    Exceptions.LogExceptionToLauncher(ex2);
                    throw new ConfigurationErrorsException("The EXE Configuration is in use.");
                }
            } catch (IOException ex2) {
                Exceptions.LogExceptionToLauncher(ex2);
                throw new ConfigurationErrorsException("The EXE Configuration is in use.");
            }

            if (exeConfiguration == null) {
                throw new ConfigurationErrorsException("The EXE Configuration is null.");
            }

            if (!create) {
                if (!exeConfiguration.HasFile) {
                    throw new ConfigurationErrorsException("The EXE Configuration has no file.");
                }
            }

            EXEConfiguration = exeConfiguration;
            EXEConfigurationName = name;
            return EXEConfiguration;
        }

        public static FlashpointSecurePlayerSection GetFlashpointSecurePlayerSection(bool create, string exeConfigurationName) {
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = null;
            Configuration exeConfiguration = null;

            // again, be careful if modifying this
            // get the appropriate cached section if it exists
            if (String.IsNullOrEmpty(exeConfigurationName)) {
                if (Shared.activeFlashpointSecurePlayerSection != null) {
                    return Shared.activeFlashpointSecurePlayerSection;
                }

                exeConfiguration = GetActiveEXEConfiguration();
            } else {
                string name = GetValidEXEConfigurationName(exeConfigurationName);

                if (!String.IsNullOrEmpty(name) && Shared.flashpointSecurePlayerSection != null) {
                    if (name.Equals(EXEConfigurationName, StringComparison.OrdinalIgnoreCase)) {
                        return Shared.flashpointSecurePlayerSection;
                    }
                }

                exeConfiguration = GetEXEConfiguration(create, exeConfigurationName);
            }

            ConfigurationErrorsException configurationErrorsException = new ConfigurationErrorsException("The flashpointSecurePlayer Section is null.");

            try {
                // initial attempt
                flashpointSecurePlayerSection = exeConfiguration.GetSection("flashpointSecurePlayer") as FlashpointSecurePlayerSection;
            } catch (ConfigurationErrorsException ex) {
                configurationErrorsException = ex;
            }

            if (flashpointSecurePlayerSection == null) {
                // create the section
                try {
                    exeConfiguration.Sections.Add("flashpointSecurePlayer", new FlashpointSecurePlayerSection());
                } catch {
                    throw configurationErrorsException;
                }

                // reload it into the configuration
                // edit: prevent download lockout
                //exeConfiguration.Save(ConfigurationSaveMode.Modified);
                //ConfigurationManager.RefreshSection("flashpointSecurePlayer");
                // get the section
                flashpointSecurePlayerSection = exeConfiguration.GetSection("flashpointSecurePlayer") as FlashpointSecurePlayerSection;
            }

            if (flashpointSecurePlayerSection == null) {
                // section was not created?
                throw configurationErrorsException;
            }

            // caching...
            if (String.IsNullOrEmpty(exeConfigurationName)) {
                Shared.activeFlashpointSecurePlayerSection = flashpointSecurePlayerSection;
            } else {
                Shared.flashpointSecurePlayerSection = flashpointSecurePlayerSection;
            }
            return flashpointSecurePlayerSection;
        }

        public static void SetFlashpointSecurePlayerSection(string exeConfigurationName) {
            // effectively saving
            Configuration activeEXEConfiguration = GetActiveEXEConfiguration();
            activeEXEConfiguration.Save(ConfigurationSaveMode.Modified);

            if (!String.IsNullOrEmpty(exeConfigurationName)) {
                Configuration exeConfiguration = GetEXEConfiguration(true, exeConfigurationName);
                exeConfiguration.Save(ConfigurationSaveMode.Modified);
            }

            ConfigurationManager.RefreshSection("flashpointSecurePlayer");
        }

        public static async Task DownloadFlashpointSecurePlayerSectionAsync(string name) {
            try {
                // important to use this function particularly - GetEXEConfiguration is for internal use by GetModificationsElement only
                GetFlashpointSecurePlayerSection(false, name);
            } catch {
                try {
                    name = GetValidEXEConfigurationName(name);
                    await DownloadAsync("http://" + CONFIGURATION_DOWNLOAD_NAME + "/" + name + ".config").ConfigureAwait(false);
                } catch {
                    // fail silently
                }
            }
        }

        // does not save!
        public static FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement GetTemplateElement(bool createTemplateElement, string exeConfigurationName) {
            // need the section to operate on
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(createTemplateElement, exeConfigurationName);
            // get the element
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement templateElement = flashpointSecurePlayerSection.Templates.Get(exeConfigurationName) as FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;

            // create it if it doesn't exist...
            if (createTemplateElement && templateElement == null) {
                // ...by new'ing it...
                templateElement = new FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement() {
                    Name = exeConfigurationName
                };

                // and setting it (which does not save!)
                SetTemplateElement(templateElement, exeConfigurationName);
            }
            // exit scene
            return templateElement;
        }

        // does not save!
        public static FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement GetActiveTemplateElement(bool createTemplateElement, string exeConfigurationName = ACTIVE_EXE_CONFIGURATION_NAME) {
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement templateElement = GetTemplateElement(createTemplateElement, ACTIVE_EXE_CONFIGURATION_NAME);

            if (!String.IsNullOrEmpty(exeConfigurationName) && templateElement != null) {
                templateElement.Active = exeConfigurationName;
            }
            return templateElement;
        }

        // does not save!
        public static void SetTemplateElement(FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement templateElement, string exeConfigurationName) {
            // need the section to operate on
            FlashpointSecurePlayerSection flashpointSecurePlayerSection = GetFlashpointSecurePlayerSection(true, exeConfigurationName);
            // set it, and forget it
            // it gets replaced if it existed already
            flashpointSecurePlayerSection.Templates.Set(templateElement);
        }

        public static void LockActiveTemplateElement() {
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

            if (activeTemplateElement == null) {
                return;
            }

            activeTemplateElement.LockItem = true;
        }

        public static void UnlockActiveTemplateElement() {
            FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

            if (activeTemplateElement == null) {
                return;
            }

            activeTemplateElement.LockItem = false;
            //activeTemplateElement.RegistryStates.LockItem = false;
        }

        public static bool TestLaunchedAsAdministratorUser() {
            AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);

            if (!(Thread.CurrentPrincipal is WindowsPrincipal currentPrinciple)) {
                return false;
            }
            return currentPrinciple.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void HideWindow(ref ProcessStartInfo processStartInfo) {
            if (processStartInfo == null) {
                processStartInfo = new ProcessStartInfo();
            }

            processStartInfo.UseShellExecute = false;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.CreateNoWindow = true;
            processStartInfo.ErrorDialog = false;
        }

        public static void RestartApplication(bool runAsAdministrator, Form form, ref Mutex applicationMutex, ProcessStartInfo processStartInfo = null) {
            if (processStartInfo == null) {
                processStartInfo = new ProcessStartInfo {
                    FileName = GetValidArgument(Application.ExecutablePath, true),
                    // can't use GetCommandLineArgs() and String.Join because arguments that were in quotes will lose their quotes
                    // need to use Environment.CommandLine and find arguments
                    Arguments = GetArgumentSliceFromCommandLine(Environment.CommandLine, 1)
                };
            }

            processStartInfo.RedirectStandardError = false;
            processStartInfo.RedirectStandardOutput = false;
            processStartInfo.RedirectStandardInput = false;

            if (runAsAdministrator) {
                processStartInfo.UseShellExecute = true;
                processStartInfo.Verb = "runas";
            }

            if (applicationMutex != null) {
                applicationMutex.ReleaseMutex();
                applicationMutex.Close();
                applicationMutex = null;
            }

            // hide the current form so two windows are not open at once
            // no this is not a race condition
            // http://stackoverflow.com/questions/33042010/in-what-cases-does-the-process-start-method-return-false
            try {
                form.Hide();
                form.ControlBox = true;
                Process.Start(processStartInfo).Dispose();
                Application.Exit();
            } catch (Exception ex) {
                Exceptions.LogExceptionToLauncher(ex);
                form.Show();
                ProgressManager.ShowError();
                MessageBox.Show(Properties.Resources.ProcessUnableToStart, Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                throw new Exceptions.ApplicationRestartRequiredException("The application failed to restart.");
            }
        }

        public static StringBuilder GetCreateProcessCommandLine(ProcessStartInfo processStartInfo) {
            if (processStartInfo == null) {
                throw new ArgumentNullException("processStartInfo must not be null.");
            }

            string fileName = processStartInfo.FileName.Trim();

            string quotes = "\"";

            if (fileName.StartsWith(quotes, StringComparison.Ordinal)
                && fileName.EndsWith(quotes, StringComparison.Ordinal)) {
                quotes = String.Empty;
            }

            StringBuilder commandLine = new StringBuilder(quotes);
            commandLine.Append(fileName);
            commandLine.Append(quotes);

            string arguments = processStartInfo.Arguments;

            if (!String.IsNullOrEmpty(arguments)) {
                commandLine.Append(" ");
                commandLine.Append(arguments);
            }
            return commandLine;
        }

        public static void StartProcessCreateBreakawayFromJob(ProcessStartInfo processStartInfo, out Process process) {
            process = null;

            if (processStartInfo == null) {
                throw new ArgumentNullException("processStartInfo must not be null.");
            }

            if (processStartInfo.UseShellExecute) {
                throw new ArgumentException("UseShellExecute must be false.");
            }

            if (processStartInfo.RedirectStandardError
                || processStartInfo.RedirectStandardOutput
                || processStartInfo.RedirectStandardInput) {
                throw new ArgumentException("RedirectStandardError, RedirectStandardOutput, and RedirectStandardInput must be false.");
            }

            string commandLine = GetCreateProcessCommandLine(processStartInfo).ToString();

            CreationFlags creationFlags = CreationFlags.CREATE_BREAKAWAY_FROM_JOB
                | CreationFlags.CREATE_SUSPENDED;

            if (processStartInfo.CreateNoWindow) {
                creationFlags |= CreationFlags.CREATE_NO_WINDOW;
            }

            string currentDirectory = processStartInfo.WorkingDirectory;

            if (String.IsNullOrEmpty(currentDirectory)) {
                currentDirectory = Environment.CurrentDirectory;
            }

            STARTUPINFO startupInfo = new STARTUPINFO();
            PROCESS_INFORMATION processInformation = new PROCESS_INFORMATION();

            if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, creationFlags, IntPtr.Zero, currentDirectory, ref startupInfo, out processInformation)) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                return;
            }

            try {
                process = Process.GetProcessById((int)processInformation.dwProcessId);

                if (process == null) {
                    throw new InvalidOperationException("process is null.");
                }

                try {
                    ProcessSync.Start(process);

                    if (ResumeThread(processInformation.hThread) == -1) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return;
                    }
                } catch {
                    process.Kill();
                    process.Dispose();
                    process = null;
                    throw;
                }
            } finally {
                if (!CloseHandle(processInformation.hProcess)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                if (!CloseHandle(processInformation.hThread)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
        }

        public static BINARY_TYPE GetLibraryBinaryType(string libFileName) {
            IntPtr moduleHandle = IntPtr.Zero;

            moduleHandle = LoadLibraryEx(libFileName, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE);

            if (moduleHandle == IntPtr.Zero) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                return BINARY_TYPE.SCS_64BIT_BINARY;
            }

            try {
                IntPtr imageDOSHeaderPointer = moduleHandle;

                if (imageDOSHeaderPointer == IntPtr.Zero) {
                    throw new Exceptions.LibraryBinaryFormatException("imageDOSHeaderPointer must not be NULL");
                }

                IMAGE_DOS_HEADER imageDOSHeader = (IMAGE_DOS_HEADER)Marshal.PtrToStructure(imageDOSHeaderPointer, typeof(IMAGE_DOS_HEADER));

                if (imageDOSHeader.e_magic != IMAGE_DOS_SIGNATURE) {
                    throw new Exceptions.LibraryBinaryFormatException("e_magic must be IMAGE_DOS_SIGNATURE");
                }

                IntPtr imageNTHeadersPointer = IntPtr.Zero;

                try {
                    imageNTHeadersPointer = ImageNtHeader(moduleHandle);

                    if (imageNTHeadersPointer == IntPtr.Zero) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return BINARY_TYPE.SCS_64BIT_BINARY;
                    }
                } catch {
                    throw new Exceptions.LibraryBinaryFormatException("imageNTHeadersPointer must not be NULL");
                }

                IMAGE_NT_HEADERS imageNTHeaders = (IMAGE_NT_HEADERS)Marshal.PtrToStructure(imageNTHeadersPointer, typeof(IMAGE_NT_HEADERS));

                if (imageNTHeaders.Signature != IMAGE_NT_SIGNATURE) {
                    throw new Exceptions.LibraryBinaryFormatException("Signature must be IMAGE_NT_SIGNATURE");
                }

                if (imageNTHeaders.FileHeader.Machine == IMAGE_FILE_MACHINE_I386) {
                    return BINARY_TYPE.SCS_32BIT_BINARY;
                }
            } finally {
                if (!FreeLibrary(moduleHandle)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
            return BINARY_TYPE.SCS_64BIT_BINARY;
        }

        public static bool ComparePaths(string path, string path2) {
            using (SafeFileHandle safeFileHandle = CreateFile(path, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileFlagsAndAttributes.BackupSemantics, IntPtr.Zero)) {
                if (safeFileHandle.IsInvalid) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    return false;
                }

                using (SafeFileHandle safeFileHandle2 = CreateFile(path2, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileFlagsAndAttributes.BackupSemantics, IntPtr.Zero)) {
                    if (safeFileHandle2.IsInvalid) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return false;
                    }

                    if (!GetFileInformationByHandle(safeFileHandle, out BY_HANDLE_FILE_INFORMATION byHandleFileInformation)) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return false;
                    }

                    if (!GetFileInformationByHandle(safeFileHandle2, out BY_HANDLE_FILE_INFORMATION byHandleFileInformation2)) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return false;
                    }

                    return byHandleFileInformation.VolumeSerialNumber == byHandleFileInformation2.VolumeSerialNumber
                        && byHandleFileInformation.FileIndexHigh == byHandleFileInformation2.FileIndexHigh
                        && byHandleFileInformation.FileIndexLow == byHandleFileInformation2.FileIndexLow;
                }
            }
        }

        /*
        public static string AddLeadingSlash(string path) {
            // can be empty, but not null
            if (path == null) {
                return path;
            }

            const string SLASH = "\\";

            if (!path.StartsWith(SLASH, StringComparison.Ordinal)) {
                path = path.Insert(0, SLASH);
            }
            return path;
        }

        public static string RemoveLeadingSlash(string path) {
            // can be empty, but not null
            if (path == null) {
                return path;
            }

            const string SLASH = "\\";

            while (path.StartsWith(SLASH, StringComparison.Ordinal)) {
                path = path.Substring(SLASH.Length);
            }
            return path;
            //return path.TrimStart('\\');
        }
        */

        public static string AddTrailingSlash(string path) {
            // can be empty, but not null
            if (path == null) {
                return path;
            }

            const string SLASH = "\\";

            if (!path.EndsWith(SLASH, StringComparison.Ordinal)) {
                path = path.Insert(path.Length, SLASH);
            }
            return path;
        }

        public static string RemoveTrailingSlash(string path) {
            // can be empty, but not null
            if (path == null) {
                return path;
            }

            const string SLASH = "\\";

            while (path.EndsWith(SLASH, StringComparison.Ordinal)) {
                path = path.Remove(path.Length - SLASH.Length);
            }
            return path;
            //return path.TrimEnd('\\');
        }

        // pathNames is intentionally not ref
        // it's a class, so it's already a reference type
        // it can be null, which is supported,
        // but it is intentionally not an optional argument, because
        // you should pass it in if you have one
        public static string LengthenValue(string value, string path, PathNames pathNames) {
            if (value == null) {
                return value;
            }

            if (value.Length > MAX_PATH + MAX_PATH + FP_STARTUP_PATH.Length) {
                return value;
            }

            if (String.IsNullOrEmpty(path)) {
                return value;
            }

            if (pathNames == null) {
                pathNames = new PathNames();
            }

            // get the short path
            string shortPathName = pathNames.Short[path];

            if (String.IsNullOrEmpty(shortPathName)) {
                return value;
            }

            // if the value is a short value...
            if (!value.StartsWith(shortPathName, StringComparison.OrdinalIgnoreCase)) {
                return value;
            }

            // get the long path
            string longPathName = pathNames.Long[path];

            if (String.IsNullOrEmpty(longPathName)) {
                return value;
            }
            // replace the short path with the long path
            return longPathName + value.Substring(shortPathName.Length);
        }

        // find path in registry value
        // string must begin with path
        public static string ReplaceStartupPathEnvironmentVariable(string lengthenedValue, PathNames pathNames) {
            if (lengthenedValue == null) {
                return lengthenedValue;
            }

            if (lengthenedValue.Length > MAX_PATH + MAX_PATH + FP_STARTUP_PATH.Length) {
                return lengthenedValue;
            }

            if (pathNames == null) {
                pathNames = new PathNames();
            }

            string longStartupPathName = pathNames.Long[Application.StartupPath];

            if (String.IsNullOrEmpty(longStartupPathName)) {
                return lengthenedValue;
            }

            longStartupPathName = RemoveTrailingSlash(longStartupPathName);

            if (lengthenedValue.Equals(longStartupPathName, StringComparison.OrdinalIgnoreCase)) {
                return "%" + FP_STARTUP_PATH + "%";
            }

            longStartupPathName = AddTrailingSlash(longStartupPathName);

            if (lengthenedValue.StartsWith(longStartupPathName, StringComparison.OrdinalIgnoreCase)) {
                return "%" + FP_STARTUP_PATH + "%\\" + lengthenedValue.Substring(longStartupPathName.Length);
            }
            return lengthenedValue;
        }
        
        public static Process GetParentProcess() {
            IntPtr parentProcessSnapshotHandle = CreateToolhelp32Snapshot(TH32CSFlags.TH32CS_SNAPPROCESS, 0);

            if (parentProcessSnapshotHandle == IntPtr.Zero) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                return null;
            }

            try {
                PROCESSENTRY32 parentProcessEntry = new PROCESSENTRY32 {
                    dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32))
                };

                int lastError = 0;

                if (!Process32First(parentProcessSnapshotHandle, ref parentProcessEntry)) {
                    lastError = Marshal.GetHRForLastWin32Error();

                    if (lastError != ERROR_SUCCESS && lastError != ERROR_NO_MORE_FILES) {
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                        return null;
                    }
                }

                int currentProcessID = CurrentProcessId;
                int parentProcessID = 0;

                do {
                    if (currentProcessID == parentProcessEntry.th32ProcessID) {
                        parentProcessID = (int)parentProcessEntry.th32ParentProcessID;

                        if (parentProcessID == 0) {
                            return null;
                        }
                        return Process.GetProcessById(parentProcessID);
                    }
                } while (Process32Next(parentProcessSnapshotHandle, ref parentProcessEntry));

                lastError = Marshal.GetHRForLastWin32Error();

                if (lastError != ERROR_SUCCESS && lastError != ERROR_NO_MORE_FILES) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                    return null;
                }
            } finally {
                if (!CloseHandle(parentProcessSnapshotHandle)) {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
            }
            return null;
        }

        public static StringBuilder GetProcessName(Process process) {
            bool queryResult = false;
            int size = MAX_PATH;
            StringBuilder processName = new StringBuilder(size);

            try {
                string processMainModuleFileName = process.MainModule.FileName;
                queryResult = true;
                processName = new StringBuilder(processMainModuleFileName);
            } catch {
                // fallback if the standard way fails
                queryResult = QueryFullProcessImageName(process.Handle, 0, processName, ref size);
            }

            if (!queryResult) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                return processName;
            }
            return processName;
        }

        public static string[] GetCommandLineToArgv(string commandLine, out int argc) {
            argc = 0;
            string[] argv = null;

            IntPtr argvPointer = CommandLineToArgv(commandLine, out argc);

            if (argvPointer == IntPtr.Zero) {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                return argv;
            }

            try {
                argv = new string[argc];

                for (int i = 0; i < argc; i++) {
                    argv[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argvPointer, i * IntPtr.Size));
                }
            } finally {
                LocalFree(argvPointer);
            }
            return argv;
        }

        // http://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/
        public static string GetValidArgument(string argument, bool force = false) {
            if (argument == null) {
                argument = String.Empty;
            }

            if (!force && argument != String.Empty && argument.IndexOfAny(new char[]{ ' ', '\t', '\n', '\v', '\"' }) == -1) {
                return argument;
            }

            int backslashes = 0;
            StringBuilder validArgument = new StringBuilder("\"");

            for (int i = 0; i < argument.Length; i++) {
                backslashes = 0;

                while (i != argument.Length && argument[i].ToString().Equals("\\", StringComparison.Ordinal)) {
                    backslashes++;
                    i++;
                }

                if (i != argument.Length) {
                    if (argument[i].ToString().Equals("\"", StringComparison.Ordinal)) {
                        validArgument.Append('\\', backslashes + backslashes + 1);
                    } else {
                        validArgument.Append('\\', backslashes);
                    }

                    validArgument.Append(argument[i]);
                }
            }

            validArgument.Append('\\', backslashes + backslashes);
            validArgument.Append("\"");
            return validArgument.ToString();
        }

        public static string GetArgumentSliceFromCommandLine(string commandLine, int begin = 0, int end = -1) {
            List<string> arguments = new List<string>();

            {
                Regex commandLineArguments = new Regex("^\\s*(?:\"[^\"\\\\]*(?:\\\\.[^\"\\\\]*)*\"?|(?:[^\"\\\\\\s]+|\\\\\\S)+|\\\\|\\s+$)+\\s?");
                Match match = commandLineArguments.Match(commandLine);

                while (match.Success) {
                    arguments.Add(match.Value);
                    commandLine = commandLine.Substring(match.Length);
                    match = commandLineArguments.Match(commandLine);
                }
            }

            int argumentsCount = arguments.Count + 1;

            if (begin < 0) {
                begin += argumentsCount;
            }

            begin = Math.Max(begin, 0);

            if (end < 0) {
                end += argumentsCount;
            }

            end = Math.Min(end, argumentsCount - 1);

            string argumentSlice = String.Empty;

            for (int i = begin; i < end; i++) {
                argumentSlice += arguments[i];
            }
            return argumentSlice;
        }

        public static StringBuilder GetOldCPUSimulatorProcessStartInfoArguments(FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.OldCPUSimulatorElement oldCPUSimulatorElement, StringBuilder software) {
            StringBuilder oldCPUSimulatorProcessStartInfoArguments = new StringBuilder("-t ");

            if (!int.TryParse(Environment.ExpandEnvironmentVariables(oldCPUSimulatorElement.TargetRate), out int targetRate)) {
                throw new ArgumentException("The Old CPU Simulator Element has an invalid Target Rate.");
            }

            oldCPUSimulatorProcessStartInfoArguments.Append(targetRate);

            if (!String.IsNullOrEmpty(oldCPUSimulatorElement.RefreshRate)) {
                if (!int.TryParse(Environment.ExpandEnvironmentVariables(oldCPUSimulatorElement.RefreshRate), out int refreshRate)) {
                    throw new ArgumentException("The Old CPU Simulator Element has an invalid Refresh Rate.");
                }

                oldCPUSimulatorProcessStartInfoArguments.Append(" -r ");
                oldCPUSimulatorProcessStartInfoArguments.Append(refreshRate);
            }

            if (oldCPUSimulatorElement.SetProcessPriorityHigh) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" -ph");
            }

            if (oldCPUSimulatorElement.SetSyncedProcessAffinityOne) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" -a1");
            }

            if (oldCPUSimulatorElement.SyncedProcessMainThreadOnly) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" -mt");
            }

            if (oldCPUSimulatorElement.RefreshRateFloorFifteen) {
                oldCPUSimulatorProcessStartInfoArguments.Append(" -rf");
            }

            oldCPUSimulatorProcessStartInfoArguments.Append(" -sw ");
            oldCPUSimulatorProcessStartInfoArguments.Append(software);
            return oldCPUSimulatorProcessStartInfoArguments;
        }
    }
}
