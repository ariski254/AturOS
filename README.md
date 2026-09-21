<p align="center">
  <img src="Resources/logo.png" alt="AturOS Logo" width="100" height="100" />
</p>

<h1 align="center">AturOS</h1>

<p align="center">
  <strong>Utilitas Desktop Native Modern untuk Optimasi, Debloat, dan Kustomisasi Windows 10 &amp; Windows 11</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=for-the-badge&logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/Architecture-x64-2563EB?style=for-the-badge" alt="x64" />
  <img src="https://img.shields.io/badge/Single--File-Standalone-16A34A?style=for-the-badge" alt="Single-File Standalone" />
  <img src="https://img.shields.io/badge/UI-Clean%20Fluent%20WPF-0F172A?style=for-the-badge" alt="WPF Fluent" />
</p>

---

## 📌 Tentang AturOS

**AturOS** adalah aplikasi desktop utility native berbasis **C# (.NET 8)** dan **WPF (Windows Presentation Foundation)** yang dirancang untuk mengoptimalkan kinerja, membersihkan aplikasi dan bloatware yang terinstall, memperbaiki kerusakan sistem, serta mempermudah kustomisasi mendalam pada **Windows 10 dan Windows 11**.

Berbeda dengan aplikasi utilitas berbasis web wrapper (Electron/Chromium), AturOS dibangun murni di atas Windows API (P/Invoke), `System.Diagnostics`, `Microsoft.Win32.Registry`, `System.ServiceProcess`, dan tool native Windows. **AturOS berinteraksi langsung dengan sistem operasi tanpa mockup, simulasi, atau UI palsu.**

> [!IMPORTANT]
> **Aman & Bertanggung Jawab:** AturOS dibuat untuk menjaga integritas sistem operasi. Aplikasi ini **bukan** alat pembobol keamanan, tidak mematikan Windows Defender, dan selalu menyediakan konfirmasi aman serta fitur pemulihan (*Restore*).

---

## ✨ Fitur Unggulan

### 1. 📊 Ringkasan Sistem Real-Time (Dashboard)
* **Monitoring Sumber Daya**: Pembacaan dinamis beban CPU via Win32 `GetSystemTimes`, penggunaan memori RAM via `GlobalMemoryStatusEx`, dan kapasitas partisi drive C: via `DriveInfo`.
* **3 Profil Performa 1-Klik**:
  * 🌱 **Mode Seimbang (Daily Balance)**: Skema daya Balanced, animasi visual normal, hemat daya saat idle.
  * 💼 **Mode Kerja & Produktivitas**: Skema daya seimbang dengan background auto-trim RAM jika >80% dan Focus Assist.
  * ⚡ **Mode Gaming (Extreme Latency & FPS)**: Skema daya *Ultimate Performance*, penonaktifan CPU Core Parking (`CPMINCORES 100`), dan respon input optimal.
* **Aksi Cepat 1-Klik**: Optimalkan RAM seketika, bersihkan file sementara, dan bersihkan papan klip (*clipboard*).

### 2. 🪶 Mode Windows Lite (Pangkas Beban Sistem Berjenjang)
* **Tingkat 1 (Mode Ringan)**: Menghapus bloatware sponsor bawaan, menonaktifkan telemetri `DiagTrack`, widget taskbar, dan iklan Start Menu (~400–700 MB RAM dibebaskan).
* **Tingkat 2 (Mode Seimbang)**: Seluruh optimasi Tingkat 1 + mencopot aplikasi UWP sekunder (Weather, News, Maps, Tips, dll.), mematikan Cortana & Copilot, menghentikan servis `SysMain` & `WSearch` (~1.2–2.0 GB RAM dibebaskan).
* **Tingkat 3 (Mode Ekstrem - Barebone)**: Seluruh optimasi Tingkat 1 & 2 + pencopotan Microsoft Edge & Edge Update, mengaktifkan kompresi sistem *CompactOS*, mematikan hibernasi, dan menyisakan hanya aplikasi esensial (~2.5–3.5+ GB RAM dibebaskan).
* **Fitur Pemulihan (Revert)**: 1-klik untuk mengembalikan seluruh servis, efek visual, dan setelan ke kondisi standar Windows.

### 3. 📦 App Debloater (Semua Aplikasi Terinstall)
* **Katalog Lengkap**: Memindai dan menampilkan **seluruh aplikasi yang terpasang di sistem**, mencakup:
  * Aplikasi Desktop Klasik (**Win32 / x64 / x86** dari Registry `HKLM` & `HKCU`).
  * Aplikasi Modern (**UWP / Windows Store Packages**).
