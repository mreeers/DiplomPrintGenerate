using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using DiplomPrintGenerate.Models;
using DiplomPrintGenerate.Services;

namespace DiplomPrintGenerate
{
    public partial class MainWindow : Window
    {
        private BitmapSource _templateImage;
        private string _templateImagePath = "";
        private readonly ObservableCollection<FieldDefinition> _fields = new ObservableCollection<FieldDefinition>();
        private FieldDefinition _selectedField = null;
        private readonly DataTable _participantsTable = new DataTable();

        // Drag & Resize state
        private bool _isDragging = false;
        private bool _isResizing = false;
        private Point _dragStartPoint;
        private double _origX, _origY, _origW, _origH;

        // Custom Guides state (2 Vertical + 2 Horizontal)
        private bool _showCustomGuides = false;
        private double _customGuideV1 = 80.0;
        private double _customGuideV2 = 720.0;
        private double _customGuideH1 = 113.0;
        private double _customGuideH2 = 1018.0;
        private int _draggingGuideIndex = 0; // 1=V1, 2=V2, 3=H1, 4=H2, 0=none
        private Point _guideDragStart;
        private double _guideOrigPos;

        // Preview state
        private int _previewParticipantIndex = 0;
        private double _currentZoom = 1.0;
        private bool _isUpdatingUi = false;
        private bool _isApplyingPreset = false;
        private List<DiplomaTemplatePreset> _presets = new List<DiplomaTemplatePreset>();

