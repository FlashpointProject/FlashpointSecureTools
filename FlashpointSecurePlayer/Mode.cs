using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlashpointSecurePlayer {
    public static class Mode {
        public enum NAME {
            WEB_BROWSER,
            SOFTWARE
        }

        public enum WEB_BROWSER_NAME {
            INTERNET_EXPLORER
        }

        public enum URL_ACTION {
            OPEN,
            DOWNLOAD
        }

        public static NAME name = NAME.WEB_BROWSER;
        public static WEB_BROWSER_NAME webBrowserName = WEB_BROWSER_NAME.INTERNET_EXPLORER;
        public static string commandLine = String.Empty;
        public static URL_ACTION urlAction = URL_ACTION.OPEN;
        public static bool hideWindow = false;
    }
}
