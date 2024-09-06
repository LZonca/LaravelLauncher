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
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace LaravelLauncher
{
    public partial class MainWindow : Window
    {
        

        private ResourceManager _resourceManager;

        private readonly IntPtr _mainWindowHandle;
        private readonly NotifyIcon _notifyIcon;
        private string? _projectPath = string.Empty;
        private bool _startNpm;
        private bool _startYarn;
        private bool _startTasks;
        private readonly Dictionary<string, string?> _folderNameToPathMap = new Dictionary<string, string?>();
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
                    if (process != null)
                    {
                        Debug.WriteLine($"Stopping process: {processName} with PID {process.Id}");

                        // Attach to the console of the target process
                        if (AttachConsole(process.Id))
                        {
                            // Send a CTRL_C_EVENT to the console
                            if (!GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                            {
                                Debug.WriteLine(
                                    $"Failed to send CTRL+C to process {processName}. Error: {Marshal.GetLastWin32Error()}");
                            }

                            // Detach immediately after sending the signal
                            FreeConsole();

                            // Wait for the process to exit
                            process.WaitForExit();

                            Debug.WriteLine($"Successfully stopped {processName} with PID: {process.Id}");
                        }
                        else
                        {
                            Debug.WriteLine(
                                $"Failed to attach to process {processName} console. Error: {Marshal.GetLastWin32Error()}");
                        }
                    }
                }
                catch (Exception e)
                {
                    if (process != null)
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

        private void TickUpdate()
        {
            UpdateButtonState();
            UpdateProcessList();
            
        }
        
        #endregion "Process Management"
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += MainWindow_Closing;
        
            _mainWindowHandle = new WindowInteropHelper(this).Handle;
            LoadRecentProjects();
        
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
            timer.Tick += (_, _) => TickUpdate();
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
        
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_processList.Count > 0)
            {
                // Prompt the user about running processes
                var result = System.Windows.MessageBox.Show(
                    _resourceManager.GetString("ProcessesStillRunning"),
                    _resourceManager.GetString("ConfirmExit"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

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
            LocalServerPathLabel.Content = _resourceManager.GetString(!string.IsNullOrEmpty(_serverPath) ? "LocalServerPath" : "UnsetLocalServerPath");
        }
        
        private void UpdateTrayMenu()
        {
            Debug.WriteLine("Updating tray menu");
            _notifyIcon.ContextMenuStrip?.Items.Clear();
            _notifyIcon.ContextMenuStrip?.Items.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestore"), null, (_, _) => RestoreFromTray()));
            if (_processList.Count > 0)
            {
                UpdateTrayMenuItems();
            }

            _notifyIcon.ContextMenuStrip?.Items.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuExit"), null, (_, _) => Application.Current.Shutdown()));
            
        }

        private void UpdateTrayMenuItems()
        {
                var stopMenuItem = new ToolStripMenuItem(_resourceManager.GetString("ActiveProcesses"));
                var restartMenuItem = new ToolStripMenuItem(_resourceManager.GetString("RestartProcesses"));
                
                var stopAllMenuItem = new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopAllProcesses"), null, (_, _) => StopAllProcesses());
                var restartAllMenuItem = new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartAllProcesses"), null, (_, _) => RestartAllProcesses());
                
                stopMenuItem.DropDownItems.Add(stopAllMenuItem);
                restartMenuItem.DropDownItems.Add(restartAllMenuItem);
                
                if (_processList.ContainsKey("php"))
                {
                    Debug.WriteLine("Adding PHP stop menu item.");
                    stopMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopServe"), null, (_, _) => StopProcess("php")));
                    restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartServe"), null, (_, _) => RestartProcess("php", "php artisan serve")));

                }
                if (_processList.ContainsKey("npm"))
                {
                    Debug.WriteLine("Adding NPM stop menu item.");
                    stopMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopNpm"), null, (_, _) => StopProcess("npm")));
                    restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartNpm"), null, (_, _) => RestartProcess("npm", "npm run dev")));

                }
                if (_processList.ContainsKey("yarn"))
                {
                    Debug.WriteLine("Adding Yarn stop menu item.");
                    stopMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopYarn"), null, (_, _) => StopProcess("yarn")));
                    stopMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartYarn"), null, (_, _) => RestartProcess("yarn", "yarn run dev")));

                }
                if (_processList.ContainsKey("tasks"))
                {
                    Debug.WriteLine("Adding Tasks stop menu item.");
                    stopMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuStopTasks"), null, (_, _) => StopProcess("tasks")));
                    restartMenuItem.DropDownItems.Add(new ToolStripMenuItem(_resourceManager.GetString("TrayMenuRestartTasks"), null, (_, _) => RestartProcess("tasks", "php artisan schedule:work")));
                }   
                
                
                
                _notifyIcon.ContextMenuStrip?.Items.Add(stopMenuItem);
                _notifyIcon.ContextMenuStrip?.Items.Add(restartMenuItem);
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
                
                private void UpdateProcessList()
                {
                    foreach (var processEntry in _processList.ToList())
                    {
                        if (processEntry.Value != null)
                        {
                            try
                            {
                                Process.GetProcessById(processEntry.Value.Id);
                            }
                            catch (ArgumentException)
                            {
                                // Process is not running
                                _processList.Remove(processEntry.Key);
                            }
                        }
                    }
                    UpdateTrayMenu();
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
                string? folderName = System.IO.Path.GetFileName(folderPath);
                RecentProjectsList.Items.Add(new ListBoxItem { Content = folderName });
                if (folderName != null) _folderNameToPathMap[folderName] = folderPath;
            }
        }

        private void LoadProjectSettings(string? projectPath)
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

        private void UpdateProjectSettings(bool useNpm, bool useYarn, bool useStartTasks)
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
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string? selectedPath = dialog.SelectedPath;

                var settings = SettingsManager.LoadSettings();
                if (settings != null)
                {
                    var projects = settings.Projects;

                    if (projects.All(p => p.Path != selectedPath))
                    {
                        projects.Add(new ProjectSettings { Path = selectedPath });
                        SettingsManager.SaveSettings(settings);
                        _projectPath = selectedPath;
                        NomProjetLabel.Content = System.IO.Path.GetFileName(_projectPath);
                        CheminProjetLabel.Content = _projectPath;
                        LoadRecentProjects();

                        // Set the newly added project as the selected item
                        foreach (ListBoxItem item in RecentProjectsList.Items)
                        {
                            if (item.Content.ToString() == System.IO.Path.GetFileName(selectedPath))
                            {
                                RecentProjectsList.SelectedItem = item;
                                break;
                            }
                        }

                        StartProjectBtn.IsEnabled = true;
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

            UpdateProjectSettings(_startNpm, _startYarn, _startTasks);

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
            
            UpdateTrayMenu();
            MinimizeToTray();
            
        }

        private void RecentProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Content != null)
            {
                string? folderName = selectedItem.Content.ToString();
                if (folderName != null && _folderNameToPathMap.TryGetValue(folderName, out string? fullPath))
                {
                    _projectPath = fullPath;
                    NomProjetLabel.Content = System.IO.Path.GetFileName(_projectPath);
                    CheminProjetLabel.Content = _projectPath;
                    LoadProjectSettings(fullPath);
                }
            }
        }
        private void RemoveProjectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem is ListBoxItem selectedItem && selectedItem.Content != null)
            {
                string? folderName = selectedItem.Content.ToString();
                if (folderName != null && _folderNameToPathMap.TryGetValue(folderName, out string? fullPath))
                {
                    // Show confirmation message box
                    var result = System.Windows.MessageBox.Show(
                        _resourceManager.GetString("ConfirmRemoveProject"),
                        _resourceManager.GetString("Confirm"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Remove from dictionary
                        _folderNameToPathMap.Remove(folderName);

                        // Remove from user settings
                        var settings = SettingsManager.LoadSettings();
                        if (settings != null)
                        {
                            settings.Projects.RemoveAll(p => p.Path == fullPath);
                            SettingsManager.SaveSettings(settings);
                        }

                        // Update RecentProjectsList
                        LoadRecentProjects();
                    }
                }
            }
        }
        #endregion

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWindow = new Settings();
            settingsWindow.Show();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}