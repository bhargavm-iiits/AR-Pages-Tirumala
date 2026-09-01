using System.Collections;
using AlipiriAR.Audio;
using AlipiriAR.Database;
using AlipiriAR.Localization;
using AlipiriAR.UI;
using UnityEngine;

namespace AlipiriAR.Core
{
    /// <summary>
    /// Entry point for the whole app. Runs via [RuntimeInitializeOnLoadMethod] rather than a
    /// component placed in Pages.unity, so nothing about startup depends on scene wiring —
    /// every screen after this is built procedurally by UIRoot. Extended phase by phase:
    /// Phase 4 brings up localisation only; Phase 5 adds route/landmark loading; Phase 6 adds
    /// ProfileService and launches UIRoot.
    /// </summary>
    public static class AppBootstrap
    {
        public const string LocalePrefsKey = "profile.locale";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            ServiceLocator.Clear();

            var driverGo = new GameObject("~AppBootstrap");
            Object.DontDestroyOnLoad(driverGo);
            var driver = driverGo.AddComponent<BootstrapDriver>();
            driver.StartCoroutine(InitializeRoutine());
        }

        private static IEnumerator InitializeRoutine()
        {
            // Registered first and cheaply (no network wait here — the probe loop runs in the
            // background) so it's available to GoogleTileSession/TileBasemap the moment the Map
            // tab can possibly open (Docs/update1.md §02 Phase 2 items 4-5).
            ServiceLocator.Register(ConnectivityService.Create());

            var localization = new LocalizationService();
            ServiceLocator.Register(localization);

            string savedLocale = PlayerPrefs.GetString(LocalePrefsKey, "en");
            yield return localization.LoadLocale(savedLocale);

            var database = new JsonDatabase();
            ServiceLocator.Register(database);
            yield return database.LoadAll();

            // Resolved eagerly (not on first Listen tap) so Android's TTS engine has time to
            // finish its own async init — otherwise the very first Speak() call could fall
            // through to caption-only while the engine is still starting up.
            VoiceNavigationManager.Resolve();

            UIRoot.Bootstrap();
        }

        private class BootstrapDriver : MonoBehaviour { }
    }
}
