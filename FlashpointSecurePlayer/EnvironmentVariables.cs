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
        private const string __COMPAT_LAYER = nameof(__COMPAT_LAYER);
        private IList<string> UnmodifiableComparableNames { get; } = new List<string> { FP_STARTUP_PATH, FP_HTDOCS_FILE }.AsReadOnly();

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

        private string GetModifiedValue(EnvironmentVariablesElement environmentVariablesElement) {
            if (String.IsNullOrEmpty(environmentVariablesElement.Find)) {
                return environmentVariablesElement.Value;
            }

            string comparableName = GetComparableName(environmentVariablesElement.Name);

            if (comparableName == __COMPAT_LAYER) {
                throw new EnvironmentVariablesFailedException("Find and replace with the " + __COMPAT_LAYER + " Environment Variable is not supported.");
            }

            string modifiedValue = null;

            try {
                modifiedValue = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.Process);
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
                modifiedValue = regex.Replace(modifiedValue, environmentVariablesElement.Replace);
            } catch (ArgumentNullException) {
                // value was not defined
                // Fail silently.
            } catch (RegexMatchTimeoutException) {
                throw new EnvironmentVariablesFailedException("The Regex Match timed out.");
            }
            return modifiedValue;
        }

        public void Activate(string templateName) {
            base.Activate(templateName);

            if (String.IsNullOrEmpty(templateName)) {
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

            string comparableName = null;
            string modifiedValue = null;
            List<string> modifiedValues = null;
            string compatibilityLayerValue = null;
            List<string> compatibilityLayerValues = new List<string>();
            string unmodifiedValue = null;

            try {
                // we need to find the compatibility layers so we can check later if the ones we want are already set
                compatibilityLayerValue = Environment.GetEnvironmentVariable(__COMPAT_LAYER, EnvironmentVariableTarget.Process);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to get the " + __COMPAT_LAYER + " Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the " + __COMPAT_LAYER + " Environment Variable requires elevation.");
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

                    if (UnmodifiableComparableNames.Contains(comparableName)) {
                        throw new EnvironmentVariablesFailedException("The " + environmentVariablesElement.Name + " Environment Variable cannot be modified at this time.");
                    }

                    unmodifiedValue = null;

                    try {
                        unmodifiedValue = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.User);

                        if (unmodifiedValue == null) {
                            unmodifiedValue = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.Machine);
                        }
                    } catch (ArgumentException) {
                        throw new EnvironmentVariablesFailedException("Failed to get the " + environmentVariablesElement.Name + " Environment Variable for user or machine.");
                    } catch (SecurityException) {
                        throw new TaskRequiresElevationException("Getting the " + environmentVariablesElement.Name + " Environment Variable for user or machine requires elevation.");
                    }

                    try {
                        Environment.SetEnvironmentVariable(environmentVariablesElement.Name, unmodifiedValue, EnvironmentVariableTarget.Process);
                    } catch (ArgumentException) {
                        throw new EnvironmentVariablesFailedException("Failed to set the " + environmentVariablesElement.Name + " Environment Variable.");
                    } catch (SecurityException) {
                        throw new TaskRequiresElevationException("Setting the " + environmentVariablesElement.Name + " Environment Variable requires elevation.");
                    }

                    modifiedValue = GetModifiedValue(environmentVariablesElement);

                    try {
                        Environment.SetEnvironmentVariable(environmentVariablesElement.Name, Environment.ExpandEnvironmentVariables(modifiedValue), EnvironmentVariableTarget.Process);
                    } catch (ArgumentException) {
                        throw new EnvironmentVariablesFailedException("Failed to set the " + environmentVariablesElement.Name + " Environment Variable.");
                    } catch (SecurityException) {
                        throw new TaskRequiresElevationException("Setting the " + environmentVariablesElement.Name + " Environment Variable requires elevation.");
                    }

                    // if this is the compatibility layer variable
                    // and the value is not what we want to set it to
                    // and we're in server mode...
                    if (comparableName == __COMPAT_LAYER && modeElement.Name == ModeElement.NAME.WEB_BROWSER) {
                        modifiedValues = new List<string>();

                        // the compatibility layers may contain more values
                        // but we're only concerned if it contains the values we want
                        if (compatibilityLayerValue != null) {
                            compatibilityLayerValues = compatibilityLayerValue.ToUpperInvariant().Split(' ').ToList();
                        }

                        if (modifiedValue != null) {
                            modifiedValues = modifiedValue.ToUpperInvariant().Split(' ').ToList();
                        }

                        // we have to restart in this case in server mode
                        // because the compatibility layers only take effect
                        // on process start
                        if (modifiedValues.Except(compatibilityLayerValues).Any()) {
                            throw new CompatibilityLayersException("The Compatibility Layers (" + modifiedValue + ") cannot be set.");
                        }
                    }

                    ProgressManager.CurrentGoal.Steps++;
                }
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }
        
        public void Deactivate() {
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

            ModeElement modeElement = templateElement.Mode;
            ModificationsElement modificationsElement = templateElement.Modifications;

            if (!modificationsElement.ElementInformation.IsPresent) {
                return;
            }

            string comparableName = null;
            string modifiedValue = null;
            List<string> modifiedValues = null;
            string compatibilityLayerValue = null;
            List<string> compatibilityLayerValues = new List<string>();
            string unmodifiedValue = null;

            try {
                compatibilityLayerValue = Environment.GetEnvironmentVariable(__COMPAT_LAYER, EnvironmentVariableTarget.Process);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to get the " + __COMPAT_LAYER + " Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the " + __COMPAT_LAYER + " Environment Variable requires elevation.");
            }

            // we get this right away here
            // as opposed to after the variable has been potentially set like during activation
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

                    if (UnmodifiableComparableNames.Contains(comparableName)) {
                        throw new EnvironmentVariablesFailedException("The " + environmentVariablesElement.Name + " Environment Variable cannot be modified at this time.");
                    }

                    modifiedValue = environmentVariablesElement.Value;
                    modifiedValues = new List<string>();

                    if (modifiedValue != null) {
                        modifiedValues = modifiedValue.ToUpperInvariant().Split(' ').ToList();
                    }

                    // if this isn't the compatibility layer variable
                    // or the value isn't what we want to set it to
                    // or we're not in server mode...
                    if (comparableName != __COMPAT_LAYER || modifiedValues.Except(compatibilityLayerValues).Any() || modeElement.Name != ModeElement.NAME.WEB_BROWSER) {
                        unmodifiedValue = null;

                        try {
                            unmodifiedValue = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.User);

                            if (unmodifiedValue == null) {
                                unmodifiedValue = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.Machine);
                            }
                        } catch (ArgumentException) {
                            throw new EnvironmentVariablesFailedException("Failed to get the " + environmentVariablesElement.Name + " Environment Variable for user or machine.");
                        } catch (SecurityException) {
                            throw new TaskRequiresElevationException("Getting the " + environmentVariablesElement.Name + " Environment Variable for user or machine requires elevation.");
                        }

                        try {
                            Environment.SetEnvironmentVariable(environmentVariablesElement.Name, unmodifiedValue, EnvironmentVariableTarget.Process);
                        } catch (ArgumentException) {
                            throw new EnvironmentVariablesFailedException("Failed to set the " + environmentVariablesElement.Name + " Environment Variable.");
                        } catch (SecurityException) {
                            throw new TaskRequiresElevationException("Setting the " + environmentVariablesElement.Name + " Environment Variable requires elevation.");
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
