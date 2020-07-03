using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection;
using static FlashpointSecurePlayer.Shared.FlashpointSecurePlayerSection.ModificationsElementCollection.ModificationsElement.DownloadBeforeElementCollection;

namespace FlashpointSecurePlayer {
    class DownloadSource : Modifications {
        public DownloadSource(Form form) : base(form) { }

        private async Task ActivateAsync(string downloadSourceModificationName) {
            if (String.IsNullOrEmpty(downloadSourceModificationName)) {
                return;
            }

            //ModificationsElement modificationsElement = GetModificationsElement(true, Name);

            // purpose of the following code is to asynchronously
            // download multiple (potentially large) files in
            // parallel, but not write them anywhere on the disk
            // just so that the GET request happens and
            // any PHP script executes all the way on the server and
            // we know the file downloaded all the way before the
            // server/software starts
            //DownloadBeforeElement downloadBeforeElement = null;
            ProgressManager.CurrentGoal.Start(1);

            try {
                await DownloadAsync(downloadSourceModificationName);
                ProgressManager.CurrentGoal.Steps++;
            } finally {
                ProgressManager.CurrentGoal.Stop();
            }
        }

        public Task Activate(string name, string downloadSourceModificationName, ref string software) {
            base.Activate(name);

            if (String.IsNullOrEmpty(downloadSourceModificationName) || String.IsNullOrEmpty(software)) {
                return CompletedTask;
            }

            StringBuilder downloadSourcePath = new StringBuilder(HTDOCS);

            try {
                Uri downloadSourceURL = new Uri(downloadSourceModificationName);
                downloadSourcePath.Append("\\");
                downloadSourcePath.Append(downloadSourceURL.Host);
                downloadSourcePath.Append(downloadSourceURL.LocalPath);
            } catch (UriFormatException) {
                throw new ArgumentException("The URL " + downloadSourceModificationName + " is malformed.");
            } catch (NullReferenceException) {
                throw new ArgumentNullException("The URL is null.");
            } catch (InvalidOperationException) {
                throw new ArgumentException("The URL " + downloadSourceModificationName + " is invalid.");
            }

            if (String.IsNullOrEmpty(downloadSourcePath.ToString())) {
                return CompletedTask;
            }
            
            StringBuilder downloadSourceSoftware = new StringBuilder(GetCommandLineArgumentRange(software, 0, 1));
            string openArguments = GetCommandLineArgumentRange(software, 1, -1);
            bool openArgumentsNullOrEmpty = String.IsNullOrEmpty(openArguments);

            if (openArgumentsNullOrEmpty) {
                downloadSourceSoftware.Append(" ");
            }

            downloadSourceSoftware.Append("\"");

            try {
                downloadSourceSoftware.Append(Path.GetFullPath(downloadSourcePath.ToString()));
            } catch (PathTooLongException) {
                throw new ArgumentException("The path is too long to " + downloadSourcePath.ToString() + ".");
            } catch (SecurityException) {
                throw new TaskRequiresElevationException("Getting the Full Path to " + downloadSourcePath.ToString() + " requires elevation.");
            } catch (NotSupportedException) {
                throw new ArgumentException("The path " + downloadSourcePath.ToString() + " is not supported.");
            }

            downloadSourceSoftware.Append("\"");

            if (!openArgumentsNullOrEmpty) {
                downloadSourceSoftware.Append(" ");
            }

            downloadSourceSoftware.Append(openArguments);
            software = downloadSourceSoftware.ToString();
            return ActivateAsync(downloadSourceModificationName);
        }
    }
}
