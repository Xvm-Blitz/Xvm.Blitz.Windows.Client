namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class SemVerComparer
{
    public static bool IsLessThan(string? left, string? right) => Compare(left, right) < 0;

    public static int Compare(string? left, string? right)
    {
        if (!TryParse(left, out var leftVersion) || !TryParse(right, out var rightVersion))
            return 0;

        for (var index = 0; index < 3; index++)
        {
            var comparison = leftVersion.Core[index].CompareTo(rightVersion.Core[index]);
            if (comparison != 0)
                return comparison;
        }

        var leftHasPreRelease = leftVersion.PreRelease is not null;
        var rightHasPreRelease = rightVersion.PreRelease is not null;

        if (!leftHasPreRelease && !rightHasPreRelease)
            return 0;

        if (!leftHasPreRelease)
            return 1;

        if (!rightHasPreRelease)
            return -1;

        return ComparePreRelease(leftVersion.PreRelease!, rightVersion.PreRelease!);
    }

    private static int ComparePreRelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var maxLength = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < maxLength; index++)
        {
            if (index >= leftParts.Length)
                return -1;

            if (index >= rightParts.Length)
                return 1;

            var leftPart = leftParts[index];
            var rightPart = rightParts[index];
            var leftIsNumeric = int.TryParse(leftPart, out var leftNumber);
            var rightIsNumeric = int.TryParse(rightPart, out var rightNumber);

            if (leftIsNumeric && rightIsNumeric)
            {
                var numericComparison = leftNumber.CompareTo(rightNumber);
                if (numericComparison != 0)
                    return numericComparison;

                continue;
            }

            if (leftIsNumeric)
                return -1;

            if (rightIsNumeric)
                return 1;

            var stringComparison = string.CompareOrdinal(leftPart, rightPart);
            if (stringComparison != 0)
                return stringComparison;
        }

        return 0;
    }

    private static bool TryParse(string? value, out SemVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var sanitized = value.Trim();
        if (sanitized.StartsWith('v') || sanitized.StartsWith('V'))
            sanitized = sanitized[1..];

        var plusIndex = sanitized.IndexOf('+');
        if (plusIndex >= 0)
            sanitized = sanitized[..plusIndex];

        string? preRelease = null;
        var dashIndex = sanitized.IndexOf('-');
        if (dashIndex >= 0)
        {
            preRelease = sanitized[(dashIndex + 1)..];
            sanitized = sanitized[..dashIndex];
        }

        var parts = sanitized.Split('.');
        if (parts.Length is < 1 or > 3)
            return false;

        var core = new int[3];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], out var number) || number < 0)
                return false;

            core[index] = number;
        }

        version = new SemVersion(core, preRelease);
        return true;
    }

    private readonly record struct SemVersion(int[] Core, string? PreRelease);
}
