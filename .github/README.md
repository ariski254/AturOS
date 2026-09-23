<p align="center">
  <img src="../Resources/logo.png" alt="AturOS Logo" width="112" height="112" />
</p>

<h1 align="center">AturOS</h1>

<p align="center">
  <strong>Utilitas Desktop Native Modern untuk Optimasi, Debloat, dan Kustomisasi Windows 10 &amp; Windows 11</strong>
</p>

<p align="center">
  <a href="https://github.com/ariski254/AturOS/releases"><img src="https://img.shields.io/badge/Release-v1.0.0--Stable-2563EB?style=for-the-badge&logo=github" alt="Release" /></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=for-the-badge&logo=windows" alt="Platform Windows" />
  <img src="https://img.shields.io/badge/Architecture-x64-0F172A?style=for-the-badge" alt="Architecture x64" />
  <img src="https://img.shields.io/badge/Framework-.NET%208%20WPF-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 8 WPF" />
  <img src="https://img.shields.io/badge/App%20Type-Portable%20Single--File-16A34A?style=for-the-badge" alt="Portable" />
  <img src="https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge" alt="License" />
</p>

---

## Tentang AturOS

AturOS adalah aplikasi desktop utility native berbasis C#, .NET 8, dan Windows Presentation Foundation (WPF) yang dirancang khusus untuk pengguna Windows 10 dan Windows 11. Aplikasi ini menghadirkan kontrol penuh atas sistem operasi, meliputi pemantauan performa real-time, pemangkasan beban sistem (debloat), pembersihan penyimpanan mendalam, optimasi RAM, kustomisasi antarmuka, serta kontrol Windows Update.

Karakteristik teknis AturOS:
* Sangat Ringan dan Responsif: Kompilasi biner murni x64 tanpa dependensi browser atau framework web.
* 100% Portabel (Single-File Executable): Didistribusikan dalam satu file AturOS.exe tanpa proses instalasi yang rumit.
* Interaksi Sistem Nyata: Menggunakan Windows API (P/Invoke), System.Diagnostics, System.ServiceProcess, dan Microsoft.Win32.Registry tanpa simulasi atau data tiruan.

---

## Unduh dan Jalankan (Quick Start)

AturOS didistribusikan secara mandiri (self-contained). Pengguna tidak perlu menginstal .NET Runtime tambahan di komputer.

