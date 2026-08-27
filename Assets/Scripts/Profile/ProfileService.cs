using System;
using AlipiriAR.Core;
using AlipiriAR.Data;
using UnityEngine;

namespace AlipiriAR.Profile
{
    /// <summary>Local profile storage — PLAN.md §12: this is profile capture and a visit log,
    /// not authentication. No password, nothing sent anywhere. Persisted to PlayerPrefs; the
    /// Excel export (ExcelLoginStore) is a separate, append-only record of every login.</summary>
    public class ProfileService
    {
        private const string NameKey = "profile.name";
        private const string AgeKey = "profile.age";
        private const string LocaleKey = "profile.locale";
        private const string CreatedKey = "profile.created_utc";

        public bool HasProfile => PlayerPrefs.HasKey(NameKey);

        public UserProfile? Current
        {
            get
            {
                if (!HasProfile) return null;
                string createdRaw = PlayerPrefs.GetString(CreatedKey, string.Empty);
                DateTime.TryParse(createdRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var created);
                return new UserProfile(
                    PlayerPrefs.GetString(NameKey, string.Empty),
                    PlayerPrefs.GetInt(AgeKey, 0),
                    PlayerPrefs.GetString(LocaleKey, "en"),
                    created);
            }
        }

        public UserProfile Save(string name, int age, string localeCode)
        {
            var profile = new UserProfile(name.Trim(), age, localeCode, DateTime.UtcNow);
            PlayerPrefs.SetString(NameKey, profile.Name);
            PlayerPrefs.SetInt(AgeKey, profile.Age);
            PlayerPrefs.SetString(LocaleKey, profile.LocaleCode);
            PlayerPrefs.SetString(CreatedKey, profile.CreatedUtc.ToString("o"));
            PlayerPrefs.Save();
            return profile;
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(NameKey);
            PlayerPrefs.DeleteKey(AgeKey);
            PlayerPrefs.DeleteKey(LocaleKey);
            PlayerPrefs.DeleteKey(CreatedKey);
        }

        public static ProfileService Resolve()
        {
            if (!ServiceLocator.TryGet<ProfileService>(out var service))
            {
                service = new ProfileService();
                ServiceLocator.Register(service);
            }
            return service;
        }
    }
}
