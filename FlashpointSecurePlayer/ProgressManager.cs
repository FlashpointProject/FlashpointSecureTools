using System;
using System.Collections.Generic;
using System.Drawing;
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

        private enum TBPF {
            TBPF_NOPROGRESS = 0x00000000,
            TBPF_INDETERMINATE = 0x00000001,
            TBPF_NORMAL = 0x00000002,
            TBPF_ERROR = 0x00000004,
            TBPF_PAUSED = 0x00000008
        }

        private enum TBATF {
            TBATF_USEMDITHUMBNAIL = 0x00000001,
            TBATF_USEMDILIVEPREVIEW = 0x00000002
        }

        private enum THB : uint {
            THB_BITMAP = 0x00000001,
            THB_ICON = 0x00000002,
            THB_TOOLTIP = 0x00000004,
            THB_FLAGS = 0x00000008
        }

        private enum THBF : uint {
            THBF_ENABLED = 0x00000000,
            THBF_DISABLED = 0x00000001,
            THBF_DISMISSONCLICK = 0x00000002,
            THBF_NOBACKGROUND = 0x00000004,
            THBF_HIDDEN = 0x00000008
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Auto)]
        private struct THUMBBUTTON {
            public THB dwMask;
            public uint iId;
            public uint iBitmap;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Shared.MAX_PATH)]
            public string szTip;

            public THBF dwFlags;
        }

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
            void SetProgressState(IntPtr hwnd, TBPF tbpFlags);
            void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
            void UnregisterTab(IntPtr hwndTab);
            void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
            void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, TBATF tbatFlags);

            void ThumbBarAddButtons(
                IntPtr hwnd,
                uint cButtons,

                [MarshalAs(UnmanagedType.LPArray)]
                THUMBBUTTON[] pButtons
            );

            void ThumbBarUpdateButtons(
                IntPtr hwnd,
                uint cButtons,

                [MarshalAs(UnmanagedType.LPArray)]
                THUMBBUTTON[] pButtons
            );

            void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);

            void SetOverlayIcon(
                IntPtr hwnd,
                IntPtr hIcon,

                [MarshalAs(UnmanagedType.LPWStr)]
                string pszDescription
            );

            void SetThumbnailTooltip(
                IntPtr hwnd,

                [MarshalAs(UnmanagedType.LPWStr)]
                string pszTip
            );

            void SetThumbnailClip(
                IntPtr hwnd,

                [MarshalAs(UnmanagedType.LPStruct)]
                Rectangle prcClip
            );
        }

        [ComImport, Guid("56FDF344-FD6D-11D0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
        private class TaskbarList { }

        private static ITaskbarList3 taskbarList = null;
        private static readonly bool taskbarListVersion = Environment.OSVersion.Version >= new Version(6, 1);

        private const int PROGRESS_FORM_VALUE_COMPLETE = 100;

        private static ProgressBar progressBar = null;
        private static Form progressForm = null;
        private static ulong progressFormValue = 0;
        private static TBPF progressFormState = TBPF.TBPF_NORMAL;
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

                if (taskbarList == null) {
                    if (taskbarListVersion) {
                        try {
                            taskbarList = (ITaskbarList3)new TaskbarList();
                            taskbarList.HrInit();
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            taskbarList = null;
                        }
                    }
                }

                ProgressFormStyle = ProgressManager.style;
                ProgressFormValue = ProgressManager.value;
                ProgressFormState = ProgressManager.state;
            }
        }

        // with form progress, style and state are both stored in one field
        // setting marquee style enters indeterminate state
        // you can't display marquee style and normal/error/paused state at the same time
        // so error/paused states take priority over the marquee style
        // and marquee style takes priority over normal state
        // furthermore, when the value is completed we enter the no progress state, which
        // takes priority over all other states
        // when the value is completed, setting the state to error/paused should do nothing
        // until the value is reset, in which case it should show the error/paused state
        // when the value is completed, setting the style to marquee should do nothing
        // until the value is reset, in which case it should show marquee style
        // (but only if not in the error/paused states)
        // when the value is completed, setting the state to normal should do nothing
        // until the value is reset, in which case it should show normal state
        // (but only if not in the error/paused states or marquee style)
        private static ProgressBarStyle ProgressFormStyle {
            /*
            get {
                return ProgressManager.progressFormState == TBPF.TBPF_INDETERMINATE ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            }
            */

            set {
                // this happens first so we don't enter indeterminate state
                // if we're completed
                // otherwise when the value is reset and updates the style
                // it'll be ignored because we aren't in normal/no progress states
                // if we're in indeterminate state, it must be shown
                if (ProgressManager.progressFormValue >= PROGRESS_FORM_VALUE_COMPLETE) {
                    return;
                }

                // if the style is not marquee
                if (value != ProgressBarStyle.Marquee) {
                    // do nothing if we have already left indeterminate state
                    if (ProgressManager.progressFormState != TBPF.TBPF_INDETERMINATE) {
                        return;
                    }

                    // leave indeterminate state
                    ProgressFormValue = ProgressManager.value;
                    return;
                }

                // only enter indeterminate state from normal/no progress states
                // we enter from no progress in case the value was reset
                // error/paused states take priority over indeterminate state
                if (ProgressManager.progressFormState != TBPF.TBPF_NORMAL
                    && ProgressManager.progressFormState != TBPF.TBPF_NOPROGRESS) {
                    return;
                }

                ProgressManager.progressFormState = TBPF.TBPF_INDETERMINATE;

                if (ProgressForm == null) {
                    return;
                }

                if (!ProgressForm.IsHandleCreated || ProgressForm.Handle == IntPtr.Zero) {
                    return;
                }

                if (taskbarList == null) {
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
                    if (ProgressManager.progressFormState == TBPF.TBPF_NOPROGRESS) {
                        return;
                    }

                    ProgressManager.progressFormState = TBPF.TBPF_NOPROGRESS;
                } else {
                    // if we haven't completed, ignore the value in the marquee style
                    // (note: we don't check indeterminate state here, in case we are trying to leave that state)
                    if (ProgressManager.style == ProgressBarStyle.Marquee) {
                        // it's really important that there's no race condition here, so
                        // we set ProgressFormStyle to Marquee specifically, not the style field
                        ProgressFormStyle = ProgressBarStyle.Marquee;
                        return;
                    }
                }

                if (ProgressForm == null) {
                    return;
                }

                if (!ProgressForm.IsHandleCreated || ProgressForm.Handle == IntPtr.Zero) {
                    return;
                }

                if (taskbarList == null) {
                    return;
                }

                taskbarList.SetProgressValue(ProgressForm.Handle, ProgressManager.progressFormValue, PROGRESS_FORM_VALUE_COMPLETE);

                // it is required to enter no progress state when completed
                if (completed) {
                    taskbarList.SetProgressState(ProgressForm.Handle, ProgressManager.progressFormState);
                    return;
                }

                // if we are here, we are not completed, and, we are not marquee style
                // so if we were previously no progress or indeterminate state, update the state
                if (ProgressManager.progressFormState == TBPF.TBPF_NOPROGRESS
                    || ProgressManager.progressFormState == TBPF.TBPF_INDETERMINATE) {
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
                // this happens first so we don't enter error/paused state
                // if we're completed
                // otherwise when the value is reset and updates the state
                // it'll be ignored because we are in error/paused state
                // if we're in error/paused state, it must be shown
                if (ProgressManager.progressFormValue >= PROGRESS_FORM_VALUE_COMPLETE) {
                    return;
                }

                if (value == PBST_ERROR) {
                    if (ProgressManager.progressFormState == TBPF.TBPF_ERROR) {
                        return;
                    }

                    ProgressManager.progressFormState = TBPF.TBPF_ERROR;
                } else if (value == PBST_PAUSED) {
                    if (ProgressManager.progressFormState == TBPF.TBPF_PAUSED) {
                        return;
                    }

                    ProgressManager.progressFormState = TBPF.TBPF_PAUSED;
                } else {
                    // normal state does not take priority over marquee style
                    // (note: we don't check indeterminate state here, in case we are trying to leave that state)
                    if (ProgressManager.style == ProgressBarStyle.Marquee) {
                        return;
                    }

                    if (ProgressManager.progressFormState == TBPF.TBPF_NORMAL) {
                        return;
                    }

                    ProgressManager.progressFormState = TBPF.TBPF_NORMAL;
                }

                if (ProgressForm == null) {
                    return;
                }

                if (!ProgressForm.IsHandleCreated || ProgressForm.Handle == IntPtr.Zero) {
                    return;
                }

                if (taskbarList == null) {
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
