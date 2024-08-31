using System;
using System.Windows;
using Microsoft.Win32;
using System.Globalization;
using System.Resources;

namespace LaravelLauncher
{
    public partial class Settings : Window
    {
        private ResourceManager _resourceManager;

        public Settings()
        {
            InitializeComponent();
            _resourceManager = new ResourceManager("LaravelLauncher.Resources.Strings", typeof(Settings).Assembly);
            LoadServerExecutablePath();
            LoadAvailableLanguages();
        }

        private void LoadAvailableLanguages()
        {
            string languages = _resourceManager.GetString("AvailableLanguages") ?? throw new InvalidOperationException();
            if (!string.IsNullOrEmpty(languages))
            {
                var languageList = languages.Split(',');
                foreach (var lang in languageList)
                {
                    var culture = new CultureInfo(lang);
                    languageSelector.Items.Add(culture.Name);
                }
            }
        }

        private void languageChangeBtn_Click(object sender, RoutedEventArgs e)
        {
            string? selectedLanguage = languageSelector.SelectedItem?.ToString();
            if (selectedLanguage != null)
            {
                CultureInfo culture = new CultureInfo(selectedLanguage);
                Properties.Settings.Default.Language = culture.Name;
            }

            Properties.Settings.Default.Save();
            MessageBox.Show(_resourceManager.GetString("RestartApp"));
        }

        private void SaveExecutablePath(string path)
        {
            Properties.Settings.Default.ServerPath = path;
            Properties.Settings.Default.Save();
        }

        public void LoadServerExecutablePath()
        {
            string path = Properties.Settings.Default.ServerPath;
            if (!string.IsNullOrEmpty(path))
            {
                pathToExecLabel.Content = path;
            }
            else
            {
                Properties.Settings.Default.ServerPath = pathToExecLabel.Content.ToString();
                Properties.Settings.Default.Save();
            }
        }

        private void OpenFileSystemBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = _resourceManager.GetString("SelectExecutable")
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                SaveExecutablePath(selectedFilePath);
                pathToExecLabel.Content = selectedFilePath;
            }
        }
        
    }
}