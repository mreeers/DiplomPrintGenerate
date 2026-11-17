using System.Collections.Generic;

namespace DiplomPrintGenerate.Models
{
    public class TemplateProject
    {
        public string TemplateImagePath { get; set; }
        public double CanvasWidth { get; set; } = 800;
        public double CanvasHeight { get; set; } = 1131; // A4 aspect ratio ~ 1 : 1.4142
        public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();
        public List<Dictionary<string, string>> Participants { get; set; } = new List<Dictionary<string, string>>();
        public string OutputFolder { get; set; } = "";
        public int OutputFormat { get; set; } = 0; // 0=MultiPagePdf, 1=SinglePdfs, 2=ImagesPng, 3=PrintDirect
        public string FileNamePattern { get; set; } = "Диплом_{ФИО}";
        public double? CustomGuideV1 { get; set; }
        public double? CustomGuideV2 { get; set; }
        public double? CustomGuideH1 { get; set; }
        public double? CustomGuideH2 { get; set; }
        public bool ShowCustomGuides { get; set; } = false;
    }
}
