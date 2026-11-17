using System;
using System.IO;
using System.Web.Script.Serialization;
using DiplomPrintGenerate.Models;

namespace DiplomPrintGenerate.Services
{
    public class ProjectFileService
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue
        };

        public static void SaveProject(string filePath, TemplateProject project)
        {
            if (project == null) return;

            string json = Serializer.Serialize(project);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        public static TemplateProject LoadProject(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var project = Serializer.Deserialize<TemplateProject>(json);

            // If template image was stored as relative path, resolve it relative to project file
            if (!string.IsNullOrEmpty(project.TemplateImagePath) && !Path.IsPathRooted(project.TemplateImagePath))
            {
                string baseDir = Path.GetDirectoryName(filePath);
                string fullPath = Path.Combine(baseDir, project.TemplateImagePath);
                if (File.Exists(fullPath))
                {
                    project.TemplateImagePath = fullPath;
                }
            }

            return project;
        }
    }
}
