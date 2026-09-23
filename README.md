# AturOS

**Utilitas Optimasi Windows Native — Portabel, Ringan, dan Berfungsi Nyata**

AturOS adalah aplikasi desktop utilitas native untuk **Windows 10** dan **Windows 11** yang membantu pengguna membersihkan, mengoptimalkan, dan menyesuaikan sistem operasi mereka secara aman melalui antarmuka yang modern dan mudah digunakan. Seluruh operasi berinteraksi langsung dengan Windows API, tidak ada simulasi atau tampilan palsu.

---

## Persyaratan Sistem

| Komponen | Persyaratan Minimum |
|---|---|
| Sistem Operasi | Windows 10 (Build 19041+) atau Windows 11 |
| Arsitektur | x64 (64-bit) |
| RAM | 256 MB tersedia |
| Penyimpanan | 80 MB ruang kosong |
| Hak Akses | Administrator (diperlukan untuk sebagian besar fitur) |
| Runtime | Tidak diperlukan — sudah terpaket dalam satu berkas |

---

## Cara Menjalankan

AturOS didistribusikan sebagai **Portable Single-File Executable** — tidak perlu instalasi.

1. Klik ganda pada **`AturOS.exe`**.
2. Saat jendela **User Account Control (UAC)** muncul, klik **Ya**.
3. Jendela utama AturOS langsung terbuka dan siap digunakan.

> **Catatan:** Hak Administrator diperlukan agar AturOS dapat berinteraksi dengan komponen sistem Windows secara langsung, seperti membersihkan file terproteksi, mengubah konfigurasi registri, dan mengelola layanan Windows.

---

## Panduan Fitur

### 1. 📊 Ringkasan Sistem (Dashboard)

Menampilkan informasi kondisi sistem secara **real-time** yang diperbarui otomatis setiap beberapa detik:

- **CPU** — Persentase beban prosesor aktual dari seluruh core menggunakan Performance Counter Windows.
- **RAM** — Memori total, terpakai, dan bebas menggunakan API `GlobalMemoryStatusEx`.
- **Penyimpanan C:** — Kapasitas, ruang terpakai, dan ruang kosong menggunakan `DriveInfo`.
- **Informasi Perangkat** — Nama OS, Build Windows, nama komputer, pengguna aktif, arsitektur, dan uptime.

**3 Profil Performa Cepat:**

| Profil | Deskripsi |
|---|---|
| Seimbang (Harian) | Mode standar untuk pemakaian sehari-hari, hemat daya |
| Kerja & Produktif | Auto-trim RAM idle + mode hening latar belakang |
| Gaming (Performa Maksimal) | Skema daya Ultimate Performance + optimasi latensi |

**Tombol Tindakan Cepat (Header Bar):**
- **Restart Explorer** — Memuat ulang shell Windows tanpa restart penuh.
- **Bunuh Task Macet** — Menutup paksa semua proses Not-Responding sekaligus.
- **Buat Restore Point** — Membuat System Restore Point Windows sebelum perubahan besar.

---

### 2. Profil Performa (Mode Windows)

Penerapan tweak sistem secara terkelompok berdasarkan kebutuhan dan spesifikasi perangkat keras:

- **Tier Perangkat Keras** — AturOS mendeteksi otomatis spesifikasi RAM dan Storage Anda lalu merekomendasikan profil yang sesuai.
- **Profil Harian** — Menonaktifkan iklan Start Menu, jeda animasi berlebihan, dan membersihkan bloatware sponsor.
- **Profil Kerja** — Tambahan: menonaktifkan Cortana, Copilot, widget taskbar, dan SysMain (SuperFetch).
- **Profil Gaming** — Tambahan: mengaktifkan Ultimate Performance Plan, menonaktifkan CPU Core Parking, dan menghapus telemetri.
- **Profil Barebone (Ekstrem)** — Untuk PC spesifikasi rendah: mengaktifkan CompactOS, menghapus Edge, dan mematikan hibernasi.

Setiap profil dilengkapi tombol **"Kembalikan ke Standar"** untuk restore ke kondisi awal Windows.

---

### 3. Pencopot Aplikasi (App Debloater)

Menampilkan dan menghapus seluruh aplikasi yang terpasang — baik aplikasi **Win32 desktop** maupun **UWP/Microsoft Store** — dengan penanganan khusus untuk aplikasi terproteksi sistem:

