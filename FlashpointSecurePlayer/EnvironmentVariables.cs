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
        public EnvironmentVariables(Form Form) : base(Form) { }

        new public void Activate(string name) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);
            EnvironmentVariablesElement environmentVariablesElement = null;

            for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                if (environmentVariablesElement == null) {
                    throw new EnvironmentVariablesFailedException();
                }

                try {
                    Environment.SetEnvironmentVariable(environmentVariablesElement.Name, RemoveVariablesFromValue(environmentVariablesElement.Value) as string);
                } catch (ArgumentNullException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException();
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
                } catch (ArgumentNullException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException();
                }
            }
        }
    }
}
