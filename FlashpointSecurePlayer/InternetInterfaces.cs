using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;

namespace FlashpointSecurePlayer {
    // this class used by the Custom Security Manager
    public static class InternetInterfaces {
        public const uint MUTZ_NOSAVEDFILECHECK = 0x00000001;
        public const uint MUTZ_ISFILE = 0x00000002;
        public const uint MUTZ_ACCEPT_WILDCARD_SCHEME = 0x00000080;
        public const uint MUTZ_ENFORCERESTRICTED = 0x00000100;
        public const uint MUTZ_RESERVED = 0x00000200;
        public const uint MUTZ_REQUIRESAVEDFILECHECK = 0x00000400;
        public const uint MUTZ_DONT_UNESCAPE = 0x00000800;
        public const uint MUTZ_DONT_USE_CACHE = 0x00001000;
        public const uint MUTZ_FORCE_INTRANET_FLAGS = 0x00002000;
        public const uint MUTZ_IGNORE_ZONE_MAPPINGS = 0x00004000;

        public const uint PUAF_DEFAULT = 0x00000000;
        public const uint PUAF_NOUI = 0x00000001;
        public const uint PUAF_ISFILE = 0x00000002;
        public const uint PUAF_WARN_IF_DENIED = 0x00000004;
        public const uint PUAF_FORCEUI_FOREGROUND = 0x00000008;
        public const uint PUAF_CHECK_TIFS = 0x00000010;
        public const uint PUAF_DONTCHECKBOXINDIALOG = 0x00000020;
        public const uint PUAF_TRUSTED = 0x00000040;
        public const uint PUAF_ACCEPT_WILDCARD_SCHEME = 0x00000080;
        public const uint PUAF_ENFORCERESTRICTED = 0x00000100;
        public const uint PUAF_NOSAVEDFILECHECK = 0x00000200;
        public const uint PUAF_REQUIRESAVEDFILECHECK = 0x00000400;
        public const uint PUAF_DONT_USE_CACHE = 0x00001000;
        public const uint PUAF_LMZ_UNLOCKED = 0x00010000;
        public const uint PUAF_LMZ_LOCKED = 0x00020000;
        public const uint PUAF_DEFAULTZONEPOL = 0x00040000;
        public const uint PUAF_NPL_USE_LOCKED_IF_RESTRICTED = 0x00080000;
        public const uint PUAF_NOUIIFLOCKED = 0x00100000;
        public const uint PUAF_DRAGPROTOCOLCHECK = 0x00200000;

        public const uint URLACTION_MIN = 0x00001000;

        public const uint URLACTION_DOWNLOAD_MIN = 0x00001000;
        public const uint URLACTION_DOWNLOAD_SIGNED_ACTIVEX = 0x00001001;
        public const uint URLACTION_DOWNLOAD_UNSIGNED_ACTIVEX = 0x00001004;
        public const uint URLACTION_DOWNLOAD_CURR_MAX = 0x00001004;
        public const uint URLACTION_DOWNLOAD_MAX = 0x000011FF;

        public const uint URLACTION_ACTIVEX_MIN = 0x00001200;
        public const uint URLACTION_ACTIVEX_RUN = 0x00001200;
        public const uint URLPOLICY_ACTIVEX_CHECK_LIST = 0x00010000;
        public const uint URLACTION_ACTIVEX_OVERRIDE_OBJECT_SAFETY = 0x00001201;
        public const uint URLACTION_ACTIVEX_OVERRIDE_DATA_SAFETY = 0x00001202;
        public const uint URLACTION_ACTIVEX_OVERRIDE_SCRIPT_SAFETY = 0x00001203;
        public const uint URLACTION_ACTIVEX_CONFIRM_NOOBJECTSAFETY = 0x00001204;
        public const uint URLACTION_ACTIVEX_TREATASUNTRUSTED = 0x00001205;
        public const uint URLACTION_ACTIVEX_NO_WEBOC_SCRIPT = 0x00001206;
        public const uint URLACTION_ACTIVEX_OVERRIDE_REPURPOSEDETECTION = 0x00001207;
        public const uint URLACTION_ACTIVEX_OVERRIDE_OPTIN = 0x00001208;
        public const uint URLACTION_ACTIVEX_SCRIPTLET_RUN = 0x00001209;
        public const uint URLACTION_ACTIVEX_DYNSRC_VIDEO_AND_ANIMATION = 0x0000120A;
        public const uint URLACTION_ACTIVEX_OVERRIDE_DOMAINLIST = 0x0000120B;
        public const uint URLACTION_ACTIVEX_CURR_MAX = 0x0000120B;
        public const uint URLACTION_ACTIVEX_MAX = 0x000013FF;