Fitur pencarian dan filter kategori tersedia untuk menemukan aplikasi dengan cepat.

---

### 4. Pembersih Drive (Storage Cleaner)

Menemukan dan menghapus berkas sampah yang aman untuk dibersihkan:

**Target Pembersihan:**
- `%TEMP%` — Berkas sementara pengguna
- `C:\Windows\Temp` — Berkas sementara sistem
- `C:\Windows\SoftwareDistribution\Download` — Cache unduhan Windows Update

Proses berjalan dengan alur **Scan → Hitung Ukuran → Tampilkan Hasil → Konfirmasi → Bersihkan**.

**Fitur Tambahan Manajemen Penyimpanan:**
- **CompactOS** — Mengompres binari sistem Windows untuk menghemat 3–5 GB ruang disk.
- **Hibernasi** — Mengaktifkan/menonaktifkan hiberfil.sys (membebaskan ruang sebesar kapasitas RAM).
- **Storage Sense** — Mengaktifkan kebijakan pembersihan otomatis bawaan Windows.
- **NTFS Last Access Time** — Menonaktifkan pencatatan waktu akses berkas untuk mempercepat I/O.
- **Reserved Storage** — Mengelola ruang cadangan Windows Update.

---

### 5. Optimasi RAM (Memory Optimizer)

Menggunakan Windows API resmi melalui P/Invoke untuk memangkas memori tanpa mematikan aplikasi:

- **Bebaskan RAM Sekarang** — Memanggil `EmptyWorkingSet` pada seluruh proses aktif yang memenuhi syarat.
- **Kosongkan Standby List** — Membersihkan daftar standby page via `NtSetSystemInformation`.

**Tweak Memori Lanjutan:** Paging Executive, Clear Pagefile, Large System Cache, Memory Compression, SysMain.

---

### 6. Tuning Hardware & Gaming

Tweak resmi berbasis Registry Windows untuk meningkatkan responsivitas:

| Tweak | Manfaat |
|---|---|
| Hardware-Accelerated GPU Scheduling (HAGS) | Mengurangi latensi render GPU |
| GPU Process Priority | Memprioritaskan antrian GPU untuk aplikasi foreground |
| Ultimate Performance Plan | Menonaktifkan CPU Core Parking dan pembatasan frekuensi |
| Win32PrioritySeparation | Meningkatkan prioritas penjadwalan proses foreground |
| Network Throttling | Menghapus batas bandwidth jaringan untuk game |
| MenuShowDelay | Menghilangkan jeda 400ms pada menu klik-kanan |

---

### 7. Dokter Sistem (System Doctor)

- **SFC Scannow** — Memindai dan memperbaiki berkas sistem Windows yang hilang atau rusak.
- **DISM Restore Health** — Memperbaiki komponen citra Windows menggunakan sumber dari Windows Update.
- **Pemeriksa Shortcut Rusak** — Menemukan dan membersihkan shortcut yang mengarah ke berkas tidak valid.
- **Pembersih Registry Orphan** — Menemukan entri registry yang mengarah ke program yang sudah dihapus.

---

### 8. Pengoptimal Jaringan & DNS

Mengganti server DNS adaptor jaringan aktif ke penyedia tercepat dalam satu klik:

| Penyedia DNS | Keunggulan |
|---|---|
| Cloudflare (1.1.1.1) | Tercepat secara global, privasi tinggi |
| Google (8.8.8.8) | Andal dan stabil |
| AdGuard DNS | Pemblokir iklan dan tracker |
| Quad9 (9.9.9.9) | Pemblokir domain berbahaya |
| OpenDNS | Proteksi phishing dan kontrol parental |

Fitur **Uji Latensi Ping** tersedia untuk mengukur respons tiap server sebelum memilih.

---

### 9. Kontrol Windows Update

- **Jeda Pembaruan** — Menangguhkan update otomatis dalam jangka panjang.
- **Mode Keamanan Saja** — Hanya mengizinkan patch keamanan penting, menolak feature update.
- **Driver Update** — Mengontrol apakah Windows Update diizinkan mengunduh driver.
- **Reset Update** — Menghapus antrian update yang gagal atau tertunda.

---

### 10. Kustomisasi Shell & File Explorer

Tweak tampilan dan perilaku Windows Explorer via Registry:

