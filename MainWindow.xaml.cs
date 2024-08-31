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
using System.Windows.Forms.VisualStyles;
using System.Windows.Interop; // Added namespace
using Application = System.Windows.Application;
using MessageBox = System.Windows.Forms.MessageBox;

namespace LaravelLauncher
{
    public partial class MainWindow : Window
    {
        

        private ResourceManager _resourceManager;

        private readonly IntPtr _mainWindowHandle;
        private readonly NotifyIcon _notifyIcon;
        private string _projectPath = string.Empty;
        private bool _startNpm;
        private bool _startYarn;
        private bool _startTasks;
        private readonly Dictionary<string?, string> _folderNameToPathMap = new Dictionary<string?, string>();
        private readonly string _serverPath = string.Empty;
        private readonly Dictionary<string, Process?> _processList = new Dictionary<string, Process?>();

        #region Process Management

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        
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
            Debug.WriteLine("\nStopping all processes");

            foreach (var processEntry in _processList.ToList()) // Create a copy of the list to avoid modification issues
            {
                StopProcess(processEntry.Key);
            }

            _processList.Clear(); // Clear the list after stopping all processes
        }

        private void StopProcess(string processName)
        {
            if (_processList.TryGetValue(processName, out Process? process))
            {
                try
                {
                    Debug.WriteLine($"Stopping process: {processName} with PID {process.Id}");

                    // Attach to the console of the target process
                    if (AttachConsole(process.Id))
                    {
                        // Send a CTRL_C_EVENT to the console
                        if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                        {
                            Debug.WriteLine($"Failed to send CTRL+C to process {processName}. Error: {Marshal.GetLastWin32Error()}");
                        }

                        // Detach immediately after sending the signal
                        FreeConsole();

                        // Wait for the process to exit
                        process.WaitForExit();

                        Debug.WriteLine($"Successfully stopped {processName} with PID: {process.Id}");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to attach to process {processName} console. Error: {Marshal.GetLastWin32Error()}");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error stopping process {processName} with PID: {process.Id}. {e.Message}");
                }
                finally
                {
                    _processList.Remove(processName); // Remove the process from the list
                }
            }
            else
            {
                Debug.WriteLine($"No process found for {processName}");
            }
            
        }

