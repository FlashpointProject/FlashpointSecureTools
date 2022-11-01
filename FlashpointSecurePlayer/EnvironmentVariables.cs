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
        private object activationLock = new object();
        private object deactivationLock = new object();

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

        private string GetValue(EnvironmentVariablesElement environmentVariablesElement) {
            if (String.IsNullOrEmpty(environmentVariablesElement.Find)) {
                return environmentVariablesElement.Value;
            }

            string comparableName = GetComparableName(environmentVariablesElement.Name);

            if (comparableName == __COMPAT_LAYER) {
                throw new EnvironmentVariablesFailedException("Find and replace with the \"" + __COMPAT_LAYER + "\" Environment Variable is not supported.");
            }

            string value = null;

            try {
                value = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.Process);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("Failed to get the \"" + environmentVariablesElement.Name + "\" Environment Variable.");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the \"" + environmentVariablesElement.Name + "\" Environment Variable requires elevation.");
            }

            Regex regex = null;

            try {
                regex = new Regex(environmentVariablesElement.Find);
            } catch (ArgumentException) {
                throw new EnvironmentVariablesFailedException("The Regex Pattern \"" + environmentVariablesElement.Find + "\" is invalid.");
            }

            try {
                value = regex.Replace(value, environmentVariablesElement.Replace);
            } catch (ArgumentNullException) {
                // value was not defined
                // Fail silently.
            } catch (RegexMatchTimeoutException) {
                throw new EnvironmentVariablesFailedException("The Regex Match timed out.");
            }
            return value;
        }

        new public void Activate(string templateName) {
            lock (activationLock) {
                base.Activate(templateName);

                // validation
                if (String.IsNullOrEmpty(TemplateName)) {
                    // no argument
                    return;
                }

                // initialize templates
                TemplateElement templateElement = GetTemplateElement(false, TemplateName);

                if (templateElement == null) {
                    return;
                }

                ModeElement modeElement = templateElement.Mode;
                ModificationsElement modificationsElement = templateElement.Modifications;

                if (!modificationsElement.ElementInformation.IsPresent) {
                    return;
                }

                TemplateElement activeTemplateElement = GetActiveTemplateElement(true, TemplateName);
                ModificationsElement activeModificationsElement = activeTemplateElement.Modifications;

                // initialize variables
                string comparableName = null;
                string value = null;
                List<string> values = null;
                string compatibilityLayerValue = null;
                List<string> compatibilityLayerValues = new List<string>();

                // compatibility settings
                try {
                    // we need to find the compatibility layers so we can check later if the ones we want are already set
                    compatibilityLayerValue = Environment.GetEnvironmentVariable(__COMPAT_LAYER, EnvironmentVariableTarget.Process);
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException("Failed to get the \"" + __COMPAT_LAYER + "\" Environment Variable.");
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException("Getting the \"" + __COMPAT_LAYER + "\" Environment Variable requires elevation.");
                }

                ProgressManager.CurrentGoal.Start(modificationsElement.EnvironmentVariables.Count + modificationsElement.EnvironmentVariables.Count);

                try {
                    EnvironmentVariablesElement activeEnvironmentVariablesElement = null;
                    EnvironmentVariablesElement environmentVariablesElement = null;

                    // set active configuration
                    for (int i = 0; i < modificationsElement.EnvironmentVariables.Count; i++) {
                        environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                        if (environmentVariablesElement == null) {
                            throw new System.Configuration.ConfigurationErrorsException("The Environment Variables Element (" + i + ") is null while creating the Active Environment Variables Element.");
                        }

                        comparableName = GetComparableName(environmentVariablesElement.Name);

                        if (UnmodifiableComparableNames.Contains(comparableName)) {
                            throw new EnvironmentVariablesFailedException("The \"" + environmentVariablesElement.Name + "\" Environment Variable cannot be modified while creating the Active Environment Variables Element.");
                        }

                        try {
                            activeEnvironmentVariablesElement = new EnvironmentVariablesElement {
                                Name = environmentVariablesElement.Name,
                                Find = environmentVariablesElement.Find,
                                Value = Environment.GetEnvironmentVariable(environmentVariablesElement.Name, EnvironmentVariableTarget.Process)
                            };
                        } catch (ArgumentException) {
                            throw new EnvironmentVariablesFailedException("Failed to get the \"" + environmentVariablesElement.Name + "\" Environment Variable.");
                        } catch (SecurityException) {
                            throw new TaskRequiresElevationException("Getting the \"" + environmentVariablesElement.Name + "\" Environment Variable requires elevation.");
                        }

                        activeModificationsElement.EnvironmentVariables.Set(activeEnvironmentVariablesElement);
                        ProgressManager.CurrentGoal.Steps++;
                    }

                    SetFlashpointSecurePlayerSection(TemplateName);

                    // set environment variables
                    for (int i = 0; i < modificationsElement.EnvironmentVariables.Count; i++) {
                        environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                        if (environmentVariablesElement == null) {
                            throw new System.Configuration.ConfigurationErrorsException("The Environment Variables Element (" + i + ") is null.");
                        }

                        comparableName = GetComparableName(environmentVariablesElement.Name);

                        if (UnmodifiableComparableNames.Contains(comparableName)) {
                            throw new EnvironmentVariablesFailedException("The \"" + environmentVariablesElement.Name + "\" Environment Variable cannot be modified at this time.");
                        }

                        value = GetValue(environmentVariablesElement);

                        try {
                            Environment.SetEnvironmentVariable(environmentVariablesElement.Name, Environment.ExpandEnvironmentVariables(value), EnvironmentVariableTarget.Process);
                        } catch (ArgumentException) {
                            throw new EnvironmentVariablesFailedException("Failed to set the \"" + environmentVariablesElement.Name + "\" Environment Variable.");
                        } catch (SecurityException) {
                            throw new TaskRequiresElevationException("Setting the \"" + environmentVariablesElement.Name + "\" Environment Variable requires elevation.");
                        }

                        // now throw up a restart in Web Browser Mode for Compatibility Settings
                        if (comparableName == __COMPAT_LAYER && modeElement.Name == ModeElement.NAME.WEB_BROWSER) {
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
                                throw new CompatibilityLayersException("The Compatibility Layers (" + value + ") cannot be set without a restart.");
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }
        
        public void Deactivate(MODIFICATIONS_REVERT_METHOD modificationsRevertMethod = MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY) {
            lock (deactivationLock) {
                // do the reverse of activation because we can
                base.Deactivate();
                TemplateElement activeTemplateElement = GetActiveTemplateElement(false);

                // if the activation backup doesn't exist, we don't need to do stuff
                if (activeTemplateElement == null) {
                    return;
                }

                ModificationsElement activeModificationsElement = activeTemplateElement.Modifications;

                if (activeModificationsElement.EnvironmentVariables.Count <= 0) {
                    return;
                }

                // if the activation backup exists, but no key is marked as active...
                // we assume the registry has changed, and don't revert the changes, to be safe
                // (it should never happen unless the user tampered with the config file)
                string templateElementName = activeTemplateElement.Active;

                // don't allow infinite recursion!
                if (String.IsNullOrEmpty(templateElementName)) {
                    activeModificationsElement.EnvironmentVariables.Clear();
                    SetFlashpointSecurePlayerSection(TemplateName);
                    return;
                }

                TemplateElement templateElement = GetTemplateElement(false, templateElementName);
                ModeElement modeElement = templateElement.Mode;
                ModificationsElement modificationsElement = null;

                // if the active element pointed to doesn't exist... same assumption
                // and another safeguard against recursion
                if (templateElement != null && templateElement != activeTemplateElement) {
                    if (templateElement.Modifications.ElementInformation.IsPresent) {
                        modificationsElement = templateElement.Modifications;
                    }
                }

                if (modificationsElement == null) {
                    activeModificationsElement.EnvironmentVariables.Clear();
                    SetFlashpointSecurePlayerSection(TemplateName);
                    return;
                }

                // initialize variables
                string comparableName = null;
                string value = null;
                List<string> values = null;
                string compatibilityLayerValue = null;
                List<string> compatibilityLayerValues = new List<string>();

                // compatibility settings
                try {
                    compatibilityLayerValue = Environment.GetEnvironmentVariable(__COMPAT_LAYER, EnvironmentVariableTarget.Process);
                } catch (ArgumentException) {
                    throw new EnvironmentVariablesFailedException("Failed to get the \"" + __COMPAT_LAYER + "\" Environment Variable.");
                } catch (SecurityException) {
                    throw new TaskRequiresElevationException("Getting the \"" + __COMPAT_LAYER + "\" Environment Variable requires elevation.");
                }

                // we get this right away here
                // as opposed to after the variable has been potentially set like during activation
                if (compatibilityLayerValue != null) {
                    compatibilityLayerValues = compatibilityLayerValue.ToUpperInvariant().Split(' ').ToList();
                }

                ProgressManager.CurrentGoal.Start(activeModificationsElement.EnvironmentVariables.Count);

                try {
                    EnvironmentVariablesElement environmentVariablesElement = null;
                    EnvironmentVariablesElement activeEnvironmentVariablesElement = null;

                    for (int i = 0; i < activeModificationsElement.EnvironmentVariables.Count; i++) {
                        environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                        if (environmentVariablesElement == null) {
                            throw new EnvironmentVariablesFailedException("The Environment Variable element (" + i + ") is null.");
                        }

                        activeEnvironmentVariablesElement = activeModificationsElement.EnvironmentVariables.Get(environmentVariablesElement.Name) as EnvironmentVariablesElement;

                        if (activeEnvironmentVariablesElement != null) {
                            comparableName = GetComparableName(activeEnvironmentVariablesElement.Name);

                            if (UnmodifiableComparableNames.Contains(comparableName)) {
                                throw new EnvironmentVariablesFailedException("The \"" + activeEnvironmentVariablesElement.Name + "\" Environment Variable cannot be modified at this time.");
                            }

                            value = environmentVariablesElement.Value;
                            values = new List<string>();

                            if (value != null) {
                                values = value.ToUpperInvariant().Split(' ').ToList();
                            }

                            if (modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                try {
                                    Environment.SetEnvironmentVariable(activeEnvironmentVariablesElement.Name, null, EnvironmentVariableTarget.Process);
                                } catch (ArgumentException) {
                                    throw new EnvironmentVariablesFailedException("Failed to delete the \"" + environmentVariablesElement.Name + "\" Environment Variable.");
                                } catch (SecurityException) {
                                    throw new TaskRequiresElevationException("Deleting the \"" + environmentVariablesElement.Name + "\" Environment Variable requires elevation.");
                                }
                            } else {
                                // don't reset Compatibility Settings if we're restarting for Web Browser Mode
                                if (comparableName != __COMPAT_LAYER || values.Except(compatibilityLayerValues).Any() || modeElement.Name != ModeElement.NAME.WEB_BROWSER) {
                                    try {
                                        Environment.SetEnvironmentVariable(activeEnvironmentVariablesElement.Name, activeEnvironmentVariablesElement.Value, EnvironmentVariableTarget.Process);
                                    } catch (ArgumentException) {
                                        throw new EnvironmentVariablesFailedException("Failed to set the \"" + environmentVariablesElement.Name + "\" Environment Variable.");
                                    } catch (SecurityException) {
                                        throw new TaskRequiresElevationException("Setting the \"" + environmentVariablesElement.Name + "\" Environment Variable requires elevation.");
                                    }
                                }
                            }

                            ProgressManager.CurrentGoal.Steps++;
                        }
                    }

                    activeModificationsElement.EnvironmentVariables.Clear();
                    SetFlashpointSecurePlayerSection(TemplateName);
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }
    }
}
