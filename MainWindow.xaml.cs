using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms;
using LaravelLauncher.Projects;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace LaravelLauncher
{
    public partial class MainWindow : Window
    {
        private NotifyIcon notifyIcon;
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
                Interval = TimeSpan.FromSeconds(5) // Check the state every 5 seconds
            };
            timer.Tick += (sender, e) => UpdateButtonState();
            timer.Start();
            UpdateButtonState();

            if (string.IsNullOrEmpty(projectPath))
            {
                StartProjectBtn.IsEnabled = false;
            }
            else
            {
                StartProjectBtn.IsEnabled = true;
            }

            // Load the icon from the embedded resources
            using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/images.ico")).Stream)
            {
                notifyIcon = new NotifyIcon
                {
                    Icon = new System.Drawing.Icon(iconStream), // Set the icon from the resource stream
                    Visible = false,
                    ContextMenuStrip = new ContextMenuStrip()
                };
            }

            var runningMenuItem = new ToolStripMenuItem("Running");
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop All Processes", null, (s, e) => StopAllProcesses()));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop serve", null, (s, e) => StopProcess("php")));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop npm", null, (s, e) => StopProcess("npm")));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop yarn", null, (s, e) => StopProcess("yarn")));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop Tasks", null, (s, e) => StopProcess("tasks")));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop Server", null, (s, e) => StopServer(System.IO.Path.GetFileNameWithoutExtension(serverPath))));

            var restartMenuItem = new ToolStripMenuItem("Restart");
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart All Processes", null, (s, e) => RestartAllProcesses()));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart serve", null, (s, e) => RestartProcess("php", "php artisan serve")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart npm", null, (s, e) => RestartProcess("npm", "npm run dev")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart yarn", null, (s, e) => RestartProcess("yarn", "yarn run dev")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart Tasks", null, (s, e) => RestartProcess("tasks", "php artisan schedule:work")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart Server", null, (s, e) => RestartServer()));

            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Restore", null, (s, e) => RestoreFromTray()));
            notifyIcon.ContextMenuStrip.Items.Add(runningMenuItem);
            notifyIcon.ContextMenuStrip.Items.Add(restartMenuItem);
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown()));
        }
        
        

        private void StopAllProcesses()
        {
            StopServer(System.IO.Path.GetFileNameWithoutExtension(serverPath));
            StopProcess("npm");
            StopProcess("yarn");
            StopProcess("php");
        }
        
        private void StopProcess(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
        
        /*static void RunCommand(string command)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd", "/c " + command);

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
        }*/

        private void LoadRecentProjects()
        {
            var settings = SettingsManager.LoadSettings();
            RecentProjectsList.Items.Clear();
            folderNameToPathMap.Clear();

            foreach (var folderPath in SettingsManager.GetProjectPaths())
            {
                string folderName = System.IO.Path.GetFileName(folderPath);
                RecentProjectsList.Items.Add(new ListBoxItem { Content = folderName });
                folderNameToPathMap[folderName] = folderPath;
            }
        }

        private void LoadProjectSettings(string projectPath)
        {
            var settings = SettingsManager.LoadSettings();
            var projectSettings = settings.Projects.FirstOrDefault(p => p.Path == projectPath);

            if (projectSettings != null)
            {
                npmCheckbox.IsChecked = projectSettings.UseNpm;
                yarnCheckbox.IsChecked = projectSettings.UseYarn;
                taskWorkCheckbox.IsChecked = projectSettings.startTasks;
                StartProjectBtn.IsEnabled = true;
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

                    var settings = SettingsManager.LoadSettings();
                    var projects = SettingsManager.GetProjectPaths();

                    if (!SettingsManager.GetProjectPaths().Contains(selectedPath))
                    {
                        projects.Add(selectedPath);
                        SettingsManager.SaveSettings(settings);
                        projectPath = dialog.SelectedPath;
                        NomProjetLabel.Content = System.IO.Path.GetFileName(projectPath);
                        CheminProjetLabel.Content = projectPath;
                        LoadRecentProjects();
                    }
                }
            }
        }

        private void StartLocalServer()
        {
            string executableName = System.IO.Path.GetFileNameWithoutExtension(serverPath);
            bool processIsRunning = IsProcessRunning(executableName);

            if (processIsRunning)
            {
                StopServer(executableName);
            }
            else
            {
                try
                {
                    string executablePath = serverPath;

                    if (!string.IsNullOrEmpty(executablePath))
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo(executablePath)
                        {
                            UseShellExecute = true,
                            Verb = "runas"
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
                    System.Windows.MessageBox.Show($"Impossible de lancer l'exécutable : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            UpdateButtonState();
        }

        private void RestartServer()
        {
            StopServer(System.IO.Path.GetFileNameWithoutExtension(serverPath));
            StartLocalServer();
        }
        
        private void StartAllProcesses()
        {
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
        private void RestartAllProcesses()
        {
            StopAllProcesses();
            StartAllProcesses();
        }
        private void RestartProcess(string processName, string command)
        {
            StopProcess(processName);
            RunCommandInNewWindow("cd " + projectPath + " && " + command);
        }
        
        
        private void StartLocalServerBtn_Click(object sender, RoutedEventArgs e)
        {
            StartLocalServer();
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

            MinimizeToTray();
        }

        private void MinimizeToTray()
        {
            notifyIcon.Visible = true;
            this.Hide();
        }

        private void RestoreFromTray()
        {
            notifyIcon.Visible = false;
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized)
            {
                MinimizeToTray();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            notifyIcon.Dispose();
            base.OnClosing(e);
        }

        private void RecentProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Content != null)
            {
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
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        private void UpdateButtonState()
        {
            if (string.IsNullOrEmpty(serverPath))
            {
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Serveur Hors ligne (Cliquez pour le lancer)";
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Red);
                return;
            }

            string executableName = System.IO.Path.GetFileNameWithoutExtension(serverPath);
            bool processIsRunning = IsProcessRunning(executableName);

            if (processIsRunning)
            {
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Arrêter le serveur local";
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Lancer le serveur local";
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Red);
            }
        }

        private void StopServer(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                process.Kill();
                process.WaitForExit();
            }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWindow = new Settings();
            settingsWindow.Show();
        }
    }
}