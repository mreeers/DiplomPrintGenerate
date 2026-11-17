using System.Windows;

namespace DiplomPrintGenerate.Views
{
    public partial class SaveTemplateDialog : Window
    {
        public string TemplateTitle { get; private set; }

        public SaveTemplateDialog(string defaultName = "Мой шаблон диплома")
        {
            InitializeComponent();
            TxtTemplateName.Text = defaultName;
            this.Loaded += (s, e) =>
            {
                TxtTemplateName.Focus();
                TxtTemplateName.SelectAll();
            };
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtTemplateName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название шаблона.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TemplateTitle = name;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
