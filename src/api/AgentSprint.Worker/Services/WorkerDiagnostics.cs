using Air.Cloud.Core;
using Air.Cloud.Core.Modules.AppPrint;

using System.Text.RegularExpressions;

namespace AgentSprint.Worker.Services;

internal static class WorkerDiagnostics
{
    public static void Info(string title, string message)
    {
        Print(title, message, AppPrintLevel.Information);
    }

    public static void Warn(string title, string message)
    {
        Print(title, message, AppPrintLevel.Warn);
    }

    public static void Error(string title, string message)
    {
        Print(title, message, AppPrintLevel.Error);
    }

    public static string Trim(string? value, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    public static string TrimAndRedact(string? value, int maxLength = 1000)
    {
        return RedactSecrets(Trim(value, maxLength));
    }

    public static string TrimAndRedact(
        string? value,
        IReadOnlyCollection<string> secretValues,
        int maxLength = 1000)
    {
        value = TrimAndRedact(value, maxLength);
        foreach (var secret in secretValues)
        {
            if (!string.IsNullOrWhiteSpace(secret))
            {
                value = value.Replace(secret, "<redacted>", StringComparison.Ordinal);
            }
        }

        return value;
    }

    private static string RedactSecrets(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = Regex.Replace(
            value,
            "(\"(?:agentToken|gitAccessToken|accessToken|apiKey|password|token)\"\\s*:\\s*\")([^\"]*)(\")",
            "$1***REDACTED***$3",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(
            value,
            "(Bearer\\s+)[A-Za-z0-9._~+\\-/=]+",
            "$1***REDACTED***",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(
            value,
            "(https?://)([^\\s/@:]+):([^\\s/@]+)@",
            "$1***REDACTED***:***REDACTED***@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return value;
    }

    private static void Print(string title, string message, AppPrintLevel level)
    {
        try
        {
            AppRealization.Output.Print(new AppPrintInformation(title, message, level));
        }
        catch
        {
        }
    }
}
