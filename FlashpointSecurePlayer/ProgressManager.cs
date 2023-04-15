using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    public static class ProgressManager {
        public const uint PBM_SETSTATE = 0x0410;
        public static readonly IntPtr PBST_NORMAL = (IntPtr)1;
        public static readonly IntPtr PBST_ERROR = (IntPtr)2;
        public static readonly IntPtr PBST_PAUSED = (IntPtr)3;

        public enum TaskbarProgressBarState {
            NoProgress = 0,
            Indeterminate = 1,
            Normal = 2,
            Error = 4,
            Paused = 8
        }

        [ComImport, Guid("56FDF344-FD6D-11D0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
        private class ITaskbarList { }

        [ComImport, Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3 {
            // ITaskbarList
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            void MarkFullscreenWindow(
                IntPtr hwnd,

                [MarshalAs(UnmanagedType.Bool)]
                bool fFullscreen
            );

            // ITaskbarList3
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            void SetProgressState(IntPtr hwnd, TaskbarProgressBarState tbpFlags);
        }

        private static readonly ITaskbarList3 taskbarList = (ITaskbarList3)new ITaskbarList();
        private static readonly bool taskbarListSupported = Environment.OSVersion.Version >= new Version(6, 1);
        private static bool taskbarListInitialized = false;

        const int PROGRESS_FORM_VALUE_COMPLETE = 100;

        private static ProgressBar progressBar = null;
        private static Form progressForm = null;
        private static ulong progressFormValue = 0;
        private static TaskbarProgressBarState progressFormState = TaskbarProgressBarState.Normal;
        private static ProgressBarStyle style = ProgressBarStyle.Marquee;
        private static int value = 0;
        private static IntPtr state = PBST_NORMAL;

        // class to update the progress bar automatically
        // the idea is that you call Start when you
        // start a new goal (be it downloading a file,
        // backing up the registry, etc.) and call Stop when
        // you're stopping, with the Steps variable keeping
        // track of how close you are to done. Truthfully,
        // this could be implemented without starting/stopping,
        // but implementing it this way allows correcting
        // for errors where not all the steps were done
        public static class CurrentGoal {
            private class Goal {
                private int size = 1;
                private int steps = 0;

                private readonly object timeoutLock = new object();
                private bool timeout = false;

                private System.Timers.Timer timer;

                public int Size {
                    get {
                        return size;
                    }

                    set {
                        // new size must not be less than old size
                        if (value < size) {
                            value = size;
                        }

                        // new size must not be less than steps
                        if (value < Steps) {
                            value = Steps;
                        }

                        size = value;
                    }
                }

                public int Steps {
                    get {
                        return steps;
                    }

                    set {
                        // new steps must not be less than old steps
                        if (value < steps) {
                            value = steps;
                        }

                        // new steps must not be greater than size
                        if (value > Size) {
                            value = Size;
                        }

                        steps = value;

                        if (Timeout) {
                            Environment.Exit(0);
                            throw new TimeoutException("The goal has timed-out.");
                        }
                    }
                }

                public uint Time { get; set; }

                private bool Timeout {
                    get {
                        lock (timeoutLock) {
                            return timeout;
                        }
                    }

                    set {
                        lock (timeoutLock) {
                            timeout = value;
                        }
                    }
                }

                public Goal(int size = 1, uint time = 0) {
                    Size = size;
                    Time = time;

                    if (Time > 0) {
                        timer = new System.Timers.Timer(Time);
                        timer.AutoReset = false;
                        timer.Elapsed += timer_Elapsed;
                        timer.Start();
                    }
                }

                private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
                    timer.Stop();
                    timer.Elapsed -= timer_Elapsed;
                    timer.Close();
                    timer = null;

                    Timeout = true;
                }
            }

            private static Stack<Goal> Goals = new Stack<Goal>();

            public static int Size {
                get {
                    if (!Goals.Any()) {
                        return 1;
                    }

                    return Goals.Peek().Size;
                }

                set {
                    if (!Goals.Any()) {
                        return;
                    }

                    Goals.Peek().Size = value;
                }
            }

            public static int Steps {
                get {
                    if (!Goals.Any()) {
                        return 0;
                    }
                    return Goals.Peek().Steps;
                }

                set {
                    if (!Goals.Any()) {
                        return;
                    }

                    Goal goal = Goals.Peek();

                    if (value == goal.Size) {
                        // can't be true unless calling Stop
                        return;
                    }

                    goal.Steps = value;
                    Show();
                }
            }

            private static void Show() {
                Goal[] goalsArray = Goals.ToArray();

                if (goalsArray.ElementAtOrDefault(0) == null) {
                    return;
                }

                int size = 0;
                double reciprocal = 1;
                double multiplier = 0;

                for (int i = 0; i < goalsArray.Length; i++) {
                    for (int j = 0; j < i; j++) {
                        size = goalsArray[j].Size;

                        if (size == 0) {
                            break;
                        }

                        reciprocal *= (double)1 / size;
                    }

                    size = goalsArray[i].Size;

                    if (size == 0) {
                        break;
                    }

                    multiplier += reciprocal * ((double)goalsArray[i].Steps / size);
                    reciprocal = 1;
                }

                int progressManagerValue = (int)Math.Floor(multiplier * 100.0);

                if (progressManagerValue < ProgressManager.Value) {
                    return;
                }

                ProgressManager.Value = progressManagerValue;
            }

            public static void Start(int size = 1, uint time = 0) {
                if (size <= 0) {
                    // it needs to be able to push invalid length goals
                    // so it doesn't pop a previous goal when stopped
                    //return;
                    size = 1;
                }

                if (!Goals.Any()) {
                    ProgressManager.Reset();
                }

                Goals.Push(new Goal(size, time));
            }

            public static void Stop() {
                if (!Goals.Any()) {
                    return;
                }

                Goal goal = Goals.Peek();
                goal.Steps = goal.Size;

                try {
                    Show();
                } finally {
                    Goals.Pop();
                }
            }
        }

        public static ProgressBar ProgressBar {
            get {
                return progressBar;
            }

            set {
                progressBar = value;

                if (progressBar == null) {
                    return;
                }

                ProgressBarStyle = ProgressManager.style;
                ProgressBarValue = ProgressManager.value;
                ProgressBarState = ProgressManager.state;
            }
        }

        private static ProgressBarStyle ProgressBarStyle {
            /*
            get {
                if (ProgressBar == null) {
                    return ProgressManager.style;
                }
                return ProgressBar.Style;
            }
            */

            set {
                if (ProgressBar == null) {
                    return;
                }

                ProgressBar.Style = value;
            }
        }

        private static int ProgressBarValue {
            /*
            get {
                if (ProgressBar == null) {
                    return ProgressManager.value;
                }
                return ProgressBar.Value;
            }
            */

            set {
                if (ProgressBar == null) {
                    return;
                }

                ProgressBar.Value = value;
            }
        }

        private static IntPtr ProgressBarState {
            /*
            get {
                return ProgressManager.state;
            }
            */

            set {
                if (ProgressBar == null) {
                    return;
                }

                if (!ProgressBar.IsHandleCreated || ProgressBar.Handle == IntPtr.Zero) {
                    return;
                }

                SendMessage(ProgressBar.Handle, PBM_SETSTATE, value, IntPtr.Zero);
            }
        }

        public static Form ProgressForm {
            get {
                return progressForm;
            }

            set {
                progressForm = value;

                if (progressForm == null) {
                    return;
                }

                if (taskbarListSupported && !taskbarListInitialized) {
                    try {
                        taskbarList.HrInit();
                        taskbarListInitialized = true;
                    } catch (Exception ex) {
                        LogExceptionToLauncher(ex);
                    }
                }

                ProgressFormStyle = ProgressManager.style;
                ProgressFormValue = ProgressManager.value;
                ProgressFormState = ProgressManager.state;
            }
        }

        private static ProgressBarStyle ProgressFormStyle {
            /*
            get {
                return ProgressManager.progressFormState == TaskbarProgressBarState.Indeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            }
            */

            set {
                if (ProgressManager.progressFormValue >= PROGRESS_FORM_VALUE_COMPLETE) {
                    return;
                }

                if (value != ProgressBarStyle.Marquee) {
                    ProgressFormValue = ProgressManager.value;
                    ProgressFormState = ProgressManager.state;
                    return;
                }

                // normal state does not take priority over indeterminate state
                if (ProgressManager.progressFormState != TaskbarProgressBarState.Normal) {
                    return;
                }

                ProgressManager.progressFormState = TaskbarProgressBarState.Indeterminate;

                if (ProgressForm == null) {
                    return;
                }

                if (!ProgressForm.IsHandleCreated || ProgressForm.Handle == IntPtr.Zero) {
                    return;
                }

                if (!taskbarListInitialized) {
                    return;
                }

                taskbarList.SetProgressState(ProgressForm.Handle, ProgressManager.progressFormState);
            }
        }

        private static int ProgressFormValue {
            /*
            get {
                return (int)ProgressManager.progressFormValue;
            }
            */

            set {
                ProgressManager.progressFormValue = (ulong)value;

                bool completed = false;

                if (ProgressManager.progressFormValue >= PROGRESS_FORM_VALUE_COMPLETE) {
                    completed = true;
                }

                if (completed) {
                    // if we have completed, ignore the value in the no progress state
                    if (ProgressManager.progressFormState == TaskbarProgressBarState.NoProgress) {
                        return;
                    }

                    ProgressManager.progressFormState = TaskbarProgressBarState.NoProgress;
                } else {
                    // if we haven't completed, ignore the value in the indeterminate state
                    if (ProgressManager.progressFormState == TaskbarProgressBarState.Indeterminate) {
                        return;
                    }
                }

                if (ProgressForm == null) {
                    return;
                }

                if (!ProgressForm.IsHandleCreated || ProgressForm.Handle == IntPtr.Zero) {
                    return;
                }

                if (!taskbarListInitialized) {
                    return;
                }

                taskbarList.SetProgressValue(ProgressForm.Handle, ProgressManager.progressFormValue, PROGRESS_FORM_VALUE_COMPLETE);

                // it is required to set the state to No Progress when completed
                if (completed) {
                    taskbarList.SetProgressState(ProgressForm.Handle, ProgressManager.progressFormState);
                    return;
                }

                // if we previously completed, update the state
                if (ProgressManager.progressFormState == TaskbarProgressBarState.NoProgress) {
                    ProgressFormState = ProgressManager.state;
                }
            }
        }

        private static IntPtr ProgressFormState {
            /*
            get {
                if (ProgressManager.progressFormState == TaskbarProgressBarState.Error) {
                    return PBST_ERROR;
                } else if (ProgressManager.progressFormState == TaskbarProgressBarState.Paused) {
                    return PBST_PAUSED;
                } else {
                    return PBST_NORMAL;
                }
            }
            */

            set {
                if (ProgressManager.progressFormValue >= PROGRESS_FORM_VALUE_COMPLETE) {
                    return;
                }

                if (value == PBST_ERROR) {
                    if (ProgressManager.progressFormState == TaskbarProgressBarState.Error) {
                        return;
                    }

                    ProgressManager.progressFormState = TaskbarProgressBarState.Error;
                } else if (value == PBST_PAUSED) {
                    if (ProgressManager.progressFormState == TaskbarProgressBarState.Paused) {
                        return;
                    }

                    ProgressManager.progressFormState = TaskbarProgressBarState.Paused;
                } else {
                    if (ProgressManager.progressFormState == TaskbarProgressBarState.Normal) {
                        return;
                    }

                    // normal state does not take priority over indeterminate state
                    if (ProgressManager.progressFormState == TaskbarProgressBarState.Indeterminate) {
                        return;
                    }

                    ProgressManager.progressFormState = TaskbarProgressBarState.Normal;
                }

                if (ProgressForm == null) {
                    return;
                }

                if (!ProgressForm.IsHandleCreated || ProgressForm.Handle == IntPtr.Zero) {
                    return;
                }

                if (!taskbarListInitialized) {
                    return;
                }

                taskbarList.SetProgressState(ProgressForm.Handle, ProgressManager.progressFormState);
            }
        }

        private static ProgressBarStyle Style {
            get {
                return ProgressManager.style;
            }

            set {
                ProgressManager.style = value;

                ProgressBarStyle = ProgressManager.style;
                ProgressFormStyle = ProgressManager.style;
            }
        }

        private static int Value {
            get {
                return ProgressManager.value;
            }

            set {
                ProgressManager.value = Math.Min(100, Math.Max(0, value));

                ProgressBarValue = ProgressManager.value;
                ProgressFormValue = ProgressManager.value;
            }
        }

        private static IntPtr State {
            get {
                return ProgressManager.state;
            }

            set {
                ProgressManager.state = value;

                ProgressBarState = ProgressManager.state;
                ProgressFormState = ProgressManager.state;
            }
        }

        public static void Reset() {
            Style = ProgressBarStyle.Blocks;
            Value = 0;

            Style = ProgressBarStyle.Continuous;
            State = PBST_NORMAL;
        }

        public static void ShowOutput() {
            Style = ProgressBarStyle.Continuous;
            State = PBST_NORMAL;
        }

        public static void ShowError() {
            Style = ProgressBarStyle.Continuous;
            Value = 100;

            State = PBST_ERROR;
        }
    }
}
