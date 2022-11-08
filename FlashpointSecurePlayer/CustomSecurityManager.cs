using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

using static FlashpointSecurePlayer.Shared;
using static FlashpointSecurePlayer.Shared.Exceptions;
using static FlashpointSecurePlayer.InternetInterfaces;

namespace FlashpointSecurePlayer {
    public class CustomSecurityManager : InternetInterfaces.IServiceProvider, InternetInterfaces.IInternetSecurityManager {
        private readonly byte[] FLASH_CONTEXT = new byte[16] { 110, 219, 124, 210, 109, 174, 207, 17, 150, 184, 68, 69, 83, 84, 0, 0 };
        private const string FLASH_EXTENSION_UPPER = ".SWF";

        private bool UseFlashActiveXControl { get; set; } = false;

        // https://docs.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/ms537182(v=vs.85)?redirectedfrom=MSDN
        public CustomSecurityManager(System.Windows.Forms.WebBrowser webBrowser, bool useFlashActiveXControl = false) {
            InternetInterfaces.IServiceProvider webBrowserServiceProviderInterface = webBrowser.ActiveXInstance as InternetInterfaces.IServiceProvider;
            IntPtr profferServiceInterfacePointer = IntPtr.Zero;
            
            int err = webBrowserServiceProviderInterface.QueryService(ref InternetInterfaces.SID_SProfferService, ref InternetInterfaces.IID_IProfferService, out profferServiceInterfacePointer);

            if (err != S_OK) {
                Marshal.ThrowExceptionForHR(err);
                return;
            }
                
            if (!(Marshal.GetObjectForIUnknown(profferServiceInterfacePointer) is InternetInterfaces.IProfferService profferServiceInterface)) {
                Marshal.ThrowExceptionForHR(err);
                return;
            }

            err = profferServiceInterface.ProfferService(ref InternetInterfaces.IID_IInternetSecurityManager, this, out int cookie);

            if (err != S_OK) {
                Marshal.ThrowExceptionForHR(err);
                return;
            }

            UseFlashActiveXControl = useFlashActiveXControl;
        }

        int InternetInterfaces.IServiceProvider.QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject) {
            ppvObject = IntPtr.Zero;

            if (guidService.CompareTo(InternetInterfaces.IID_IInternetSecurityManager) == 0) {
                return Marshal.QueryInterface(Marshal.GetIUnknownForObject(this), ref riid, out ppvObject);
            }
            return E_NOINTERFACE;
        }

        int InternetInterfaces.IInternetSecurityManager.SetSecuritySite(IntPtr pSite) {
            return INET_E_DEFAULT_ACTION;
        }

        int InternetInterfaces.IInternetSecurityManager.GetSecuritySite(out IntPtr pSite) {
            pSite = IntPtr.Zero;
            return INET_E_DEFAULT_ACTION;
        }

