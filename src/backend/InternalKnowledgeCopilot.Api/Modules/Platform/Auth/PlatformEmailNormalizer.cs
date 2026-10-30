namespace InternalKnowledgeCopilot.Api.Modules.Platform.Auth;

// Giai đoạn đầu chỉ chấp nhận email ASCII cho Platform Admin: trim + lowercase, khớp CHECK lower(trim(...)) của S01-A.
// Email Unicode bị từ chối một cách xác định thay vì chuẩn hóa không nhất quán. Không áp dụng cho email tenant.
public static class PlatformEmailNormalizer
{
    private const int MaxLength = 320;
    private const int MaxLocalPartLength = 64;
    private const int MaxDomainLength = 255;
    private const int MaxDomainLabelLength = 63;
    private const string LocalPartSymbols = "!#$%&'*+/=?^_`{|}~.-";

    public static string? TryNormalize(string? email)
    {
        if (email is null)
        {
            return null;
        }

        var candidate = email.Trim();
        if (candidate.Length is < 3 or > MaxLength || candidate.Any(character => character > 0x7E || character < 0x21))
        {
            return null;
        }

        candidate = candidate.ToLowerInvariant();
        var at = candidate.IndexOf('@');
        if (at <= 0 || at != candidate.LastIndexOf('@'))
        {
            return null;
        }

        var local = candidate[..at];
        var domain = candidate[(at + 1)..];
        return IsValidLocalPart(local) && IsValidDomain(domain) ? candidate : null;
    }

    private static bool IsValidLocalPart(string local)
    {
        return local.Length <= MaxLocalPartLength
            && local[0] != '.'
            && local[^1] != '.'
            && !local.Contains("..", StringComparison.Ordinal)
            && local.All(character => char.IsAsciiLetterOrDigit(character) || LocalPartSymbols.Contains(character));
    }

    private static bool IsValidDomain(string domain)
    {
        if (domain.Length is 0 or > MaxDomainLength)
        {
            return false;
        }

        var labels = domain.Split('.');
        return labels.Length >= 2 && labels.All(label =>
            label.Length is > 0 and <= MaxDomainLabelLength
            && label[0] != '-'
            && label[^1] != '-'
            && label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
    }
}
