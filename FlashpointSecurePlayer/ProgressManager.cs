using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    public static class ProgressManager {
        public const uint PBM_SETSTATE = 0x0410;
        public static readonly IntPtr PBST_NORMAL = (IntPtr)1;
        public static readonly IntPtr PBST_ERROR = (IntPtr)2;
        public static readonly IntPtr PBST_PAUSED = (IntPtr)3;

        private static ProgressBar progressBar = null;
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
                private readonly System.Threading.Timer timer;

                public int Size {
                    get {
                        return size;
                    }

                    set {
                        // new size cannot be less than old size
                        if (value < size) {
                            value = size;
                        }

                        // new size cannot be less than steps
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
                        // new steps cannot be less than old steps
                        if (value < steps) {
                            value = steps;
                        }

                        // new steps cannot be greater than size
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
                        timer = new System.Threading.Timer(new TimerCallback(delegate (object state) {
                            Timeout = true;
                        }), null, time, System.Threading.Timeout.Infinite);
                    }
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

                for (int i = 0;i < goalsArray.Length;i++) {
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

                Style = ProgressManager.style;
                Value = ProgressManager.value;
                State = ProgressManager.state;
            }
        }

        private static ProgressBarStyle Style {
            get {
                return ProgressManager.style;
            }

            set {
                ProgressManager.style = value;

                if (ProgressBar == null) {
                    return;
                }

                ProgressBar.Style = ProgressManager.style;
            }
        }

        private static int Value {
            get {
                return ProgressManager.value;
            }

            set {
                ProgressManager.value = Math.Min(100, Math.Max(0, value));

                if (ProgressBar == null) {
                    return;
                }

                ProgressBar.Value = ProgressManager.value;
            }
        }

        private static IntPtr State {
            get {
                return ProgressManager.state;
            }

            set {
                ProgressManager.state = value;

                if (ProgressBar == null) {
                    return;
                }

                SendMessage(ProgressBar.Handle, PBM_SETSTATE, ProgressManager.state, IntPtr.Zero);
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
