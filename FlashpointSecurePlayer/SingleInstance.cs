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
        public SingleInstance(Form Form) : base(Form) { }

        private DialogResult? ShowClosableMessageBox(Task[] tasks, string text, string caption, MessageBoxButtons messageBoxButtons, MessageBoxIcon messageBoxIcon) {
            Form closableForm = new Form() {
                Size = new Size(0, 0)
            };

            closableForm.BringToFront();
            bool dialogResultSet = true;

            Task.WhenAll(tasks).ContinueWith(delegate (Task task) {
                closableForm.Close();
                dialogResultSet = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            DialogResult dialogResult = MessageBox.Show(closableForm, text, caption, messageBoxButtons, messageBoxIcon);

            if (!dialogResultSet) {
                return null;
            }

            return dialogResult;
        }

        private DialogResult? ShowClosableMessageBox(Task task, string text, string caption, MessageBoxButtons messageBoxButtons, MessageBoxIcon messageBoxIcon) {
            return ShowClosableMessageBox(new Task[] { task }, text, caption, messageBoxButtons, messageBoxIcon);
        }

        public void Activate(string name, string commandLine) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);
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

            // default to zero in case of error
            int argc = 0;
            string[] argv = CommandLineToArgv(commandLine, out argc);

            string comparablePath = "";
            string activeComparablePath = "";

            try {
                activeComparablePath = Path.GetFullPath(argv[0]);
            } catch (PathTooLongException) {
                throw new ArgumentException("The path is too long to " + argv[0] + ".");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the Full Path to " + argv[0] + " requires elevation.");
            } catch (NotSupportedException) {
                throw new ArgumentException("The path " + argv[0] + " is not supported.");
            }

            try {
                activeComparablePath = new Uri(activeComparablePath).LocalPath.ToLower();
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
            string activeProcessEXEName = Path.GetFileNameWithoutExtension(argv[0]);

            do {
                processesByName = Process.GetProcessesByName(activeProcessEXEName).ToList();
                processesByNameStrict = new List<Process>();

                if (singleInstanceElement.Strict) {
                    for (int i = 0;i < processesByName.Count;i++) {
                        processEXEName = GetProcessEXEName(processesByName[i]);

                        try {
                            comparablePath = new Uri(processEXEName.ToString()).LocalPath.ToLower();

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
            } while (processesByNameStrict.Any());
        }
    }
}
