using System;
using System.ComponentModel;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;

namespace DiplomPrintGenerate.Models
{
    public class FieldDefinition : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private double _x;
        private double _y;
        private double _width;
        private double _height;
        private string _fontFamily;
        private double _fontSize;
        private bool _isBold;
        private bool _isItalic;
        private TextAlignment _alignment;
        private string _textColorHex;
        private string _sampleText;
        private bool _autoShrink;
        private bool _isCustom;
        private System.Collections.Generic.Dictionary<string, string> _customAttributes;

        public event PropertyChangedEventHandler PropertyChanged;

        public FieldDefinition()
        {
            _id = Guid.NewGuid().ToString("N");
            _name = "Новое поле";
            _x = 100;
            _y = 100;
            _width = 300;
            _height = 60;
            _fontFamily = "Times New Roman";
            _fontSize = 28;
            _isBold = true;
            _isItalic = false;
            _alignment = TextAlignment.Center;
            _textColorHex = "#1A202C";
            _sampleText = "Пример текста";
            _autoShrink = true;
        }

        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged("Id"); } }
        }

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged("Name"); } }
        }

        public double X
        {
            get => _x;
            set { if (_x != value) { _x = value; OnPropertyChanged("X"); } }
        }

        public double Y
        {
            get => _y;
            set { if (_y != value) { _y = value; OnPropertyChanged("Y"); } }
        }

        public double Width
        {
            get => _width;
            set { if (_width != value) { _width = Math.Max(20, value); OnPropertyChanged("Width"); } }
        }

        public double Height
        {
            get => _height;
            set { if (_height != value) { _height = Math.Max(15, value); OnPropertyChanged("Height"); } }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set { if (_fontFamily != value) { _fontFamily = value; OnPropertyChanged("FontFamily"); } }
        }

        public double FontSize
        {
            get => _fontSize;
            set { if (_fontSize != value) { _fontSize = Math.Max(6, value); OnPropertyChanged("FontSize"); } }
        }

        public bool IsBold
        {
            get => _isBold;
            set { if (_isBold != value) { _isBold = value; OnPropertyChanged("IsBold"); } }
        }

        public bool IsItalic
        {
            get => _isItalic;
            set { if (_isItalic != value) { _isItalic = value; OnPropertyChanged("IsItalic"); } }
        }

        public TextAlignment Alignment
        {
            get => _alignment;
            set { if (_alignment != value) { _alignment = value; OnPropertyChanged("Alignment"); } }
        }

        public string TextColorHex
        {
            get => _textColorHex;
            set { if (_textColorHex != value) { _textColorHex = value; OnPropertyChanged("TextColorHex"); OnPropertyChanged("TextColorBrush"); } }
        }

        [ScriptIgnore]
        public SolidColorBrush TextColorBrush
        {
            get
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(_textColorHex) ?? Brushes.Black;
                }
                catch
                {
                    return Brushes.Black;
                }
            }
        }

        public string SampleText
        {
            get => _sampleText;
            set { if (_sampleText != value) { _sampleText = value; OnPropertyChanged("SampleText"); } }
        }

        public bool AutoShrink
        {
            get => _autoShrink;
            set { if (_autoShrink != value) { _autoShrink = value; OnPropertyChanged("AutoShrink"); } }
        }

        public bool IsCustom
        {
            get => _isCustom;
            set { if (_isCustom != value) { _isCustom = value; OnPropertyChanged("IsCustom"); } }
        }

        public System.Collections.Generic.Dictionary<string, string> CustomAttributes
        {
            get => _customAttributes ?? (_customAttributes = new System.Collections.Generic.Dictionary<string, string>());
            set { _customAttributes = value; OnPropertyChanged("CustomAttributes"); }
        }

        public FieldDefinition Clone()
        {
            var clone = new FieldDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = this.Name + " (копия)",
                X = this.X + 20,
                Y = this.Y + 20,
                Width = this.Width,
                Height = this.Height,
                FontFamily = this.FontFamily,
                FontSize = this.FontSize,
                IsBold = this.IsBold,
                IsItalic = this.IsItalic,
                Alignment = this.Alignment,
                TextColorHex = this.TextColorHex,
                SampleText = this.SampleText,
                AutoShrink = this.AutoShrink,
                IsCustom = this.IsCustom
            };
            if (this._customAttributes != null)
            {
                clone.CustomAttributes = new System.Collections.Generic.Dictionary<string, string>(this._customAttributes);
            }
            return clone;
        }

        protected void OnPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
