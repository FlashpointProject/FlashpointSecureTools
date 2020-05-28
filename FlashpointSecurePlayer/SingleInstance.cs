using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement;

namespace FlashpointSecurePlayer {
    class SingleInstance : Modifications {
        public SingleInstance(Form form) : base(form) { }

        // function to create a real MessageBox which
        // automatically closes upon completion of tasks
        private DialogResult? ShowClosableMessageBox(Task[] tasks, string text, string caption, MessageBoxButtons messageBoxButtons, MessageBoxIcon messageBoxIcon) {
            Form closableForm = new Form() {
                Size = new Size(0, 0)
            };

            closableForm.BringToFront();
            // need this because dialogResult defaults to Cancel when
            // we want it to default to null
            bool dialogResultSet = true;

            Task.WhenAll(tasks).ContinueWith(delegate (Task task) {
                // this closes the form hosting the Message Box and
                // causes it to stop blocking
                closableForm.Close();
                dialogResultSet = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            // this line blocks execution, but the task above causes it to stop blocking
            DialogResult dialogResult = MessageBox.Show(closableForm, text, caption, messageBoxButtons, messageBoxIcon);

            if (!dialogResultSet) {
                return null;
            }
            return dialogResult;
        }

        // single task version
        private DialogResult? ShowClosableMessageBox(Task task, string text, string caption, MessageBoxButtons messageBoxButtons, MessageBoxIcon messageBoxIcon) {
            return ShowClosableMessageBox(new Task[] { task }, text, caption, messageBoxButtons, messageBoxIcon);
        }

        public void Activate(string name, string commandLine) {
            base.Activate(name);

            if (String.IsNullOrEmpty(name)) {
                // no argument
                return;
            }

            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }

            SingleInstanceElement singleInstanceElement = modificationsElement.SingleInstance;

            if (!singleInstanceElement.ElementInformation.IsPresent) {
                return;
            }

            if (!String.IsNullOrEmpty(singleInstanceElement.CommandLine)) {
                commandLine = singleInstanceElement.CommandLine;
            }

            if (String.IsNullOrEmpty(commandLine)) {
                return;
            }
            
            string[] argv = CommandLineToArgv(commandLine, out int argc);

            // the paths we'll be comparing to test if the executable is strictly the same
            string comparablePath = String.Empty;
            string activeComparablePath = String.Empty;

            try {
                activeComparablePath = Path.GetFullPath(argv[0]);
            } catch (PathTooLongException) {
                throw new ArgumentException("The path is too long to " + argv[0] + ".");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the Full Path to " + argv[0] + " requires elevation.");
            } catch (NotSupportedException) {
                throw new ArgumentException("The path " + argv[0] + " is not supported.");
            }

            // converting to a Uri canonicalizes the path
            // making them possible to compare
            try {
                activeComparablePath = new Uri(activeComparablePath).LocalPath.ToUpper();
            } catch (UriFormatException) {
                throw new ArgumentException("The path " + activeComparablePath + " is malformed.");
            } catch (NullReferenceException) {
                throw new ArgumentNullException("The path is null.");
            } catch (InvalidOperationException) {
                throw new ArgumentException("The path " + activeComparablePath + " is invalid.");
            }

            List<Process> processesByName;
            List<Process> processesByNameStrict;
            string processEXEName = null;
            // GetProcessesByName can't have extension (stupidly)
            string activeProcessEXEName = Path.GetFileNameWithoutExtension(argv[0]);

            do {
                // the strict list is the one which will be checked against for real
                processesByName = Process.GetProcessesByName(activeProcessEXEName).ToList();
                processesByNameStrict = new List<Process>();

                if (singleInstanceElement.Strict) {
                    for (int i = 0;i < processesByName.Count;i++) {
                        processEXEName = GetProcessEXEName(processesByName[i]);

                        try {
                            comparablePath = new Uri(processEXEName.ToString()).LocalPath.ToUpper();

                            if (comparablePath == activeComparablePath) {
                                processesByNameStrict.Add(processesByName[i]);
                            }
                        } catch (UriFormatException) { }
                        catch (ArgumentNullException) { }
                        catch (NullReferenceException) { }
                        catch (InvalidOperationException) { }
                    }
                } else {
                    processesByNameStrict = processesByName;
                }

                // don't allow preceding further until
                // all processes with the same name have been killed
                if (processesByNameStrict.Any()) {
                    DialogResult? dialogResult = ShowClosableMessageBox(Task.Run(delegate () {
                        while (processesByNameStrict.Any()) {
                            if (!processesByNameStrict[0].HasExited) {
                                processesByNameStrict[0].WaitForExit();
                            }

                            processesByNameStrict.RemoveAt(0);
                        }
                    }), String.Format(Properties.Resources.ProcessCompatibilityConflict, activeProcessEXEName), Properties.Resources.FlashpointSecurePlayer, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                    if (dialogResult == DialogResult.Cancel) {
                        Application.Exit();
                        throw new InvalidModificationException("The operation was aborted by the user.");
                    }
                }
                // continue this process until the problem is resolved
            } while (processesByNameStrict.Any());
        }
    }
}