        private void StartAllProcesses()
        {
            RunCommandInNewWindow("cd " + _projectPath + " && php artisan serve", "php");

            if (taskWorkCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + _projectPath + " && php artisan schedule:work", "tasks");
            }
            if (npmCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + _projectPath + " && npm run dev", "npm");
            }
            if (yarnCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + _projectPath + " && yarn run dev", "yarn");
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
            RunCommandInNewWindow("cd " + _projectPath + " && " + command, processName);
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
                Debug.WriteLine($"Started process {commandName} with PID: {process.Id}");
                _processList[commandName] = process;

                // Set the parent of the command window to the main application window
                SetParent(process.MainWindowHandle, _mainWindowHandle);
            }
        }
        
        
        #endregion "Process Management"
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;

            _mainWindowHandle = new WindowInteropHelper(this).Handle;
            LoadRecentProjects();

            // Ensure _resourceManager is initialized before use
            _resourceManager = new ResourceManager("LaravelLauncher.Resources.Strings", typeof(MainWindow).Assembly);

            string path = Properties.Settings.Default.ServerPath;
            if (!string.IsNullOrEmpty(path))
            {
                _serverPath = path;
                LocalServerPathLabel.Content = path;
            }
            else
            {
                LocalServerPathLabel.Content = "";
            }

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (_, _) => UpdateButtonState();
            timer.Start();
            UpdateButtonState();

            if (string.IsNullOrEmpty(_projectPath))
            {
                StartProjectBtn.IsEnabled = false;
            }
            else
            {
                StartProjectBtn.IsEnabled = true;
            }

            using (Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/images.ico"))!.Stream)
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = new Icon(iconStream),
                    Visible = true,
                    ContextMenuStrip = new ContextMenuStrip()
                };
            }

            UpdateTrayMenu(); // Ensure the tray menu is updated after initialization

            SetLanguage(Properties.Settings.Default.Language ?? "en");
        }
        
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_processList.Count > 0)
            {
                // Prompt the user about running processes
                var result = System.Windows.MessageBox.Show(
                    "There are still running processes. Do you want to terminate them?",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Stop all processes and then close the app
                    StopAllProcesses();
                    // Allow the closing event to proceed
                }
                else if (result == MessageBoxResult.No)
                {
                    // Allow the closing event to proceed without stopping processes
                    
                }
            }
        }
        
        private void ExitApplication(object sender, EventArgs e)
        {
            var args = new System.ComponentModel.CancelEventArgs();
            MainWindow_Closing(this, args);
            if (!args.Cancel)
            {
                Application.Current.Shutdown();
            }
        }
        
        #region Language
        private void SetLanguage(string cultureCode)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
            _resourceManager = new ResourceManager("LaravelLauncher.Resources.Strings", typeof(MainWindow).Assembly);
            UpdateUi();
            UpdateTrayMenu();
        }
        
        private void UpdateUi()
        {
            NomProjetLabel.Content = _resourceManager.GetString("ProjectTitle");
            CheminProjetLabel.Content = _resourceManager.GetString("ProjectPath");
            FileSelectBtn.Content = _resourceManager.GetString("SelectFolder");
            StartLocalServerLabel.Text = _resourceManager.GetString("StartDevEnv");
            StartProjectBtn.Content = _resourceManager.GetString("LaunchProject");
            OptionsTitleLabel.Content = _resourceManager.GetString("LaunchOptions");
            npmCheckbox.Content = _resourceManager.GetString("LaunchNpm");
            yarnCheckbox.Content = _resourceManager.GetString("LaunchYarn");
            taskWorkCheckbox.Content = _resourceManager.GetString("StartScheduledTasks");
            LocalServerPathLabel.Content = _resourceManager.GetString("LocalServerPath");
            LocalServerPathLabel.Content = _resourceManager.GetString(_serverPath != null ? "LocalServerPath" : "UnsetLocalServerPath");
        }
        
        private void UpdateTrayMenu()
        {
            _notifyIcon.ContextMenuStrip?.Items.Clear();

            var runningMenuItem = new ToolStripMenuItem(_resourceManager.GetString("ActiveProcesses"));
            

            var restartMenuItem = new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartAllProcesses"));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartDevEnv"), null, (_, _) => RestartServer()));

            // Additional process management items
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartNpm"), null, (_, _) => RestartProcess("npm", "npm run dev")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartYarn"), null, (_, _) => RestartProcess("yarn", "yarn run dev")));
            restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartTasks"), null, (_, _) => RestartProcess("tasks", "php artisan schedule:work")));

            _notifyIcon.ContextMenuStrip?.Items.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestore"), null, (_, _) => RestoreFromTray()));
            _notifyIcon.ContextMenuStrip?.Items.Add(runningMenuItem);
            _notifyIcon.ContextMenuStrip?.Items.Add(restartMenuItem);
            _notifyIcon.ContextMenuStrip?.Items.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuExit"), null, (_, _) => Application.Current.Shutdown()));
            UpdateTrayMenuItems(runningMenuItem);
        }

        private void UpdateTrayMenuItems(ToolStripMenuItem runningMenuItem)
        {
            if (_processList.ContainsKey("php"))
            {
                runningMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopDevEnv"), null, (_, _) => StopServer(System.IO.Path.GetFileNameWithoutExtension(_serverPath))));
            }
            if (_processList.ContainsKey("npm"))
            {
                runningMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopNpm"), null, (_, _) => StopProcess("npm")));
            }
            if (_processList.ContainsKey("yarn"))
            {
                runningMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopYarn"), null, (_, _) => StopProcess("yarn")));
            }
            if (_processList.ContainsKey("tasks"))
            {
                runningMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopTasks"), null, (_, _) => StopProcess("tasks")));
            }
        }


        #endregion

        #region DevEnv

        private void StartLocalServer()
        {
            string executableName = System.IO.Path.GetFileNameWithoutExtension(_serverPath);
            bool processIsRunning = IsProcessRunning(executableName);

            if (processIsRunning)
            {
                StopServer(executableName);
            }
            else
            {
                try
                {
                    string executablePath = _serverPath;

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
                        System.Windows.MessageBox.Show(
                            _resourceManager.GetString("UnsetLocalServerPath"), 
                            _resourceManager.GetString("Error"), 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"{_resourceManager.GetString("UnableToLaunchExecutable")} : {ex.Message}", 
                        _resourceManager.GetString("Error"), 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error
                    );
                }
            }

            UpdateButtonState();
        }

        private void RestartServer()
        {
            StopServer(System.IO.Path.GetFileNameWithoutExtension(_serverPath));
            StartLocalServer();
        }
        
        private bool IsProcessRunning(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        private void UpdateButtonState()
        {
            if (string.IsNullOrEmpty(_serverPath))
            {
                Debug.WriteLine("serverPath is null or empty.");
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerBtn.Content = "Environnement de développement hors-ligne (Cliquez pour le lancer)";
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Red);
                return;
            }

            string executableName = System.IO.Path.GetFileNameWithoutExtension(_serverPath);
            bool processIsRunning = IsProcessRunning(executableName);

            if (processIsRunning)
            {
                Debug.WriteLine("Process is running.");
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerLabel.Text = _resourceManager?.GetString("StopDevEnv") ?? "Stop Development Env"; // Added null check
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                Debug.WriteLine("Process is not running.");
                StartLocalServerBtn.IsEnabled = true;
                StartLocalServerLabel.Text = _resourceManager?.GetString("StartDevEnv") ?? "Start Development Env"; // Added null check
                StartLocalServerBtn.Background = new SolidColorBrush(Colors.Red);
            }
        }
        
        private void StartLocalServerBtn_Click(object sender, RoutedEventArgs e)
                {
                    StartLocalServer();
                }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && AreProcessesRunning())
            {
                MinimizeToTray();
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
        
        #endregion

        #region Tray Management

        private void MinimizeToTray()
                {
                    _notifyIcon.Visible = true;
                    this.Hide();
                }
        
                private void RestoreFromTray()
                {
                    _notifyIcon.Visible = false;
                    this.Show();
                    this.WindowState = WindowState.Normal;
                }
        
                protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
                {
                    _notifyIcon.Dispose();
                    base.OnClosing(e);
                }

                private bool AreProcessesRunning()
                {
                    return _processList.Count > 0;
                }
        #endregion

        #region Projects
        private void LoadRecentProjects()
        {
            var settings = SettingsManager.LoadSettings();
            RecentProjectsList.Items.Clear();
            _folderNameToPathMap.Clear();

            foreach (var folderPath in SettingsManager.GetProjectPaths())
            {
                string folderName = System.IO.Path.GetFileName(folderPath);
                RecentProjectsList.Items.Add(new ListBoxItem { Content = folderName });
                _folderNameToPathMap[folderName] = folderPath;
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

        private void UpdateProjectSettings(string selectedProjectPath, bool useNpm, bool useYarn, bool useStartTasks)
        {
            var settings = SettingsManager.LoadSettings();
            var projectSettings = settings.Projects.FirstOrDefault(p => p.Path == this._projectPath);

            if (projectSettings == null)
            {
                projectSettings = new ProjectSettings { Path = this._projectPath };
                settings.Projects.Add(projectSettings);
            }

            projectSettings.UseNpm = useNpm;
            projectSettings.UseYarn = useYarn;
            projectSettings.startTasks = useStartTasks;

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
                        _projectPath = dialog.SelectedPath;
                        NomProjetLabel.Content = System.IO.Path.GetFileName(_projectPath);
                        CheminProjetLabel.Content = _projectPath;
                        LoadRecentProjects();
                    }
                }
            }
        }
        private void StartProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Lancement du projet");

            _startTasks = taskWorkCheckbox.IsChecked == true;
            _startNpm = npmCheckbox.IsChecked == true;
            _startYarn = yarnCheckbox.IsChecked == true;

            UpdateProjectSettings(_projectPath, _startNpm, _startYarn, _startTasks);

            RunCommandInNewWindow("cd " + _projectPath + " && php artisan serve", "php");

            if (taskWorkCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + _projectPath + " && php artisan schedule:work", "tasks");
            }
            if (npmCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + _projectPath + " && npm run dev", "npm");
            }
            if (yarnCheckbox.IsChecked == true)
            {
                RunCommandInNewWindow("cd " + _projectPath + " && yarn run dev", "yarn");
            }

            MinimizeToTray();
        }
        
        private void RecentProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Content != null)
            {
                string? folderName = selectedItem.Content.ToString();
                if (folderName != null && _folderNameToPathMap.TryGetValue(folderName, out string fullPath))
                {
                    _projectPath = fullPath;
                    NomProjetLabel.Content = System.IO.Path.GetFileName(_projectPath);
                    CheminProjetLabel.Content = _projectPath;
                    LoadProjectSettings(fullPath);
                }
            }
        }
        
        #endregion

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWindow = new Settings();
            settingsWindow.Show();
        }
    }
}