using AlipiriAR.Core;
using UnityEngine;

namespace AlipiriAR.Localization
{
    /// <summary>Static facade over the registered LocalizationService — <c>Loc.T("home.ar_nav")</c>
    /// is the one-line call every screen uses instead of hardcoding English strings.</summary>
    public static class Loc
    {
        public static string T(string key, params object[] args)
        {
            if (!ServiceLocator.TryGet<LocalizationService>(out var service))
            {
                Debug.LogWarning($"[Loc] LocalizationService not ready — returning raw key \"{key}\".");
                return key;
            }
            return service.Translate(key, args);
        }

        public static string CurrentLocale =>
            ServiceLocator.TryGet<LocalizationService>(out var s) ? s.CurrentLocale : "en";

        /// <summary>Starts a live language switch. AppBootstrap's driver runs the coroutine;
        /// UI callers (login screen, settings) go through this rather than the service directly.</summary>
        public static void SetLocale(string code)
        {
            if (!ServiceLocator.TryGet<LocalizationService>(out var service)) return;
            LocaleSwitcher.Run(service, code);
        }

        public static event System.Action OnLocaleChanged
        {
            add { if (ServiceLocator.TryGet<LocalizationService>(out var s)) s.OnLocaleChanged += value; }
            remove { if (ServiceLocator.TryGet<LocalizationService>(out var s)) s.OnLocaleChanged -= value; }
        }
    }

    /// <summary>Tiny hidden coroutine host so Loc.SetLocale can be called from anywhere
    /// (a static context) without every caller needing its own MonoBehaviour.</summary>
    internal class LocaleSwitcher : MonoBehaviour
    {
        private static LocaleSwitcher _instance;

        public static void Run(LocalizationService service, string code)
        {
            if (_instance == null)
            {
                var go = new GameObject("~LocaleSwitcher");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<LocaleSwitcher>();
            }
            _instance.StartCoroutine(service.LoadLocale(code));
        }
    }
}