        public MainWindow()
        {
            InitializeComponent();
            PopulateFontFamilies();
            InitDefaultProject();
            InitParticipantsTable();

            // Set default output folder to Documents\Diplomas
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultFolder = System.IO.Path.Combine(docs, "Дипломы_Экспорт");
            OutputFolderTextBox.Text = defaultFolder;

            this.Loaded += MainWindow_Loaded;
            this.PreviewMouseDown += MainWindow_PreviewMouseDown;
            this.PreviewMouseUp += MainWindow_PreviewMouseUp;
            this.PreviewMouseMove += MainWindow_PreviewMouseMove;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isEyedropperActive)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    StopEyedropper(applyColor: true);
                }
                else
                {
                    StopEyedropper(applyColor: false);
                }
                e.Handled = true;
                return;
            }

            try
            {
                var fe = e.OriginalSource as FrameworkElement;
                string elementName = !string.IsNullOrEmpty(fe?.Name) ? fe.Name : (fe?.Tag?.ToString() ?? fe?.GetType().Name ?? "unnamed");
                Point pos = e.GetPosition(this);
                Logger.LogClick(elementName, e.OriginalSource?.GetType().Name, pos.X, pos.Y);
            }
            catch { }
        }

        private void MainWindow_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isEyedropperActive)
            {
                UpdateEyedropperPreview(e);
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isEyedropperActive && e.Key == Key.Escape)
            {
                StopEyedropper(applyColor: false);
                e.Handled = true;
                return;
            }

            // Ctrl + Shift + D: Toggle Debug Tools (Screenshots & Logs)
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.D)
            {
                ToggleDebugTools();
                e.Handled = true;
                return;
            }

            // Arrow keys: move selected field on canvas if not typing in a TextBox
            if (MainTabs.SelectedIndex == 0 && _selectedField != null && !(FocusManager.GetFocusedElement(this) is TextBox))
            {
                if (key == Key.Left)
                {
                    NudgeSelectedField(-1, 0);
                    e.Handled = true;
                    return;
                }
                if (key == Key.Right)
                {
                    NudgeSelectedField(1, 0);
                    e.Handled = true;
                    return;
                }
                if (key == Key.Up)
                {
                    NudgeSelectedField(0, -1);
                    e.Handled = true;
                    return;
                }
                if (key == Key.Down)
                {
                    NudgeSelectedField(0, 1);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void ToggleDebugTools()
        {
            if (DebugToolsPanel.Visibility == Visibility.Visible)
            {
                DebugToolsPanel.Visibility = Visibility.Collapsed;
                Logger.Log("Панель отладки скрыта (Ctrl+Shift+D).");
            }
            else
            {
                DebugToolsPanel.Visibility = Visibility.Visible;
                Logger.Log("Панель отладки отображена (Ctrl+Shift+D).");
            }
        }

        private void MainWindow_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var fe = e.OriginalSource as FrameworkElement;
                string elementName = !string.IsNullOrEmpty(fe?.Name) ? fe.Name : (fe?.Tag?.ToString() ?? fe?.GetType().Name ?? "unnamed");

                if (ScreenshotService.IsEnabled)
                {
                    // Delay screenshot until the dispatcher completes current event handling & layout render
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        ScreenshotService.CaptureWindow(this, $"click_{elementName}");
                    }));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка в обработчике клика окна", ex);
            }
        }

        private void ChkAutoScreenshots_Changed(object sender, RoutedEventArgs e)
        {
            ScreenshotService.IsEnabled = ChkAutoScreenshots.IsChecked == true;
            Logger.Log($"Режим автоскриншотов: {(ScreenshotService.IsEnabled ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")}");
        }

        private void BtnOpenScreenshots_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(ScreenshotService.ScreenshotsDirectory))
                {
                    Process.Start("explorer.exe", ScreenshotService.ScreenshotsDirectory);
                }
                else
                {
                    MessageBox.Show("Папка скриншотов еще не создана. Сделайте хотя бы один клик в окне.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Не удалось открыть папку скриншотов", ex);
            }
        }

        private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(Logger.CurrentLogFile))
                {
                    Process.Start("notepad.exe", Logger.CurrentLogFile);
                }
                else if (Directory.Exists(Logger.LogDirectory))
                {
                    Process.Start("explorer.exe", Logger.LogDirectory);
                }
                else
                {
                    MessageBox.Show("Журнал логов еще не создан.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Не удалось открыть файл логов", ex);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateCanvasZoom();
            UpdatePermanentGuides();
            SetDefaultCustomGuides();
            RefreshAllFieldVisuals();
            SelectField(_fields.FirstOrDefault());
            EnsureSamplePresetExists();
            RefreshTemplatesList();

            Logger.Log("Окно приложения готово к работе.");

            // Initial screenshot
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                ScreenshotService.CaptureWindow(this, "window_opened");
            }));
        }

        private void PopulateFontFamilies()
        {
            var fontNames = Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .OrderBy(name => name)
                .ToList();

            PropFontFamily.ItemsSource = fontNames;
        }

        private void InitDefaultProject()
        {
            // Create default template
            _templateImage = TemplateGeneratorService.CreateDefaultDiplomaTemplate(2480, 3508);
            TemplateImageDisplay.Source = _templateImage;

            // Default fields mapped to coordinates on 800x1131 canvas
            _fields.Clear();

            _fields.Add(new FieldDefinition
            {
                Name = "ФИО",
                SampleText = "Иванов Иван Иванович",
                X = 100,
                Y = 280,
                Width = 600,
                Height = 60,
                FontFamily = "Georgia",
                FontSize = 32,
                IsBold = true,
                Alignment = TextAlignment.Center,
                TextColorHex = "#182B49",
                AutoShrink = true
            });

            _fields.Add(new FieldDefinition
            {
                Name = "Номинация",
                SampleText = "За победу в краеведческой олимпиаде",
                X = 100,
                Y = 360,
                Width = 600,
                Height = 45,
                FontFamily = "Times New Roman",
                FontSize = 20,
                IsBold = false,
                IsItalic = true,
                Alignment = TextAlignment.Center,
                TextColorHex = "#CDA04B",
                AutoShrink = true
            });

            _fields.Add(new FieldDefinition
            {
                Name = "Место",
                SampleText = "I МЕСТО",
                X = 250,
                Y = 430,
                Width = 300,
                Height = 50,
                FontFamily = "Georgia",
                FontSize = 26,
                IsBold = true,
                Alignment = TextAlignment.Center,
                TextColorHex = "#182B49",
                AutoShrink = true
            });

            _fields.Add(new FieldDefinition
            {
                Name = "Дата",
                SampleText = DateTime.Now.ToString("dd MMMM yyyy г.", new System.Globalization.CultureInfo("ru-RU")),
                X = 100,
                Y = 960,
                Width = 250,
                Height = 35,
                FontFamily = "Times New Roman",
                FontSize = 15,
                IsBold = false,
                Alignment = TextAlignment.Left,
                TextColorHex = "#475569",
                AutoShrink = true
            });
        }

        private void InitParticipantsTable()
        {
            SyncTableColumns();

            // Add 3 sample participants
            var row1 = _participantsTable.NewRow();
            row1["ФИО"] = "Иванов Иван Иванович";
            row1["Номинация"] = "За победу в краеведческой олимпиаде";
            row1["Место"] = "I МЕСТО";
            row1["Дата"] = DateTime.Now.ToString("dd MMMM yyyy г.", new System.Globalization.CultureInfo("ru-RU"));
            _participantsTable.Rows.Add(row1);

            var row2 = _participantsTable.NewRow();
            row2["ФИО"] = "Петрова Анна Сергеевна";
            row2["Номинация"] = "За выдающиеся успехи в исследовании края";
            row2["Место"] = "II МЕСТО";
            row2["Дата"] = DateTime.Now.ToString("dd MMMM yyyy г.", new System.Globalization.CultureInfo("ru-RU"));
            _participantsTable.Rows.Add(row2);

            var row3 = _participantsTable.NewRow();
            row3["ФИО"] = "Смирнов Алексей Дмитриевич";
            row3["Номинация"] = "За активное участие в краеведческом слете";
            row3["Место"] = "Диплом участника";
            row3["Дата"] = DateTime.Now.ToString("dd MMMM yyyy г.", new System.Globalization.CultureInfo("ru-RU"));
            _participantsTable.Rows.Add(row3);

            UpdateParticipantsCount();
        }

        private void SyncTableColumns()
        {
            // Ensure all field names exist as columns
            foreach (var field in _fields)
            {
                if (!_participantsTable.Columns.Contains(field.Name))
                {
                    _participantsTable.Columns.Add(field.Name, typeof(string));
                }
            }

            ParticipantsGrid.ItemsSource = null;
            ParticipantsGrid.Columns.Clear();

            foreach (DataColumn col in _participantsTable.Columns)
            {
                var textCol = new DataGridTextColumn
                {
                    Header = col.ColumnName,
                    Binding = new Binding(col.ColumnName)
                };
                ParticipantsGrid.Columns.Add(textCol);
            }

            ParticipantsGrid.ItemsSource = _participantsTable.DefaultView;
            UpdateParticipantsCount();
        }

        private void UpdateParticipantsCount()
        {
            int count = _participantsTable.Rows.Count;
            ParticipantsCountText.Text = string.Format("Всего участников: {0}", count);
        }

        #region Field Visuals & Canvas Drag/Resize

        private void RefreshAllFieldVisuals()
        {
            // Remove existing field controls (keep only TemplateImageDisplay at index 0)
            while (DesignerCanvas.Children.Count > 1)
            {
                DesignerCanvas.Children.RemoveAt(1);
            }

            foreach (var field in _fields)
            {
                var element = CreateFieldVisualElement(field);
                DesignerCanvas.Children.Add(element);
            }
        }

        private FrameworkElement CreateFieldVisualElement(FieldDefinition field)
        {
            bool isSelected = field == _selectedField;

            var border = new Border
            {
                Width = field.Width,
                Height = field.Height,
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(40, 37, 99, 235)) : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromArgb(160, 100, 116, 139)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                CornerRadius = new CornerRadius(3),
                Cursor = Cursors.SizeAll,
                Tag = field,
                SnapsToDevicePixels = true
            };

            if (!isSelected)
            {
                // Dashed border for unselected
                var dashStyle = new DoubleCollection { 3, 2 };
                // Border doesn't support dash directly, so we keep simple border
            }

            Canvas.SetLeft(border, field.X);
            Canvas.SetTop(border, field.Y);

            // Container inside border
            var grid = new Grid();

            // Field Label / Text
            var tb = new TextBlock
            {
                Text = string.IsNullOrEmpty(field.SampleText) ? ("[" + field.Name + "]") : field.SampleText,
                FontFamily = new FontFamily(field.FontFamily ?? "Times New Roman"),
                FontSize = field.FontSize,
                FontWeight = field.IsBold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = field.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                Foreground = field.TextColorBrush,
                TextAlignment = field.Alignment,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 4, 2),
                ClipToBounds = true
            };
            grid.Children.Add(tb);

            // Small header tag with field name
            var tagBorder = new Border
            {
                Background = isSelected ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromArgb(180, 51, 65, 85)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 1, 3, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 2, 0, 0)
            };
            var tagText = new TextBlock
            {
                Text = field.Name,
                Foreground = Brushes.White,
                FontSize = 9,
                FontWeight = FontWeights.Bold
            };
            tagBorder.Child = tagText;
            grid.Children.Add(tagBorder);

            // Resize Handle (bottom-right corner thumb)
            if (isSelected)
            {
                var resizeThumb = new Border
                {
                    Width = 10,
                    Height = 10,
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                    BorderThickness = new Thickness(2),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Cursor = Cursors.SizeNWSE,
                    Margin = new Thickness(0, 0, -2, -2)
                };

                resizeThumb.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    _isResizing = true;
                    _dragStartPoint = e.GetPosition(DesignerCanvas);
                    _origW = field.Width;
                    _origH = field.Height;
                    resizeThumb.CaptureMouse();
                };

                resizeThumb.MouseMove += (s, e) =>
                {
                    if (_isResizing)
                    {
                        var curPoint = e.GetPosition(DesignerCanvas);
                        double dx = curPoint.X - _dragStartPoint.X;
                        double dy = curPoint.Y - _dragStartPoint.Y;

                        field.Width = Math.Max(30, _origW + dx);
                        field.Height = Math.Max(20, _origH + dy);

                        border.Width = field.Width;
                        border.Height = field.Height;
                    }
                };

                resizeThumb.MouseLeftButtonUp += (s, e) =>
                {
                    _isResizing = false;
                    resizeThumb.ReleaseMouseCapture();
                    HideSmartGuides();
                    RefreshPropertiesUi();
                };

                grid.Children.Add(resizeThumb);
            }

            border.Child = grid;

            // Drag Move events
            border.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                SelectField(field);

                _isDragging = true;
                _dragStartPoint = e.GetPosition(DesignerCanvas);
                _origX = field.X;
                _origY = field.Y;
                border.CaptureMouse();
            };

            border.MouseMove += (s, e) =>
            {
                if (_isDragging)
                {
                    var curPoint = e.GetPosition(DesignerCanvas);
                    double dx = curPoint.X - _dragStartPoint.X;
                    double dy = curPoint.Y - _dragStartPoint.Y;

                    double newX = Math.Max(0, Math.Min(DesignerCanvas.Width - field.Width, _origX + dx));
                    double newY = Math.Max(0, Math.Min(DesignerCanvas.Height - field.Height, _origY + dy));

                    // Smart Snapping & Guides (Photoshop-style)
                    ApplySmartSnapping(field, ref newX, ref newY);

                    field.X = newX;
                    field.Y = newY;

                    Canvas.SetLeft(border, newX);
                    Canvas.SetTop(border, newY);

                    if (PropPosXText != null) PropPosXText.Text = string.Format("{0:0} px", newX);
                    if (PropPosYText != null) PropPosYText.Text = string.Format("{0:0} px", newY);
                }
            };

            border.MouseLeftButtonUp += (s, e) =>
            {
                _isDragging = false;
                border.ReleaseMouseCapture();
                HideSmartGuides();
                RefreshPropertiesUi();
            };

            return border;
        }

        private void SelectField(FieldDefinition field)
        {
            _selectedField = field;
            RefreshAllFieldVisuals();
            RefreshPropertiesUi();
        }

        private void DesignerCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Click on empty canvas unselects
            if (e.OriginalSource == DesignerCanvas || e.OriginalSource == TemplateImageDisplay)
            {
                _selectedField = null;
                RefreshAllFieldVisuals();
                RefreshPropertiesUi();
            }
        }

        private void RefreshPropertiesUi()
        {
            _isUpdatingUi = true;

            if (_selectedField == null)
            {
                NoSelectionPlaceholder.Visibility = Visibility.Visible;
                PropertiesScrollViewer.Visibility = Visibility.Collapsed;
                _isUpdatingUi = false;
                return;
            }

            NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
            PropertiesScrollViewer.Visibility = Visibility.Visible;
            PropertiesPanel.IsEnabled = true;
            PropertiesPanel.Opacity = 1.0;

            PropName.Text = _selectedField.Name;
            PropSampleText.Text = _selectedField.SampleText;
            PropFontFamily.SelectedItem = _selectedField.FontFamily;
            PropFontSizeSlider.Value = _selectedField.FontSize;
            PropFontSizeText.Text = string.Format("{0:0} pt", _selectedField.FontSize);

            PropBoldToggle.IsChecked = _selectedField.IsBold;
            PropItalicToggle.IsChecked = _selectedField.IsItalic;

            PropAlignLeft.IsChecked = _selectedField.Alignment == TextAlignment.Left;
            PropAlignCenter.IsChecked = _selectedField.Alignment == TextAlignment.Center;
            PropAlignRight.IsChecked = _selectedField.Alignment == TextAlignment.Right;

            PropColorHex.Text = _selectedField.TextColorHex;
            try
            {
                PropColorPreview.Background = _selectedField.TextColorBrush;
            }
            catch { }

            PropAutoShrink.IsChecked = _selectedField.AutoShrink;

            if (PropPosXText != null) PropPosXText.Text = string.Format("{0:0} px", _selectedField.X);
            if (PropPosYText != null) PropPosYText.Text = string.Format("{0:0} px", _selectedField.Y);

            _isUpdatingUi = false;
        }

        #endregion

        #region Properties Panel Event Handlers

        private void PropName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            string oldName = _selectedField.Name;
            string newName = PropName.Text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                _selectedField.Name = newName;

                // Update DataTable column name if changed
                if (_participantsTable.Columns.Contains(oldName) && !_participantsTable.Columns.Contains(newName))
                {
                    _participantsTable.Columns[oldName].ColumnName = newName;
                    SyncTableColumns();
                }

                RefreshAllFieldVisuals();
            }
        }

        private void PropSampleText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            _selectedField.SampleText = PropSampleText.Text;
            RefreshAllFieldVisuals();
        }

        private void PropFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null || PropFontFamily.SelectedItem == null) return;
            _selectedField.FontFamily = PropFontFamily.SelectedItem.ToString();
            RefreshAllFieldVisuals();
        }

        private void PropFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            _selectedField.FontSize = Math.Round(e.NewValue);
            PropFontSizeText.Text = string.Format("{0:0} pt", _selectedField.FontSize);
            RefreshAllFieldVisuals();
        }

        private void PropBoldToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            _selectedField.IsBold = PropBoldToggle.IsChecked == true;
            RefreshAllFieldVisuals();
        }

        private void PropItalicToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            _selectedField.IsItalic = PropItalicToggle.IsChecked == true;
            RefreshAllFieldVisuals();
        }

        private void PropAlign_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            if (PropAlignLeft.IsChecked == true) _selectedField.Alignment = TextAlignment.Left;
            else if (PropAlignCenter.IsChecked == true) _selectedField.Alignment = TextAlignment.Center;
            else if (PropAlignRight.IsChecked == true) _selectedField.Alignment = TextAlignment.Right;
            RefreshAllFieldVisuals();
        }

        private void SetColor_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedField == null) return;
            var btn = sender as Button;
            if (btn != null && btn.Tag != null)
            {
                PropColorHex.Text = btn.Tag.ToString();
            }
        }

        private void PropColorHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            string hex = PropColorHex.Text.Trim();
            if (hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9))
            {
                _selectedField.TextColorHex = hex;
                try
                {
                    PropColorPreview.Background = _selectedField.TextColorBrush;
                    RefreshAllFieldVisuals();
                }
                catch { }
            }
        }

        #region Eyedropper Tool

        private bool _isEyedropperActive = false;

        private void PropColorPreview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                StartEyedropper();
                e.Handled = true;
            }
        }

        private void StartEyedropper()
        {
            if (_selectedField == null && _fields.Count > 0)
            {
                SelectField(_fields[0]);
            }

            _isEyedropperActive = true;
            EyedropperService.IsActive = true;
            Mouse.OverrideCursor = Cursors.Cross;
            EyedropperPopup.IsOpen = true;
            this.CaptureMouse();

            UpdateEyedropperPreview();
            Logger.Log("Режим пипетки активирован: наведите курсор на нужный цвет и кликните левой кнопкой мыши.");
        }

        private void StopEyedropper(bool applyColor = false)
        {
            if (!_isEyedropperActive) return;

            _isEyedropperActive = false;
            EyedropperService.IsActive = false;
            this.ReleaseMouseCapture();
            Mouse.OverrideCursor = null;
            EyedropperPopup.IsOpen = false;

            if (applyColor)
            {
                Color color = EyedropperService.GetColorAtCursor();
                string hex = EyedropperService.ColorToHex(color);
                ApplyColorToSelectedField(hex);
                Logger.Log($"Цвет выбран пипеткой: {hex} (RGB: {color.R}, {color.G}, {color.B})");
                ScreenshotService.CaptureWindow(this, "eyedropper_picked");
            }
            else
            {
                Logger.Log("Режим пипетки отменен.");
            }
        }

        private void ApplyColorToSelectedField(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return;
            PropColorHex.Text = hex;
        }

        private void UpdateEyedropperPreview(MouseEventArgs e = null)
        {
            try
            {
                Color color = EyedropperService.GetColorAtCursor();
                EyedropperSwatch.Background = new SolidColorBrush(color);
                EyedropperHexText.Text = EyedropperService.ColorToHex(color);
                EyedropperRgbText.Text = string.Format("RGB: {0}, {1}, {2}", color.R, color.G, color.B);

                Point pos = e != null ? e.GetPosition(RootGrid) : Mouse.GetPosition(RootGrid);
                double x = pos.X + 18;
                double y = pos.Y + 18;

                double popupW = 200;
                double popupH = 65;

                if (x + popupW > RootGrid.ActualWidth)
                    x = pos.X - popupW - 10;
                if (y + popupH > RootGrid.ActualHeight)
                    y = pos.Y - popupH - 10;

                EyedropperPopup.HorizontalOffset = Math.Max(4, x);
                EyedropperPopup.VerticalOffset = Math.Max(4, y);
            }
            catch { }
        }

        #endregion

        private void PropAutoShrink_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi || _selectedField == null) return;
            _selectedField.AutoShrink = PropAutoShrink.IsChecked == true;
        }

        private double GetNudgeStep()
        {
            if (NudgeStepSelector == null) return 10.0;
            switch (NudgeStepSelector.SelectedIndex)
            {
                case 0: return 1.0;
                case 1: return 5.0;
                case 2: return 10.0;
                case 3: return 25.0;
                default: return 10.0;
            }
        }

        private void NudgeSelectedField(double dx, double dy)
        {
            if (_selectedField == null) return;

            double step = GetNudgeStep();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                step *= 5.0; // Fast nudge with Shift
            }

            double newX = Math.Max(0, Math.Min(DesignerCanvas.Width - _selectedField.Width, _selectedField.X + dx * step));
            double newY = Math.Max(0, Math.Min(DesignerCanvas.Height - _selectedField.Height, _selectedField.Y + dy * step));

            _selectedField.X = newX;
            _selectedField.Y = newY;

            RefreshAllFieldVisuals();
            RefreshPropertiesUi();
        }

        private void BtnNudgeLeft_Click(object sender, RoutedEventArgs e)
        {
            NudgeSelectedField(-1, 0);
        }

        private void BtnNudgeRight_Click(object sender, RoutedEventArgs e)
        {
            NudgeSelectedField(1, 0);
        }

        private void BtnNudgeUp_Click(object sender, RoutedEventArgs e)
        {
            NudgeSelectedField(0, -1);
        }

        private void BtnNudgeDown_Click(object sender, RoutedEventArgs e)
        {
            NudgeSelectedField(0, 1);
        }

        private void BtnCenterHorizontally_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedField == null) return;
            _selectedField.X = (DesignerCanvas.Width - _selectedField.Width) / 2.0;
            RefreshAllFieldVisuals();
            RefreshPropertiesUi();
        }

        private void BtnDuplicateField_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedField == null) return;
            var copy = _selectedField.Clone();
            _fields.Add(copy);
            SyncTableColumns();
            SelectField(copy);
        }

        private void BtnDeleteField_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedField == null) return;
            var fieldToRemove = _selectedField;
            _fields.Remove(fieldToRemove);
            _selectedField = _fields.LastOrDefault();
            SyncTableColumns();
            RefreshAllFieldVisuals();
            RefreshPropertiesUi();
        }

        #endregion

        #region Drag and Drop Files onto Window

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string file = files[0];
                    string ext = System.IO.Path.GetExtension(file).ToLower();

                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".pdf")
                    {
                        LoadTemplateFromFile(file);
                    }
                    else if (ext == ".dpg")
                    {
                        LoadProjectFile(file);
                    }
                    else if (ext == ".dpt")
                    {
                        string dest = System.IO.Path.Combine(TemplatePresetService.DirectoryPath, System.IO.Path.GetFileName(file));
                        if (!string.Equals(file, dest, StringComparison.OrdinalIgnoreCase))
                        {
                            System.IO.File.Copy(file, dest, true);
                        }
                        RefreshTemplatesList();
                        System.Windows.MessageBox.Show("Файл шаблона успешно импортирован в библиотеку!", "Импорт шаблона", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (ext == ".csv" || ext == ".tsv" || ext == ".txt")
                    {
                        LoadParticipantsFromCsvFile(file);
                    }
                }
            }
        }

        #endregion

        #region Template Presets Library

        private void EnsureSamplePresetExists()
        {
            try
            {
                var existing = TemplatePresetService.LoadAllPresets();
                if (existing.Count == 0 && _templateImage != null)
                {
                    TemplatePresetService.SavePreset("Классический диплом (образец)", _templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка создания образца шаблона", ex);
            }
        }

        private void RefreshTemplatesList(string selectTitle = null)
        {
            _isApplyingPreset = true;
            try
            {
                _presets = TemplatePresetService.LoadAllPresets();
                TemplatesSelector.ItemsSource = null;
                TemplatesSelector.ItemsSource = _presets;
                TemplatesSelector.DisplayMemberPath = "Title";

                if (!string.IsNullOrEmpty(selectTitle))
                {
                    TemplatesSelector.SelectedItem = _presets.FirstOrDefault(p => p.Title == selectTitle);
                }
                else if (_presets.Count > 0)
                {
                    TemplatesSelector.SelectedIndex = 0;
                }
            }
            finally
            {
                _isApplyingPreset = false;
            }
        }

        private void TemplatesSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingPreset || TemplatesSelector.SelectedItem == null)
                return;

            var preset = TemplatesSelector.SelectedItem as DiplomaTemplatePreset;
            if (preset == null) return;

            try
            {
                // Restore background image
                if (!string.IsNullOrEmpty(preset.ImageBase64))
                {
                    var bmp = TemplatePresetService.Base64ToBitmap(preset.ImageBase64);
                    if (bmp != null)
                    {
                        _templateImage = bmp;
                        _templateImagePath = "";
                        TemplateImageDisplay.Source = _templateImage;
                    }
                }

                if (preset.CanvasWidth > 0 && preset.CanvasHeight > 0)
                {
                    DesignerCanvas.Width = preset.CanvasWidth;
                    DesignerCanvas.Height = preset.CanvasHeight;
                    UpdatePermanentGuides();
                }

                // Restore all custom fields
                _fields.Clear();
                if (preset.Fields != null)
                {
                    foreach (var f in preset.Fields)
                    {
                        _fields.Add(f.Clone());
                    }
                }

                // Restore custom guides
                if (preset.CustomGuideV1.HasValue) _customGuideV1 = preset.CustomGuideV1.Value;
                if (preset.CustomGuideV2.HasValue) _customGuideV2 = preset.CustomGuideV2.Value;
                if (preset.CustomGuideH1.HasValue) _customGuideH1 = preset.CustomGuideH1.Value;
                if (preset.CustomGuideH2.HasValue) _customGuideH2 = preset.CustomGuideH2.Value;
                _showCustomGuides = preset.ShowCustomGuides;
                BtnToggleCustomGuides.IsChecked = _showCustomGuides;
                CustomGuidesCanvas.Visibility = _showCustomGuides ? Visibility.Visible : Visibility.Collapsed;
                CustomGuidesSettingsCard.Visibility = _showCustomGuides ? Visibility.Visible : Visibility.Collapsed;
                UpdateCustomGuidesVisuals();

                // Synchronize DataGrid table columns
                SyncTableColumns();
                RefreshAllFieldVisuals();
                SelectField(_fields.FirstOrDefault());

                Logger.Log($"Применен сохраненный шаблон: '{preset.Title}' ({_fields.Count} полей)");
                ScreenshotService.CaptureWindow(this, $"preset_applied_{preset.Title}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка применения шаблона: " + preset.Title, ex);
                MessageBox.Show("Ошибка применения шаблона: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveAsTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (_templateImage == null)
            {
                MessageBox.Show("Сначала загрузите бланк диплома или используйте текущий шаблон.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new Views.SaveTemplateDialog("Шаблон диплома " + DateTime.Now.ToString("dd.MM.yyyy"));
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string title = dlg.TemplateTitle;
                    TemplatePresetService.SavePreset(title, _templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, _customGuideV1, _customGuideV2, _customGuideH1, _customGuideH2, _showCustomGuides);
                    RefreshTemplatesList(title);

                    Logger.Log($"Шаблон успешно сохранен под именем '{title}'");
                    ScreenshotService.CaptureWindow(this, "template_saved");
                    MessageBox.Show($"Шаблон «{title}» успешно сохранен в библиотеку!\n\nТеперь он доступен в выпадающем списке шаблонов в любое время.", "Шаблон сохранен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Ошибка сохранения шаблона", ex);
                    MessageBox.Show("Ошибка сохранения шаблона: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDeleteSelectedTemplate_Click(object sender, RoutedEventArgs e)
        {
            var preset = TemplatesSelector.SelectedItem as DiplomaTemplatePreset;
            if (preset == null)
            {
                MessageBox.Show("Выберите шаблон из списка, который хотите удалить.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Удалить шаблон «{preset.Title}» из библиотеки?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                TemplatePresetService.DeletePreset(preset.Title);
                RefreshTemplatesList();
                MessageBox.Show("Шаблон удален.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Toolbar: Add Field Buttons & Template Loading

        private void BtnLoadTemplateImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Все поддерживаемые бланки (*.png;*.jpg;*.jpeg;*.bmp;*.pdf)|*.png;*.jpg;*.jpeg;*.bmp;*.pdf|PDF документы (*.pdf)|*.pdf|Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Все файлы (*.*)|*.*",
                Title = "Выберите бланк диплома (JPG, PNG или PDF)"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadTemplateFromFile(dlg.FileName);
            }
        }

        public void LoadTemplateFromFile(string filePath)
        {
            try
            {
                BitmapSource bitmap = null;
                string ext = System.IO.Path.GetExtension(filePath).ToLower();

                if (ext == ".pdf")
                {
                    bitmap = PdfRenderer.RenderPdfToBitmap(filePath, 0, 300.0);
                }
                else
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.UriSource = new Uri(filePath);
                    img.EndInit();
                    img.Freeze();
                    bitmap = img;
                }

                if (bitmap == null)
                    throw new Exception("Не удалось загрузить или декодировать бланк диплома.");

                _templateImage = bitmap;
                _templateImagePath = filePath;
                TemplateImageDisplay.Source = _templateImage;

                // Adjust canvas aspect ratio to image
                double aspect = (double)bitmap.PixelHeight / bitmap.PixelWidth;
                DesignerCanvas.Height = DesignerCanvas.Width * aspect;
                UpdatePermanentGuides();

                RefreshAllFieldVisuals();
                Logger.Log($"Загружен бланк диплома: {filePath} ({bitmap.PixelWidth}x{bitmap.PixelHeight}, формат: {ext.ToUpper()})");
                ScreenshotService.CaptureWindow(this, "template_loaded");
                MessageBox.Show("Бланк диплома успешно загружен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка загрузки изображения: " + filePath, ex);
                MessageBox.Show("Ошибка загрузки изображения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddNewField(string name, string sample, double y, double fontSize, bool isBold, bool isItalic, TextAlignment align, string colorHex)
        {
            var field = new FieldDefinition
            {
                Name = name,
                SampleText = sample,
                X = (DesignerCanvas.Width - 500) / 2.0,
                Y = y,
                Width = 500,
                Height = 50,
                FontFamily = "Georgia",
                FontSize = fontSize,
                IsBold = isBold,
                IsItalic = isItalic,
                Alignment = align,
                TextColorHex = colorHex,
                AutoShrink = true
            };

            _fields.Add(field);
            SyncTableColumns();
            SelectField(field);

            Logger.Log($"Добавлено поле: '{name}' на ({field.X:0}, {field.Y:0}), размер шрифта: {fontSize}");
            ScreenshotService.CaptureWindow(this, $"field_added_{name}");
        }

        private void BtnAddField_Fio_Click(object sender, RoutedEventArgs e)
        {
            AddNewField("ФИО", "Иванов Иван Иванович", 280, 32, true, false, TextAlignment.Center, "#182B49");
        }

        private void BtnAddField_Nomination_Click(object sender, RoutedEventArgs e)
        {
            AddNewField("Номинация", "За выдающиеся успехи в исследовании края", 360, 20, false, true, TextAlignment.Center, "#CDA04B");
        }

        private void BtnAddField_Place_Click(object sender, RoutedEventArgs e)
        {
            AddNewField("Место", "I МЕСТО", 430, 26, true, false, TextAlignment.Center, "#182B49");
        }

        private void BtnAddField_Date_Click(object sender, RoutedEventArgs e)
        {
            AddNewField("Дата", DateTime.Now.ToString("dd MMMM yyyy г.", new System.Globalization.CultureInfo("ru-RU")), 960, 16, false, false, TextAlignment.Left, "#475569");
        }

        private void BtnAddField_Custom_Click(object sender, RoutedEventArgs e)
        {
            int index = _fields.Count + 1;
            AddNewField("Поле_" + index, "Текст поля " + index, 500, 22, false, false, TextAlignment.Center, "#1A202C");
        }

        #endregion

        #region Canvas Zoom

        private void BtnZoom50_Click(object sender, RoutedEventArgs e) { SetZoom(0.5); }
        private void BtnZoom75_Click(object sender, RoutedEventArgs e) { SetZoom(0.75); }
        private void BtnZoom100_Click(object sender, RoutedEventArgs e) { SetZoom(1.0); }
        private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
        {
            double availableH = CanvasScrollViewer.ActualHeight - 40;
            if (availableH > 200 && DesignerCanvas.Height > 0)
            {
                SetZoom(availableH / DesignerCanvas.Height);
            }
        }

        private void SetZoom(double zoom)
        {
            _currentZoom = Math.Max(0.2, Math.Min(2.0, zoom));
            UpdateCanvasZoom();
        }

        private void UpdateCanvasZoom()
        {
            CanvasContainer.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
        }

        #endregion

        #region Alignment Guides & Photoshop Snapping

        private void BtnToggleGuides_Click(object sender, RoutedEventArgs e)
        {
            bool show = BtnToggleGuides.IsChecked == true;
            PermanentCenterGuideV.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            PermanentCenterGuideH.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            UpdatePermanentGuides();
            Logger.Log($"Отображение направляющих центра: {(show ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
        }

        private void BtnToggleCustomGuides_Click(object sender, RoutedEventArgs e)
        {
            _showCustomGuides = BtnToggleCustomGuides.IsChecked == true;
            CustomGuidesCanvas.Visibility = _showCustomGuides ? Visibility.Visible : Visibility.Collapsed;
            CustomGuidesSettingsCard.Visibility = _showCustomGuides ? Visibility.Visible : Visibility.Collapsed;
            UpdateCustomGuidesVisuals();
            Logger.Log($"Отображение 4 направляющих: {(_showCustomGuides ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО")}");
        }

        private void SetDefaultCustomGuides()
        {
            double w = DesignerCanvas.Width > 0 ? DesignerCanvas.Width : 800;
            double h = DesignerCanvas.Height > 0 ? DesignerCanvas.Height : 1131;
            _customGuideV1 = Math.Round(w * 0.10);
            _customGuideV2 = Math.Round(w * 0.90);
            _customGuideH1 = Math.Round(h * 0.10);
            _customGuideH2 = Math.Round(h * 0.90);
            UpdateCustomGuidesVisuals();
        }

        private void UpdateCustomGuidesVisuals()
        {
            if (CustomGuidesCanvas == null || GripGuideV1 == null) return;

            double canvasW = DesignerCanvas.Width;
            double canvasH = DesignerCanvas.Height;

            _customGuideV1 = Math.Max(0, Math.Min(canvasW, _customGuideV1));
            _customGuideV2 = Math.Max(0, Math.Min(canvasW, _customGuideV2));
            _customGuideH1 = Math.Max(0, Math.Min(canvasH, _customGuideH1));
            _customGuideH2 = Math.Max(0, Math.Min(canvasH, _customGuideH2));

            Canvas.SetLeft(GripGuideV1, _customGuideV1 - 7);
            Canvas.SetTop(GripGuideV1, 0);

            Canvas.SetLeft(GripGuideV2, _customGuideV2 - 7);
            Canvas.SetTop(GripGuideV2, 0);

            Canvas.SetLeft(GripGuideH1, 0);
            Canvas.SetTop(GripGuideH1, _customGuideH1 - 7);

            Canvas.SetLeft(GripGuideH2, 0);
            Canvas.SetTop(GripGuideH2, _customGuideH2 - 7);

            if (TxtGuideV1 != null) TxtGuideV1.Text = _customGuideV1.ToString("0");
            if (TxtGuideV2 != null) TxtGuideV2.Text = _customGuideV2.ToString("0");
            if (TxtGuideH1 != null) TxtGuideH1.Text = _customGuideH1.ToString("0");
            if (TxtGuideH2 != null) TxtGuideH2.Text = _customGuideH2.ToString("0");
        }

        private void Guide_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var grip = sender as FrameworkElement;
            if (grip?.Tag == null) return;

            int tag = Convert.ToInt32(grip.Tag);
            _draggingGuideIndex = tag;
            _guideDragStart = e.GetPosition(DesignerCanvas);

            switch (tag)
            {
                case 1: _guideOrigPos = _customGuideV1; break;
                case 2: _guideOrigPos = _customGuideV2; break;
                case 3: _guideOrigPos = _customGuideH1; break;
                case 4: _guideOrigPos = _customGuideH2; break;
            }

            grip.CaptureMouse();
            e.Handled = true;

            GuideDragBadge.Visibility = Visibility.Visible;
            UpdateGuideDragBadge(tag);
        }

        private void Guide_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingGuideIndex == 0) return;
            var grip = sender as FrameworkElement;
            if (grip == null) return;

            Point cur = e.GetPosition(DesignerCanvas);
            double dx = cur.X - _guideDragStart.X;
            double dy = cur.Y - _guideDragStart.Y;

            double canvasW = DesignerCanvas.Width;
            double canvasH = DesignerCanvas.Height;

            switch (_draggingGuideIndex)
            {
                case 1:
                    _customGuideV1 = Math.Max(0, Math.Min(canvasW, Math.Round(_guideOrigPos + dx)));
                    break;
                case 2:
                    _customGuideV2 = Math.Max(0, Math.Min(canvasW, Math.Round(_guideOrigPos + dx)));
                    break;
                case 3:
                    _customGuideH1 = Math.Max(0, Math.Min(canvasH, Math.Round(_guideOrigPos + dy)));
                    break;
                case 4:
                    _customGuideH2 = Math.Max(0, Math.Min(canvasH, Math.Round(_guideOrigPos + dy)));
                    break;
            }

            UpdateCustomGuidesVisuals();
            UpdateGuideDragBadge(_draggingGuideIndex);
            e.Handled = true;
        }

        private void Guide_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingGuideIndex != 0)
            {
                var grip = sender as FrameworkElement;
                grip?.ReleaseMouseCapture();
                _draggingGuideIndex = 0;
                GuideDragBadge.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void UpdateGuideDragBadge(int tag)
        {
            if (GuideDragBadge == null || GuideDragBadgeText == null) return;
            double canvasW = DesignerCanvas.Width;
            double canvasH = DesignerCanvas.Height;

            switch (tag)
            {
                case 1:
                    GuideDragBadgeText.Text = $"Левая: {_customGuideV1:0} px";
                    Canvas.SetLeft(GuideDragBadge, Math.Min(canvasW - 100, Math.Max(10, _customGuideV1 + 10)));
                    Canvas.SetTop(GuideDragBadge, 14);
                    break;
                case 2:
                    GuideDragBadgeText.Text = $"Правая: {_customGuideV2:0} px";
                    Canvas.SetLeft(GuideDragBadge, Math.Min(canvasW - 100, Math.Max(10, _customGuideV2 + 10)));
                    Canvas.SetTop(GuideDragBadge, 14);
                    break;
                case 3:
                    GuideDragBadgeText.Text = $"Верхняя: {_customGuideH1:0} px";
                    Canvas.SetLeft(GuideDragBadge, 14);
                    Canvas.SetTop(GuideDragBadge, Math.Min(canvasH - 30, Math.Max(10, _customGuideH1 + 10)));
                    break;
                case 4:
                    GuideDragBadgeText.Text = $"Нижняя: {_customGuideH2:0} px";
                    Canvas.SetLeft(GuideDragBadge, 14);
                    Canvas.SetTop(GuideDragBadge, Math.Min(canvasH - 30, Math.Max(10, _customGuideH2 + 10)));
                    break;
            }
        }

        private void BtnResetGuides_Click(object sender, RoutedEventArgs e)
        {
            SetDefaultCustomGuides();
        }

        private void TxtGuide_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGuideInputs();
        }

        private void TxtGuide_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyGuideInputs();
                e.Handled = true;
            }
        }

        private void ApplyGuideInputs()
        {
            double v1, v2, h1, h2;
            if (double.TryParse(TxtGuideV1.Text, out v1)) _customGuideV1 = v1;
            if (double.TryParse(TxtGuideV2.Text, out v2)) _customGuideV2 = v2;
            if (double.TryParse(TxtGuideH1.Text, out h1)) _customGuideH1 = h1;
            if (double.TryParse(TxtGuideH2.Text, out h2)) _customGuideH2 = h2;
            UpdateCustomGuidesVisuals();
        }

        private void UpdatePermanentGuides()
        {
            if (PermanentCenterGuideV == null || PermanentCenterGuideH == null) return;
            double w = DesignerCanvas.Width;
            double h = DesignerCanvas.Height;
            double cx = w / 2.0;
            double cy = h / 2.0;

            PermanentCenterGuideV.X1 = cx;
            PermanentCenterGuideV.X2 = cx;
            PermanentCenterGuideV.Y1 = 0;
            PermanentCenterGuideV.Y2 = h;

            PermanentCenterGuideH.X1 = 0;
            PermanentCenterGuideH.X2 = w;
            PermanentCenterGuideH.Y1 = cy;
            PermanentCenterGuideH.Y2 = cy;
        }

        private void ApplySmartSnapping(FieldDefinition field, ref double newX, ref double newY)
        {
            // If Alt key is held down, bypass snapping (Photoshop convention)
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                HideSmartGuides();
                return;
            }

            double snapDist = 7.0;
            double canvasW = DesignerCanvas.Width;
            double canvasH = DesignerCanvas.Height;

            double centerX = canvasW / 2.0;
            double centerY = canvasH / 2.0;

            double fieldCenterX = newX + field.Width / 2.0;
            double fieldCenterY = newY + field.Height / 2.0;

            bool snappedV = false;
            bool snappedH = false;

            // 0. Custom Guides Snap (if active)
            if (_showCustomGuides)
            {
                // Check V1
                if (Math.Abs(newX - _customGuideV1) <= snapDist)
                {
                    newX = _customGuideV1;
                    ShowVerticalGuide(_customGuideV1, "📐 Направляющая (левая)");
                    snappedV = true;
                }
                else if (Math.Abs((newX + field.Width) - _customGuideV1) <= snapDist)
                {
                    newX = _customGuideV1 - field.Width;
                    ShowVerticalGuide(_customGuideV1, "📐 Направляющая (левая)");
                    snappedV = true;
                }
                else if (Math.Abs(fieldCenterX - _customGuideV1) <= snapDist)
                {
                    newX = _customGuideV1 - field.Width / 2.0;
                    ShowVerticalGuide(_customGuideV1, "📐 Направляющая (левая)");
                    snappedV = true;
                }

                // Check V2
                if (!snappedV)
                {
                    if (Math.Abs((newX + field.Width) - _customGuideV2) <= snapDist)
                    {
                        newX = _customGuideV2 - field.Width;
                        ShowVerticalGuide(_customGuideV2, "📐 Направляющая (правая)");
                        snappedV = true;
                    }
                    else if (Math.Abs(newX - _customGuideV2) <= snapDist)
                    {
                        newX = _customGuideV2;
                        ShowVerticalGuide(_customGuideV2, "📐 Направляющая (правая)");
                        snappedV = true;
                    }
                    else if (Math.Abs(fieldCenterX - _customGuideV2) <= snapDist)
                    {
                        newX = _customGuideV2 - field.Width / 2.0;
                        ShowVerticalGuide(_customGuideV2, "📐 Направляющая (правая)");
                        snappedV = true;
                    }
                }

                // Check H1
                if (Math.Abs(newY - _customGuideH1) <= snapDist)
                {
                    newY = _customGuideH1;
                    ShowHorizontalGuide(_customGuideH1, "📐 Направляющая (верх)");
                    snappedH = true;
                }
                else if (Math.Abs((newY + field.Height) - _customGuideH1) <= snapDist)
                {
                    newY = _customGuideH1 - field.Height;
                    ShowHorizontalGuide(_customGuideH1, "📐 Направляющая (верх)");
                    snappedH = true;
                }

                // Check H2
                if (!snappedH)
                {
                    if (Math.Abs((newY + field.Height) - _customGuideH2) <= snapDist)
                    {
                        newY = _customGuideH2 - field.Height;
                        ShowHorizontalGuide(_customGuideH2, "📐 Направляющая (низ)");
                        snappedH = true;
                    }
                    else if (Math.Abs(newY - _customGuideH2) <= snapDist)
                    {
                        newY = _customGuideH2;
                        ShowHorizontalGuide(_customGuideH2, "📐 Направляющая (низ)");
                        snappedH = true;
                    }
                }
            }

            // 1. Vertical Snap: Canvas Horizontal Center
            if (!snappedV && Math.Abs(fieldCenterX - centerX) <= snapDist)
            {
                newX = centerX - field.Width / 2.0;
                ShowVerticalGuide(centerX, "📐 Центр листа");
                snappedV = true;
            }
            else
            {
                // Align with other fields
                foreach (var of in _fields)
                {
                    if (of == field) continue;

                    double ofCenterX = of.X + of.Width / 2.0;
                    if (Math.Abs(fieldCenterX - ofCenterX) <= snapDist)
                    {
                        newX = ofCenterX - field.Width / 2.0;
                        ShowVerticalGuide(ofCenterX, $"По центру [{of.Name}]");
                        snappedV = true;
                        break;
                    }

                    if (Math.Abs(newX - of.X) <= snapDist)
                    {
                        newX = of.X;
                        ShowVerticalGuide(of.X, $"По левому краю [{of.Name}]");
                        snappedV = true;
                        break;
                    }

                    if (Math.Abs((newX + field.Width) - (of.X + of.Width)) <= snapDist)
                    {
                        newX = of.X + of.Width - field.Width;
                        ShowVerticalGuide(of.X + of.Width, $"По правому краю [{of.Name}]");
                        snappedV = true;
                        break;
                    }
                }
            }

            if (!snappedV)
            {
                SnapGuideLineV.Visibility = Visibility.Collapsed;
                SnapGuideBadgeV.Visibility = Visibility.Collapsed;
            }

            // 2. Horizontal Snap: Canvas Vertical Center
            if (!snappedH && Math.Abs(fieldCenterY - centerY) <= snapDist)
            {
                newY = centerY - field.Height / 2.0;
                ShowHorizontalGuide(centerY, "📐 Центр листа");
                snappedH = true;
            }
            else
            {
                // Align with other fields Y
                foreach (var of in _fields)
                {
                    if (of == field) continue;

                    double ofCenterY = of.Y + of.Height / 2.0;
                    if (Math.Abs(fieldCenterY - ofCenterY) <= snapDist)
                    {
                        newY = ofCenterY - field.Height / 2.0;
                        ShowHorizontalGuide(ofCenterY, $"По высоте [{of.Name}]");
                        snappedH = true;
                        break;
                    }
                }
            }

            if (!snappedH)
            {
                SnapGuideLineH.Visibility = Visibility.Collapsed;
                SnapGuideBadgeH.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowVerticalGuide(double x, string label)
        {
            double canvasH = DesignerCanvas.Height;
            SnapGuideLineV.X1 = x;
            SnapGuideLineV.X2 = x;
            SnapGuideLineV.Y1 = 0;
            SnapGuideLineV.Y2 = canvasH;
            SnapGuideLineV.Visibility = Visibility.Visible;

            SnapGuideBadgeVText.Text = label;
            Canvas.SetLeft(SnapGuideBadgeV, Math.Min(DesignerCanvas.Width - 110, Math.Max(10, x + 6)));
            Canvas.SetTop(SnapGuideBadgeV, 14);
            SnapGuideBadgeV.Visibility = Visibility.Visible;
        }

        private void ShowHorizontalGuide(double y, string label)
        {
            double canvasW = DesignerCanvas.Width;
            SnapGuideLineH.X1 = 0;
            SnapGuideLineH.X2 = canvasW;
            SnapGuideLineH.Y1 = y;
            SnapGuideLineH.Y2 = y;
            SnapGuideLineH.Visibility = Visibility.Visible;

            SnapGuideBadgeHText.Text = label;
            Canvas.SetLeft(SnapGuideBadgeH, 14);
            Canvas.SetTop(SnapGuideBadgeH, Math.Min(DesignerCanvas.Height - 30, Math.Max(10, y + 6)));
            SnapGuideBadgeH.Visibility = Visibility.Visible;
        }

        private void HideSmartGuides()
        {
            if (SnapGuideLineV != null) SnapGuideLineV.Visibility = Visibility.Collapsed;
            if (SnapGuideBadgeV != null) SnapGuideBadgeV.Visibility = Visibility.Collapsed;
            if (SnapGuideLineH != null) SnapGuideLineH.Visibility = Visibility.Collapsed;
            if (SnapGuideBadgeH != null) SnapGuideBadgeH.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Table Operations (Tab 2)

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            var row = _participantsTable.NewRow();
            foreach (var field in _fields)
            {
                row[field.Name] = field.SampleText;
            }
            _participantsTable.Rows.Add(row);
            UpdateParticipantsCount();
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (ParticipantsGrid.SelectedItem is DataRowView rowView)
            {
                rowView.Row.Delete();
                _participantsTable.AcceptChanges();
                UpdateParticipantsCount();
            }
        }

        private void BtnClearTable_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Очистить всех участников из таблицы?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _participantsTable.Rows.Clear();
                UpdateParticipantsCount();
            }
        }

        private void BtnPasteFromExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var imported = CsvImportExportService.ImportFromClipboard();
                if (imported == null || imported.Count == 0)
                {
                    MessageBox.Show("Буфер обмена пуст или не содержит табличных данных.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ApplyImportedRows(imported);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка вставки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Таблицы (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите файл со списком участников"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadParticipantsFromCsvFile(dlg.FileName);
            }
        }

        public void LoadParticipantsFromCsvFile(string filePath)
        {
            try
            {
                var imported = CsvImportExportService.ImportFromFile(filePath);
                if (imported != null && imported.Count > 0)
                {
                    ApplyImportedRows(imported);
                }
                else
                {
                    MessageBox.Show("Не удалось прочитать строки из файла.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка импорта: " + filePath, ex);
                MessageBox.Show("Ошибка импорта: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyImportedRows(List<Dictionary<string, string>> imported)
        {
            int addedCount = 0;
            foreach (var dict in imported)
            {
                var row = _participantsTable.NewRow();
                foreach (var field in _fields)
                {
                    // Find case-insensitive match
                    string val = "";
                    foreach (var kvp in dict)
                    {
                        if (string.Equals(kvp.Key.Trim(), field.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            val = kvp.Value;
                            break;
                        }
                    }
                    row[field.Name] = val;
                }
                _participantsTable.Rows.Add(row);
                addedCount++;
            }

            UpdateParticipantsCount();
            Logger.Log($"Импортировано участников в таблицу: {addedCount} чел.");
            ScreenshotService.CaptureWindow(this, "participants_imported");
            MessageBox.Show(string.Format("Успешно добавлено {0} участников!", addedCount), "Импорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV файл (*.csv)|*.csv",
                FileName = "Участники.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    CsvImportExportService.ExportToCsv(dlg.FileName, _participantsTable);
                    MessageBox.Show("Список сохранен в CSV!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка экспорта: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Preview & Generation (Tab 3)

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == MainTabs)
            {
                Logger.Log($"Переключение вкладки: индекс {MainTabs.SelectedIndex} ('{((TabItem)MainTabs.SelectedItem)?.Header}')");
                if (MainTabs.SelectedIndex == 2) // Tab 3: Generation & Preview
                {
                    UpdateLivePreview();
                }
                ScreenshotService.CaptureWindow(this, $"tab_switch_{MainTabs.SelectedIndex}");
            }
        }

        private void UpdateLivePreview()
        {
            if (_templateImage == null) return;

            int rowCount = _participantsTable.Rows.Count;
            if (rowCount == 0)
            {
                PreviewParticipantInfo.Text = "Нет участников в таблице (показан образец)";
                var previewBmp = RenderService.RenderDiploma(_templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, null, isPreview: true);
                LivePreviewImage.Source = previewBmp;
                return;
            }

            if (_previewParticipantIndex < 0) _previewParticipantIndex = 0;
            if (_previewParticipantIndex >= rowCount) _previewParticipantIndex = rowCount - 1;

            var row = _participantsTable.Rows[_previewParticipantIndex];
            var dict = new Dictionary<string, string>();
            foreach (DataColumn col in _participantsTable.Columns)
            {
                dict[col.ColumnName] = row[col]?.ToString() ?? "";
            }

            string fio = dict.ContainsKey("ФИО") ? dict["ФИО"] : "";
            PreviewParticipantInfo.Text = string.Format("Участник {0} из {1}: {2}", _previewParticipantIndex + 1, rowCount, fio);

            var bmp = RenderService.RenderDiploma(_templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, dict, isPreview: false);
            LivePreviewImage.Source = bmp;
        }

        private void BtnPrevParticipant_Click(object sender, RoutedEventArgs e)
        {
            if (_previewParticipantIndex > 0)
            {
                _previewParticipantIndex--;
                UpdateLivePreview();
            }
        }

        private void BtnNextParticipant_Click(object sender, RoutedEventArgs e)
        {
            if (_previewParticipantIndex < _participantsTable.Rows.Count - 1)
            {
                _previewParticipantIndex++;
                UpdateLivePreview();
            }
        }

        private void BtnBrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            // System.Windows.Forms.FolderBrowserDialog alternative via OpenFileDialog folder trick or standard input
            var dlg = new SaveFileDialog
            {
                Title = "Выберите папку для сохранения (укажите любое имя внутри нужной папки)",
                FileName = "сохранить_сюда",
                Filter = "Папка|*.folder"
            };

            if (dlg.ShowDialog() == true)
            {
                string dir = System.IO.Path.GetDirectoryName(dlg.FileName);
                OutputFolderTextBox.Text = dir;
            }
        }

        private void BtnGenerateAll_Click(object sender, RoutedEventArgs e)
        {
            if (_templateImage == null)
            {
                MessageBox.Show("Сначала загрузите или создайте шаблон диплома!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_participantsTable.Rows.Count == 0)
            {
                MessageBox.Show("Список участников пуст. Добавьте участников на вкладке «2. Список участников».", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outFolder = OutputFolderTextBox.Text.Trim();
            if (string.IsNullOrEmpty(outFolder))
            {
                MessageBox.Show("Укажите папку для сохранения дипломов.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!Directory.Exists(outFolder))
                {
                    Directory.CreateDirectory(outFolder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать папку: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Start generation
            BtnGenerateAll.IsEnabled = false;
            GenerationProgressBar.Visibility = Visibility.Visible;
            GenerationProgressBar.Value = 0;
            BtnOpenOutputFolder.Visibility = Visibility.Collapsed;
            GenerationStatusText.Text = "Генерация дипломов...";

            int total = _participantsTable.Rows.Count;
            string pattern = FileNamePatternTextBox.Text.Trim();
            if (string.IsNullOrEmpty(pattern)) pattern = "Диплом_{ФИО}";

            try
            {
                if (RadioDirectPrint.IsChecked == true)
                {
                    GenerateDirectPrint(total);
                }
                else if (RadioMultiPagePdf.IsChecked == true)
                {
                    GenerateMultiPagePdf(outFolder, total);
                }
                else if (RadioSinglePdfs.IsChecked == true)
                {
                    GenerateSinglePdfs(outFolder, pattern, total);
                }
                else // RadioImagesPng
                {
                    GenerateImages(outFolder, pattern, total);
                }

                GenerationProgressBar.Value = 100;
                GenerationStatusText.Text = string.Format("✅ Успешно сгенерировано {0} дипломов!", total);
                BtnOpenOutputFolder.Visibility = Visibility.Visible;

                Logger.Log($"Пакетная генерация успешно завершена! Создано {total} дипломов в {outFolder}");
                ScreenshotService.CaptureWindow(this, "generation_finished");
            }
            catch (Exception ex)
            {
                GenerationStatusText.Text = "❌ Ошибка: " + ex.Message;
                Logger.LogError("Ошибка в процессе генерации дипломов", ex);
                ScreenshotService.CaptureWindow(this, "generation_error");
                MessageBox.Show("Ошибка генерации: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnGenerateAll.IsEnabled = true;
            }
        }

        private void GenerateMultiPagePdf(string outFolder, int total)
        {
            var pages = new List<PdfWriter.PageImageInfo>();

            for (int i = 0; i < total; i++)
            {
                var dict = GetParticipantDict(i);
                var bmp = RenderService.RenderDiploma(_templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, dict);
                byte[] jpeg = RenderService.EncodeToJpeg(bmp, 95);

                pages.Add(new PdfWriter.PageImageInfo
                {
                    JpegBytes = jpeg,
                    PixelWidth = bmp.PixelWidth,
                    PixelHeight = bmp.PixelHeight
                });

                GenerationProgressBar.Value = (double)(i + 1) / total * 100.0;
            }

            byte[] pdfBytes = PdfWriter.CreateMultiPagePdf(pages);
            string filePath = System.IO.Path.Combine(outFolder, "Все_Дипломы.pdf");
            File.WriteAllBytes(filePath, pdfBytes);
        }

        private void GenerateSinglePdfs(string outFolder, string pattern, int total)
        {
            for (int i = 0; i < total; i++)
            {
                var dict = GetParticipantDict(i);
                var bmp = RenderService.RenderDiploma(_templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, dict);
                byte[] jpeg = RenderService.EncodeToJpeg(bmp, 95);

                byte[] pdfBytes = PdfWriter.CreateSinglePagePdf(jpeg, bmp.PixelWidth, bmp.PixelHeight);

                string fileName = FormatFileName(pattern, dict, i + 1) + ".pdf";
                string fullPath = System.IO.Path.Combine(outFolder, fileName);
                File.WriteAllBytes(fullPath, pdfBytes);

                GenerationProgressBar.Value = (double)(i + 1) / total * 100.0;
            }
        }

        private void GenerateImages(string outFolder, string pattern, int total)
        {
            for (int i = 0; i < total; i++)
            {
                var dict = GetParticipantDict(i);
                var bmp = RenderService.RenderDiploma(_templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, dict);

                string fileName = FormatFileName(pattern, dict, i + 1) + ".png";
                string fullPath = System.IO.Path.Combine(outFolder, fileName);
                RenderService.SaveToFile(bmp, fullPath, asJpeg: false);

                GenerationProgressBar.Value = (double)(i + 1) / total * 100.0;
            }
        }

        private void GenerateDirectPrint(int total)
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                for (int i = 0; i < total; i++)
                {
                    var dict = GetParticipantDict(i);
                    var bmp = RenderService.RenderDiploma(_templateImage, DesignerCanvas.Width, DesignerCanvas.Height, _fields, dict);

                    var img = new Image
                    {
                        Source = bmp,
                        Width = printDlg.PrintableAreaWidth,
                        Height = printDlg.PrintableAreaHeight,
                        Stretch = Stretch.Uniform
                    };

                    img.Measure(new Size(printDlg.PrintableAreaWidth, printDlg.PrintableAreaHeight));
                    img.Arrange(new Rect(new Point(0, 0), img.DesiredSize));

                    printDlg.PrintVisual(img, string.Format("Диплом {0}", i + 1));
                    GenerationProgressBar.Value = (double)(i + 1) / total * 100.0;
                }
            }
        }

        private Dictionary<string, string> GetParticipantDict(int rowIndex)
        {
            var row = _participantsTable.Rows[rowIndex];
            var dict = new Dictionary<string, string>();
            foreach (DataColumn col in _participantsTable.Columns)
            {
                dict[col.ColumnName] = row[col]?.ToString() ?? "";
            }
            return dict;
        }

        private string FormatFileName(string pattern, Dictionary<string, string> dict, int index)
        {
            string res = pattern;
            res = res.Replace("{Номер}", index.ToString());
            foreach (var kvp in dict)
            {
                res = res.Replace("{" + kvp.Key + "}", kvp.Value);
            }

            // Remove invalid file path characters
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                res = res.Replace(c, '_');
            }

            return res;
        }

        private void BtnOpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            string outFolder = OutputFolderTextBox.Text.Trim();
            if (Directory.Exists(outFolder))
            {
                Process.Start("explorer.exe", outFolder);
            }
        }

        #endregion

        #region Project Save & Load

        private void BtnNewProject_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Создать новый проект? Несохраненные данные будут сброшены.", "Новый проект", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                InitDefaultProject();
                _participantsTable.Rows.Clear();
                InitParticipantsTable();
                RefreshAllFieldVisuals();
                SelectField(_fields.FirstOrDefault());
            }
        }

        private void BtnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Проект дипломов (*.dpg)|*.dpg",
                FileName = "Шаблон_Диплома.dpg"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var proj = new TemplateProject
                    {
                        TemplateImagePath = _templateImagePath,
                        CanvasWidth = DesignerCanvas.Width,
                        CanvasHeight = DesignerCanvas.Height,
                        Fields = _fields.ToList(),
                        OutputFolder = OutputFolderTextBox.Text,
                        FileNamePattern = FileNamePatternTextBox.Text,
                        OutputFormat = RadioMultiPagePdf.IsChecked == true ? 0 :
                                       RadioSinglePdfs.IsChecked == true ? 1 :
                                       RadioImagesPng.IsChecked == true ? 2 : 3
                    };

                    // Serialize participants
                    proj.Participants = new List<Dictionary<string, string>>();
                    foreach (DataRow row in _participantsTable.Rows)
                    {
                        var dict = new Dictionary<string, string>();
                        foreach (DataColumn col in _participantsTable.Columns)
                        {
                            dict[col.ColumnName] = row[col]?.ToString() ?? "";
                        }
                        proj.Participants.Add(dict);
                    }

                    proj.CustomGuideV1 = _customGuideV1;
                    proj.CustomGuideV2 = _customGuideV2;
                    proj.CustomGuideH1 = _customGuideH1;
                    proj.CustomGuideH2 = _customGuideH2;
                    proj.ShowCustomGuides = _showCustomGuides;

                    ProjectFileService.SaveProject(dlg.FileName, proj);
                    MessageBox.Show("Проект успешно сохранен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка сохранения проекта: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnOpenProject_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Проект дипломов (*.dpg)|*.dpg",
                Title = "Открыть проект"
            };

            if (dlg.ShowDialog() == true)
            {
                LoadProjectFile(dlg.FileName);
            }
        }

        public void LoadProjectFile(string filePath)
        {
            try
            {
                var proj = ProjectFileService.LoadProject(filePath);
                if (proj == null) return;

                // Load Image or PDF
                if (!string.IsNullOrEmpty(proj.TemplateImagePath) && File.Exists(proj.TemplateImagePath))
                {
                    string ext = System.IO.Path.GetExtension(proj.TemplateImagePath).ToLower();
                    BitmapSource bitmap = null;
                    if (ext == ".pdf")
                    {
                        bitmap = PdfRenderer.RenderPdfToBitmap(proj.TemplateImagePath, 0, 300.0);
                    }
                    else
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.UriSource = new Uri(proj.TemplateImagePath);
                        img.EndInit();
                        img.Freeze();
                        bitmap = img;
                    }

                    _templateImage = bitmap;
                    _templateImagePath = proj.TemplateImagePath;
                    TemplateImageDisplay.Source = _templateImage;

                    double aspect = (double)bitmap.PixelHeight / bitmap.PixelWidth;
                    DesignerCanvas.Height = DesignerCanvas.Width * aspect;
                    UpdatePermanentGuides();
                }

                // Load Fields
                _fields.Clear();
                foreach (var f in proj.Fields)
                {
                    _fields.Add(f);
                }

                // Load Participants
                _participantsTable.Clear();
                _participantsTable.Columns.Clear();
                SyncTableColumns();

                if (proj.Participants != null)
                {
                    foreach (var dict in proj.Participants)
                    {
                        var row = _participantsTable.NewRow();
                        foreach (var field in _fields)
                        {
                            if (dict.ContainsKey(field.Name))
                            {
                                row[field.Name] = dict[field.Name];
                            }
                        }
                        _participantsTable.Rows.Add(row);
                    }
                }

                UpdateParticipantsCount();

                // Settings
                if (!string.IsNullOrEmpty(proj.OutputFolder)) OutputFolderTextBox.Text = proj.OutputFolder;
                if (!string.IsNullOrEmpty(proj.FileNamePattern)) FileNamePatternTextBox.Text = proj.FileNamePattern;

                switch (proj.OutputFormat)
                {
                    case 0: RadioMultiPagePdf.IsChecked = true; break;
                    case 1: RadioSinglePdfs.IsChecked = true; break;
                    case 2: RadioImagesPng.IsChecked = true; break;
                    case 3: RadioDirectPrint.IsChecked = true; break;
                }

                // Restore custom guides
                if (proj.CustomGuideV1.HasValue) _customGuideV1 = proj.CustomGuideV1.Value;
                if (proj.CustomGuideV2.HasValue) _customGuideV2 = proj.CustomGuideV2.Value;
                if (proj.CustomGuideH1.HasValue) _customGuideH1 = proj.CustomGuideH1.Value;
                if (proj.CustomGuideH2.HasValue) _customGuideH2 = proj.CustomGuideH2.Value;
                _showCustomGuides = proj.ShowCustomGuides;
                BtnToggleCustomGuides.IsChecked = _showCustomGuides;
                CustomGuidesCanvas.Visibility = _showCustomGuides ? Visibility.Visible : Visibility.Collapsed;
                CustomGuidesSettingsCard.Visibility = _showCustomGuides ? Visibility.Visible : Visibility.Collapsed;
                UpdateCustomGuidesVisuals();

                RefreshAllFieldVisuals();
                SelectField(_fields.FirstOrDefault());
                Logger.Log($"Загружен проект: {filePath}");
                ScreenshotService.CaptureWindow(this, "project_loaded");
                MessageBox.Show("Проект успешно загружен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка открытия проекта: " + filePath, ex);
                MessageBox.Show("Ошибка открытия проекта: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
