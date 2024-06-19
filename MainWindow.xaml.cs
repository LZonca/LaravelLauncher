using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using LaravelLauncher.Projects;


namespace LaravelLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string projectPath = string.Empty;
        private bool startNpm;
        private bool startYarn;
        private bool startTasks;
        private Dictionary<string, string> folderNameToPathMap = new Dictionary<string, string>();




        public MainWindow()
        {
            InitializeComponent();
            LoadRecentProjects();
        }

        static void RunCommand(string command)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd", "/c " + command); //TODO: modifier le path

            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;

            using (Process process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.Start();

                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                Console.WriteLine(result);
            }
        }


        private void LoadRecentProjects()
        {
            var settings = SettingsManager.LoadSettings();
            RecentProjectsList.Items.Clear();
            folderNameToPathMap.Clear(); // Nettoyer le dictionnaire avant de le remplir à nouveau

            foreach (var folderPath in SettingsManager.GetProjectPaths())
            {
                string folderName = System.IO.Path.GetFileName(folderPath);
                RecentProjectsList.Items.Add(new ListBoxItem { Content = folderName });
                // Stocker la correspondance entre le nom du dossier et le chemin complet
                folderNameToPathMap[folderName] = folderPath;
                
            }
        }

        private void LoadProjectSettings(string projectPath)
        {
            var settings = SettingsManager.LoadSettings();
            var projectSettings = settings.Projects.FirstOrDefault(p => p.Path == projectPath);

            if (projectSettings != null)
            {
                // Mettre à jour les cases à cocher en fonction des paramètres du projet
                npmCheckbox.IsChecked = projectSettings.UseNpm;
                yarnCheckbox.IsChecked = projectSettings.UseYarn;
                taskWorkCheckbox.IsChecked = projectSettings.startTasks;

            }
        }

        private void UpdateProjectSettings(string projectPath, bool useNpm, bool useYarn, bool startTasks)
        {
            var settings = SettingsManager.LoadSettings();
            var projectSettings = settings.Projects.FirstOrDefault(p => p.Path == projectPath);

            if (projectSettings == null)
            {
                projectSettings = new ProjectSettings { Path = projectPath };
                settings.Projects.Add(projectSettings);
            }

            projectSettings.UseNpm = useNpm;
            projectSettings.UseYarn = useYarn;
            projectSettings.startTasks = startTasks;

            SettingsManager.SaveSettings(settings);
        }


        private void SelectProjectPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // Charger les paramètres existants
  
                    var settings = SettingsManager.LoadSettings();
                    var projects = SettingsManager.GetProjectPaths();

                    // Ajouter le dossier sélectionné à la liste, en évitant les doublons
                    if (!SettingsManager.GetProjectPaths().Contains(selectedPath))
                    {
                        projects.Add(selectedPath);
                        // Sauvegarder les nouveaux paramètres
                        SettingsManager.SaveSettings(settings);
                        projectPath = dialog.SelectedPath;
                        NomProjetLabel.Content = System.IO.Path.GetFileName(projectPath);
                        CheminProjetLabel.Content = projectPath;
                        LoadRecentProjects();
                    }
                }
            }
        }

        private void StartLocalServerBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StartProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            // Supposons que startNpm, startYarn, et startTasks sont définis ailleurs dans votre classe
            // et représentent les paramètres actuels pour le projet sélectionné.

            // Mettre à jour et sauvegarder les paramètres du projet sélectionné
            startTasks = taskWorkCheckbox.IsChecked == true;
            startNpm = npmCheckbox.IsChecked == true;
            startYarn = yarnCheckbox.IsChecked == true;

            UpdateProjectSettings(projectPath, startNpm, startYarn, startTasks); // Remplacez new List<string>() par vos tâches réelles

            // Lancer l'application
            // Ici, vous pouvez ajouter la logique pour démarrer le projet, par exemple en exécutant des commandes spécifiques
        }

        private void RecentProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Content != null)
            {
                // Supposons que le dictionnaire folderNameToPathMap contient la correspondance entre le nom du dossier et le chemin complet
                string folderName = selectedItem.Content.ToString();
                if (folderNameToPathMap.TryGetValue(folderName, out string fullPath))
                {
                    projectPath = fullPath;
                    NomProjetLabel.Content = System.IO.Path.GetFileName(projectPath);
                    CheminProjetLabel.Content = projectPath;
                    LoadProjectSettings(fullPath);
                }
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