        public const uint URLACTION_SCRIPT_MIN = 0x00001400;
        public const uint URLACTION_SCRIPT_RUN = 0x00001400;
        public const uint URLACTION_SCRIPT_OVERRIDE_SAFETY = 0x00001401;
        public const uint URLACTION_SCRIPT_JAVA_USE = 0x00001402;
        public const uint URLACTION_SCRIPT_SAFE_ACTIVEX = 0x00001405;
        public const uint URLACTION_CROSS_DOMAIN_DATA = 0x00001406;
        public const uint URLACTION_SCRIPT_PASTE = 0x00001407;
        public const uint URLACTION_ALLOW_XDOMAIN_SUBFRAME_RESIZE = 0x00001408;
        public const uint URLACTION_SCRIPT_XSSFILTER = 0x00001409;
        public const uint URLACTION_SCRIPT_CURR_MAX = 0x00001409;
        public const uint URLACTION_SCRIPT_MAX = 0x000015FF;

        public const uint URLACTION_HTML_MIN = 0x00001600;
        public const uint URLACTION_HTML_SUBMIT_FORMS = 0x00001601;
        public const uint URLACTION_HTML_SUBMIT_FORMS_FROM = 0x00001602;
        public const uint URLACTION_HTML_SUBMIT_FORMS_TO = 0x00001603;
        public const uint URLACTION_HTML_FONT_DOWNLOAD = 0x00001604;
        public const uint URLACTION_HTML_JAVA_RUN = 0x00001605;
        public const uint URLACTION_HTML_USERDATA_SAVE = 0x00001606;
        public const uint URLACTION_HTML_SUBFRAME_NAVIGATE = 0x00001607;
        public const uint URLACTION_HTML_META_REFRESH = 0x00001608;
        public const uint URLACTION_HTML_MIXED_CONTENT = 0x00001609;
        public const uint URLACTION_HTML_INCLUDE_FILE_PATH = 0x0000160A;

        public const uint URLACTION_HTML_MAX = 0x000017FF;

        public const uint URLACTION_SHELL_MIN = 0x00001800;
        public const uint URLACTION_SHELL_INSTALL_DTITEMS = 0x00001800;
        public const uint URLACTION_SHELL_MOVE_OR_COPY = 0x00001802;
        public const uint URLACTION_SHELL_FILE_DOWNLOAD = 0x00001803;
        public const uint URLACTION_SHELL_VERB = 0x00001804;
        public const uint URLACTION_SHELL_WEBVIEW_VERB = 0x00001805;
        public const uint URLACTION_SHELL_SHELLEXECUTE = 0x00001806;

        public const uint URLACTION_SHELL_EXECUTE_HIGHRISK = 0x00001806;
        public const uint URLACTION_SHELL_EXECUTE_MODRISK = 0x00001807;
        public const uint URLACTION_SHELL_EXECUTE_LOWRISK = 0x00001808;
        public const uint URLACTION_SHELL_POPUPMGR = 0x00001809;
        public const uint URLACTION_SHELL_RTF_OBJECTS_LOAD = 0x0000180A;
        public const uint URLACTION_SHELL_ENHANCED_DRAGDROP_SECURITY = 0x0000180B;
        public const uint URLACTION_SHELL_EXTENSIONSECURITY = 0x0000180C;
        public const uint URLACTION_SHELL_SECURE_DRAGSOURCE = 0x0000180D;