* **Pencarian & Filter Cepat**: Filter berdasarkan status (Semua, Desktop Win32, Modern Store) dengan pencarian nama instan.
* **Pencopotan Aman**: Menjalankan silent uninstaller bawaan resmi atau pencopotan paket PowerShell secara terisolasi.

### 4. 🧹 Pembersih Drive (Storage Cleaner)
* **Pembersihan File Sampah**: Memindai dan menghapus file sementara di `%TEMP%`, `C:\Windows\Temp`, `SoftwareDistribution\Download`, `Prefetch`, Crash Dumps, Thumbnails, dan Windows Delivery Optimization Cache.
* **Kompresi Sistem (CompactOS)**: Mengaktifkan/menonaktifkan kompresi biner OS (`compact.exe /compactos:always`) untuk menghemat hingga 3–5 GB ruang disk.
* **Manajemen Hibernasi**: Nonaktifkan `hiberfil.sys` untuk membebaskan ruang disk sebesar kapasitas RAM fisik.
* **Penyimpanan Cadangan Windows (Reserved Storage)**: Bebaskan ~7 GB ruang disk cadangan pembaruan Windows.
* **Penyimpanan Pintar (Storage Sense) & NTFS Last Access**: Otomasi pembersihan ruang dan pengurangan beban penulisan I/O SSD.

### 5. ⚡ Optimasi RAM Mendalam
* **Bebaskan RAM Seketika**: Memangkas working set proses tidak aktif melalui P/Invoke `EmptyWorkingSet` tanpa menghentikan aplikasi secara paksa.
* **Bersihkan Standby List & Cache**: Membebaskan cache memori standby sistem.
* **Kunci Kernel di RAM Fisik (`DisablePagingExecutive`)**: Mencegah kernel dan driver dipindahkan ke pagefile disk.
* **Bersihkan Pagefile saat Shutdown**: Menghapus residu memori virtual saat PC dimatikan.
* **Kompresi Memori Windows (MMAgent)**: Atur kompresi memori untuk hemat RAM (PC rendah) atau hemat siklus CPU (PC gaming).

### 6. 🎮 Tuning Hardware & Gaming
* **Skema Daya Ultimate Performance**: Mengaktifkan profil performa tertinggi Windows tanpa batasan throttling daya.
* **Prioritas CPU Aplikasi Aktif (`Win32PrioritySeparation`)**: Nilai optimal `38` (`0x26`) untuk alokasi siklus kuantum prosesor maksimum pada game atau software aktif.
* **Respon Menu Instan (`MenuShowDelay`)**: Menghilangkan jeda 400ms Windows saat membuka klik-kanan dan start menu menjadi `0 ms`.
* **Prioritas GPU & Latensi Jaringan**: Optimasi scheduler task GPU (`Games`), nonaktifkan Network Throttling Index (`0xffffffff`), dan matikan algoritma Nagle (TCPNoDelay).
* **Respon Input 1:1**: Nonaktifkan akselerasi mouse Windows dan maksimalkan responsivitas keyboard.

### 7. 🩺 Dokter Sistem & Auto-Repair
* **Integritas Berkas (SFC & Auto-DISM)**: Menjalankan pemindaian `sfc /scannow`. Jika perbaikan gagal, otomatis menjalankan pemulihan citra komponen via `dism /Online /Cleanup-Image /RestoreHealth`.
* **Troubleshooter Windows Update**: 1-klik mereset folder antrian `SoftwareDistribution` dan `Catroot2` serta merestart layanan update.
* **Perbaikan Jaringan & TCP/IP**: Reset Winsock, reset TCP/IP stack, dan pembersihan DNS cache.
* **Detektor Dependensi (.DLL Missing)**: Memeriksa dan memasang Visual C++ Redistributable (2015–2022), DirectX, dan .NET Runtime via Winget.
* **Pembersih Shortcut Rusak & Orphaned Registry**: Mendeteksi jalan pintas `.lnk` yang targetnya hilang dan entri registry uninstaller bekas.

