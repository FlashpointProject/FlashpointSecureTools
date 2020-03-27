using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;

namespace FlashpointSecurePlayer {
    class RunAsAdministrator : Modifications {
        public RunAsAdministrator(Form Form) : base(Form) { }

        public void Activate(string name, bool runAsAdministrator) {
            base.Activate(name);
            /*
            ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            if (!modificationsElement.RunAsAdministrator) {
                return;
            }
            */

            if (!runAsAdministrator) {
                return;
            }

            if (!TestProcessRunningAsAdministrator()) {
                throw new TaskRequiresElevationException();
            }
        }
    }
}