        public const uint URLACTION_SHELL_REMOTEQUERY = 0x0000180E;
        public const uint URLACTION_SHELL_PREVIEW = 0x0000180F;

        public const uint URLACTION_SHELL_CURR_MAX = 0x0000180F;
        public const uint URLACTION_SHELL_MAX = 0x000019FF;

        public const uint URLACTION_NETWORK_MIN = 0x00001A00;

        public const uint URLACTION_CREDENTIALS_USE = 0x00001A00;
        public const uint URLPOLICY_CREDENTIALS_SILENT_LOGON_OK = 0x00000000;
        public const uint URLPOLICY_CREDENTIALS_MUST_PROMPT_USER = 0x00010000;
        public const uint URLPOLICY_CREDENTIALS_CONDITIONAL_PROMPT = 0x00020000;
        public const uint URLPOLICY_CREDENTIALS_ANONYMOUS_ONLY = 0x00030000;

        public const uint URLACTION_AUTHENTICATE_CLIENT = 0x00001A01;
        public const uint URLPOLICY_AUTHENTICATE_CLEARTEXT_OK = 0x00000000;
        public const uint URLPOLICY_AUTHENTICATE_CHALLENGE_RESPONSE = 0x00010000;
        public const uint URLPOLICY_AUTHENTICATE_MUTUAL_ONLY = 0x00030000;

        public const uint URLACTION_COOKIES = 0x00001A02;
        public const uint URLACTION_COOKIES_SESSION = 0x00001A03;

        public const uint URLACTION_CLIENT_CERT_PROMPT = 0x00001A04;

        public const uint URLACTION_COOKIES_THIRD_PARTY = 0x00001A05;
        public const uint URLACTION_COOKIES_SESSION_THIRD_PARTY = 0x00001A06;

        public const uint URLACTION_COOKIES_ENABLED = 0x00001A10;

        public const uint URLACTION_NETWORK_CURR_MAX = 0x00001A10;
        public const uint URLACTION_NETWORK_MAX = 0x00001BFF;

        public const uint URLACTION_JAVA_MIN = 0x00001C00;
        public const uint URLACTION_JAVA_PERMISSIONS = 0x00001C00;
        public const uint URLPOLICY_JAVA_PROHIBIT = 0x00000000;
        public const uint URLPOLICY_JAVA_HIGH = 0x00010000;
        public const uint URLPOLICY_JAVA_MEDIUM = 0x00020000;
        public const uint URLPOLICY_JAVA_LOW = 0x00030000;
        public const uint URLPOLICY_JAVA_CUSTOM = 0x00800000;
        public const uint URLACTION_JAVA_CURR_MAX = 0x00001C00;
        public const uint URLACTION_JAVA_MAX = 0x00001CFF;

        public const uint URLACTION_INFODELIVERY_MIN = 0x00001D00;
        public const uint URLACTION_INFODELIVERY_NO_ADDING_CHANNELS = 0x00001D00;
        public const uint URLACTION_INFODELIVERY_NO_EDITING_CHANNELS = 0x00001D01;
        public const uint URLACTION_INFODELIVERY_NO_REMOVING_CHANNELS = 0x00001D02;
        public const uint URLACTION_INFODELIVERY_NO_ADDING_SUBSCRIPTIONS = 0x00001D03;
        public const uint URLACTION_INFODELIVERY_NO_EDITING_SUBSCRIPTIONS = 0x00001D04;
        public const uint URLACTION_INFODELIVERY_NO_REMOVING_SUBSCRIPTIONS = 0x00001D05;
        public const uint URLACTION_INFODELIVERY_NO_CHANNEL_LOGGING = 0x00001D06;
        public const uint URLACTION_INFODELIVERY_CURR_MAX = 0x00001D06;
        public const uint URLACTION_INFODELIVERY_MAX = 0x00001DFF;
        public const uint URLACTION_CHANNEL_SOFTDIST_MIN = 0x00001E00;
        public const uint URLACTION_CHANNEL_SOFTDIST_PERMISSIONS = 0x00001E05;
        public const uint URLPOLICY_CHANNEL_SOFTDIST_PROHIBIT = 0x00010000;
        public const uint URLPOLICY_CHANNEL_SOFTDIST_PRECACHE = 0x00020000;
        public const uint URLPOLICY_CHANNEL_SOFTDIST_AUTOINSTALL = 0x00030000;
        public const uint URLACTION_CHANNEL_SOFTDIST_MAX = 0x00001EFF;

