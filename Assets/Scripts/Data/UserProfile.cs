using System;

namespace AlipiriAR.Data
{
    public readonly struct UserProfile
    {
        public readonly string Name;
        public readonly int Age;
        public readonly string LocaleCode;
        public readonly DateTime CreatedUtc;

        public UserProfile(string name, int age, string localeCode, DateTime createdUtc)
        {
            Name = name;
            Age = age;
            LocaleCode = localeCode;
            CreatedUtc = createdUtc;
        }
    }
}