### 8. 🌐 Pengoptimal Jaringan & DNS
* **1-Click DNS Switcher**: Beralih ke DNS cepat dan aman dengan pilihan preset:
  * Cloudflare DNS (`1.1.1.1` & `1.0.0.1`)
  * Google Public DNS (`8.8.8.8` & `8.8.4.4`)
  * AdGuard DNS (`94.140.14.14` & `94.140.15.15`) — Blokir Iklan & Pelacak
  * Quad9 DNS (`9.9.9.9` & `149.112.112.112`) — Keamanan Malware
* **Tes Latensi DNS**: Mengukur kecepatan respon ping real-time untuk memilih server tercepat.

### 9. 📁 Kustomisasi Shell & File Explorer
* **Menu Konteks Klasik Windows 11**: Kembalikan menu klik-kanan klasik Windows 10 tanpa submenu "Show more options".
* **Tampilkan Ekstensi & File Tersembunyi**: Tampilkan ekstensi file dan folder tersembunyi secara permanen.
* **Hapus Teks "- Shortcut"**: Menghilangkan teks imbuhan saat membuat pintasan baru.
* **Pintasan Berguna**: Menambahkan menu *Take Ownership* dan *Buka Terminal sebagai Administrator* pada klik-kanan folder.

### 10. 🛡️ Kontrol Windows Update
* Pilihan mode kontrol pembaruan:
  * **Hentikan Total (Hard Lockdown)**: Mematikan seluruh servis update dan Task Scheduler.
  * **Jeda Jangka Panjang (Hingga 2099)**: Menjeda pembaruan tanpa merusak fungsionalitas Microsoft Store.
  * **Mode Patch Keamanan Saja**: Hanya izinkan update keamanan krusial.
  * **Kembalikan ke Default**: Pulihkan setelan update ke bawaan pabrik Windows.

### 11. 🚀 Pemasang Aplikasi Massal (Winget GUI)
* Pasang aplikasi esensial (7-Zip, Notepad++, Git, Chrome, Brave, Discord, Steam, OBS Studio, PowerToys, dll.) secara otomatis tanpa iklan atau installer manual melalui integrasi `winget`.

### 12. 🔒 Backup & Restore
* **System Restore Point**: Buat titik pemulihan sistem Windows via PowerShell `Checkpoint-Computer` sebelum menerapkan tweak besar.
* **Buka Wizard Pemulihan**: Peluncur 1-klik untuk `rstrui.exe` Windows.
* **Cadangan Registri (.reg)**: Ekspor dan simpan backup konfigurasi registri ke `%LocalAppData%\AturOS\RegistryBackups\`.

---

## 💻 Persyaratan Sistem

| Komponen | Persyaratan |
| :--- | :--- |
| **Sistem Operasi** | Windows 10 (Build 19041+) atau Windows 11 (Semua Versi) |
| **Arsitektur CPU** | x64 (64-bit) |
| **Hak Akses** | Administrator (*UAC prompt otomatis saat dibuka*) |
| **Versi Standalone** | **Tanpa syarat tambahan** (sudah mencakup .NET 8 runtime internal) |

---

## 🚀 Cara Menjalankan

### Opsi 1: Menggunakan File Standalone Jadi (Rekomendasi Pengguna)
1. Unduh file `AturOS.exe` dari tab [**Releases**](https://github.com/USERNAME/AturOS/releases).
2. Klik ganda pada file `AturOS.exe`.
3. Klik **Yes** pada jendela prompt Administrator (UAC).
4. Aplikasi langsung terbuka dan siap digunakan tanpa perlu instalasi apapun.

### Opsi 2: Build Mandiri dari Source Code (Developer)
Pastikan Anda telah memasang [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
# 1. Clone repositori ini
git clone https://github.com/USERNAME/AturOS.git
cd AturOS

# 2. Build aplikasi menjadi 1 file tunggal (Single-File Executable)
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish

# 3. Jalankan aplikasi hasil build
.\publish\AturOS.exe
```

---

## 📁 Struktur Repositori