        public const uint URLACTION_MANAGED_SIGNED = 0x00002001;
        public const uint URLACTION_MANAGED_UNSIGNED = 0x00002001;
        public const uint URLACTION_DOTNET_USERCONTROLS = 0x00002005;

        public const uint URLACTION_BEHAVIOR_MIN = 0x00002000;
        public const uint URLACTION_BEHAVIOR_RUN = 0x00002000;
        public const uint URLPOLICY_BEHAVIOR_CHECK_LIST = 0x00010000;

        public const uint URLACTION_FEATURE_MIN = 0x00002100;
        public const uint URLACTION_FEATURE_MIME_SNIFFING = 0x00002100;
        public const uint URLACTION_FEATURE_ZONE_ELEVATION = 0x00002101;
        public const uint URLACTION_FEATURE_WINDOW_RESTRICTIONS = 0x00002102;
        public const uint URLACTION_FEATURE_SCRIPT_STATUS_BAR = 0x00002103;
        public const uint URLACTION_FEATURE_FORCE_ADDR_AND_STATUS = 0x00002104;
        public const uint URLACTION_FEATURE_BLOCK_INPUT_PROMPTS = 0x00002105;
        public const uint URLACTION_FEATURE_DATA_BINDING = 0x00002106;
        public const uint URLACTION_FEATURE_CROSSDOMAIN_FOCUS_CHANGE = 0x00002107;

        public const uint URLACTION_AUTOMATIC_DOWNLOAD_UI_MIN = 0x00002200;
        public const uint URLACTION_AUTOMATIC_DOWNLOAD_UI = 0x00002200;
        public const uint URLACTION_AUTOMATIC_ACTIVEX_UI = 0x00002201;

        public const uint URLACTION_ALLOW_RESTRICTEDPROTOCOLS = 0x00002300;

        public const uint URLACTION_ALLOW_APEVALUATION = 0x00002301;
        public const uint URLACTION_WINDOWS_BROWSER_APPLICATIONS = 0x00002400;
        public const uint URLACTION_XPS_DOCUMENTS = 0x00002401;
        public const uint URLACTION_LOOSE_XAML = 0x00002402;
        public const uint URLACTION_LOWRIGHTS = 0x00002500;
        public const uint URLACTION_WINFX_SETUP = 0x00002600;
        public const uint URLACTION_INPRIVATE_BLOCKING = 0x00002700;

        public const uint URLACTION_ALLOW_AUDIO_VIDEO = 0x00002701;
        public const uint URLACTION_ALLOW_ACTIVEX_FILTERING = 0x00002702;
        public const uint URLACTION_ALLOW_STRUCTURED_STORAGE_SNIFFING = 0x00002703;
        public const uint URLACTION_ALLOW_AUDIO_VIDEO_PLUGINS = 0x00002704;
        public const uint URLACTION_ALLOW_ZONE_ELEVATION_VIA_OPT_OUT = 0x00002705;
        public const uint URLACTION_ALLOW_ZONE_ELEVATION_OPT_OUT_ADDITION = 0x00002706;
        public const uint URLACTION_ALLOW_CROSSDOMAIN_DROP_WITHIN_WINDOW = 0x00002708;
        public const uint URLACTION_ALLOW_CROSSDOMAIN_DROP_ACROSS_WINDOWS = 0x00002709;
        public const uint URLACTION_ALLOW_CROSSDOMAIN_APPCACHE_MANIFEST = 0x0000270A;
        public const uint URLACTION_ALLOW_RENDER_LEGACY_DXTFILTERS = 0x0000270B;

