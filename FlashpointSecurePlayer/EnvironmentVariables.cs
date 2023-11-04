using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.EnvironmentVariablesElementCollection;

namespace FlashpointSecurePlayer {
    public class EnvironmentVariables : Modifications {
        private const string __COMPAT_LAYER = nameof(__COMPAT_LAYER);

        private IReadOnlyList<string> UnmodifiableComparableNames { get; } = new List<string> { FP_STARTUP_PATH, FP_HTDOCS_FILE }.AsReadOnly();

        public EnvironmentVariables(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        private string GetComparableName(string name) {
            if (name == null) {
                return name;
            }

            int comparableNameLength = name.IndexOf('\0');
            return comparableNameLength == -1 ? name : name.Substring(0, comparableNameLength);
        }

        private string GetFlashpointProxyName(string name, out string comparableName) {
            comparableName = null;

            if (name == null) {
                return name;
            }

            comparableName = GetComparableName(name);

            if (comparableName == null) {
                return name;
            }

            if (comparableName.Equals(FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY, StringComparison.OrdinalIgnoreCase)) {
                comparableName = FlashpointProxy.FP_PROXY;
            } else if (comparableName.Equals(FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY_PORT, StringComparison.OrdinalIgnoreCase)) {
                comparableName = FlashpointProxy.FP_PROXY_PORT;
            }

            try {
                if (comparableName.Equals(FlashpointProxy.FP_PROXY, StringComparison.OrdinalIgnoreCase)) {
                    name = FlashpointProxy.FP_PROXY;

                    FlashpointProxy.GetPreferences(out bool proxy, out int port);

                    if (Environment.GetEnvironmentVariable(FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY, EnvironmentVariableTarget.Process) == null) {
                        Environment.SetEnvironmentVariable(FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY, proxy ? "1" : "0", EnvironmentVariableTarget.Process);
                    }
                } else if (comparableName.Equals(FlashpointProxy.FP_PROXY_PORT, StringComparison.OrdinalIgnoreCase)) {
                    name = FlashpointProxy.FP_PROXY_PORT;

                    FlashpointProxy.GetPreferences(out bool proxy, out int port);

                    if (Environment.GetEnvironmentVariable(FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY_PORT, EnvironmentVariableTarget.Process) == null) {
                        Environment.SetEnvironmentVariable(FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY_PORT, port.ToString(), EnvironmentVariableTarget.Process);
                    }
                }
            } catch (SecurityException ex) {
                LogExceptionToLauncher(ex);
                throw new TaskRequiresElevationException("Getting the \"" + FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY + "\" Environment Variable requires elevation.");
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidEnvironmentVariablesException("Failed to get the \"" + FlashpointProxy.FLASHPOINT_SECURE_PLAYER_PROXY + "\" Environment Variable.");
            }
            return name;
        }

        private string GetValue(EnvironmentVariablesElement environmentVariablesElement) {
            if (String.IsNullOrEmpty(environmentVariablesElement.Find)) {
                return environmentVariablesElement.Value;
            }

            string value = null;

            string name = GetFlashpointProxyName(environmentVariablesElement.Name, out string comparableName);

            if (name == null) {
                return value;
            }

            if (comparableName != null) {
                if (comparableName.Equals(__COMPAT_LAYER, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidEnvironmentVariablesException("Find and replace with the \"" + __COMPAT_LAYER + "\" Environment Variable is not supported.");
                }
            }

            try {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
            } catch (SecurityException ex) {
                LogExceptionToLauncher(ex);
                throw new TaskRequiresElevationException("Getting the \"" + name + "\" Environment Variable requires elevation.");
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidEnvironmentVariablesException("Failed to get the \"" + name + "\" Environment Variable.");
            }

            if (value == null) {
                return value;
            }

            Regex regex = null;

            try {
                regex = new Regex(environmentVariablesElement.Find);
            } catch (Exception ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidEnvironmentVariablesException("The Regex Pattern \"" + environmentVariablesElement.Find + "\" is invalid.");
            }

            try {
                value = regex.Replace(value, environmentVariablesElement.Replace);
            } catch (RegexMatchTimeoutException ex) {
                LogExceptionToLauncher(ex);
                throw new InvalidEnvironmentVariablesException("The Regex Match timed out.");
            } catch (Exception ex) {
                // match failed?
                LogExceptionToLauncher(ex);
            }
            return value;
        }

        public override void Activate(string templateName) {
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
                string name = null;
                string comparableName = null;
                string value = null;
                List<string> values = null;
                string compatibilityLayerValue = null;
                List<string> compatibilityLayerValues = null;

                // compatibility settings
                try {
                    // we need to find the compatibility layers so we can check later if the ones we want are already set
                    compatibilityLayerValue = Environment.GetEnvironmentVariable(__COMPAT_LAYER, EnvironmentVariableTarget.Process);
                } catch (SecurityException ex) {
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Getting the \"" + __COMPAT_LAYER + "\" Environment Variable requires elevation.");
                } catch (Exception ex) {
                    LogExceptionToLauncher(ex);
                    throw new InvalidEnvironmentVariablesException("Failed to get the \"" + __COMPAT_LAYER + "\" Environment Variable.");
                }

                ProgressManager.CurrentGoal.Start(modificationsElement.EnvironmentVariables.Count + modificationsElement.EnvironmentVariables.Count);

                try {
                    EnvironmentVariablesElement activeEnvironmentVariablesElement = null;
                    EnvironmentVariablesElement environmentVariablesElement = null;

                    // set active configuration
                    for (int i = 0; i < modificationsElement.EnvironmentVariables.Count; i++) {
                        environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                        if (environmentVariablesElement == null) {
                            throw new ConfigurationErrorsException("The Environment Variables Element (" + i + ") is null while creating the Active Environment Variables Element.");
                        }

                        name = GetFlashpointProxyName(environmentVariablesElement.Name, out comparableName);

                        if (name == null) {
                            throw new InvalidEnvironmentVariablesException("The name is null while creating the Active Environment Variables Element.");
                        }

                        if (comparableName != null) {
                            if (UnmodifiableComparableNames.Contains(comparableName, StringComparer.OrdinalIgnoreCase)) {
                                throw new InvalidEnvironmentVariablesException("The \"" + name + "\" Environment Variable could not be modified while creating the Active Environment Variables Element.");
                            }
                        }

                        try {
                            activeEnvironmentVariablesElement = new EnvironmentVariablesElement {
                                Name = name,
                                Find = environmentVariablesElement.Find,
                                Value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
                            };
                        } catch (SecurityException ex) {
                            LogExceptionToLauncher(ex);
                            throw new TaskRequiresElevationException("Getting the \"" + name + "\" Environment Variable requires elevation.");
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            throw new InvalidEnvironmentVariablesException("Failed to get the \"" + name + "\" Environment Variable.");
                        }

                        activeModificationsElement.EnvironmentVariables.Set(activeEnvironmentVariablesElement);
                        ProgressManager.CurrentGoal.Steps++;
                    }

                    SetFlashpointSecurePlayerSection(TemplateName);

                    // set environment variables
                    for (int i = 0; i < modificationsElement.EnvironmentVariables.Count; i++) {
                        environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                        if (environmentVariablesElement == null) {
                            throw new ConfigurationErrorsException("The Environment Variables Element (" + i + ") is null.");
                        }
                        
                        name = GetFlashpointProxyName(environmentVariablesElement.Name, out comparableName);

                        if (name == null) {
                            throw new InvalidEnvironmentVariablesException("The name is null.");
                        }

                        if (comparableName != null) {
                            if (UnmodifiableComparableNames.Contains(comparableName, StringComparer.OrdinalIgnoreCase)) {
                                throw new InvalidEnvironmentVariablesException("The \"" + name + "\" Environment Variable could not be modified at this time.");
                            }
                        }

                        value = GetValue(environmentVariablesElement);

                        try {
                            Environment.SetEnvironmentVariable(name, Environment.ExpandEnvironmentVariables(value), EnvironmentVariableTarget.Process);
                        } catch (SecurityException ex) {
                            LogExceptionToLauncher(ex);
                            throw new TaskRequiresElevationException("Setting the \"" + name + "\" Environment Variable requires elevation.");
                        } catch (Exception ex) {
                            LogExceptionToLauncher(ex);
                            throw new InvalidEnvironmentVariablesException("Failed to set the \"" + name + "\" Environment Variable.");
                        }

                        // now throw up a restart in Web Browser Mode for Compatibility Settings
                        if (comparableName != null) {
                            if (comparableName.Equals(__COMPAT_LAYER, StringComparison.OrdinalIgnoreCase)
                                && modeElement.Name == ModeElement.NAME.WEB_BROWSER) {
                                // the compatibility layers may contain more values
                                // but we're only concerned if it contains the values we want
                                compatibilityLayerValues = compatibilityLayerValue == null ? new List<string>() : compatibilityLayerValue.Split().ToList();
                                values = value == null ? new List<string>() : value.Split().ToList();

                                // we have to restart in this case in server mode
                                // because the compatibility layers only take effect
                                // on process start
                                if (values.Except(compatibilityLayerValues, StringComparer.OrdinalIgnoreCase).Any()) {
                                    throw new CompatibilityLayersException("The Compatibility Layers \"" + value + "\" require an application restart to be set.");
                                }
                            }
                        }

                        ProgressManager.CurrentGoal.Steps++;
                    }
                } finally {
                    ProgressManager.CurrentGoal.Stop();
                }
            }
        }

