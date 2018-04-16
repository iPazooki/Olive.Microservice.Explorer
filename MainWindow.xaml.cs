﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Serialization;
using EnvDTE;
using EnvDTE80;
using MacroserviceExplorer.TCPIP;
using MacroserviceExplorer.Utils;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MessageBox = System.Windows.Forms.MessageBox;
using Process = System.Diagnostics.Process;
using Thread = System.Threading.Thread;


namespace MacroserviceExplorer
{

    public partial class MainWindow
    {
        const string services_file_name = "services.json";
        public List<MacroserviceGridItem> serviceData = new List<MacroserviceGridItem>();
        public ObservableCollection<MacroserviceGridItem> MacroserviceGridItems = new ObservableCollection<MacroserviceGridItem>();
        readonly System.Windows.Forms.NotifyIcon notifyIcon;
        bool exit;
        public Visibility FileOpened { get; set; }
        public MacroserviceGridItem SelectedService { get; set; }

        #region Commands

        public static readonly RoutedCommand EditCommand = new RoutedUICommand("Edit", "EditCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.E, ModifierKeys.Control)
        }));

        public static readonly RoutedCommand RefreshCommand = new RoutedUICommand("Refresh", "RefreshCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.R, ModifierKeys.Control)
        }));

        public static readonly RoutedCommand CloseCommand = new RoutedUICommand("Close", "CloseCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.Q, ModifierKeys.Control)
        }));

        public static readonly RoutedCommand ExitCommand = new RoutedUICommand("Exit", "ExitCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.Q, ModifierKeys.Control | ModifierKeys.Shift)
        }));

        public static readonly RoutedCommand RunAllCommand = new RoutedUICommand("RunAll", "RunAllCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.L, ModifierKeys.Control )
        }));

        public static readonly RoutedCommand StopAllCommand = new RoutedUICommand("StopAll", "StopAllCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Shift)
        }));

        public static readonly RoutedCommand RunAllFilteredCommand = new RoutedUICommand("RunAllFiltered", "RunAllFilteredCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Alt)
        }));

        public static readonly RoutedCommand StopAllFilteredCommand = new RoutedUICommand("StopAllFiltered", "StopAllFilteredCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)
        }));

        public static readonly RoutedCommand AlwaysOnTopCommand = new RoutedUICommand("AlwaysOnTop", "AlwaysOnTopCommand", typeof(MainWindow), new InputGestureCollection(new InputGesture[]
        {
            new KeyGesture(Key.T, ModifierKeys.Control)
        }));

        #endregion

        public MainWindow()
        {
            var components = new Container();
            notifyIcon = new System.Windows.Forms.NotifyIcon(components)
            {
                ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(),
                Icon = Properties.Resources.Olive,
                Text = @"Olive Macroservice Explorer",
                Visible = true,
            };
            notifyIcon.ContextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Open Explorer Window", null, trayOpenWindow));

            notifyIcon.ContextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripMenuItem("Exit", null, exitMenuItem_Click));

            //notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            notifyIcon.Click += trayOpenWindow;
            //notifyIcon.MouseUp += notifyIcon_MouseUp;


            InitializeComponent();
            DataContext = MacroserviceGridItems;
            StartAutoRefresh();
        }

        readonly DispatcherTimer autoRefreshTimer = new DispatcherTimer();
        void StartAutoRefresh()
        {
            autoRefreshTimer.Tick += async (sender, args) =>
            {
                var gridHasFocused = srvGrid.IsFocused;

                MacroserviceGridItem selItem = null;
                if (SelectedService != null)
                    selItem = SelectedService;

                await Refresh();

                foreach (var service in MacroserviceGridItems)
                {
                    if (service.WebsiteFolder.IsEmpty() || service.Port.IsEmpty()) continue;

                    service.ProcId = getListeningPortProcessId(service.Port.To<int>());
                    service.Status = service.ProcId < 0 ? MacroserviceGridItem.enumStatus.Stop : MacroserviceGridItem.enumStatus.Run;
                }
                srvGrid.SelectedItem = selItem;

                if (gridHasFocused)
                    srvGrid.Focus();


            };
            autoRefreshTimer.Interval = new TimeSpan(0, 0, 3);
            autoRefreshTimer.Start();
        }

        void exitMenuItem_Click(object sender, EventArgs e)
        {
            exit = true;
            Close();
        }


        void trayOpenWindow(object sender, EventArgs eventArgs)
        {
            Visibility = Visibility.Visible;
        }

        void chrome_OnClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var cm = new ContextMenu();
            var btn = (Button)sender;
            var serviceName = btn.Tag.ToString();
            var service = MacroserviceGridItems.Single(s => s.Service == serviceName);
            if (int.TryParse(service.Port, out var port))
            {
                var webAddr = $"http://localhost:{port}";
                var localMenuItem = new MenuItem { Header = $"Local\t  {webAddr}" };
                localMenuItem.Click += BrowsItem_Click;
                localMenuItem.Tag = service;
                cm.Items.Add(localMenuItem);
            }

            var uatMenuItem = new MenuItem { Header = "UAT" };
            if (!string.IsNullOrEmpty(service.UatUrl))
            {
                uatMenuItem.Header += $"\t  {service.UatUrl}";
                uatMenuItem.Tag = service;
                uatMenuItem.Click += BrowsItem_Click;
            }
            else
                uatMenuItem.IsEnabled = false;
            cm.Items.Add(uatMenuItem);

            var liveMenuItem = new MenuItem { Header = "Live" };
            if (!string.IsNullOrEmpty(service.LiveUrl))
            {
                liveMenuItem.Header += $"\t  {service.LiveUrl}";
                liveMenuItem.Tag = service;
                liveMenuItem.Click += BrowsItem_Click;
            }
            else
                liveMenuItem.IsEnabled = false;
            cm.Items.Add(liveMenuItem);

            cm.PlacementTarget = btn;
            cm.IsOpen = true;
        }

        void BrowsItem_Click(object sender, RoutedEventArgs e)
        {
            var menuitem = (MenuItem)sender;
            menuitem.Click -= BrowsItem_Click;
            var service = (MacroserviceGridItem)menuitem.Tag;
            var address = menuitem.Header.ToString().Substring(menuitem.Header.ToString().IndexOf(" ", StringComparison.Ordinal) + 1);
            //var address = menuitem.Header.ToString().TrimBefore(, caseSensitive: false, trimPhrase: true).Trim();
            if (address.Contains("localhost:") && service.Status != MacroserviceGridItem.enumStatus.Run)
            {
                void OnServiceOnPropertyChanged(object obj, PropertyChangedEventArgs args)
                {
                    if (args.PropertyName != nameof(service.Status) || service.Status != MacroserviceGridItem.enumStatus.Run) return;
                    service.PropertyChanged -= OnServiceOnPropertyChanged;
                    Process.Start(address);
                }

                service.PropertyChanged += OnServiceOnPropertyChanged;
                StartService(service);
            }
            else
                Process.Start(address);
        }

        List<string> _recentFiles = new List<string>();
        const string RecentsXml = "Recents.xml";

        void SaveRecentFilesXml()
        {
            var serializer = new XmlSerializer(typeof(List<string>));
            using (var sww = new StringWriter())
            using (var writer = XmlWriter.Create(sww))
            {
                serializer.Serialize(writer, _recentFiles);
                File.WriteAllText(RecentsXml, sww.ToString().Replace("utf-16", "utf-8"));
            }
        }

        async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(RecentsXml))
            {
                OpenProject_Executed(sender, null);
                if (!projextLoaded)
                    exitMenuItem_Click(sender, e);
                return;
            }

            ReloadRecentFiles();
            while (_recentFiles.Count > 0)
            {
                if (await LoadFile(_recentFiles[_recentFiles.Count - 1]))
                    break;
            }

            if (!projextLoaded)
            {
                File.Delete(RecentsXml);
                MainWindow_OnLoaded(sender, e);
            }
        }

        void ReloadRecentFiles()
        {
            var serializer = new XmlSerializer(typeof(List<string>));
            mnuRecentFiles.Items.Clear();
            using (var reader = XmlReader.Create(RecentsXml))
                _recentFiles = (List<string>)serializer.Deserialize(reader);

            foreach (var recentFile in _recentFiles)
                AddRecentMenuItem(recentFile);

        }

        void AddRecentMenuItem(string recentFile)
        {
            var menuItem = new MenuItem { Header = recentFile };
            menuItem.Click += RecentMenuItem_Click;
            mnuRecentFiles.Items.Insert(0,menuItem);
            var hasClearAll = false;
            foreach (var o in mnuRecentFiles.Items)
            {
                if (o is MenuItem item1 && item1.Header.ToString() != "Clear All") continue;
                hasClearAll = true;
            }

            if (!hasClearAll)
                AddClearRecentMenuItem();
        }

        void AddClearRecentMenuItem()
        {
            var menuItem = new MenuItem { Header = "Clear All" };
            menuItem.Click += (sender, args) =>
            {                
                mnuRecentFiles.Items.Clear();
                mnuRecentFiles.Items.Add(new MenuItem {Header = "[Empty]" });
                RecentsXml.AsFile().Delete();
            };
            mnuRecentFiles.Items.Add(new Separator());
            mnuRecentFiles.Items.Add(menuItem);
        }
        async void RecentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await LoadFile(((MenuItem)e.Source).Header.ToString());
        }

        FileInfo servicesJsonFile;
        bool projextLoaded;
        private DateTime servicesJsonFileLastWriteTime;

        async Task<bool> LoadFile(string filePath)
        {
            servicesJsonFile = new FileInfo(filePath);

            if (!servicesJsonFile.Exists)
            {

                var result = MessageBox.Show($"file : {servicesJsonFile.FullName} \ndoes not exist anymore. \nDo you want to removed it from recent files list?", "File Not Found", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question);
                _recentFiles.Remove(servicesJsonFile.FullName);
                if (result != System.Windows.Forms.DialogResult.Yes) return false;

                SaveRecentFilesXml();
                ReloadRecentFiles();

                servicesJsonFile = null;
                projextLoaded = false;
                return false;
            }

            servicesJsonFileLastWriteTime = servicesJsonFile.LastWriteTime;

            //try
            {

                var servicesAllText = File.ReadAllText(servicesJsonFile.FullName);
                var servicesJObject = Newtonsoft.Json.Linq.JObject.Parse(servicesAllText);
                txtFileInfo.Text = servicesJsonFile.FullName;
                txtSolName.Text = servicesJObject["Solution"]["FullName"].ToString();

                var children = servicesJObject["Services"].Children();
                foreach (var jToken in children)
                {
                    var serviceName = jToken.Path.Replace("Services.", "");
                    var srv = serviceData.SingleOrDefault(srvc => srvc.Service == serviceName);
                    if (srv == null)
                    {
                        srv = new MacroserviceGridItem();
                        serviceData.Add(srv);
                    }
                    string liveUrl = null;
                    string uatUrl = null;
                    foreach (var url in jToken.Children())
                    {
                        liveUrl = url["LiveUrl"].ToString();
                        uatUrl = url["UatUrl"].ToString();
                    }

                    var port = "";
                    var status = MacroserviceGridItem.enumStatus.NoSourcerLocally;
                    var parentFullName = servicesJsonFile.Directory?.Parent?.FullName ?? "";
                    var projFolder = Path.Combine(parentFullName, serviceName);
                    var websiteFolder = Path.Combine(projFolder, "website");
                    var launchSettings = Path.Combine(websiteFolder, "properties", "launchSettings.json");
                    var procId = -1;

                    if (File.Exists(launchSettings))
                    {
                        var launchSettingsAllText = File.ReadAllText(launchSettings);
                        var launchSettingsJObject = Newtonsoft.Json.Linq.JObject.Parse(launchSettingsAllText);
                        var appUrl = launchSettingsJObject["profiles"]["Website"]["applicationUrl"].ToString();
                        port = appUrl.Substring(appUrl.LastIndexOf(":", StringComparison.Ordinal) + 1);
                        status = MacroserviceGridItem.enumStatus.Stop;
                        if (!int.TryParse(port, out var _))
                        {
                            var pos = 0;
                            var portNumer = "";
                            while (pos < port.Length - 1 && char.IsDigit(port[pos]))
                                portNumer += port[pos++];
                            port = portNumer;
                        }
                        procId = getListeningPortProcessId(port.To<int>());
                        if (procId > 0)
                            status = MacroserviceGridItem.enumStatus.Run;
                    }



                    srv.Status = status;
                    srv.Service = serviceName;
                    srv.Port = port;
                    srv.LiveUrl = liveUrl;
                    srv.UatUrl = uatUrl;
                    srv.ProcId = procId;
                    srv.WebsiteFolder = websiteFolder;
                    srv.NugetUpdates = random.Next(50);

                    //var i = GetNugetUpdates(projFolder);
                    //var gitUpdates = await GetGitUpdates(projFolder);

                    srv.VsDTE = GetVSDTE(srv);
                }


                FilterListBy(txtSearch.Text);
            }
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //    return false;
            //}

            if (watcher == null)
                StartFileSystemWatcher(servicesJsonFile);

            projextLoaded = true;
            return true;
        }
        Random random = new Random(10);

        async Task<bool> RefreshFile(string filePath)
        {
            var srvFile = new FileInfo(filePath);
            if (srvFile.LastWriteTime != servicesJsonFileLastWriteTime)
            {                
                return await LoadFile(filePath);
            }

            foreach (var srv in serviceData)
            {
                var projFolder = Path.Combine(servicesJsonFile.Directory?.Parent?.FullName ?? "", srv.Service);
                var websiteFolder = Path.Combine(projFolder, "website");
                var launchSettings = Path.Combine(websiteFolder, "properties", "launchSettings.json");
                if (File.Exists(launchSettings))
                {
                    var launchSettingsAllText = File.ReadAllText(launchSettings);
                    var launchSettingsJObject = Newtonsoft.Json.Linq.JObject.Parse(launchSettingsAllText);
                    var appUrl = launchSettingsJObject["profiles"]["Website"]["applicationUrl"].ToString();
                    var port = appUrl.Substring(appUrl.LastIndexOf(":", StringComparison.Ordinal) + 1);

                    if (!int.TryParse(port, out var _))
                    {
                        var pos = 0;
                        var portNumer = "";
                        while (pos < port.Length - 1 && char.IsDigit(port[pos]))
                            portNumer += port[pos++];
                        port = portNumer;
                    }

                    srv.Port = port;
                }
                else
                {
                    srv.Status = MacroserviceGridItem.enumStatus.NoSourcerLocally;
                    srv.WebsiteFolder = null;
                    srv.ProcId = -1;
                    srv.VsDTE = null;
                    srv.Port = null;
                    srv.VsIsOpen = false;
                    continue;
                }


                srv.ProcId = getListeningPortProcessId(srv.Port.To<int>());
                if (srv.ProcId > 0)
                    srv.Status = MacroserviceGridItem.enumStatus.Run;
                else if (srv.WebsiteFolder.HasValue())
                    srv.Status = MacroserviceGridItem.enumStatus.Stop;
                else
                    srv.Status = MacroserviceGridItem.enumStatus.NoSourcerLocally;

                srv.NugetUpdates = random.Next(50);

                //var i = GetNugetUpdates(projFolder);
                //var gitUpdates = await GetGitUpdates(projFolder);

                srv.VsDTE = GetVSDTE(srv);
            }

            return true;
        }

        int GetNugetUpdates(string projFolder)
        {
            //var repo = PackageRepositoryFactory.Default.CreateRepository("https://packages.nuget.org/api/v3");
            //string path = "";
            //PackageManager packageManager = new PackageManager(repo, path);
            //var packages = repo.GetPackages().ToList();
            //foreach (var package in packages)
            //{

            //}
            return 0;
        }

        void FilterListBy(string txtSearchText)
        {
            MacroserviceGridItems.Clear();
            if (txtSearch.Text.IsEmpty())
            {
                MacroserviceGridItems.AddRange(serviceData);
                return;
            }

            MacroserviceGridItems.AddRange(serviceData.Where(x => x.Service.ToLower().Contains(txtSearchText.ToLower()) || x.Port.Contains(txtSearchText)));

        }

        async Task<int> GetGitUpdates(string projFolder)
        {
            if (!Directory.Exists(Path.Combine(projFolder, ".git")))
            {
                return -1;
            }

            string run()
            {
                return "git.exe".AsFile(searchEnvironmentPath: true)
                    .Execute("rev-list --count --all", waitForExit: true,
                                     configuration: x => x.StartInfo.WorkingDirectory = projFolder);
            }

            var output = await Task.Run((Func<string>)run);

            return output.To<int>();
        }

        FileSystemWatcher watcher;
        void StartFileSystemWatcher(FileInfo fileInfo)
        {
            if (watcher != null)
                StopWatcher();

            watcher = new FileSystemWatcher(fileInfo.Directory?.FullName ?? throw new InvalidOperationException($"File '{fileInfo.FullName}' does not exists anymore ..."), services_file_name);

            watcher.Changed += Watcher_Changed;
            watcher.EnableRaisingEvents = true;
        }

        void StopWatcher()
        {
            if (watcher == null) return;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= Watcher_Changed;
            watcher.Dispose();
            watcher = null;
        }

        public delegate void MyDelegate();

        void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (!string.Equals(e.FullPath, servicesJsonFile.FullName,
                StringComparison.CurrentCultureIgnoreCase)) return;



            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    break;
                case WatcherChangeTypes.Deleted:
                    break;
                case WatcherChangeTypes.Changed:
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, new MyDelegate(async () => await Refresh()));
                    break;
                case WatcherChangeTypes.Renamed:
                    break;
                case WatcherChangeTypes.All:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        int getListeningPortProcessId(int port)
        {
            var tcpRow = ManagedIpHelper.GetExtendedTcpTable(sorted: true)
                .FirstOrDefault(tcprow => tcprow.LocalEndPoint.Port == port);

            if (tcpRow == null) return -1;
            if (Process.GetProcessById(tcpRow.ProcessId).ProcessName.ToLower() != "dotnet") return -1;
            return tcpRow.ProcessId;
        }

        void MainWindow_OnClosed(object sender, EventArgs e)
        {
            StopWatcher();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
        }

        void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Visibility = Visibility.Hidden;
            e.Cancel = !exit;
        }

        void StartStop_OnClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var btn = (Button)sender;
            var serviceName = btn.Tag.ToString();
            var service = MacroserviceGridItems.Single(s => s.Service == serviceName);
            switch (service.Status)
            {
                case MacroserviceGridItem.enumStatus.Pending:
                    MessageBox.Show("Macroservice is loading.\nPlease Wait ...", "Loading ...");
                    break;
                case MacroserviceGridItem.enumStatus.Run:
                    StopService(service);
                    break;
                case MacroserviceGridItem.enumStatus.Stop:
                    StartService(service);
                    break;
                case MacroserviceGridItem.enumStatus.NoSourcerLocally:
                    break;
            }
        }

        void StopService(MacroserviceGridItem service)
        {
            service.Status = MacroserviceGridItem.enumStatus.Pending;

            var process = Process.GetProcessById(service.ProcId);
            process.Kill();
            Thread.Sleep(300);
            service.ProcId = getListeningPortProcessId(service.Port.To<int>());
            if (service.ProcId < 0)
                service.Status = MacroserviceGridItem.enumStatus.Stop;
        }

        void StartService(MacroserviceGridItem service)
        {
            autoRefreshTimer.Stop();
            service.Status = MacroserviceGridItem.enumStatus.Pending;
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --no-build --project " + service.WebsiteFolder,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            proc.Start();

            var dispatcherTimer = new DispatcherTimer { Tag = service };
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            var timer = (DispatcherTimer)sender;
            var service = (MacroserviceGridItem)timer.Tag;
            service.ProcId = getListeningPortProcessId(service.Port.To<int>());
            if (service.ProcId < 0) return;

            timer.Stop();
            timer.Tick -= DispatcherTimer_Tick;
            service.Status = MacroserviceGridItem.enumStatus.Run;
            autoRefreshTimer.Start();
        }

        void OpenVs(MacroserviceGridItem service, FileInfo solutionFile)
        {
            var dte2 = service.VsDTE ?? GetVSDTE(solutionFile);
            if (dte2 != null)
            {
                dte2.MainWindow.Visible = true;
                dte2.MainWindow.SetFocus();
            }
            else
                Process.Start(solutionFile.FullName);
        }

        static DTE2 GetVSDTE(MacroserviceGridItem service)
        {
            return GetVSDTE(GetServiceSolutionFilePath(service));
        }

        static DTE2 GetVSDTE(FileInfo solutionFile)
        {
            if (solutionFile == null)
                return null;
            try
            {
                return Helper.GetVsInstances().FirstOrDefault(dte2 => string.Equals(dte2.Solution.FullName, solutionFile.FullName, StringComparison.CurrentCultureIgnoreCase));
            }
            catch (Exception)
            {
                return null;
            }

        }

        void OpenCode_OnClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var btn = (Button)sender;
            var serviceName = btn.Tag.ToString();
            var service = MacroserviceGridItems.Single(s => s.Service == serviceName);
            var solutionFile = GetServiceSolutionFilePath(service);
            OpenVs(service, solutionFile);
        }

        static FileInfo GetServiceSolutionFilePath(MacroserviceGridItem service)
        {
            return !Directory.Exists(service.WebsiteFolder) ? null : service.WebsiteFolder.AsDirectory().Parent?.GetFiles("*.sln").FirstOrDefault();
        }

        async void OpenProject_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog()
            {
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = $@"Services JSON File |{services_file_name}",
                RestoreDirectory = true,
                Multiselect = false,
                SupportMultiDottedExtensions = true,
                Title = $@"Select {services_file_name} file"
            };
            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            if (_recentFiles.Count == 0 || !_recentFiles.Contains(openFileDialog.FileName))
            {
                if (_recentFiles.Count == 0)
                    mnuRecentFiles.Items.Clear();

                _recentFiles.Add(openFileDialog.FileName);
                AddRecentMenuItem(openFileDialog.FileName);

                SaveRecentFilesXml();
            }

            await LoadFile(openFileDialog.FileName);
        }

        async Task Refresh()
        {
            if(!projextLoaded)
                autoRefreshTimer.Stop();

            if (servicesJsonFile != null)
            {
                await RefreshFile(servicesJsonFile.FullName);
            }
        }

        void EditProject_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (servicesJsonFile != null)
                Process.Start("Notepad.exe", servicesJsonFile.FullName);
        }

        void CloseMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        async void RefreshMenuItem_OnClick(object sender, ExecutedRoutedEventArgs e)
        {
            await Refresh();
        }

        void OpenExplorer_OnClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var btn = (Button)sender;
            var serviceName = btn.Tag.ToString();
            var service = MacroserviceGridItems.Single(s => s.Service == serviceName);
            Process.Start(service.WebsiteFolder.AsDirectory().Parent?.FullName ?? throw new Exception("Macroservice WebsiteFolder Not Exists ..."));

        }
        void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterListBy(txtSearch.Text);

        }

        void VsDebuggerAttach_OnClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var btn = (Button)sender;
            var serviceName = btn.Tag.ToString();
            var service = MacroserviceGridItems.Single(s => s.Service == serviceName);

            if (service.VsDTE.Mode == vsIDEMode.vsIDEModeDebug)
            {
                service.VsDTE.Debugger.DetachAll();
                return;
            }

            var processes = service.VsDTE.Debugger.LocalProcesses.OfType<EnvDTE.Process>();
            var process = processes.SingleOrDefault(x => x.ProcessID == service.ProcId);
            if (process == null) return;

            OpenVs(service, GetServiceSolutionFilePath(service));
            process.Attach();



        }

        void RunAllMenuItem_Click(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var service in MacroserviceGridItems)
                if (service.Status == MacroserviceGridItem.enumStatus.Stop)
                    StartService(service);
        }

        void StopAllMenuItem_Click(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var service in MacroserviceGridItems)
            {
                if (service.Status == MacroserviceGridItem.enumStatus.Run)
                    StopService(service);
            }
        }

        void RunAllFilteredMenuItem_Click(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var service in MacroserviceGridItems)
                if (service.Status == MacroserviceGridItem.enumStatus.Stop)
                    StartService(service);

        }

        void StopAllFilteredMenuItem_Click(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var service in MacroserviceGridItems)
            {
                if (service.Status == MacroserviceGridItem.enumStatus.Run)
                    StopService(service);
            }
        }

        async void WindowTitlebarControl_OnRefreshClicked(object sender, EventArgs e)
        {
            await Refresh();
        }

        void MnuAlwaysOnTop_OnChecked(object sender, RoutedEventArgs e)
        {
            Topmost = mnuAlwaysOnTop.IsChecked;
        }

        void WindowOpacityMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem)sender;

            var template = SliderMenuItem.Template;
            var slider = (Slider)template.FindName("OpacitySlider", SliderMenuItem);
            slider.Value = menuItem.Header.ToString().TrimEnd('%').To<int>();
        }

        void OpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = e.NewValue / 100d;
            foreach (MenuItem item in OpacityMenuItem.Items)
            {
                if (item.Header == null || !item.Header.ToString().EndsWith("%"))
                    continue;

                item.IsChecked = Math.Abs(item.Header.ToString().TrimEnd('%').To<int>() - e.NewValue) < .0001;
            }
        }
    }
}

namespace System
{
    public static class TempExtDeleteMeAfterNugetUpdate
    {
        /// <summary>
        /// It will search in all environment PATH directories, as well as the current directory, to find this file.
        /// For example for 'git.exe' it will return `C:\Program Files\Git\bin\git.exe`.
        /// </summary>
        public static FileInfo AsFile(this string exe, bool searchEnvironmentPath)
        {
            if (!searchEnvironmentPath) return exe.AsFile();

            var result = Environment.ExpandEnvironmentVariables(exe).AsFile();
            if (result.Exists()) return result;

            if (Path.GetDirectoryName(exe).IsEmpty())
            {
                var environmentFolders = Environment.GetEnvironmentVariable("PATH").OrEmpty().Split(';').Trim();
                foreach (var test in environmentFolders)
                {
                    result = Path.Combine(test, exe).AsFile();
                    if (result.Exists()) return result;
                }
            }

            throw new FileNotFoundException(new FileNotFoundException().Message, exe);
        }
    }
}