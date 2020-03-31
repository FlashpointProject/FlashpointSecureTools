using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement.EnvironmentVariablesElementCollection;

namespace FlashpointSecurePlayer {
    class EnvironmentVariables : Modifications {
        const string COMPATIBILITY_LAYER = "__COMPAT_LAYER";

        public EnvironmentVariables(Form Form) : base(Form) { }

        public void Activate(string name, string server, string applicationMutexName) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);
            EnvironmentVariablesElement environmentVariablesElement = null;

            for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                if (environmentVariablesElement == null) {
                    throw new EnvironmentVariablesFailedException();
                }

                string compatibilityLayer = null;

                try {
                    compatibilityLayer = Environment.GetEnvironmentVariable(COMPATIBILITY_LAYER);
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException();
                }

                try {
                    Environment.SetEnvironmentVariable(environmentVariablesElement.Name, RemoveVariablesFromValue(environmentVariablesElement.Value) as string);
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException();
                }

                if (environmentVariablesElement.Name == COMPATIBILITY_LAYER && String.IsNullOrEmpty(compatibilityLayer) && !String.IsNullOrEmpty(server)) {
                    RestartApplication(false, Form, applicationMutexName);
                    throw new InvalidModificationException();
                }
            }
        }

        new public void Deactivate() {
            base.Deactivate();
            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }

            EnvironmentVariablesElement environmentVariablesElement = null;

            for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                if (environmentVariablesElement == null) {
                    throw new EnvironmentVariablesFailedException();
                }

                try {
                    Environment.SetEnvironmentVariable(environmentVariablesElement.Name, null);
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException();
                }
            }
        }
    }
}
