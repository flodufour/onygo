namespace Onygo.Api.Profiles;

public class Profile(string userId, string firstName, DateOnly birthDate)
{
    public string UserId { get; private set; } = userId;

    public string FirstName { get; private set; } = firstName;

    // Non modifiable après création : sinon la vérification des 18 ans pourrait être contournée.
    public DateOnly BirthDate { get; private set; } = birthDate;

    public void ChangeFirstName(string firstName) => FirstName = firstName;
}
