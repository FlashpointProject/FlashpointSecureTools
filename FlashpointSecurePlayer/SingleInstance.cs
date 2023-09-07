using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.SingleInstanceElement;

namespace FlashpointSecurePlayer {
    public class SingleInstance : Modifications {
        public SingleInstance(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        private delegate Task[] MessageBoxClosableTasksDelegate(CancellationToken token);

        // function to create a MessageBox which
        // automatically closes upon completion of multiple tasks
        private DialogResult? MessageBoxShowClosable(Form owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxClosableTasksDelegate tasksDelegate) {
            if (owner == null) {
                throw new ArgumentNullException("The owner is null.");
            }

            Form closable = null;
            
            // owner is required for this invoke
            owner.InvokeIfRequired(new MethodInvoker(delegate () {
                // the form is created, but not shown
                // its owner is set so the Message Box will act as an owned window
                closable = new Form {
                    Owner = owner
                };
            }));

            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource()) {
                try {
                    Task task = Task.WhenAll(
                        tasksDelegate(
                            cancellationTokenSource.Token
                        )
                    ).ContinueWith(
                        delegate (Task antecedentTask) {
                            HandleAntecedentTask(antecedentTask);

                            // this closes the form hosting the Message Box and
                            // causes it to stop blocking
                            closable.InvokeIfRequired(new MethodInvoker(delegate () {
                                closable.Close();
                            }));
                        },

                        TaskScheduler.FromCurrentSynchronizationContext()
                    );

                    DialogResult? dialogResult = null;

                    // this blocks execution, but the task above causes it to stop blocking
                    // if the dialog was closed upon completion of the tasks, we return null
                    // faulted/cancelled task status doesn't count
                    // (because it happens last, the task must run to completion to close the dialog)
                    closable.InvokeIfRequired(new MethodInvoker(delegate () {
                        dialogResult = MessageBox.Show(closable, text, caption, buttons, icon);
                    }));
                    return task.Status == TaskStatus.RanToCompletion ? null : dialogResult;
                } finally {
                    cancellationTokenSource.Cancel();
                }
            }
        }

        private delegate Task MessageBoxClosableTaskDelegate(CancellationToken token);

        // overload for a single task
        private DialogResult? MessageBoxShowClosable(Form owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxClosableTaskDelegate taskDelegate) {
            return MessageBoxShowClosable(
                owner,
                text,
                caption,
                buttons,
                icon,
                
                delegate (CancellationToken token) {
                    return new Task[] { taskDelegate(token) };
                }
            );
        }

        public void Activate(string templateName, Form owner) {
            base.Activate(templateName);

            if (String.IsNullOrEmpty(TemplateName)) {
                // no argument
                return;
            }

            TemplateElement templateElement = GetTemplateElement(false, TemplateName);

            if (templateElement == null) {
                return;
            }

            ModeElement modeElement = templateElement.Mode;
            ModificationsElement modificationsElement = templateElement.Modifications;

            if (!modificationsElement.ElementInformation.IsPresent) {
                return;
            }

            SingleInstanceElement singleInstanceElement = modificationsElement.SingleInstance;

            if (!singleInstanceElement.ElementInformation.IsPresent) {
                return;
            }

            string executable = singleInstanceElement.Executable;

            if (String.IsNullOrEmpty(executable)) {
                string[] argv = GetCommandLineToArgv(Environment.ExpandEnvironmentVariables(modeElement.CommandLine), out int argc);

                if (argc > 0) {
                    executable = argv[0];
                }
            }

            if (String.IsNullOrEmpty(executable)) {
                return;
            }

            if (owner == null) {
                throw new ArgumentNullException("The owner is null.");
            }

            const int PROCESS_BY_NAME_STRICT_WAIT_FOR_EXIT_MILLISECONDS = 1000;

            Process[] processesByName = null;
            Stack<Process> processesByNameStrict = null;
            string processName = null;
            // GetProcessesByName can't have extension (stupidly)
            string activeProcessName = Path.GetFileNameWithoutExtension(executable);
            bool clear = false;

            do {
                if (processesByNameStrict != null) {
                    // don't allow preceding further until
                    // all processes with the same name have been killed
                    DialogResult? dialogResult = MessageBoxShowClosable(
                        owner,

                        String.Format(
                            Properties.Resources.ProcessCompatibilityConflict,
                            activeProcessName
                        ),

                        Properties.Resources.FlashpointSecurePlayer,
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning,
                        
                        delegate (CancellationToken token) {
                            return Task.Run(delegate () {
                                // copy this, so it doesn't get set to null upon hitting OK
                                Stack<Process> _processesByNameStrict = new Stack<Process>(processesByNameStrict);

                                while (_processesByNameStrict.Any()) {
                                    using (Process processByNameStrict = _processesByNameStrict.Pop()) {
                                        try {
                                            if (processByNameStrict != null) {
                                                // test for cancellation before waiting
                                                // (so we don't wait unnecessarily for the next processes after this one)
                                                while (token == null
                                                || !token.IsCancellationRequested) {
                                                    if (processByNameStrict.WaitForExit(PROCESS_BY_NAME_STRICT_WAIT_FOR_EXIT_MILLISECONDS)) {
                                                        break;
                                                    }
                                                }
                                            }
                                        } catch {
                                            // fail silently
                                            // (ensure we dispose every process)
                                        }
                                    }
                                }

                                _processesByNameStrict = null;
                            });
                        }
                    );

                    if (dialogResult == DialogResult.Cancel) {
                        Application.Exit();
                        throw new InvalidModificationException("The operation was aborted by the user.");
                    }

                    processesByNameStrict = null;
                }

                // the strict list is the one which will be checked against for real
                processesByName = Process.GetProcessesByName(activeProcessName);

                if (processesByName == null) {
                    return;
                }

                if (singleInstanceElement.Strict) {
                    processesByNameStrict = new Stack<Process>();

                    for (int i = 0; i < processesByName.Length; i++) {
                        if (processesByName[i] != null) {
                            processName = GetProcessName(processesByName[i]).ToString();

                            clear = false;

                            try {
                                if (!ComparePaths(executable, processName)) {
                                    clear = true;
                                }
                            } catch {
                                // fail silently
                            }

                            if (clear) {
                                processesByName[i].Dispose();
                            } else {
                                processesByNameStrict.Push(processesByName[i]);
                            }

                            processesByName[i] = null;
                        }
                    }
                } else {
                    processesByNameStrict = new Stack<Process>(processesByName);
                }

                processesByName = null;
                // continue this process until the problem is resolved
            } while (processesByNameStrict.Any());
        }
    }
}
