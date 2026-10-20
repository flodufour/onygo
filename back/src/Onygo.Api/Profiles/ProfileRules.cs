namespace Onygo.Api.Profiles;

public static class ProfileRules
{
    public const int MinimumAge = 18;
    public const int MaxFirstNameLength = 50;

    // Né un 29 février : les années non bissextiles, l'anniversaire tombe le 1er mars (lecture prudente pour les 18 ans).
    public static int AgeOn(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        var hasHadBirthdayThisYear = (today.Month, today.Day).CompareTo((birthDate.Month, birthDate.Day)) >= 0;
        return hasHadBirthdayThisYear ? age : age - 1;
    }

    public static bool IsAdult(DateOnly birthDate, DateOnly today) => AgeOn(birthDate, today) >= MinimumAge;
}
