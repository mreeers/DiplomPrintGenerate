using System.Collections.Generic;

namespace DiplomPrintGenerate.Models
{
    /// <summary>
    /// Модель файла проекта дипломов (*.dpg).
    /// Поддерживает неограниченное количество полей, участников, кастомных атрибутов и направляющих.
    /// </summary>
    public class TemplateProject
    {
        public string TemplateImagePath { get; set; }
        public double CanvasWidth { get; set; } = 800;
        public double CanvasHeight { get; set; } = 1131; // A4 aspect ratio ~ 1 : 1.4142

        /// <summary>
        /// Список всех текстовых полей проекта без каких-либо ограничений.
        /// </summary>
        public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

        /// <summary>
        /// Произвольные кастомные поля (ключ-значение) в неограниченном количестве.
        /// </summary>
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Дополнительные кастомные свойства проекта.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Данные участников: список словарей { НазваниеКолонки: Значение }.
        /// </summary>
        public List<Dictionary<string, string>> Participants { get; set; } = new List<Dictionary<string, string>>();

        public string OutputFolder { get; set; } = "";
        public int OutputFormat { get; set; } = 0; // 0=MultiPagePdf, 1=SinglePdfs, 2=ImagesPng, 3=PrintDirect
        public string FileNamePattern { get; set; } = "Диплом_{ФИО}";

        /// <summary>
        /// Список вертикальных направляющих (в пикселях) без ограничений.
        /// </summary>
        public List<double> VerticalGuides { get; set; } = new List<double>();

        /// <summary>
        /// Список горизонтальных направляющих (в пикселях) без ограничений.
        /// </summary>
        public List<double> HorizontalGuides { get; set; } = new List<double>();

        #region Свойства для обратной совместимости со старыми файлами *.dpg
        public double? CustomGuideV1
        {
            get => VerticalGuides != null && VerticalGuides.Count > 0 ? VerticalGuides[0] : _customGuideV1;
            set
            {
                _customGuideV1 = value;
                if (value.HasValue && VerticalGuides != null && !VerticalGuides.Contains(value.Value))
                    VerticalGuides.Add(value.Value);
            }
        }
        private double? _customGuideV1;

        public double? CustomGuideV2
        {
            get => VerticalGuides != null && VerticalGuides.Count > 1 ? VerticalGuides[1] : _customGuideV2;
            set
            {
                _customGuideV2 = value;
                if (value.HasValue && VerticalGuides != null && !VerticalGuides.Contains(value.Value))
                    VerticalGuides.Add(value.Value);
            }
        }
        private double? _customGuideV2;

        public double? CustomGuideH1
        {
            get => HorizontalGuides != null && HorizontalGuides.Count > 0 ? HorizontalGuides[0] : _customGuideH1;
            set
            {
                _customGuideH1 = value;
                if (value.HasValue && HorizontalGuides != null && !HorizontalGuides.Contains(value.Value))
                    HorizontalGuides.Add(value.Value);
            }
        }
        private double? _customGuideH1;

        public double? CustomGuideH2
        {
            get => HorizontalGuides != null && HorizontalGuides.Count > 1 ? HorizontalGuides[1] : _customGuideH2;
            set
            {
                _customGuideH2 = value;
                if (value.HasValue && HorizontalGuides != null && !HorizontalGuides.Contains(value.Value))
                    HorizontalGuides.Add(value.Value);
            }
        }
        private double? _customGuideH2;
        #endregion

        public bool ShowCustomGuides { get; set; } = false;
    }
}