        public void Deactivate(MODIFICATIONS_REVERT_METHOD modificationsRevertMethod) {
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
                string name = null;
                string comparableName = null;
                string value = null;
                List<string> values = null;
                string compatibilityLayerValue = null;
                List<string> compatibilityLayerValues = null;

                // compatibility settings
                try {
                    compatibilityLayerValue = Environment.GetEnvironmentVariable(__COMPAT_LAYER, EnvironmentVariableTarget.Process);
                } catch (SecurityException ex) {
                    LogExceptionToLauncher(ex);
                    throw new TaskRequiresElevationException("Getting the \"" + __COMPAT_LAYER + "\" Environment Variable requires elevation.");
                } catch (Exception ex) {
                    LogExceptionToLauncher(ex);
                    throw new InvalidEnvironmentVariablesException("Failed to get the \"" + __COMPAT_LAYER + "\" Environment Variable.");
                }

                // we get this right away here
                // as opposed to after the variable has been potentially set like during activation
                compatibilityLayerValues = compatibilityLayerValue == null ? new List<string>() : compatibilityLayerValue.Split().ToList();

                ProgressManager.CurrentGoal.Start(activeModificationsElement.EnvironmentVariables.Count);

                try {
                    EnvironmentVariablesElement environmentVariablesElement = null;
                    EnvironmentVariablesElement activeEnvironmentVariablesElement = null;

                    for (int i = 0; i < activeModificationsElement.EnvironmentVariables.Count; i++) {
                        environmentVariablesElement = modificationsElement.EnvironmentVariables.Get(i) as EnvironmentVariablesElement;

                        if (environmentVariablesElement == null) {
                            throw new InvalidEnvironmentVariablesException("The Environment Variable element (" + i + ") is null.");
                        }

                        name = GetFlashpointProxyName(environmentVariablesElement.Name, out comparableName);

                        if (name == null) {
                            throw new InvalidEnvironmentVariablesException("The name is null.");
                        }

                        activeEnvironmentVariablesElement = activeModificationsElement.EnvironmentVariables.Get(name) as EnvironmentVariablesElement;

                        if (activeEnvironmentVariablesElement != null) {
                            if (comparableName != null) {
                                if (UnmodifiableComparableNames.Contains(comparableName, StringComparer.OrdinalIgnoreCase)) {
                                    throw new InvalidEnvironmentVariablesException("The \"" + activeEnvironmentVariablesElement.Name + "\" Environment Variable could not be modified at this time.");
                                }
                            }

                            value = environmentVariablesElement.Value;
                            values = value == null ? new List<string>() : value.Split().ToList();

                            if (modificationsRevertMethod == MODIFICATIONS_REVERT_METHOD.DELETE_ALL) {
                                try {
                                    Environment.SetEnvironmentVariable(activeEnvironmentVariablesElement.Name, null, EnvironmentVariableTarget.Process);
                                } catch (SecurityException ex) {
                                    LogExceptionToLauncher(ex);
                                    throw new TaskRequiresElevationException("Deleting the \"" + name + "\" Environment Variable requires elevation.");
                                } catch (Exception ex) {
                                    LogExceptionToLauncher(ex);
                                    throw new InvalidEnvironmentVariablesException("Failed to delete the \"" + name + "\" Environment Variable.");
                                }
                            } else {
                                // don't reset Compatibility Settings if we're restarting for Web Browser Mode
                                if (comparableName != null) {
                                    if (comparableName.Equals(__COMPAT_LAYER, StringComparison.OrdinalIgnoreCase)
                                        || values.Except(compatibilityLayerValues, StringComparer.OrdinalIgnoreCase).Any()
                                        || modeElement.Name != ModeElement.NAME.WEB_BROWSER) {
                                        try {
                                            Environment.SetEnvironmentVariable(activeEnvironmentVariablesElement.Name, activeEnvironmentVariablesElement.Value, EnvironmentVariableTarget.Process);
                                        } catch (SecurityException ex) {
                                            LogExceptionToLauncher(ex);
                                            throw new TaskRequiresElevationException("Setting the \"" + name + "\" Environment Variable requires elevation.");
                                        } catch (Exception ex) {
                                            LogExceptionToLauncher(ex);
                                            throw new InvalidEnvironmentVariablesException("Failed to set the \"" + name + "\" Environment Variable.");
                                        }
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

        public override void Deactivate() {
            Deactivate(MODIFICATIONS_REVERT_METHOD.CRASH_RECOVERY);
        }
    }
}
