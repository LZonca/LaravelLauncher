using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms;
using LaravelLauncher.Projects;
using System.Windows.Threading;



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
        private string serverPath = string.Empty;



        public MainWindow()
        {
            InitializeComponent();
            LoadRecentProjects();
            string path = Properties.Settings.Default.ServerPath;
            if (!string.IsNullOrEmpty(path))
            {
                serverPath = path;
                LocalServerPathLabel.Content = path;
            }
            else
            {
                LocalServerPathLabel.Content = "Aucun serveur local sélectionné";
            }

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Vérifie l'état toutes les 5 secondes
            };
            timer.Tick += (sender, e) => UpdateButtonState();
            timer.Start();
            UpdateButtonState();
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
            using (var dialog = new FolderBrowserDialog())
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
            try
            {
                // Récupère le chemin de l'exécutable sauvegardé dans les préférences utilisateur
                string executablePath = serverPath;

                // Vérifie si le chemin n'est pas vide
                if (!string.IsNullOrEmpty(executablePath))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(executablePath)
                    {
                        UseShellExecute = true, // Nécessaire pour lancer en tant qu'admin
                        Verb = "runas" // Indique de lancer le processus avec des privilèges d'administrateur
                    };

                    Process.Start(startInfo);
                }
                else
                {
                    System.Windows.MessageBox.Show("Le chemin de l'exécutable n'est pas défini.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                // Gestion des erreurs si l'exécutable ne peut pas être lancé
                System.Windows.MessageBox.Show($"Impossible de lancer l'exécutable : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunCommandInNewWindow(string command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
            Process.Start(startInfo);
        }

        private void StartProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Lancement du projet");

            startTasks = taskWorkCheckbox.IsChecked == true;
            startNpm = npmCheckbox.IsChecked == true;
            startYarn = yarnCheckbox.IsChecked == true;

            UpdateProjectSettings(projectPath, startNpm, startYarn, startTasks);

            RunCommandInNewWindow("cd " + projectPath + " && php artisan serve");

            if (taskWorkCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && php artisan schedule:work");
            }
            if (npmCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && npm run dev");
            }
            if (yarnCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && yarn run dev");
            }
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

        private bool IsProcessRunning(string processName)
        {
            // Obtient tous les processus en cours d'exécution avec le nom spécifié
            Process[] processes = Process.GetProcessesByName(processName);

            // Vérifie si au moins un processus avec le nom spécifié est en cours d'exécution
            return processes.Length > 0;
        }



        private void UpdateButtonState()
        {
            // Remplacez "nomDuProcessus" par le nom réel du processus que vous vérifiez
            string executableName = System.IO.Path.GetFileNameWithoutExtension(serverPath);
            bool processIsRunning = IsProcessRunning(executableName);

            if (processIsRunning)
            {
                // Si le processus est en cours d'exécution, désactivez le bouton et changez le texte
                StartLocalServerBtn.IsEnabled = false;
                StartLocalServerBtn.Content = "Serveur en cours d'exécution";
                StartLocalServerBtn.Background = Brushes.Green;
            }
            else
            {
                // Si le processus n'est pas en cours d'exécution, activez le bouton et changez le texte
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Serveur Hors ligne (Cliquez pour le lancer)";
                StartLocalServerBtn.Background = Brushes.Red;
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWindow = new Settings();
            settingsWindow.Show();
        }
    }
}