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
        return await RunAsync(
            fileName,
            arguments,
            workingDirectory,
            timeout,
            Array.Empty<string>(),
            cancellationToken);
    }

    public static async Task<CommandProbeResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        TimeSpan timeout,
        IReadOnlyCollection<string> secretValues,
        CancellationToken cancellationToken)
    {
        var effectiveWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;
        var safeArguments = WorkerDiagnostics.TrimAndRedact(arguments, secretValues, 2000);
        WorkerDiagnostics.Info(
            "进程命令启动",
            $"fileName={fileName}, arguments={safeArguments}, workingDirectory={effectiveWorkingDirectory}, timeout={timeout}");

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
                WorkingDirectory = effectiveWorkingDirectory,
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
                WorkerDiagnostics.Error(
                    "进程命令启动失败",
                    $"fileName={fileName}, arguments={safeArguments}, workingDirectory={effectiveWorkingDirectory}, reason=Process failed to start.");
                return new CommandProbeResult(fileName, arguments, null, string.Empty, string.Empty, false, "Process failed to start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(timeoutCts.Token);

            WorkerDiagnostics.Info(
                "进程命令结束",
                $"fileName={fileName}, arguments={safeArguments}, exitCode={process.ExitCode}, stdout={WorkerDiagnostics.TrimAndRedact(stdout.ToString(), secretValues)}, stderr={WorkerDiagnostics.TrimAndRedact(stderr.ToString(), secretValues)}");
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
            WorkerDiagnostics.Warn(
                "进程命令超时",
                $"fileName={fileName}, arguments={safeArguments}, timeout={timeout}, stdout={WorkerDiagnostics.TrimAndRedact(stdout.ToString(), secretValues)}, stderr={WorkerDiagnostics.TrimAndRedact(stderr.ToString(), secretValues)}");
            return new CommandProbeResult(fileName, arguments, null, stdout.ToString(), stderr.ToString(), true, "Process timed out.");
        }
        catch (Exception ex)
        {
            TryKillProcessTree(process);
            WorkerDiagnostics.Error(
                "进程命令异常",
                $"fileName={fileName}, arguments={safeArguments}, error={WorkerDiagnostics.TrimAndRedact(ex.Message, secretValues)}, stdout={WorkerDiagnostics.TrimAndRedact(stdout.ToString(), secretValues)}, stderr={WorkerDiagnostics.TrimAndRedact(stderr.ToString(), secretValues)}");
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
