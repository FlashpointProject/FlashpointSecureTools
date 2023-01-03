using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    public static class ProcessSync {
        [StructLayout(LayoutKind.Sequential)]
        private struct IOCounters {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        private enum JOBOBJECTINFOCLASS {
            JobObjectAssociateCompletionPortInformation = 7,
            JobObjectBasicLimitInformation = 2,
            JobObjectBasicUIRestrictions = 4,
            JobObjectCpuRateControlInformation = 15,
            JobObjectEndOfJobTimeInformation = 6,
            JobObjectExtendedLimitInformation = 9,
            JobObjectGroupInformation = 11,
            JobObjectGroupInformationEx = 14,
            JobObjectLimitViolationInformation2 = 35,
            JobObjectNetRateControlInformation = 32,
            JobObjectNotificationLimitInformation = 12,
            JobObjectNotificationLimitInformation2 = 34,
            JobObjectSecurityLimitInformation = 5
        }

        [Flags]
        private enum JOB_OBJECT_LIMITFlags : uint {
            JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x00000008,
            JOB_OBJECT_LIMIT_AFFINITY = 0x00000010,
            JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800,
            JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00000400,
            JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200,
            JOB_OBJECT_LIMIT_JOB_TIME = 0x00000004,
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000,
            JOB_OBJECT_LIMIT_PRESERVE_JOB_TIME = 0x00000040,
            JOB_OBJECT_LIMIT_PRIORITY_CLASS = 0x00000020,
            JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100,
            JOB_OBJECT_LIMIT_PROCESS_TIME = 0x00000002,
            JOB_OBJECT_LIMIT_SCHEDULING_CLASS = 0x00000080,
            JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000,
            JOB_OBJECT_LIMIT_SUBSET_AFFINITY = 0x00004000,
            JOB_OBJECT_LIMIT_WORKINGSET = 0x00000001
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public JOB_OBJECT_LIMITFlags LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public long Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IOCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [DllImport("KERNEL32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateJobObject(
            IntPtr jobAttributesPointer,

            [MarshalAs(UnmanagedType.LPTStr)]
            string name
        );

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr job, JOBOBJECTINFOCLASS jobObjectInformationClass, IntPtr jobObjectInfoPointer, uint jobObjectInfoLengthSize);

        [DllImport("KERNEL32.DLL", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        private static IntPtr jobHandle = IntPtr.Zero;

        private static bool Started { get; set; } = false;

        public static void Start(Process process = null) {
            // this is here to make it so if the player crashes
            // the process it started is killed along with it
            if (Started) {
                return;
            }

            if (jobHandle == IntPtr.Zero) {
                jobHandle = CreateJobObject(IntPtr.Zero, null);

                if (jobHandle == IntPtr.Zero) {
                    throw new JobObjectException("Could not create the Job Object.");
                }
            }

            JOBOBJECT_BASIC_LIMIT_INFORMATION jobobjectBasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION {
                LimitFlags = JOB_OBJECT_LIMITFlags.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            };

            JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobobjectExtendedLimitInformation = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
                BasicLimitInformation = jobobjectBasicLimitInformation
            };

            int jobobjectExtendedLimitInformationSize = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr jobobjectExtendedLimitInformationPointer = Marshal.AllocHGlobal(jobobjectExtendedLimitInformationSize);

            Marshal.StructureToPtr(jobobjectExtendedLimitInformation, jobobjectExtendedLimitInformationPointer, false);

            bool result = SetInformationJobObject(jobHandle, JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, jobobjectExtendedLimitInformationPointer, (uint)jobobjectExtendedLimitInformationSize);

            Marshal.FreeHGlobal(jobobjectExtendedLimitInformationPointer);

            if (process == null) {
                process = Process.GetCurrentProcess();
            }

            if (!result || !AssignProcessToJobObject(jobHandle, process.Handle)) {
                throw new JobObjectException("Could not set the Job Object Information or assign the Process to the Job Object.");
            }

            Started = true;
        }
    }
}