        int InternetInterfaces.IInternetSecurityManager.MapUrlToZone([MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, ref uint pdwZone, uint dwFlags) {
            // behave like local intranet
            pdwZone = 1;

            // don't map zone for file:// URLs, that's outside the proxy
            if ((dwFlags & MUTZ_ISFILE) == MUTZ_ISFILE) {
                return INET_E_DEFAULT_ACTION;
            }

            // error if URL is null
            if (pwszUrl == null) {
                return E_INVALIDARG;
            }

            // unescape URL if needed
            if ((dwFlags & MUTZ_DONT_UNESCAPE) != MUTZ_DONT_UNESCAPE) {
                try {
                    pwszUrl = Uri.UnescapeDataString(pwszUrl);
                } catch (ArgumentNullException) {
                    // error if URL is null
                    return E_INVALIDARG;
                }
            }

            // error if not URI formatted
            Uri flashpointURI;

            try {
                flashpointURI = new Uri(pwszUrl);
            } catch {
                return E_INVALIDARG;
            }

            if (!TestInternetURI(flashpointURI)) {
                // we've wandered off from Flashpoint Server, revert to default zone settings
                return INET_E_DEFAULT_ACTION;
            }
            return S_OK;
        }

        int InternetInterfaces.IInternetSecurityManager.GetSecurityId([MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, [MarshalAs(UnmanagedType.LPArray)] byte[] pbSecurityId, ref uint pcbSecurityId, uint dwReserved) {
            return INET_E_DEFAULT_ACTION;
        }

        int InternetInterfaces.IInternetSecurityManager.ProcessUrlAction([MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, uint dwAction, out uint pPolicy, uint cbPolicy, IntPtr pContext, uint cbContext, uint dwFlags, uint dwReserved) {
            pPolicy = URLPOLICY_DISALLOW;

            if (cbPolicy < Marshal.SizeOf(pPolicy.GetType())) {
                return S_FALSE;
            }

            // don't process file:// URLS, they are outside the proxy
            if ((dwFlags & PUAF_ISFILE) == PUAF_ISFILE) {
                return INET_E_DEFAULT_ACTION;
            }

            // error if URL is null
            if (pwszUrl == null) {
                return E_INVALIDARG;
            }

            // error if not URI formatted
            Uri flashpointURI;

            try {
                flashpointURI = new Uri(pwszUrl);
            } catch {
                return E_INVALIDARG;
            }

            try {
                byte[] context = new byte[cbContext];

                if (pContext != IntPtr.Zero) {
                    Marshal.Copy(pContext, context, 0, (int)cbContext);
                }

                if (!UseFlashActiveXControl) {
                    if (context.SequenceEqual(FLASH_CONTEXT) || Path.GetExtension(flashpointURI.LocalPath).ToUpperInvariant() == FLASH_EXTENSION_UPPER) {
                        if (dwAction == URLACTION_ACTIVEX_TREATASUNTRUSTED) { // don't trust Flash ActiveX Controls
                            pPolicy = URLPOLICY_ALLOW;
                        }
                        return S_OK;
                    }
                }
            } catch {
                return S_FALSE;
            }

            if (!TestInternetURI(flashpointURI)) {
                // we've wandered off from Flashpoint Server, don't allow zone elevation
                if (dwAction == URLACTION_FEATURE_ZONE_ELEVATION) {
                    return S_OK;
                }
                return INET_E_DEFAULT_ACTION;
            }

            if (dwAction == URLACTION_ACTIVEX_TREATASUNTRUSTED || // trust other ActiveX Controls
                dwAction == URLACTION_HTML_MIXED_CONTENT || // block HTTPS content on HTTP websites for Flashpoint Proxy
                dwAction == URLACTION_CLIENT_CERT_PROMPT || // don't allow invalid certificates
                dwAction == URLACTION_AUTOMATIC_ACTIVEX_UI || // do not display the install dialog for ActiveX Controls
                dwAction == URLACTION_ALLOW_APEVALUATION || // the phishing filter is not applicable to this application
                dwAction == URLACTION_LOWRIGHTS || // turn off Protected Mode
                dwAction == URLACTION_ALLOW_ACTIVEX_FILTERING) { // don't allow ActiveX filtering
                return S_OK;
            }

            pPolicy = URLPOLICY_JAVA_LOW;

            if (dwAction == URLACTION_JAVA_PERMISSIONS) { // allow Java applets to be as terrible as they need to be to function
                return S_OK;
            }

            pPolicy = 0x00010000;

            if (dwAction == 0x00002007) { // undocumented action: permissions for components with manifests
                return S_OK;
            }

            pPolicy = URLPOLICY_ALLOW;

            if ((dwAction >= URLACTION_DOWNLOAD_MIN && dwAction <= URLACTION_DOWNLOAD_MAX) || // allow downloading ActiveX Controls, scripts, etc.
                (dwAction >= URLACTION_ACTIVEX_MIN && dwAction <= URLACTION_ACTIVEX_MAX) || // allow ActiveX Controls
                (dwAction >= URLACTION_SCRIPT_MIN && dwAction <= URLACTION_SCRIPT_MAX) || // allow scripts
                (dwAction >= URLACTION_HTML_MIN && dwAction <= URLACTION_HTML_MAX) || // allow forms, fonts, meta elements, etc.
                (dwAction >= URLACTION_JAVA_MIN && dwAction <= URLACTION_JAVA_MAX) || // allow Java applets
                dwAction == URLACTION_COOKIES || // allow all cookies
                dwAction == URLACTION_COOKIES_SESSION ||
                dwAction == URLACTION_COOKIES_THIRD_PARTY ||
                dwAction == URLACTION_COOKIES_SESSION_THIRD_PARTY ||
                dwAction == URLACTION_COOKIES_ENABLED ||
                dwAction == URLACTION_BEHAVIOR_RUN || // allow running behaviours
                dwAction == URLACTION_MANAGED_SIGNED || // run components regardless of if they're signed or not
                dwAction == URLACTION_MANAGED_UNSIGNED ||
                dwAction == URLACTION_DOTNET_USERCONTROLS || // allow .NET user controls
                dwAction == URLACTION_FEATURE_ZONE_ELEVATION || // allow entering this zone from about:blank
                dwAction == URLACTION_FEATURE_DATA_BINDING || // allow databinding
                dwAction == URLACTION_FEATURE_CROSSDOMAIN_FOCUS_CHANGE || // allow crossdomain
                dwAction == URLACTION_ALLOW_RESTRICTEDPROTOCOLS || // allow active content regardless of if the protocol is restricted
                dwAction == URLACTION_ALLOW_AUDIO_VIDEO || // allow audio and video always
                dwAction == URLACTION_ALLOW_AUDIO_VIDEO_PLUGINS ||
                dwAction == URLACTION_ALLOW_CROSSDOMAIN_DROP_WITHIN_WINDOW || // allow crossdomain, again
                dwAction == URLACTION_ALLOW_CROSSDOMAIN_DROP_ACROSS_WINDOWS ||
                dwAction == URLACTION_ALLOW_CROSSDOMAIN_APPCACHE_MANIFEST ||
                dwAction == URLACTION_ALLOW_RENDER_LEGACY_DXTFILTERS) { // allow DX transforms
                return S_OK;
            }
            // default zone setting for any other action
            return INET_E_DEFAULT_ACTION;
        }

        int InternetInterfaces.IInternetSecurityManager.QueryCustomPolicy([MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, ref Guid guidKey, ref byte ppPolicy, ref uint pcbPolicy, ref byte pContext, uint cbContext, uint dwReserved) {
            return INET_E_DEFAULT_ACTION;
        }

        int InternetInterfaces.IInternetSecurityManager.SetZoneMapping(uint dwZone, [MarshalAs(UnmanagedType.LPWStr)] string lpszPattern, uint dwFlags) {
            return INET_E_DEFAULT_ACTION;
        }

        int InternetInterfaces.IInternetSecurityManager.GetZoneMappings(uint dwZone, out IEnumString ppenumString, uint dwFlags) {
            ppenumString = null;
            return INET_E_DEFAULT_ACTION;
        }
    }
}