1. Unduh Versi Terbaru:
   Kunjungi halaman rilis resmi: [Halaman Releases AturOS](https://github.com/ariski254/AturOS/releases)
2. Jalankan Aplikasi:
   * Klik ganda pada berkas AturOS.exe.
   * Saat jendela User Account Control (UAC) muncul, pilih Yes (diperlukan agar aplikasi memiliki hak akses untuk membersihkan file sistem, mengelola service, dan menerapkan konfigurasi registri).
3. Aplikasi siap digunakan.

---

## Fitur Utama

### 1. Dashboard dan Monitoring Real-Time
* Pemantauan Beban Sistem: Menampilkan persentase penggunaan CPU, alokasi memori RAM fisik (Total, Terpakai, Bebas), serta kapasitas penyimpanan drive C:.
* 3 Profil Performa:
  * Mode Seimbang (Daily Balance): Skema daya seimbang, animasi visual normal, stabil dan hemat energi untuk pemakaian harian.
  * Mode Kerja dan Produktivitas: Optimal untuk aktivitas multitasking dengan pembersihan memori idle otomatis di latar belakang.
  * Mode Gaming (Extreme Performance): Mengaktifkan skema daya Ultimate Performance, mematikan CPU Core Parking, dan memprioritaskan respon sistem.
* Aksi Cepat: Pemangkasan RAM instan, pembersihan berkas sementara, dan penghapusan riwayat clipboard sistem dalam satu klik.

### 2. Mode Windows Lite (Pangkas Beban Sistem 3-Tier)
* Tier 1 (Mode Ringan): Menghapus bloatware bawaan (TikTok, Spotify, Candy Crush), mematikan telemetri pelacak DiagTrack, widget taskbar, dan iklan Start Menu (menghemat sekitar 400–700 MB RAM).
* Tier 2 (Mode Seimbang): Seluruh optimasi Tier 1 ditambah penghapusan aplikasi bawaan sekunder (Cuaca, Berita, Peta), penonaktifan Cortana dan Copilot, serta penonaktifan service latar belakang SysMain dan WSearch (menghemat sekitar 1.2–2.0 GB RAM).
* Tier 3 (Mode Ekstrem - Barebone): Dirancang untuk komputer spesifikasi rendah atau kebutuhan gaming kompetitif. Menghapus Microsoft Edge secara menyeluruh, mengaktifkan kompresi CompactOS, mematikan hibernasi, dan mempertahankan hanya komponen sistem yang esensial (menghemat sekitar 2.5–3.5+ GB RAM).
* Fitur Pemulihan (Revert): Mengembalikan seluruh service dan setelan sistem ke kondisi standar bawaan Windows dalam satu klik.

### 3. App Debloater dan Uninstaller
* Pemindaian Menyeluruh: Memindai aplikasi desktop klasik (Win32 / x64) dan aplikasi modern (UWP / Microsoft Store).
* Pencopotan Microsoft Edge:
  * Membuka kunci proteksi registri NoRemove = 0 dan SystemComponent = 0.
  * Menghentikan proses pengunci latar belakang (msedge.exe, edgeupdate).
  * Menjalankan uninstaller resmi dengan parameter: --uninstall --msedge --channel=stable --system-level --force-uninstall.
  * Mengunci registri DoNotUpdateToEdgeWithChromium = 1 agar Windows Update tidak mengunduh ulang Edge secara otomatis.
  * Menonaktifkan service edgeupdate dan scheduled tasks pembaruannya.
* Pencopotan Microsoft OneDrive: Menghentikan proses aktif, menjalankan uninstaller resmi bawaan Windows, membersihkan entri startup di registri Run, serta menghapus folder pintasan OneDrive dari bilah navigasi Windows Explorer.
* Penanganan Aplikasi Non-Removable: Menampilkan label status "Diproteksi Windows" untuk aplikasi yang dikunci oleh sistem, mencabut paket Provisioning sistem agar tidak muncul kembali setelah update Windows, serta melucuti izin aktivitas latar belakangnya.

### 4. Pembersih Drive (Storage Cleaner)
* Pembersihan Berkas Sampah: Membersihkan berkas sementara di %TEMP%, C:\Windows\Temp, antrian download SoftwareDistribution, folder Prefetch, crash dump memori, thumbnail cache, dan log sistem lama.
* Kompresi Sistem CompactOS: Menghemat ruang penyimpanan drive C: sebesar 3–5 GB menggunakan algoritma kompresi biner bawaan Windows (compact.exe /compactos:always).
* Manajemen Hibernasi: Menonaktifkan berkas hiberfil.sys untuk membebaskan ruang disk sebesar kapasitas RAM fisik komputer.
* Bebaskan Reserved Storage: Mengembalikan ruang penyimpanan sekitar 7 GB yang dicadangkan oleh Windows Update.
* Storage Sense dan Optimasi NTFS: Otomasi pembersihan berkas berkala dan pengurangan beban penulisan I/O disk untuk menjaga ketahanan SSD.

### 5. Optimasi RAM
* Pemangkasan Working Set: Mengurangi alokasi working set proses yang tidak aktif menggunakan Win32 API EmptyWorkingSet tanpa mematikan aplikasi secara paksa.
* Pembersihan Standby List dan Cache: Mengosongkan cache memori standby sistem yang menumpuk.
* Kunci Kernel di RAM Fisik (DisablePagingExecutive): Memastikan modul kernel dan driver tetap berada di RAM fisik agar respon sistem tidak terhambat oleh kecepatan media penyimpanan.
* Pembersihan Pagefile saat Shutdown: Menghapus residu memori virtual secara otomatis saat komputer dimatikan.

### 6. Tuning Hardware dan Latensi Gaming
* Skema Daya Ultimate Performance: Membuka dan mengaktifkan profil performa tertinggi Windows tanpa batasan throttling daya.
* Prioritas CPU Aplikasi Aktif (Win32PrioritySeparation): Menerapkan nilai optimal 0x26 (38 desimal) untuk alokasi siklus prosesor maksimum pada aplikasi atau game yang sedang aktif.
* Respon Menu Instan (MenuShowDelay): Menghilangkan jeda bawaan 400ms Windows saat membuka menu konteks klik-kanan menjadi 0 ms.
* Prioritas GPU dan Latensi Jaringan: Optimasi task scheduler GPU, pembebasan Network Throttling Index, dan pengaktifan TCPNoDelay (penonaktifan algoritma Nagle).
* Respon Input Presisi: Menonaktifkan akselerasi mouse Windows untuk respon bidikan kursor murni dan memaksimalkan responsivitas ketikan keyboard.

### 7. Dokter Sistem dan Auto-Repair
* Pemeriksaan Integritas Sistem (SFC dan DISM): Menjalankan pemindaian sfc /scannow. Jika ditemukan file rusak yang gagal diperbaiki oleh SFC, sistem otomatis menjalankan pemulihan citra komponen melalui dism /Online /Cleanup-Image /RestoreHealth.
* Troubleshooter Windows Update: Mereset folder antrian SoftwareDistribution dan Catroot2 serta merestart layanan update yang terhenti.
* Perbaikan Jaringan: Reset Winsock, reset TCP/IP stack, dan pembersihan DNS cache untuk mengatasi kendala koneksi internet.
* Pemeriksa Dependensi Runtime: Mendeteksi dan menginstal paket pustaka runtime esensial (Visual C++ 2015–2022, DirectX, .NET) dalam satu langkah.

### 8. Pengoptimal Jaringan dan DNS
* Pemilihan DNS Cepat: Tersedia pilihan preset server DNS terpercaya:
  * Cloudflare DNS (1.1.1.1 dan 1.0.0.1)
  * Google Public DNS (8.8.8.8 dan 8.8.4.4)
  * AdGuard DNS (94.140.14.14 dan 94.140.15.15) — Pemblokir Iklan dan Pelacak
  * Quad9 DNS (9.9.9.9 dan 149.112.112.112) — Keamanan Malware
* Tes Latensi DNS: Mengukur kecepatan respon ping real-time ke setiap server DNS.
* Reset ke DHCP: Mengembalikan setelan DNS ke mode otomatis bawaan router atau ISP.

### 9. Kustomisasi Shell dan File Explorer
* Menu Konteks Klasik Windows 11: Mengembalikan menu klik-kanan klasik Windows 10 tanpa submenu "Show more options".
* Tampilkan Ekstensi dan Berkas Tersembunyi: Mengaktifkan tampilan ekstensi file dan folder tersembunyi secara permanen.
* Pintasan Tambahan: Menambahkan opsi Take Ownership dan Buka Terminal sebagai Administrator pada menu konteks file dan folder.

### 10. Kontrol Windows Update
* Pilihan Mode Kontrol:
  * Hentikan Total (Hard Lockdown): Menonaktifkan seluruh service update dan Task Scheduler otomatis.
  * Jeda Jangka Panjang (Hingga 2099): Menjeda pembaruan sistem tanpa mengganggu fungsi Microsoft Store.
  * Mode Patch Keamanan Saja: Membatasi update hanya pada paket keamanan esensial.
  * Kembalikan ke Default: Mengembalikan seluruh konfigurasi update ke standar pabrik Windows.

### 11. Pemasang Aplikasi Massal (Winget GUI)
* Memasang perangkat lunak esensial (7-Zip, Notepad++, Git, Chrome, Brave, Discord, Steam, OBS Studio, PowerToys, dan lainnya) secara otomatis tanpa installer manual atau iklan.

### 12. Backup dan Titik Pemulihan (Restore Point)
* System Restore Point: Membuat titik pemulihan sistem Windows via PowerShell Checkpoint-Computer sebelum menerapkan konfigurasi besar.
* Peluncur Wizard Pemulihan: Membuka wizard pemulihan resmi Windows (rstrui.exe) dalam satu klik.
* Cadangan Registri (.reg): Mengekspor dan menyimpan cadangan konfigurasi registri ke folder penyimpanan lokal.

---

## Persyaratan Sistem

| Komponen | Persyaratan Minimum |
| :--- | :--- |
| Sistem Operasi | Windows 10 (Build 19041 ke atas) atau Windows 11 (Semua Versi) |
| Arsitektur | 64-bit (x64) |
| Hak Akses | Administrator Privilege (Wajib via konfirmasi UAC) |
| Dependensi Eksternal | Tidak ada (Biner Single-File telah menyertakan seluruh runtime yang dibutuhkan) |

---

## Kompilasi dari Source Code

Untuk mengompilasi AturOS secara mandiri dari source code:

### Prasyarat:
* .NET 8.0 SDK (x64)
* Visual Studio 2022 / JetBrains Rider / Visual Studio Code
* Windows 10 / 11 x64

### Langkah Build:
1. Clone repositori:
   ```bash
   git clone https://github.com/ariski254/AturOS.git
   cd AturOS
   ```
2. Restore paket dependensi:
   ```bash
   dotnet restore
   ```
3. Kompilasi ke single-file executable:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -o publish_latest
   ```
4. Biner hasil kompilasi tersedia di publish_latest\AturOS.exe.

---

## Kebijakan Keamanan

AturOS mematuhi prinsip integritas dan keamanan sistem operasi:
* Tidak menonaktifkan perangkat lunak keamanan atau antivirus Windows Defender.
* Tidak mematikan fungsi firewall jaringan Windows Firewall.
* Tidak menghapus paket shell esensial Windows (Settings, Start Menu Shell, LockApp, SecHealthUI, Windows Hello).
* Seluruh perubahan konfigurasi dapat dikembalikan melalui fitur Revert dan System Restore Point.

---

## Lisensi dan Kontribusi

Proyek ini dilisensikan di bawah lisensi MIT License. Masukan, laporan kendala, dan kontribusi kode dapat disampaikan melalui menu Issues dan Pull Requests pada repositori GitHub.