```text
AturOS/
├── App.xaml / .cs                    # Entry point aplikasi WPF
├── MainWindow.xaml / .cs             # Antarmuka utama (Sidebar 250px + Content Host)
├── AturOS.csproj                     # Konfigurasi proyek .NET 8 WPF
├── app.manifest                      # Manifest hak akses Administrator & DPI Awareness
├── .gitignore                        # Konfigurasi pengecualian file build Git
│
├── Helpers/                          # Helper API & Utilitas Sistem
│   ├── AdministratorHelper.cs        # Deteksi status elevated & restart admin
│   ├── NativeMethods.cs              # Deklarasi Win32 API P/Invoke
│   ├── ProcessHelper.cs              # Eksekutor proses background tanpa konsol
│   ├── RegistryHelper.cs             # Helper baca/tulis aman registri
│   └── WindowsApi.cs                 # Struktur memori & CPU timing
│
├── Models/                           # Model Data
│   ├── InstalledAppItem.cs           # Model aplikasi terinstall (Win32 & UWP)
│   ├── CleanableItem.cs              # Item target pembersihan disk
│   ├── DnsPresetItem.cs              # Model preset server DNS
│   ├── SystemDoctorModels.cs         # Model diagnosa & validator runtime
│   └── SystemMetrics.cs              # Model metrik CPU, RAM, & Storage
│
├── Services/                         # Lapisan Logika & Operasi Sistem
│   ├── InstalledAppsService.cs       # Service pemindaian seluruh aplikasi terinstall
│   ├── SystemInfoService.cs          # Service pemantauan metrik hardware real-time
│   ├── WindowsLiteService.cs         # Service 3 mode Windows Lite
│   ├── StorageCleanerService.cs      # Service pembersihan cache & file sampah
│   ├── MemoryOptimizerService.cs     # Service EmptyWorkingSet & Standby List
│   ├── HardwareTuningService.cs      # Service tuning CPU, GPU, & jaringan
│   ├── SystemDoctorService.cs        # Service SFC/DISM auto-repair & runtime
│   ├── DnsOptimizerService.cs        # Service konfigurasi DNS
│   ├── ExplorerTweaksService.cs      # Service kustomisasi Explorer & shell
│   ├── WindowsUpdateControlService.cs# Service kontrol Windows Update
│   ├── WingetInstallerService.cs     # Service installer aplikasi massal
│   └── RestorePointService.cs        # Service System Restore Point
│
├── Views/                            # Tampilan Halaman (WPF UserControl)
│   ├── DashboardView.xaml            # Halaman Ringkasan & Profil Performa
│   ├── WindowsLiteView.xaml          # Halaman Mode Windows Lite
│   ├── AppDebloaterView.xaml         # Halaman Semua Aplikasi Terinstall
│   ├── StorageCleanerView.xaml       # Halaman Pembersih Drive
│   ├── MemoryOptimizerView.xaml      # Halaman Optimasi RAM
│   ├── HardwareGamingView.xaml       # Halaman Tuning Hardware & Gaming
│   ├── SystemDoctorView.xaml         # Halaman Dokter Sistem & Auto-Repair
│   ├── NetworkDnsView.xaml           # Halaman Jaringan & DNS
│   ├── ExplorerTweaksView.xaml       # Halaman Shell & File Explorer
│   ├── WindowsUpdateView.xaml        # Halaman Kontrol Windows Update
│   ├── WingetInstallerView.xaml      # Halaman Pemasang Aplikasi Massal
│   ├── EmergencyToolsView.xaml       # Halaman Alat Darurat & Diagnosa
│   └── BackupRestoreView.xaml        # Halaman Backup & Restore
│
└── Resources/                        # Aset Ikon, Gaya Visual, & Vektor
    ├── Styles.xaml                   # Desain sistem Fluent Light, tombol, & kartu
    ├── Icons.xaml                    # Vektor path geometris ikon antarmuka
    ├── app.ico / logo.svg            # Ikon dan logo resmi
    └── app_badge.png / logo.png
```

---

## 🛡️ Prinsip Keamanan & Integritas Sistem

1. **Anti-Mock (Real Interaction)**: Seluruh status sistem dibaca langsung dari Windows API, WMI, atau Registry. Tidak ada data simulasi atau progress bar palsu.
2. **Penanganan Error Terisolasi**: Setiap operasi file dan proses dilindungi blok `try-catch` terisolasi sehingga kegagalan satu item (misal file terkunci) tidak menggagalkan seluruh proses.
3. **Fungsi Pemulihan (Undo/Restore)**: Setiap penyesuaian sistem disediakan tombol pemulihan kembali ke standar bawaan Windows.
4. **Bukan Malware atau Bypass Tool**: AturOS tidak menonaktifkan proteksi antivirus, tidak mematikan firewall, dan tidak menyentuh keamanan kredensial pengguna.

---

## 📄 Lisensi & Kontribusi

Proyek ini bersifat open-source untuk keperluan utilitas sistem Windows. Kontribusi berupa bug report, ide fitur, atau pull request sangat dipersilakan!