- Tampilkan/Sembunyikan ekstensi nama file
- Tampilkan/Sembunyikan hidden files dan folder
- Hapus akhiran "- Shortcut" pada ikon baru
- Tambah menu klik-kanan "Take Ownership"
- Nonaktifkan pop-up Security Warning saat membuka installer
- Kembalikan menu konteks klasik Windows 10 (khusus Windows 11)
- Tambah menu "Buka Terminal sebagai Administrator"
- Nonaktifkan pencarian Bing di Start Menu

---

### 11. Pemasang Aplikasi Massal (Winget Installer)

Memasang kumpulan aplikasi penting secara otomatis menggunakan **Windows Package Manager (winget)**. Tersedia kategori: Runtime, Peramban, Developer Tools, Media & Gaming, Utilitas Sistem. Pengguna dapat memilih dan memasang banyak aplikasi sekaligus tanpa iklan.

---

### 12. Alat Darurat & Pemeliharaan

- **Tutup Paksa Aplikasi Membeku** — Menutup seketika semua proses Not-Responding.
- **Restart Windows Explorer** — Memuat ulang shell Windows.
- **Bersihkan Clipboard** — Menghapus konten dari buffer clipboard.
- **Laporan Baterai Laptop** — Laporan kesehatan baterai lengkap dalam format HTML.
- **Aksi Tutup Layar (Lid Close)** — Mengatur perilaku saat laptop ditutup.
- **Boot ke Safe Mode** — Mengatur bcdedit agar restart berikutnya masuk Safe Mode.
- **Pembersih Ghost Devices** — Menghapus entri driver perangkat keras lama dari Device Manager.
- **SFC & DISM** — Perbaikan integritas berkas sistem satu klik.

---

### 13. Cadangan & Pemulihan (Backup & Restore)

- **Buat System Restore Point** — Membuat titik pemulihan via PowerShell `Checkpoint-Computer`.
- **Daftar Restore Point** — Menampilkan semua titik pemulihan tersimpan dengan opsi pemulihan langsung.
- **Ekspor Cadangan Registry (.reg)** — Menyimpan konfigurasi registry penting ke file `.reg`.
- **Kelola Berkas Cadangan** — Melihat, membuka, memulihkan, atau menghapus file cadangan.
- **Buka Wizard Pemulihan (rstrui)** — Membuka antarmuka System Restore bawaan Windows.

---

## Pertanyaan Umum (FAQ)

**Q: Muncul peringatan SmartScreen "Windows protected your PC"?**
A: Normal terjadi pada aplikasi yang belum memiliki sertifikat digital berbayar. Klik **"More info"** → **"Run anyway"**. AturOS bersih dari malware dan tidak mengandung iklan pihak ketiga.

**Q: Apakah aman untuk PC gaming dengan anti-cheat (Vanguard, EAC, BattlEye)?**
A: Aman. AturOS tidak menyuntikkan kode ke proses manapun. Seluruh optimasi menggunakan konfigurasi resmi Windows yang didokumentasikan Microsoft.

**Q: Bagaimana membatalkan perubahan yang telah diterapkan?**
A: Gunakan tombol **"Kembalikan ke Default"** yang tersedia di setiap baris tweak. Untuk pemulihan menyeluruh, buka **Cadangan & Pemulihan** → jalankan **Wizard Pemulihan Sistem**.

**Q: Mengapa beberapa fitur tidak tersedia di Windows 10?**
A: Fitur seperti Menu Konteks Klasik, Copilot, dan Widgets hanya tersedia di Windows 11. AturOS mendeteksi versi OS secara otomatis dan menampilkan status **"Tidak Didukung"** untuk fitur yang tidak kompatibel.

**Q: Apakah data pribadi dikumpulkan?**
A: Tidak. AturOS berjalan sepenuhnya offline. Tidak ada koneksi ke server eksternal, tidak ada pengiriman data, tidak ada telemetri.

---

## Informasi Teknis

| Item | Detail |
|---|---|
| Bahasa Pemrograman | C# (.NET 8) |
| Framework UI | WPF (Windows Presentation Foundation) |
| Desain UI | Fluent Light / Windows 11 Clean UI |
| Distribusi | Portable Single-File Executable |
| Target Platform | Windows 10/11, x64 |
| Ukuran Berkas | ~75 MB (self-contained) |

---

## Repositori

**https://github.com/ariski254/AturOS**
