using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement.DownloadBeforeElementCollection;

namespace FlashpointSecurePlayer {
    class DownloadsBefore : Modifications {
        public DownloadsBefore(Form form) : base(form) { }

        private void Activate() { }

        public async Task ActivateAsync(string name, List<string> downloadsBeforeNames) {
            base.Activate(name);
            //ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            // purpose of the following code is to asynchronously
            // download multiple (potentially large) files in
            // parallel, but not write them anywhere on the disk
            // just so that the GET request happens and
            // any PHP script executes all the way on the server and
            // we know the file downloaded all the way before the
            // server/software starts
            //DownloadBeforeElement downloadBeforeElement = null;
            ProgressManager.CurrentGoal.Start(downloadsBeforeNames.Count);

            try {
                Task[] downloadTasks = new Task[downloadsBeforeNames.Count];

                for (int i = 0;i < downloadsBeforeNames.Count;i++) {
                    downloadTasks[i] = DownloadAsync(downloadsBeforeNames[i]).ContinueWith(delegate (Task antecedentTask) {
                        HandleAntecedentTask(antecedentTask);

                        ProgressManager.CurrentGoal.Steps++;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }

                await Task.WhenAll(downloadTasks).ConfigureAwait(true);
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }
    }
}
