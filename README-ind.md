<p align="center">
  <img src="Assets/musix.png" width="120" alt="Ikon MusiX">
</p>

<h1 align="center">MusiX</h1>

<p align="center">
  Pemutar musik lokal yang ringan dengan tema gelap untuk Windows.<br>
  <a href="README.md">🇬🇧 Read in English</a>
</p>

---

## Fitur

- **Window tanpa frame (frameless)** dengan title bar buatan sendiri, bisa di-resize dari semua tepi, dan mendukung **Snap** Windows (seret ke tepi layar atau pakai `Win + Panah`).
- **Buka folder** (dipindai sampai subfolder) atau **seret & lepas** file dan folder langsung ke jendela.
- **Playlist** dengan judul, artis, dan durasi dari tag file; duplikat dilewati otomatis.
- **Kontrol pemutaran:** putar / jeda, sebelumnya, berikutnya, stop, seek bar, volume.
- **Shuffle** dan **repeat** (mati / semua / satu lagu).
- **Mengingat sesi terakhir:** playlist, lagu terakhir, volume, serta pengaturan shuffle dan repeat dipulihkan saat aplikasi dibuka lagi.
- File yang hilang ditandai di daftar, tidak membuat pemutar error.

Ekstensi yang didukung: `.mp3` `.wav` `.wma` `.m4a` `.aac` `.flac` `.ogg` `.aiff`
(pemutaran sebagian format bergantung pada codec yang tersedia di Windows kamu).

## Pintasan keyboard

| Tombol | Fungsi |
| --- | --- |
| `Space` | Putar / jeda |
| `←` / `→` | Mundur 5 detik / maju 5 detik |
| `↑` / `↓` | Volume +5 / −5 |
| `M` | Bisukan / aktifkan suara |
| `N` / `P` | Lagu berikutnya / sebelumnya |
| `S` | Stop |
| `Enter` atau klik dua kali | Putar lagu yang dipilih |
| `Delete` | Hapus lagu terpilih dari playlist |

## Kebutuhan

- **Windows 10 atau 11** (hanya Windows, lihat [Dukungan platform](#dukungan-platform))
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) untuk build dari source
- Koneksi internet saat **pertama kali dijalankan** (ikon UI diunduh sekali, lalu disimpan di cache)

## Build & jalankan

```bash
git clone https://github.com/Andrew-466/Musix.git
cd Musix
dotnet run
```

### Publish menjadi satu file `.exe`

```bash
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

Hasilnya ada di `bin/Release/net8.0-windows/win-x64/publish/MusicPlayer.exe` dan bisa dijalankan di PC Windows mana pun tanpa menginstal .NET.
(Di PowerShell, ganti `^` dengan backtick `` ` `` atau tulis perintahnya dalam satu baris.)

> Jangan aktifkan `PublishTrimmed`; Windows Forms tidak mendukung trimming dengan andal.

## Struktur proyek

| File | Fungsi |
| --- | --- |
| `Program.cs` | Titik masuk aplikasi |
| `Form1.cs` | Jendela utama, tata letak UI, logika pemutaran, playlist, pengaturan |
| `WindowChrome.cs` | Pendukung window frameless: tombol title bar dan area resize |
| `AccentSlider.cs` | Slider kustom untuk seek dan volume |
| `RoundButton.cs` | Tombol membulat kustom |
| `BxIcon.cs` | Memuat dan mewarnai SVG [Boxicons](https://boxicons.com) |
| `Assets/` | Ikon aplikasi (`musix.ico`, `musix.png`) |

## Lokasi penyimpanan data

| Data | Lokasi |
| --- | --- |
| Pengaturan dan playlist terakhir | `%AppData%\MusicPlayer\settings.json` |
| Cache ikon UI | `%LocalAppData%\MusicPlayer\icons` |

File musikmu tidak pernah disalin atau diubah; hanya lokasinya yang disimpan.

## Dukungan platform

MusiX **hanya untuk Windows**. Aplikasi ini memakai Windows Forms, output audio Windows milik NAudio, dan beberapa API Win32 (`user32`, `dwmapi`, `uxtheme`). Versi lintas platform membutuhkan toolkit UI lain (misalnya Avalonia) dan backend audio yang berbeda.

## Dibuat dengan

- [.NET 8](https://dotnet.microsoft.com/) dan Windows Forms
- [NAudio](https://github.com/naudio/NAudio): pemutaran audio
- [TagLibSharp](https://github.com/mono/taglib-sharp): membaca tag dan durasi
- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia): merender ikon SVG
- [Boxicons](https://boxicons.com): kumpulan ikon
