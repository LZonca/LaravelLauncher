using System;
using System.Windows;
using Microsoft.Win32;
using System.Globalization;
using System.Resources;
using System.Threading;

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
            SetLanguage(Properties.Settings.Default.Language ?? "en");
            LoadAvailableLanguages();
        }
        
        private void SetLanguage(string cultureCode)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
            _resourceManager = new ResourceManager("LaravelLauncher.Resources.Strings", typeof(MainWindow).Assembly);
            UpdateUi();
        }

    private void UpdateUi()
    {
        SettingsTitle.Content = _resourceManager.GetString("Settings");
        DevEnvManagerTitle.Text = _resourceManager.GetString("DevEnvManager");
        SelectFileBtn.Text = _resourceManager.GetString("SelectExecutable");
        LanguageSettingsLabel.Content = _resourceManager.GetString("LanguageSettings");

        if (Properties.Settings.Default.ServerPath == null)
        {
            PathToExecLabel.Text = _resourceManager.GetString("SelectServerPath");
        }
        else
        {
            PathToExecLabel.Text = _resourceManager.GetString("DevEnvManager");
        }

        LanguageChangeBtn.Content = _resourceManager.GetString("ChangeLanguage");
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
                languageSelector.SelectedItem = Properties.Settings.Default.Language;
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
            MessageBox.Show(
                _resourceManager.GetString("RestartApp"),
                "Confirm Exit",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            
            
            Application.Current.Shutdown();
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
                PathToExecLabel.Text = path;
            }
            else
            {
                Properties.Settings.Default.ServerPath = PathToExecLabel.Text;
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
                PathToExecLabel.Text = selectedFilePath;
            }
        }
        
    }
}