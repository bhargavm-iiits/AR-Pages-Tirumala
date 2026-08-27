using System;
using System.Collections;
using System.Collections.Generic;
using AlipiriAR.Utilities;
using Newtonsoft.Json;
using UnityEngine;

namespace AlipiriAR.Localization
{
    public readonly struct LocaleInfo
    {
        public readonly string Code;
        public readonly string NativeName;

        public LocaleInfo(string code, string nativeName)
        {
            Code = code;
            NativeName = nativeName;
        }
    }

    /// <summary>
    /// Loads StreamingAssets/Localization/{code}.json (a flat key→string map) and exposes
    /// lookups with an en fallback. Every label in the app resolves through here — see
    /// PLAN.md §04/§05/§07 for why this is custom rather than the Unity Localization package.
    /// </summary>
    public class LocalizationService
    {
        /// <summary>Locked language set — PLAN.md §13. UI text only exists for "en" at v1;
        /// the rest are selectable and logged, and fall back to English strings until translated.</summary>
        public static readonly LocaleInfo[] AvailableLocales =
        {
            new("en", "English"),
            new("te", "తెలుగు"),
            new("hi", "हिन्दी"),
            new("ta", "தமிழ்"),
            new("kn", "ಕನ್ನಡ"),
        };

        private const string FallbackLocale = "en";

        private readonly Dictionary<string, string> _current = new();
        private readonly Dictionary<string, string> _fallback = new();
        private readonly HashSet<string> _missingKeysLogged = new();

        public string CurrentLocale { get; private set; } = FallbackLocale;
        public event Action OnLocaleChanged;

        /// <summary>Coroutine — run this from a MonoBehaviour (AppBootstrap's driver owns the
        /// first call; screens call it again via Loc.SetLocale for a live language switch).</summary>
        public IEnumerator LoadLocale(string code)
        {
            if (string.IsNullOrEmpty(code)) code = FallbackLocale;

            // Always load "en" once, regardless of the requested code — the previous
            // "only load fallback when code != FallbackLocale" condition meant en.json was
            // never loaded at all on the extremely common path where the requested locale IS
            // "en" (fresh install, no saved preference), leaving every Loc.T() call returning
            // its raw key. Found on a real device; invisible in the Editor since nothing here
            // was ever run end-to-end there before now.
            if (_fallback.Count == 0)
                yield return LoadJsonInto(FallbackLocale, _fallback);

            Dictionary<string, string> target;
            if (code == FallbackLocale)
            {
                target = _fallback;
            }
            else
            {
                target = new Dictionary<string, string>();
                yield return LoadJsonInto(code, target);
            }

            _current.Clear();
            foreach (var kv in target) _current[kv.Key] = kv.Value;

            CurrentLocale = code;
            _missingKeysLogged.Clear();
            OnLocaleChanged?.Invoke();
        }

        public string Translate(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_current.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value))
                return args is { Length: > 0 } ? SafeFormat(value, args) : value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_missingKeysLogged.Add(key))
                Debug.LogWarning($"[Localization] Missing key: \"{key}\" (locale={CurrentLocale})");
#endif
            return key;
        }

        private static string SafeFormat(string value, object[] args)
        {
            try { return string.Format(value, args); }
            catch (FormatException) { return value; }
        }

        private IEnumerator LoadJsonInto(string code, Dictionary<string, string> target)
        {
            string relativePath = $"Localization/{code}.json";
            string json = null;
            yield return StreamingAssetsLoader.LoadText(relativePath, text => json = text);

            if (string.IsNullOrEmpty(json))
            {
                if (code != FallbackLocale)
                    Debug.LogWarning($"[Localization] No string table for locale \"{code}\", falling back to en.");
                yield break;
            }

            Dictionary<string, string> parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (JsonException e)
            {
                Debug.LogError($"[Localization] Failed to parse {relativePath}: {e.Message}");
                yield break;
            }

            if (parsed == null) yield break;
            foreach (var kv in parsed) target[kv.Key] = kv.Value;
        }
    }
}
