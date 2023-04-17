using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;

namespace FlashpointSecurePlayer {
    public class RunAsAdministrator : Modifications {
        public RunAsAdministrator(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        public void Activate(string templateName, bool runAsAdministrator) {
            base.Activate(templateName);
            /*
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            if (!modificationsElement.RunAsAdministrator) {
                return;
            }
            */

            if (!runAsAdministrator) {
                return;
            }

            if (!TestLaunchedAsAdministratorUser()) {
                throw new TaskRequiresElevationException("The Run As Administrator Modification requires elevation.");
            }
        }
    }
}