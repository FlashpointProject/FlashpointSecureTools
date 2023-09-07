using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.TemplatesElementCollection.TemplateElement.ModificationsElement.DownloadBeforeElementCollection;

namespace FlashpointSecurePlayer {
    public class DownloadsBefore : Modifications {
        public DownloadsBefore(EventHandler importStart, EventHandler importStop) : base(importStart, importStop) { }

        public async Task ActivateAsync(string templateName, List<string> downloadsBeforeNames) {
            base.Activate(templateName);

            //ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            // purpose of the following code is to asynchronously
            // download multiple (potentially large) files in
            // parallel, but not write them anywhere on the disk
            // just so that the GET request happens and
            // any PHP script executes all the way on the server and
            // we know the file downloaded all the way before the
            // server/software starts
            //DownloadBeforeElement downloadBeforeElement = null;

            if (downloadsBeforeNames == null) {
                return;
            }

            ProgressManager.CurrentGoal.Start(downloadsBeforeNames.Count);

            try {
                Task[] downloadTasks = new Task[downloadsBeforeNames.Count];

                for (int i = 0; i < downloadsBeforeNames.Count; i++) {
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
