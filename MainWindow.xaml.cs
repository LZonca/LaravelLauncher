using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Forms;
using LaravelLauncher.Projects;
using System.Windows.Threading;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop; // Added namespace
using Application = System.Windows.Application;

namespace LaravelLauncher
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        private ResourceManager resourceManager;
        private IntPtr mainWindowHandle;
        private NotifyIcon notifyIcon;
        private string projectPath = string.Empty;
        private bool startNpm;
        private bool startYarn;
        private bool startTasks;
        private Dictionary<string, string> folderNameToPathMap = new Dictionary<string, string>();
        private string serverPath = string.Empty;
        private Dictionary<string, Process> processList = new Dictionary<string, Process>();

        #region Process Management

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        // ReSharper disable once InconsistentNaming
        const uint CTRL_C_EVENT = 0;
        
        private void StopAllProcesses()
        {
            Console.WriteLine("\nStopping all processes");

            foreach (var processEntry in processList.ToList()) // Create a copy of the list to avoid modification issues
            {
                StopProcess(processEntry.Key);
            }

            processList.Clear(); // Clear the list after stopping all processes
        }

        private void StopProcess(string processName)
        {
            if (processList.TryGetValue(processName, out Process process))
            {
                try
                {
                    Console.WriteLine($"Stopping process: {processName} with PID {process.Id}");

                    // Attach to the console of the target process
                    if (AttachConsole(process.Id))
                    {
                        // Send a CTRL_C_EVENT to the console
                        if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                        {
                            Console.WriteLine($"Failed to send CTRL+C to process {processName}. Error: {Marshal.GetLastWin32Error()}");
                        }

                        // Detach immediately after sending the signal
                        FreeConsole();

                        // Wait for the process to exit
                        process.WaitForExit();

                        Console.WriteLine($"Successfully stopped {processName} with PID: {process.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to attach to process {processName} console. Error: {Marshal.GetLastWin32Error()}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error stopping process {processName} with PID: {process.Id}. {e.Message}");
                }
                finally
                {
                    processList.Remove(processName); // Remove the process from the list
                }
            }
            else
            {
                Console.WriteLine($"No process found for {processName}");
            }
            
        }

        private void StartAllProcesses()
        {
            RunCommandInNewWindow("cd " + projectPath + " && php artisan serve", "php");

            if (taskWorkCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && php artisan schedule:work", "tasks");
            }
            if (npmCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && npm run dev", "npm");
            }
            if (yarnCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && yarn run dev", "yarn");
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
            RunCommandInNewWindow("cd " + projectPath + " && " + command, processName);
        }
        private void RunCommandInNewWindow(string command, string commandName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                UseShellExecute = true, // Do not redirect output
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var process = Process.Start(startInfo);

            if (process != null)
            {
                Console.WriteLine($"Started process {commandName} with PID: {process.Id}");
                processList[commandName] = process;

                // Set the parent of the command window to the main application window
                SetParent(process.MainWindowHandle, mainWindowHandle);
            }
        }
        #endregion "Process Management"
        
        
        
        
        
        public MainWindow()
        {
            InitializeComponent();
            

            mainWindowHandle = new WindowInteropHelper(this).Handle;
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
                Interval = TimeSpan.FromSeconds(5)
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

            using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/images.ico"))!.Stream)
            {
                notifyIcon = new NotifyIcon
                {
                    Icon = new Icon(iconStream),
                    Visible = true,
                    ContextMenuStrip = new ContextMenuStrip()
                };
            }

            var runningMenuItem = new ToolStripMenuItem("Running");
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop All Processes", null, (s, e) => StopAllProcesses()));
            /*runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop npm", null, (s, e) => StopProcess("npm", "npm run dev")));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop yarn", null, (s, e) => StopProcess("yarn", "yarn run dev")));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop Tasks", null, (s, e) => StopProcess("tasks", "php artisan schedule:work")));*/
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop developpment env", null, (s, e) => StopServer(System.IO.Path.GetFileNameWithoutExtension(serverPath))));
            /*runningMenuItem.DropDownItems.Add(new ToolStripMenuItem("Stop php artisan serve", null, (s, e) => StopProcess("php", "php artisan serve")));*/

            var restartMenuItem = new ToolStripMenuItem("Restart");
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart All Processes", null, (s, e) => RestartAllProcesses()));
            /*restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart npm", null, (s, e) => RestartProcess("npm", "npm run dev")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart yarn", null, (s, e) => RestartProcess("yarn", "yarn run dev")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart Tasks", null, (s, e) => RestartProcess("tasks", "php artisan schedule:work")));*/
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart developpment env", null, (s, e) => RestartServer()));
            /*restartMenuItem.DropDownItems.Add(new ToolStripMenuItem("Restart php artisan serve", null, (s, e) => RestartProcess("php", "php artisan serve")));*/

            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Restore", null, (s, e) => RestoreFromTray()));
            notifyIcon.ContextMenuStrip.Items.Add(runningMenuItem);
            notifyIcon.ContextMenuStrip.Items.Add(restartMenuItem);
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown()));
            SetLanguage(Properties.Settings.Default.Language ?? "en");
        }
        
        private void SetLanguage(string cultureCode)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
            resourceManager = new ResourceManager("LaravelLauncher.Resources.Strings", typeof(MainWindow).Assembly);
            UpdateUI();
            UpdateTrayMenu();
        }
        
        private void UpdateUI()
        {
            NomProjetLabel.Content = resourceManager.GetString("ProjectTitle");
            CheminProjetLabel.Content = resourceManager.GetString("ProjectPath");
            FileSelectBtn.Content = resourceManager.GetString("SelectFolder");
            StartLocalServerBtn.Content = resourceManager.GetString("StartDevEnv");
            StartProjectBtn.Content = resourceManager.GetString("LaunchProject");
            OptionsTitleLabel.Content = resourceManager.GetString("LaunchOptions");
            npmCheckbox.Content = resourceManager.GetString("LaunchNpm");
            yarnCheckbox.Content = resourceManager.GetString("LaunchYarn");
            taskWorkCheckbox.Content = resourceManager.GetString("StartScheduledTasks");
            LocalServerPathLabel.Content = resourceManager.GetString("LocalServerPath");
        }
        
        private void UpdateTrayMenu()
        {
            notifyIcon.ContextMenuStrip?.Items.Clear();

            var runningMenuItem = new ToolStripMenuItem(resourceManager.GetString("TrayMenuStopAllProcesses"));
            runningMenuItem.DropDownItems.Add(new ToolStripMenuItem(resourceManager.GetString("TrayMenuStopDevEnv"), null, (s, e) => StopServer(System.IO.Path.GetFileNameWithoutExtension(serverPath))));

            var restartMenuItem = new ToolStripMenuItem(resourceManager.GetString("TrayMenuRestartAllProcesses"));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(resourceManager.GetString("TrayMenuRestartDevEnv"), null, (s, e) => RestartServer()));

            notifyIcon.ContextMenuStrip?.Items.Add(new ToolStripMenuItem(resourceManager.GetString("TrayMenuRestore"), null, (s, e) => RestoreFromTray()));
            notifyIcon.ContextMenuStrip?.Items.Add(runningMenuItem);
            notifyIcon.ContextMenuStrip?.Items.Add(restartMenuItem);
            notifyIcon.ContextMenuStrip?.Items.Add(new ToolStripMenuItem(resourceManager.GetString("TrayMenuExit"), null, (s, e) => Application.Current.Shutdown()));
        }

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
        
        
        
        private void StartLocalServerBtn_Click(object sender, RoutedEventArgs e)
        {
            StartLocalServer();
        }

        
        
        /*private void RunCommandInNewWindow(string command, string commandName)
        {
            if (PIDList == null)
            {
                PIDList = new Dictionary<string, int>();
            }

            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
            var process = Process.Start(startInfo);
            if (process != null)
            {
                Console.WriteLine($"Started process {commandName} with PID: {process.Id}");
                PIDList[commandName] = process.Id;

                // Set the parent of the command window to the main application window
                SetParent(process.MainWindowHandle, mainWindowHandle);
            }
        }*/

        private void StartProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Lancement du projet");

            startTasks = taskWorkCheckbox.IsChecked == true;
            startNpm = npmCheckbox.IsChecked == true;
            startYarn = yarnCheckbox.IsChecked == true;

            UpdateProjectSettings(projectPath, startNpm, startYarn, startTasks);

            RunCommandInNewWindow("cd " + projectPath + " && php artisan serve", "php");

            if (taskWorkCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && php artisan schedule:work", "tasks");
            }
            if (npmCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && npm run dev", "npm");
            }
            if (yarnCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + projectPath + " && yarn run dev", "yarn");
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
                StartLocalServerBtn.Content = "Environnement de développement hors-ligne (Cliquez pour le lancer)";
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Red);
                return;
            }

            string executableName = System.IO.Path.GetFileNameWithoutExtension(serverPath);
            bool processIsRunning = IsProcessRunning(executableName);

            if (processIsRunning)
            {
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Arrêter l'environnement de développement";
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Lancer l'environnement de développement";
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