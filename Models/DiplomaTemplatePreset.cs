using System;
using System.Collections.Generic;

namespace DiplomPrintGenerate.Models
{
    /// <summary>
    /// Модель сохраненного пресета шаблона диплома (*.dpt).
    /// Поддерживает неограниченное количество любых полей (текстовые блоки, направляющие, кастомные метаданные).
    /// </summary>
    public class DiplomaTemplatePreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Новый шаблон";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public double CanvasWidth { get; set; } = 800;
        public double CanvasHeight { get; set; } = 1131;
        public string ImageBase64 { get; set; } = "";

        /// <summary>
        /// Список всех текстовых полей диплома без каких-либо ограничений по количеству.
        /// Включает как стандартные поля (ФИО, Номинация, Место, Дата), так и любое число пользовательских (кастомных) полей.
        /// </summary>
        public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();

        /// <summary>
        /// Словарь произвольных кастомных строковых полей и атрибутов (ключ-значение) в неограниченном количестве.
        /// </summary>
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Дополнительные кастомные свойства и метаданные шаблона.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Список вертикальных направляющих (в пикселях) в неограниченном количестве.
        /// </summary>
        public List<double> VerticalGuides { get; set; } = new List<double>();

        /// <summary>
        /// Список горизонтальных направляющих (в пикселях) в неограниченном количестве.
        /// </summary>
        public List<double> HorizontalGuides { get; set; } = new List<double>();

        #region Свойства для обратной совместимости со старыми файлами *.dpt
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

        public override string ToString()
        {
            return $"{Title} ({Fields?.Count ?? 0} полей)";
        }
    }
}
