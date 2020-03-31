﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement.ModeTemplatesElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement.ModeTemplatesElement.ModeTemplateElement.RegexElementCollection;

namespace FlashpointSecurePlayer {
    class ModeTemplates : Modifications {
        public ModeTemplates(Form Form) : base(Form) { }

        public void Activate(string name, ref string server, ref string software, ref ProcessStartInfo softwareProcessStartInfo) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            if (!modificationsElement.ModeTemplates.ServerModeTemplate.ElementInformation.IsPresent && !modificationsElement.ModeTemplates.SoftwareModeTemplate.ElementInformation.IsPresent) {
                return;
            }

            if (modificationsElement.ModeTemplates.SoftwareModeTemplate.HideWindow) {
                if (softwareProcessStartInfo == null) {
                    softwareProcessStartInfo = new ProcessStartInfo();
                }

                softwareProcessStartInfo.UseShellExecute = false;
                softwareProcessStartInfo.RedirectStandardError = false;
                softwareProcessStartInfo.RedirectStandardOutput = false;
                softwareProcessStartInfo.RedirectStandardInput = false;
                softwareProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                softwareProcessStartInfo.CreateNoWindow = true;
                softwareProcessStartInfo.ErrorDialog = false;
            }

            RegexElement regexElement = null;
            Regex regex = null;

            for (int i = 0;i < modificationsElement.ModeTemplates.ServerModeTemplate.Regexes.Count;i++) {
                regexElement = modificationsElement.ModeTemplates.ServerModeTemplate.Regexes.Get(i) as RegexElement;

                if (regexElement == null) {
                    throw new ModeTemplatesFailedException();
                }

                try {
                    regex = new Regex(regexElement.Name);
                } catch (ArgumentException) {
                    throw new ModeTemplatesFailedException();
                }

                try {
                    server = regex.Replace(server, regexElement.Replace);
                } catch (ArgumentNullException) {
                    throw new ModeTemplatesFailedException();
                } catch (RegexMatchTimeoutException) {
                    throw new ModeTemplatesFailedException();
                }
            }

            for (int i = 0;i < modificationsElement.ModeTemplates.SoftwareModeTemplate.Regexes.Count;i++) {
                regexElement = modificationsElement.ModeTemplates.SoftwareModeTemplate.Regexes.Get(i) as RegexElement;

                if (regexElement == null) {
                    throw new ModeTemplatesFailedException();
                }

                try {
                    regex = new Regex(regexElement.Name);
                } catch (ArgumentException) {
                    throw new ModeTemplatesFailedException();
                }

                try {
                    software = regex.Replace(software, regexElement.Replace);
                } catch (ArgumentNullException) {
                    throw new ModeTemplatesFailedException();
                } catch (RegexMatchTimeoutException) {
                    throw new ModeTemplatesFailedException();
                }
            }
        }
    }
}