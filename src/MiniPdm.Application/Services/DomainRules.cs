using System.Text.RegularExpressions;
using MiniPdm.Domain.Enums;

namespace MiniPdm.Application.Services;

public static class DomainRules
{
    private static readonly Regex DesignationRegex = new(@"^[А-ЯЁ]{4}\.[0-9]{6}\.[0-9]{3}$", RegexOptions.Compiled);

    public static bool IsValidDesignation(string? designation)
    {
        return !string.IsNullOrWhiteSpace(designation) && DesignationRegex.IsMatch(designation);
    }

    public static bool CanChangeState(ObjectState current, ObjectState next)
    {
        if (current == ObjectState.InWork && next == ObjectState.Approved) return true;
        if (current == ObjectState.InWork && next == ObjectState.Cancelled) return true;
        if (current == ObjectState.Approved && next == ObjectState.Cancelled) return true;
        return false;
    }
}
