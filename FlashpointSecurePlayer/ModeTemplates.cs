/*
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.ModificationsElement.ModeTemplatesElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.ModificationsElement.ModeTemplatesElement.ModeTemplateElement.RegexElementCollection;

namespace FlashpointSecurePlayer {
    class ModeTemplates : Modifications {
        public ModeTemplates(Form form) : base(form) { }

        private void RegexElementReplace(RegexElement regexElement, ref string mode) {
            if (regexElement == null) {
                throw new System.Configuration.ConfigurationErrorsException("The Regex Element is null.");
            }

            Regex regex = null;

            try {
                regex = new Regex(regexElement.Name);
            } catch (ArgumentException) {
                throw new ModeTemplatesFailedException("The Regex Name " + regexElement.Name + " is invalid.");
            }

            try {
                mode = regex.Replace(mode, regexElement.Replace);
            } catch (ArgumentNullException) {
                throw new ModeTemplatesFailedException("Mode cannot be null.");
            } catch (RegexMatchTimeoutException) {
                throw new ModeTemplatesFailedException("The Regex Match timed out.");
            }
        }

        public void Activate(string name, ref string server, ref string software, ref ProcessStartInfo softwareProcessStartInfo) {
            base.Activate(name);

            if (String.IsNullOrEmpty(name)) {
                // no argument
                return;
            }

            ModificationsElement modificationsElement = GetTemplateElement(false, Name);

            if (modificationsElement == null) {
                return;
            }

            if (!modificationsElement.ModeTemplates.ServerModeTemplate.ElementInformation.IsPresent && !modificationsElement.ModeTemplates.SoftwareModeTemplate.ElementInformation.IsPresent) {
                return;
            }

            ProgressManager.CurrentGoal.Start(modificationsElement.ModeTemplates.ServerModeTemplate.Regexes.Count + modificationsElement.ModeTemplates.SoftwareModeTemplate.Regexes.Count + 3);

            try {
                if (modificationsElement.ModeTemplates.SoftwareModeTemplate.HideWindow) {
                    HideWindow(ref softwareProcessStartInfo);
                }

                ProgressManager.CurrentGoal.Steps++;

                if (!String.IsNullOrEmpty(Environment.ExpandEnvironmentVariables(modificationsElement.ModeTemplates.SoftwareModeTemplate.WorkingDirectory))) {
                    try {
                        SetWorkingDirectory(ref softwareProcessStartInfo, modificationsElement.ModeTemplates.SoftwareModeTemplate.WorkingDirectory);
                    } catch (ArgumentNullException) {
                        throw new ModeTemplatesFailedException("Working Directory cannot be null.");
                    }
                }

                ProgressManager.CurrentGoal.Steps++;

                try {
                    string[] argv = CommandLineToArgv(software, out int argc);

                    if (!String.IsNullOrEmpty(modificationsElement.ModeTemplates.SoftwareModeTemplate.Format)) {
                        try {
                            software = String.Format(modificationsElement.ModeTemplates.SoftwareModeTemplate.Format, argv);
                        } catch (ArgumentNullException) {
                            throw new ModeTemplatesFailedException("argv cannot be null.");
                        } catch (FormatException) {
                            throw new ModeTemplatesFailedException("Format is null.");
                        }
                    }
                } catch (Win32Exception) {
                    throw new ModeTemplatesFailedException("Failed to get argv from command line.");
                }

                ProgressManager.CurrentGoal.Steps++;

                for (int i = 0;i < modificationsElement.ModeTemplates.ServerModeTemplate.Regexes.Count;i++) {
                    RegexElementReplace(modificationsElement.ModeTemplates.ServerModeTemplate.Regexes.Get(i) as RegexElement, ref server);
                    ProgressManager.CurrentGoal.Steps++;
                }

                for (int i = 0;i < modificationsElement.ModeTemplates.SoftwareModeTemplate.Regexes.Count;i++) {
                    RegexElementReplace(modificationsElement.ModeTemplates.SoftwareModeTemplate.Regexes.Get(i) as RegexElement, ref software);
                    ProgressManager.CurrentGoal.Steps++;
                }
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }
    }
}
*/