using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security;
using AturOS.Services;
using Microsoft.Win32;

namespace AturOS.Helpers;

public record RegistryValueSnapshot(
    RegistryHive Hive,
    string SubKey,
    string ValueName,
    object? PreviousValue,
    RegistryValueKind ValueKind,
    bool KeyExisted,
    bool ValueExisted,
    DateTime Timestamp
);

/// <summary>
/// Production-ready, safe registry manipulation helper.
/// Provides existence checks, explicit typing, automatic pre-write snapshots, and clean rollback capability.
/// </summary>
public static class RegistryHelper
{
    private static readonly ConcurrentDictionary<string, RegistryValueSnapshot> _snapshots = new();
    private static readonly string BackupDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AturOS",
        "RegistrySnapshots"
    );

    static RegistryHelper()
    {
        try
        {
            if (!Directory.Exists(BackupDir))
            {
                Directory.CreateDirectory(BackupDir);
            }
        }
        catch { }
    }

    private static string GetSnapshotKey(RegistryHive hive, string subKey, string valueName) =>
        $"{hive}\\{subKey}\\{valueName}".ToLowerInvariant();

    private static RegistryKey GetBaseKey(RegistryHive hive, RegistryView view = RegistryView.Default) =>
        RegistryKey.OpenBaseKey(hive, view);

    /// <summary>
    /// Captures the current state of a registry value before modification.
    /// </summary>
    public static RegistryValueSnapshot CaptureSnapshot(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, false);

            if (key == null)
            {
                var snap = new RegistryValueSnapshot(hive, subKey, valueName, null, RegistryValueKind.None, false, false, DateTime.UtcNow);
                _snapshots[GetSnapshotKey(hive, subKey, valueName)] = snap;
                return snap;
            }

            var val = key.GetValue(valueName);
            var kind = val != null ? key.GetValueKind(valueName) : RegistryValueKind.None;

            var snapshot = new RegistryValueSnapshot(
                hive,
                subKey,
                valueName,
                val,
                kind,
                true,
                val != null,
                DateTime.UtcNow
            );

            _snapshots[GetSnapshotKey(hive, subKey, valueName)] = snapshot;
            return snapshot;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membuat snapshot registri {hive}\\{subKey}\\{valueName}: {ex.Message}");
            return new RegistryValueSnapshot(hive, subKey, valueName, null, RegistryValueKind.None, false, false, DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Safely writes a DWORD value with existence checking, explicit type enforcement, and pre-write backup.
    /// </summary>
    public static bool SetDWord(RegistryHive hive, string subKey, string valueName, int value, bool backup = true)
    {
        if (backup)
        {
            CaptureSnapshot(hive, subKey, valueName);
        }

        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.CreateSubKey(subKey, true);
            if (key == null) return false;

            key.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch (SecurityException ex)
        {
            LoggerService.Instance.Error($"Hak akses ditolak (SecurityException) pada registri {hive}\\{subKey}: {ex.Message}");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            LoggerService.Instance.Error($"Akses tidak diizinkan pada registri {hive}\\{subKey}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal menulis DWORD ke {hive}\\{subKey}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Safely writes a String value with explicit type enforcement and pre-write backup.
    /// </summary>
    public static bool SetString(RegistryHive hive, string subKey, string valueName, string value, bool backup = true)
    {
        if (backup)
        {
            CaptureSnapshot(hive, subKey, valueName);
        }

        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.CreateSubKey(subKey, true);
            if (key == null) return false;

            key.SetValue(valueName, value, RegistryValueKind.String);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal menulis String ke {hive}\\{subKey}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Safely reads a DWORD value, returning defaultValue if non-existent or inaccessible.
    /// </summary>
    public static int GetDWord(RegistryHive hive, string subKey, string valueName, int defaultValue = -1)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, false);
            if (key == null) return defaultValue;

            var val = key.GetValue(valueName);
            if (val is int intVal) return intVal;
            if (val is long longVal) return (int)longVal;
            if (val != null && int.TryParse(val.ToString(), out int parsed)) return parsed;

            return defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Safely reads a String value, returning null if key or value does not exist.
    /// </summary>
    public static string? GetString(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, false);
            return key?.GetValue(valueName)?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Safely deletes a registry value with prior snapshot.
    /// </summary>
    public static bool DeleteValue(RegistryHive hive, string subKey, string valueName, bool backup = true)
    {
        if (backup)
        {
            CaptureSnapshot(hive, subKey, valueName);
        }

        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, true);
            if (key == null) return true; // Already gone

            key.DeleteValue(valueName, false);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal menghapus nilai registri {hive}\\{subKey}\\{valueName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Safely deletes a registry subkey tree.
    /// </summary>
    public static bool DeleteSubKeyTree(RegistryHive hive, string subKey)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            baseKey.DeleteSubKeyTree(subKey, false);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal menghapus subkey tree {hive}\\{subKey}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restores a value to its captured snapshot state.
    /// </summary>
    public static bool Rollback(RegistryHive hive, string subKey, string valueName)
    {
        var cacheKey = GetSnapshotKey(hive, subKey, valueName);
        if (!_snapshots.TryGetValue(cacheKey, out var snapshot))
        {
            LoggerService.Instance.Warning($"Tidak ada snapshot untuk {hive}\\{subKey}\\{valueName}");
            return false;
        }

        try
        {
            using var baseKey = GetBaseKey(hive);

            if (!snapshot.KeyExisted)
            {
                // Key did not exist originally
                baseKey.DeleteSubKey(subKey, false);
                return true;
            }

            using var key = baseKey.OpenSubKey(subKey, true);
            if (key == null) return false;

            if (!snapshot.ValueExisted || snapshot.PreviousValue == null)
            {
                // Value did not exist originally
                key.DeleteValue(valueName, false);
                return true;
            }

            key.SetValue(valueName, snapshot.PreviousValue, snapshot.ValueKind);
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Rollback gagal pada {hive}\\{subKey}\\{valueName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a registry subkey exists without throwing exceptions.
    /// </summary>
    public static bool SubKeyExists(RegistryHive hive, string subKey)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, false);
            return key != null;
        }
        catch
        {
            return false;
        }
    }
}
