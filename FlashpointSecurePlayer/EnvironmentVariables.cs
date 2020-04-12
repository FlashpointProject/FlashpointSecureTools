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
        const string COMPATIBILITY_LAYER_NAME = "__COMPAT_LAYER";

        public EnvironmentVariables(Form Form) : base(Form) { }

        public void Activate(string name, string server, string applicationMutexName) {
            base.Activate(name);
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);
            string value = null;
            List<string> values = null;
            string compatibilityLayerValue = null;
            List<string> compatibilityLayerValues = new List<string>();

            try {
                compatibilityLayerValue = Environment.GetEnvironmentVariable(COMPATIBILITY_LAYER_NAME);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException();
            } catch (SecurityException) {
                throw new TaskRequiresElevationException();
            }

            EnvironmentVariablesElement environmentVariablesElement = null;

            for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                if (environmentVariablesElement == null) {
                    throw new EnvironmentVariablesFailedException();
                }

                value = environmentVariablesElement.Value;

                try {
                    Environment.SetEnvironmentVariable(environmentVariablesElement.Name, RemoveVariablesFromLengthenedValue(value) as string);
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException();
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException();
                }

                // if this is the compatibility layer variable
                // and the value is not what we want to set it to
                // and we're in server mode...
                if (environmentVariablesElement.Name == COMPATIBILITY_LAYER_NAME && !String.IsNullOrEmpty(server)) {
                    values = new List<string>();

                    if (compatibilityLayerValue != null) {
                        compatibilityLayerValues = compatibilityLayerValue.ToUpper().Split(' ').ToList();
                    }

                    if (value != null) {
                        values = value.ToUpper().Split(' ').ToList();
                    }
                    
                    if (values.Except(compatibilityLayerValues).Any()) {
                        throw new CompatibilityLayersException();
                    }
                }
            }
        }

        public void Deactivate(string server) {
            base.Deactivate();

            if (String.IsNullOrEmpty(Name)) {
                return;
            }

            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }

            string value = null;
            List<string> values = null;
            string compatibilityLayerValue = null;
            List<string> compatibilityLayerValues = new List<string>();

            try {
                compatibilityLayerValue = Environment.GetEnvironmentVariable(COMPATIBILITY_LAYER_NAME);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException();
            } catch (SecurityException) {
                throw new TaskRequiresElevationException();
            }

            if (compatibilityLayerValue != null) {
                compatibilityLayerValues = compatibilityLayerValue.ToUpper().Split(' ').ToList();
            }

            EnvironmentVariablesElement environmentVariablesElement = null;

            for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                if (environmentVariablesElement == null) {
                    throw new EnvironmentVariablesFailedException();
                }

                value = environmentVariablesElement.Value;
                values = new List<string>();

                if (value != null) {
                    values = value.ToUpper().Split(' ').ToList();
                }

                // if this isn't the compatibility layer variable
                // or the value isn't what we want to set it to
                // or we're not in server mode...
                if (environmentVariablesElement.Name != COMPATIBILITY_LAYER_NAME || values.Except(compatibilityLayerValues).Any() || String.IsNullOrEmpty(server)) {
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
}
