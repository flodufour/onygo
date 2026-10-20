namespace Onygo.Api.Activities;

/// <summary>
/// Types proposés dans l'appli ; « Other » s'accompagne d'un libellé libre (CustomType).
/// Sérialisés en texte dans le JSON et en base : ne pas renommer une valeur existante sans migration.
/// </summary>
public enum ActivityType
{
    Bar,
    Restaurant,
    Cafe,
    Cinema,
    Concert,
    Sport,
    Museum,
    Walk,
    Party,
    Other,
}

public enum ActivityStatus
{
    Open,
    Filled,
    Expired,
    Cancelled,
}