        public const uint URLPOLICY_ALLOW = 0x00000000;
        public const uint URLPOLICY_QUERY = 0x00000001;
        public const uint URLPOLICY_DISALLOW = 0x00000003;
        public const uint URLPOLICY_NOTIFY_ON_ALLOW = 0x00000010;
        public const uint URLPOLICY_NOTIFY_ON_DISALLOW = 0x00000020;
        public const uint URLPOLICY_LOG_ON_ALLOW = 0x00000040;
        public const uint URLPOLICY_LOG_ON_DISALLOW = 0x00000080;

        public const uint URLPOLICY_MASK_PERMISSIONS = 0x0000000F;

        public const uint URLPOLICY_DONTCHECKDLGBOX = 0x00000100;

        [ComImport, Guid("CB728B20-F786-11CE-92AD-00AA00A74CD0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IProfferService {
            [PreserveSig]
            int ProfferService(ref Guid guidService, IServiceProvider psp, out int cookie);

            [PreserveSig]
            int RevokeService(int cookie);
        }

        public static Guid SID_SProfferService = Marshal.GenerateGuidForType(typeof(IProfferService));
        public static Guid IID_IProfferService = Marshal.GenerateGuidForType(typeof(IProfferService));

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IServiceProvider {
            [PreserveSig]
            int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        }

        public static Guid IID_IServiceProvider = Marshal.GenerateGuidForType(typeof(IServiceProvider));

        [ComImport, Guid("79EAC9EE-BAF9-11CE-8C82-00AA004BA90B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IInternetSecurityManager {
            [PreserveSig]
            int SetSecuritySite(IntPtr pSite);
            
            [PreserveSig]
            int GetSecuritySite(out IntPtr pSite);
            
            [PreserveSig]
            int MapUrlToZone(
                [MarshalAs(UnmanagedType.LPWStr)]
                string pwszUrl,
                
                ref uint pdwZone,
                uint dwFlags
            );
            
            [PreserveSig]
            int GetSecurityId(
                [MarshalAs(UnmanagedType.LPWStr)]
                string pwszUrl,
                
                [MarshalAs(UnmanagedType.LPArray)] byte[] pbSecurityId,
                ref uint pcbSecurityId,
                uint dwReserved
            );
            
            [PreserveSig]
            int ProcessUrlAction(
                [MarshalAs(UnmanagedType.LPWStr)]
                string pwszUrl,
                
                uint dwAction,
                ref uint pPolicy,
                uint cbPolicy,
                IntPtr pContext,
                uint cbContext,
                uint dwFlags,
                uint dwReserved
            );
            
            [PreserveSig]
            int QueryCustomPolicy(
                [MarshalAs(UnmanagedType.LPWStr)]
                string pwszUrl,
                
                ref Guid guidKey,
                ref byte ppPolicy,
                ref uint pcbPolicy,
                ref byte pContext,
                uint cbContext,
                uint dwReserved
            );
            
            [PreserveSig]
            int SetZoneMapping(
                uint dwZone,
                
                [MarshalAs(UnmanagedType.LPWStr)]
                string lpszPattern,
                
                uint dwFlags
            );
            
            [PreserveSig]
            int GetZoneMappings(uint dwZone, out IEnumString ppenumString, uint dwFlags);
        }

        public static Guid IID_IInternetSecurityManager = Marshal.GenerateGuidForType(typeof(IInternetSecurityManager));
    }
}
