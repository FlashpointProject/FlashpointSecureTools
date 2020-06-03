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

        public EnvironmentVariables(Form form) : base(form) { }

        private string GetComparableName(string name) {
            int comparableNameLength = name.IndexOf('\0');
            string comparableName = null;

            if (comparableNameLength == -1) {
                comparableName = name;
            } else {
                comparableName = name.Substring(comparableNameLength);
            }
            return comparableName;
        }

        public void Activate(string name, string server) {
            base.Activate(name);

            if (String.IsNullOrEmpty(name)) {
                // no argument
                return;
            }

            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }

            string comparableName = null;
            string value = null;
            List<string> values = null;
            string compatibilityLayerValue = null;
            List<string> compatibilityLayerValues = new List<string>();

            try {
                // we need to find the compatibility layers so we can check later if the ones we want are already set
                compatibilityLayerValue = Environment.GetEnvironmentVariable(COMPATIBILITY_LAYER_NAME);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to get the " + COMPATIBILITY_LAYER_NAME + " Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the " + COMPATIBILITY_LAYER_NAME + " Environment Variable requires elevation.");
            }

            ProgressManager.CurrentGoal.Start(modificationsElement.EnvironmentVariables.Count);

            try {
                EnvironmentVariablesElement environmentVariablesElement = null;

                for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                    environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                    if (environmentVariablesElement == null) {
                        throw new System.Configuration.ConfigurationErrorsException("The Environment Variables Element (" + i + ") is null.");
                    }
                    
                    comparableName = GetComparableName(environmentVariablesElement.Name);

                    if (comparableName == FLASHPOINT_SECURE_PLAYER_STARTUP_PATH) {
                        throw new EnvironmentVariablesFailedException("The " + FLASHPOINT_SECURE_PLAYER_STARTUP_PATH + " Environment Variable cannot be modified.");
                    }

                    value = environmentVariablesElement.Value;

                    try {
                        Environment.SetEnvironmentVariable(environmentVariablesElement.Name, Environment.ExpandEnvironmentVariables(value), EnvironmentVariableTarget.Process);
                    } catch (ArgumentException) {
                        throw new EnvironmentVariablesFailedException("Failed to set the " + environmentVariablesElement.Name + " Environment Variable.");
                    } catch (SecurityException) {
                        throw new TaskRequiresElevationException("Setting the " + environmentVariablesElement.Name + " Environment Variable requires elevation.");
                    }

                    // if this is the compatibility layer variable
                    // and the value is not what we want to set it to
                    // and we're in server mode...
                    if (comparableName == COMPATIBILITY_LAYER_NAME && !String.IsNullOrEmpty(server)) {
                        values = new List<string>();

                        // the compatibility layers may contain more values
                        // but we're only concerned if it contains the values we want
                        if (compatibilityLayerValue != null) {
                            compatibilityLayerValues = compatibilityLayerValue.ToUpper().Split(' ').ToList();
                        }

                        if (value != null) {
                            values = value.ToUpper().Split(' ').ToList();
                        }

                        // we have to restart in this case in server mode
                        // because the compatibility layers only take effect
                        // on process start
                        if (values.Except(compatibilityLayerValues).Any()) {
                            throw new CompatibilityLayersException("The Compatibility Layers (" + value + ") cannot be set.");
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;
                }
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }

        public void Deactivate(string server) {
            // do the reverse of activation because we can
            base.Deactivate();

            if (String.IsNullOrEmpty(Name)) {
                return;
            }

            // don't need to get active name, we're only deactivating for this process
            ModificationsElement modificationsElement = GetModificationsElement(false, Name);

            if (modificationsElement == null) {
                return;
            }
            
            string comparableName = null;
            string value = null;
            List<string> values = null;
            string compatibilityLayerValue = null;
            List<string> compatibilityLayerValues = new List<string>();

            try {
                compatibilityLayerValue = Environment.GetEnvironmentVariable(COMPATIBILITY_LAYER_NAME);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to get the " + COMPATIBILITY_LAYER_NAME + " Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the " + COMPATIBILITY_LAYER_NAME + " Environment Variable requires elevation.");
            }

            if (compatibilityLayerValue != null) {
                compatibilityLayerValues = compatibilityLayerValue.ToUpper().Split(' ').ToList();
            }

            ProgressManager.CurrentGoal.Start(modificationsElement.EnvironmentVariables.Count);

            try {
                EnvironmentVariablesElement environmentVariablesElement = null;

                for (int i = 0;i < modificationsElement.EnvironmentVariables.Count;i++) {
                    environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                    if (environmentVariablesElement == null) {
                        throw new System.Configuration.ConfigurationErrorsException("The Environment Variables Element (" + i + ") is null.");
                    }

                    comparableName = GetComparableName(environmentVariablesElement.Name);

                    if (comparableName == FLASHPOINT_SECURE_PLAYER_STARTUP_PATH) {
                        throw new EnvironmentVariablesFailedException("The " + FLASHPOINT_SECURE_PLAYER_STARTUP_PATH + " Environment Variable cannot be modified.");
                    }

                    value = environmentVariablesElement.Value;
                    values = new List<string>();

                    if (value != null) {
                        values = value.ToUpper().Split(' ').ToList();
                    }

                    // if this isn't the compatibility layer variable
                    // or the value isn't what we want to set it to
                    // or we're not in server mode...
                    if (comparableName != COMPATIBILITY_LAYER_NAME || values.Except(compatibilityLayerValues).Any() || String.IsNullOrEmpty(server)) {
                        try {
                            Environment.SetEnvironmentVariable(environmentVariablesElement.Name, null, EnvironmentVariableTarget.Process);
                        } catch (ArgumentException) {
                            throw new EnvironmentVariablesFailedException("Failed to set the " + environmentVariablesElement.Name + " Environment Variable.");
                        } catch (SecurityException) {
                            throw new TaskRequiresElevationException("Getting the " + COMPATIBILITY_LAYER_NAME + " Environment Variable requires elevation.");
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;
                }
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }
    }
}
