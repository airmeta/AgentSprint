using System.Diagnostics;
using System.Text;

using AgentSprint.Worker.Models;

namespace AgentSprint.Worker.Services;

internal static class ProcessCommandRunner
{
    public static async Task<CommandProbeResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        try
        {
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    stdout.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    stderr.AppendLine(args.Data);
                }
            };

            if (!process.Start())
            {
                return new CommandProbeResult(fileName, arguments, null, string.Empty, string.Empty, false, "Process failed to start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(timeoutCts.Token);

            return new CommandProbeResult(
                fileName,
                arguments,
                process.ExitCode,
                stdout.ToString(),
                stderr.ToString(),
                false,
                null);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            return new CommandProbeResult(fileName, arguments, null, stdout.ToString(), stderr.ToString(), true, "Process timed out.");
        }
        catch (Exception ex)
        {
            TryKillProcessTree(process);
            return new CommandProbeResult(fileName, arguments, null, stdout.ToString(), stderr.ToString(), false, ex.Message);
        }
    }

    public static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only. The caller records the original failure reason.
        }
    }
}
