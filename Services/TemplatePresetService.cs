using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;
using DiplomPrintGenerate.Models;

namespace DiplomPrintGenerate.Services
{
    public static class TemplatePresetService
    {
        private static readonly string TemplatesDirectory;
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue
        };

        static TemplatePresetService()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                TemplatesDirectory = Path.Combine(baseDir, "templates");
                if (!Directory.Exists(TemplatesDirectory))
                {
                    Directory.CreateDirectory(TemplatesDirectory);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Не удалось инициализировать папку шаблонов", ex);
            }
        }

        public static string DirectoryPath => TemplatesDirectory;

        public static List<DiplomaTemplatePreset> LoadAllPresets()
        {
            var result = new List<DiplomaTemplatePreset>();
            if (!Directory.Exists(TemplatesDirectory))
                return result;

            var files = Directory.GetFiles(TemplatesDirectory, "*.dpt");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file, System.Text.Encoding.UTF8);
                    var preset = Serializer.Deserialize<DiplomaTemplatePreset>(json);
                    if (preset != null)
                    {
                        result.Add(preset);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка чтения файла шаблона: " + file, ex);
                }
            }

            return result.OrderBy(p => p.Title).ToList();
        }

        public static void SavePreset(string title, BitmapSource image, double canvasW, double canvasH, IEnumerable<FieldDefinition> fields, double? v1 = null, double? v2 = null, double? h1 = null, double? h2 = null, bool showCustomGuides = false, Dictionary<string, string> customFields = null, IEnumerable<double> verticalGuides = null, IEnumerable<double> horizontalGuides = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                title = "Шаблон " + DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

            string safeFileName = SanitizeFileName(title) + ".dpt";
            string fullPath = Path.Combine(TemplatesDirectory, safeFileName);

            string imageBase64 = "";
            if (image != null)
            {
                byte[] jpegBytes = RenderService.EncodeToJpeg(image, 92);
                imageBase64 = Convert.ToBase64String(jpegBytes);
            }

            var preset = new DiplomaTemplatePreset
            {
                Title = title.Trim(),
                CreatedDate = DateTime.Now,
                CanvasWidth = canvasW,
                CanvasHeight = canvasH,
                ImageBase64 = imageBase64,
                Fields = fields != null ? new List<FieldDefinition>(fields.Select(f => f.Clone())) : new List<FieldDefinition>(),
                CustomFields = customFields != null ? new Dictionary<string, string>(customFields) : new Dictionary<string, string>(),
                CustomGuideV1 = v1,
                CustomGuideV2 = v2,
                CustomGuideH1 = h1,
                CustomGuideH2 = h2,
                ShowCustomGuides = showCustomGuides
            };

            if (verticalGuides != null)
            {
                preset.VerticalGuides = new List<double>(verticalGuides);
            }
            if (horizontalGuides != null)
            {
                preset.HorizontalGuides = new List<double>(horizontalGuides);
            }

            string json = Serializer.Serialize(preset);
            File.WriteAllText(fullPath, json, System.Text.Encoding.UTF8);
            Logger.Log($"Шаблон сохранен в библиотеку: '{title}' ({fields?.Count()} полей) в {safeFileName}");
        }

        public static void DeletePreset(string title)
        {
            if (string.IsNullOrEmpty(title)) return;
            string safeFileName = SanitizeFileName(title) + ".dpt";
            string fullPath = Path.Combine(TemplatesDirectory, safeFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Logger.Log($"Шаблон удален из библиотеки: {title}");
            }
        }

        public static BitmapSource Base64ToBitmap(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;

            byte[] bytes = Convert.FromBase64String(base64);
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
