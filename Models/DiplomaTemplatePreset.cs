using System;
using System.Collections.Generic;

namespace DiplomPrintGenerate.Models
{
    public class DiplomaTemplatePreset
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Новый шаблон";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public double CanvasWidth { get; set; } = 800;
        public double CanvasHeight { get; set; } = 1131;
        public string ImageBase64 { get; set; } = "";
        public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();
        public double? CustomGuideV1 { get; set; }
        public double? CustomGuideV2 { get; set; }
        public double? CustomGuideH1 { get; set; }
        public double? CustomGuideH2 { get; set; }
        public bool ShowCustomGuides { get; set; } = false;

        public override string ToString()
        {
            return $"{Title} ({Fields?.Count ?? 0} полей)";
        }
    }
}
