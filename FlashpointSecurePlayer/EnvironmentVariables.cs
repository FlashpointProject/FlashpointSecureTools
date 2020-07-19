using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.EnvironmentVariablesElementCollection;

namespace FlashpointSecurePlayer {
    class EnvironmentVariables : Modifications {
        private const string COMPATIBILITY_LAYER_NAME = "__COMPAT_LAYER";
        private IList<string> ReadOnlyComparableNames { get; } = new List<string> { FLASHPOINT_STARTUP_PATH, FLASHPOINT_HTDOCS_FILE }.AsReadOnly();

        public EnvironmentVariables(Form form) : base(form) { }

        private void FindAndReplace(EnvironmentVariablesElement environmentVariablesElement) {
            if (String.IsNullOrEmpty(environmentVariablesElement.Find)) {
                return;
            }

            string value = null;

            try {
                value = Environment.GetEnvironmentVariable(environmentVariablesElement.Name);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to get the " + environmentVariablesElement.Name + " Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the " + environmentVariablesElement.Name + " Environment Variable requires elevation.");
            }

            Regex regex = null;

            try {
                regex = new Regex(environmentVariablesElement.Find);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("The Regex Pattern " + environmentVariablesElement.Find + " is invalid.");
            }

            try {
                value = regex.Replace(value, environmentVariablesElement.Replace);
            } catch (ArgumentNullException) {
                // value was not defined
                return;
            } catch (RegexMatchTimeoutException) {
                throw new EnvironmentVariablesFailedException("The Regex Match timed out.");
            }

            try {
                Environment.SetEnvironmentVariable(environmentVariablesElement.Name, value, EnvironmentVariableTarget.Process);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to set the " + environmentVariablesElement.Name + " Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Setting the " + environmentVariablesElement.Name + " Environment Variable requires elevation.");
            }
        }

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

        public void Activate(string templateName, ModeElement modeElement) {
            base.Activate(templateName);

            if (String.IsNullOrEmpty(templateName)) {
                // no argument
                return;
            }

            TemplateElement templateElement = GetTemplateElement(false, TemplateName);

            if (templateElement == null) {
                return;
            }

            ModificationsElement modificationsElement = templateElement.Modifications;

            if (!modificationsElement.ElementInformation.IsPresent) {
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

                    if (ReadOnlyComparableNames.Contains(comparableName)) {
                        throw new EnvironmentVariablesFailedException("The " + environmentVariablesElement.Name + " Environment Variable cannot be modified.");
                    }
                    
                    FindAndReplace(environmentVariablesElement);
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
                    if (comparableName == COMPATIBILITY_LAYER_NAME && modeElement.Name == ModeElement.NAME.WEB_BROWSER) {
                        values = new List<string>();

                        // the compatibility layers may contain more values
                        // but we're only concerned if it contains the values we want
                        if (compatibilityLayerValue != null) {
                            compatibilityLayerValues = compatibilityLayerValue.ToUpperInvariant().Split(' ').ToList();
                        }

                        if (value != null) {
                            values = value.ToUpperInvariant().Split(' ').ToList();
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

        /*
        public void Deactivate(ModeElement modeElement) {
            // do the reverse of activation because we can
            base.Deactivate();

            if (String.IsNullOrEmpty(TemplateName)) {
                return;
            }

            // don't need to get active name, we're only deactivating for this process
            TemplateElement templateElement = GetTemplateElement(false, TemplateName);

            if (templateElement == null) {
                return;
            }

            ModificationsElement modificationsElement = templateElement.Modifications;

            if (!modificationsElement.ElementInformation.IsPresent) {
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
                compatibilityLayerValues = compatibilityLayerValue.ToUpperInvariant().Split(' ').ToList();
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

                    if (ReadOnlyComparableNames.Contains(comparableName)) {
                        throw new EnvironmentVariablesFailedException("The " + environmentVariablesElement.Name + " Environment Variable cannot be modified.");
                    }

                    value = environmentVariablesElement.Value;
                    values = new List<string>();

                    if (value != null) {
                        values = value.ToUpperInvariant().Split(' ').ToList();
                    }

                    // if this isn't the compatibility layer variable
                    // or the value isn't what we want to set it to
                    // or we're not in server mode...
                    if (comparableName != COMPATIBILITY_LAYER_NAME || values.Except(compatibilityLayerValues).Any() || modeElement.Name != ModeElement.NAME.WEB_BROWSER) {
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
        */
    }
}
