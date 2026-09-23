using System;
using System.Diagnostics;
using System.Text;
using AturOS.Services;

namespace AturOS.Helpers;

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Hardened asynchronous process runner for CLI and PowerShell.
/// Eliminates UI freezing, prevents buffer deadlocks, enforces timeouts,
/// and uses Base64 EncodedCommand for bulletproof script execution.
/// </summary>
public static class ProcessHelper
{
    public static Task<ProcessResult> RunProcessAsync(string fileName, string arguments, int timeoutMs = 60000)
        => RunCommandAsync(fileName, arguments, timeoutMs);

    public static async Task<ProcessResult> RunCommandAsync(string fileName, string arguments, int timeoutMs = 30000)
    {
        var result = new ProcessResult();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) stdoutBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderrBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
                result.ExitCode = process.ExitCode;
                result.StandardOutput = stdoutBuilder.ToString().Trim();
                result.StandardError = stderrBuilder.ToString().Trim();
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch { }

                result.ExitCode = -1;
                result.StandardError = $"Batas waktu eksekusi ({timeoutMs} ms) terlampaui. Proses dimatikan.";
                LoggerService.Instance.Warning($"Proses {fileName} dibatalkan karena timeout.");
            }
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.StandardError = ex.Message;
            LoggerService.Instance.Error($"Eksekusi {fileName} gagal: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Executes a PowerShell script safely using Base64 EncodedCommand.
    /// This completely avoids escaping issues with nested quotes, spaces in paths, or pipes.
    /// Flags applied: -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass.
    /// </summary>
    public static async Task<ProcessResult> RunPowerShellScriptAsync(string script, int timeoutMs = 60000)
    {
        // Unicode UTF-16LE encoding required by PowerShell -EncodedCommand
        byte[] bytes = Encoding.Unicode.GetBytes(script);
        string base64 = Convert.ToBase64String(bytes);

        string arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {base64}";
        return await RunCommandAsync("powershell.exe", arguments, timeoutMs);
    }

    /// <summary>
    /// Parses a single line of CSV into distinct column values respecting double quotes.
    /// Handles commas inside quoted values and escaped double quotes ("").
    /// </summary>
    public static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        if (string.IsNullOrEmpty(line)) return fields;

        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // Skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString().Trim());
        return fields;
    }
}
