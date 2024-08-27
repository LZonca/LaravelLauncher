using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;



namespace LaravelLauncher
{
    /// <summary>
    /// Logique d'interaction pour Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
            LoadServerExecutablePath();
        }
        private void SaveExecutablePath(string path)
        {
            Properties.Settings.Default.ServerPath = path;
            Properties.Settings.Default.Save(); // Sauvegarde les modifications
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
                Title = "Sélectionnez un fichier exécutable"
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                // Chemin d'accès au fichier sélectionné
                string selectedFilePath = openFileDialog.FileName;
                // Sauvegarde le chemin d'accès dans les préférences utilisateur
                SaveExecutablePath(selectedFilePath);
                pathToExecLabel.Content = selectedFilePath;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
