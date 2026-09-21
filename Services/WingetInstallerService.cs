using AturOS.Helpers;
using AturOS.Models;

namespace AturOS.Services;

public class WingetInstallerService
{
    public List<WingetAppItem> GetCatalog()
    {
        return new List<WingetAppItem>
        {
            // Runtimes
            new WingetAppItem
            {
                Id = "Microsoft.VCRedist.2015+.x64",
                Name = "Visual C++ Redistributable (2015-2022 x64)",
                Category = "Runtimes",
                Description = "Pustaka runtime C++ esensial untuk kompatibilitas game dan aplikasi Windows."
            },
            new WingetAppItem
            {
                Id = "Microsoft.DirectX",
                Name = "DirectX End-User Runtimes",
                Category = "Runtimes",
                Description = "Kumpulan pustaka DirectX 9/10/11 warisan untuk game klasik dan emulator."
            },
            new WingetAppItem
            {
                Id = "Microsoft.DotNet.DesktopRuntime.8",
                Name = ".NET Desktop Runtime 8.0",
                Category = "Runtimes",
                Description = "Runtime resmi Microsoft untuk menjalankan aplikasi desktop WPF dan WinForms modern."
            },

            // Utilitas & Kompresi
            new WingetAppItem
            {
                Id = "7zip.7zip",
                Name = "7-Zip",
                Category = "Utilitas",
                Description = "Aplikasi pengarsipan dan kompresi file gratis, cepat, dan ringan."
            },
            new WingetAppItem
            {
                Id = "M2Team.NanaZip",
                Name = "NanaZip",
                Category = "Utilitas",
                Description = "Kompresor turunan 7-Zip dengan integrasi menu konteks modern Windows 11."
            },
            new WingetAppItem
            {
                Id = "Notepad++.Notepad++",
                Name = "Notepad++",
                Category = "Utilitas",
                Description = "Editor teks dan kode sumber cepat dengan syntax highlighting."
            },
            new WingetAppItem
            {
                Id = "Microsoft.PowerToys",
                Name = "Microsoft PowerToys",
                Category = "Utilitas",
                Description = "Kumpulan utilitas tingkat lanjut Microsoft untuk produktivitas desktop."
            },

            // Peramban & Gaming
            new WingetAppItem
            {
                Id = "Brave.Brave",
                Name = "Brave Browser",
                Category = "Peramban & Gaming",
                Description = "Peramban web berbasis Chromium yang cepat dengan pemblokir iklan dan pelacak bawaan."
            },
            new WingetAppItem
            {
                Id = "Google.Chrome",
                Name = "Google Chrome",
                Category = "Peramban & Gaming",
                Description = "Peramban web Google yang populer, cepat, dan stabil."
            },
            new WingetAppItem
            {
                Id = "Mozilla.Firefox",
                Name = "Mozilla Firefox",
                Category = "Peramban & Gaming",
                Description = "Peramban web independen dengan fokus privasi dan kustomisasi."
            },
            new WingetAppItem
            {
                Id = "Valve.Steam",
                Name = "Steam",
                Category = "Peramban & Gaming",
                Description = "Platform distribusi digital game PC terbesar di dunia."
            },
            new WingetAppItem
            {
                Id = "Discord.Discord",
                Name = "Discord",
                Category = "Peramban & Gaming",
                Description = "Aplikasi komunikasi suara, video, dan teks untuk gamer dan komunitas."
            },
            new WingetAppItem
            {
                Id = "OBSProject.OBSStudio",
                Name = "OBS Studio",
                Category = "Peramban & Gaming",
                Description = "Software open-source terbaik untuk perekaman video dan live streaming."
            }
        };
    }

    public async Task<bool> InstallAppAsync(WingetAppItem app, Action<string>? onProgress = null)
    {
        app.IsInstalling = true;
        app.Status = "Mengunduh & memasang...";
        onProgress?.Invoke($"Memasang {app.Name} via winget...");
        LoggerService.Instance.Info($"Memasang {app.Name} ({app.Id}) via winget...");

        var args = $"install --id {app.Id} --silent --accept-package-agreements --accept-source-agreements";
        var result = await ProcessHelper.RunCommandAsync("winget.exe", args, timeoutMs: 300000);

        app.IsInstalling = false;

        if (result.Success || result.ExitCode == 0)
        {
            app.Status = "Berhasil terpasang";
            LoggerService.Instance.Success($"{app.Name} berhasil dipasang.");
            return true;
        }

        app.Status = $"Gagal ({result.ExitCode})";
        LoggerService.Instance.Error($"Pemasangan {app.Name} gagal: {result.StandardError}");
        return false;
    }
}
