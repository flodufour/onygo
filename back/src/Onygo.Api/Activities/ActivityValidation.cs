namespace Onygo.Api.Activities;

/// <summary>
/// Renvoie les erreurs par champ, au format attendu par ValidationProblem (clés en camelCase comme le JSON).
/// </summary>
public static class ActivityValidation
{
    private sealed record TextField(string Name, int MaxLength, string RequiredCode, string TooLongCode);

    private static readonly TextField Title =
        new("title", ActivityRules.MaxTitleLength, ActivityErrorCodes.TitleRequired, ActivityErrorCodes.TitleTooLong);

    private static readonly TextField Hook =
        new("hook", ActivityRules.MaxHookLength, ActivityErrorCodes.HookRequired, ActivityErrorCodes.HookTooLong);

    private static readonly TextField CustomType =
        new("customType", ActivityRules.MaxCustomTypeLength, ActivityErrorCodes.CustomTypeRequired, ActivityErrorCodes.CustomTypeTooLong);

    private static readonly TextField Address =
        new("location.address", ActivityRules.MaxAddressLength, ActivityErrorCodes.AddressRequired, ActivityErrorCodes.AddressTooLong);

    public static Dictionary<string, string[]> ValidateCreation(CreateActivityRequest request, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        AddTypeErrors(errors, request.Type, request.CustomType);
        AddTextError(errors, Title, request.Title);
        AddTextError(errors, Hook, request.Hook);
        AddLocationErrors(errors, request.Location);
        AddStartsAtError(errors, request.StartsAt, now);
        return errors;
    }

    private static void AddTypeErrors(Dictionary<string, string[]> errors, ActivityType? type, string? customType)
    {
        if (type is null)
        {
            errors["type"] = [ActivityErrorCodes.TypeRequired];
        }
        else if (type == ActivityType.Other)
        {
            AddTextError(errors, CustomType, customType);
        }
        else if (!string.IsNullOrWhiteSpace(customType))
        {
            errors[CustomType.Name] = [ActivityErrorCodes.CustomTypeNotAllowed];
        }
    }

    private static void AddTextError(Dictionary<string, string[]> errors, TextField field, string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            errors[field.Name] = [field.RequiredCode];
        }
        else if (trimmed.Length > field.MaxLength)
        {
            errors[field.Name] = [field.TooLongCode];
        }
    }

    private static void AddLocationErrors(Dictionary<string, string[]> errors, ActivityLocationRequest? location)
    {
        if (location is null)
        {
            errors["location"] = [ActivityErrorCodes.LocationRequired];
            return;
        }

        AddTextError(errors, Address, location.Address);
        if (location.Latitude is not (>= -90 and <= 90))
        {
            errors["location.latitude"] = [ActivityErrorCodes.LatitudeInvalid];
        }

        if (location.Longitude is not (>= -180 and <= 180))
        {
            errors["location.longitude"] = [ActivityErrorCodes.LongitudeInvalid];
        }
    }

    private static void AddStartsAtError(Dictionary<string, string[]> errors, DateTimeOffset? startsAt, DateTimeOffset now)
    {
        if (startsAt is null)
        {
            errors["startsAt"] = [ActivityErrorCodes.StartsAtRequired];
        }
        else if (startsAt <= now)
        {
            errors["startsAt"] = [ActivityErrorCodes.StartsAtInPast];
        }
        else if (ActivityRules.AreApplicationsClosed(startsAt.Value, now))
        {
            errors["startsAt"] = [ActivityErrorCodes.StartsAtTooSoon];
        }
        else if (startsAt > now + ActivityRules.MaxSchedulingHorizon)
        {
            errors["startsAt"] = [ActivityErrorCodes.StartsAtTooFarAhead];
        }
    }
}
