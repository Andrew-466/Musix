using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using Svg.Skia;

namespace MusicPlayer
{
    public static class BxIcon
    {
        private const string CdnBase =
            "https://cdn.jsdelivr.net/npm/boxicons@2.1.4/svg/regular/";

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicPlayer", "icons");

        private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly Dictionary<string, Image> memoryCache = new();

        public static Image Get(string iconName, int size = 22, Color? tint = null)
        {
            tint ??= Color.White;
            var key = $"{iconName}_{size}_{tint.Value.ToArgb():X8}";
            if (memoryCache.TryGetValue(key, out var cached)) return cached;

            var svgContent = LoadSvgContent(iconName);
            var bmp = RenderSvg(svgContent, size, tint.Value);
            memoryCache[key] = bmp;
            return bmp;
        }

        // ================= SHORTCUT =================
        public static Image Play(int s = 22, Color? t = null)       => Get("bx-play", s, t);
        public static Image Pause(int s = 22, Color? t = null)      => Get("bx-pause", s, t);
        public static Image Stop(int s = 22, Color? t = null)       => Get("bx-stop", s, t);
        public static Image SkipNext(int s = 22, Color? t = null)   => Get("bx-skip-next", s, t);
        public static Image SkipPrev(int s = 22, Color? t = null)   => Get("bx-skip-previous", s, t);
        public static Image Shuffle(int s = 22, Color? t = null)    => Get("bx-shuffle", s, t);
        public static Image Repeat(int s = 22, Color? t = null)     => Get("bx-repeat", s, t);
        public static Image FolderOpen(int s = 22, Color? t = null) => Get("bx-folder-open", s, t);
        public static Image VolumeFull(int s = 22, Color? t = null) => Get("bx-volume-full", s, t);
        public static Image VolumeMute(int s = 22, Color? t = null) => Get("bx-volume-mute", s, t);

        // ================= DOWNLOAD / CACHE =================
        private static string LoadSvgContent(string iconName)
        {
            Directory.CreateDirectory(CacheDir);
            var cachePath = Path.Combine(CacheDir, iconName + ".svg");

            if (File.Exists(cachePath))
            {
                try { return File.ReadAllText(cachePath); } catch { }
            }

            try
            {
                var url = CdnBase + iconName + ".svg";
                var content = http.GetStringAsync(url).GetAwaiter().GetResult();
                File.WriteAllText(cachePath, content);
                return content;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Gagal memuat ikon '{iconName}'.\nURL: {CdnBase + iconName}.svg",
                    ex);
            }
        }

        // ================= RENDER SVG → BITMAP (FIX) =================
        private static Bitmap RenderSvg(string svgContent, int size, Color tint)
        {
            var hex = $"#{tint.R:X2}{tint.G:X2}{tint.B:X2}";

            // 1. Ganti currentColor → warna tint
            svgContent = svgContent.Replace("currentColor", hex);

            // 2. Ganti warna hitam hardcoded → warna tint
            svgContent = Regex.Replace(svgContent,
                @"(fill|stroke)\s*=\s*[""'](#000000|#000|black)[""']",
                $"$1=\"{hex}\"",
                RegexOptions.IgnoreCase);

            // 3. ⭐ FIX UTAMA: kalau <path> tidak punya atribut fill sama sekali,
            //    SVG akan default ke hitam. Inject fill ke path tersebut.
            svgContent = Regex.Replace(svgContent,
                @"<path\b(?![^>]*\bfill\s*=)",
                $"<path fill=\"{hex}\"",
                RegexOptions.IgnoreCase);

            // 4. Sama untuk <circle>, <rect>, <polygon>, <polyline>, <ellipse>
            foreach (var tag in new[] { "circle", "rect", "polygon", "polyline", "ellipse" })
            {
                svgContent = Regex.Replace(svgContent,
                    $@"<{tag}\b(?![^>]*\bfill\s*=)",
                    $"<{tag} fill=\"{hex}\"",
                    RegexOptions.IgnoreCase);
            }

            // 5. Kalau <svg> root tidak punya fill, tambahkan sebagai fallback
            if (!Regex.IsMatch(svgContent, @"<svg[^>]*\bfill\s*="))
            {
                svgContent = Regex.Replace(svgContent,
                    @"<svg\b",
                    $"<svg fill=\"{hex}\"",
                    RegexOptions.IgnoreCase);
            }

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgContent));
            var svg = new SKSvg();
            svg.Load(stream);
            if (svg.Picture == null) throw new InvalidOperationException("SVG tidak valid");

            var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var skBitmap = new SKBitmap(info);

            using (var canvas = new SKCanvas(skBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                var cull = svg.Picture.CullRect;
                if (cull.Width > 0 && cull.Height > 0)
                {
                    var scale = Math.Min(size / cull.Width, size / cull.Height);
                    canvas.Translate((size - cull.Width * scale) / 2f,
                                     (size - cull.Height * scale) / 2f);
                    canvas.Scale(scale);
                }
                canvas.DrawPicture(svg.Picture);
            }

            using var skImage = SKImage.FromBitmap(skBitmap);
            using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            return new Bitmap(ms);
        }

        public static void ClearDiskCache()
        {
            try
            {
                if (Directory.Exists(CacheDir))
                    Directory.Delete(CacheDir, true);
            }
            catch { }
        }

        public static void DisposeAll()
        {
            foreach (var img in memoryCache.Values) img?.Dispose();
            memoryCache.Clear();
        }
    }
}