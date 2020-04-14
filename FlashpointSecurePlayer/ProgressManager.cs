using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private static ProgressBarStyle style = ProgressBarStyle.Marquee;
        private static int value = 0;
        private static IntPtr state = PBST_NORMAL;

        public static class Goal {
            private class SmallGoal {
                private int size = 1;
                private int steps = 0;

                public int Size {
                    get {
                        return size;
                    }

                    set {
                        if (value < size) {
                            value = size;
                        }

                        size = value;
                    }
                }

                public int Steps {
                    get {
                        return steps;
                    }

                    set {
                        if (value < steps) {
                            value = steps;
                        }

                        if (value > Size) {
                            value = Size;
                        }

                        steps = value;
                    }
                }

                public SmallGoal(int size = 1) {
                    Size = size;
                }
            }

            private static Stack<SmallGoal> SmallGoals = new Stack<SmallGoal>();

            public static int Size {
                get {
                    if (!SmallGoals.Any()) {
                        return 1;
                    }

                    return SmallGoals.Peek().Size;
                }

                set {
                    if (value > 0) {
                        SmallGoals.Push(new SmallGoal(value));
                    }
                }
            }

            public static int Steps {
                get {
                    if (!SmallGoals.Any()) {
                        return 0;
                    }
                    
                    return SmallGoals.Peek().Steps;
                }

                set {
                    if (!SmallGoals.Any()) {
                        return;
                    }

                    SmallGoal smallGoal = SmallGoals.Peek();
                    smallGoal.Steps = value;
                    SmallGoal[] smallGoalsArray = SmallGoals.ToArray();
                    double multiplier = (double)smallGoal.Steps / smallGoal.Size;

                    for (int i = 1;i < smallGoalsArray.Length;i++) {
                        multiplier *= (double)(smallGoalsArray[i].Steps + 1) / smallGoalsArray[i].Size;
                    }

                    int progressManagerValue = (int)(multiplier * 100.0);

                    if (smallGoal.Steps >= smallGoal.Size) {
                        SmallGoals.Pop();
                    }

                    if (progressManagerValue < ProgressManager.Value) {
                        return;
                    }

                    ProgressManager.Value = progressManagerValue;
                }
            }
        }

        public static ProgressBar ProgressBar { get; set; } = null;

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
                ProgressManager.value = Math.Max(Math.Min(value, 100), 0);

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
