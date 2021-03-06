﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MicroserviceExplorer
{
    /// <summary>
    /// Interaction logic for LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window, INotifyPropertyChanged
    {
        public static readonly DependencyProperty TextLogProperty =
            DependencyProperty.Register("TextLog", typeof(string), typeof(Window) /*, new PropertyMetadata(false) */);

        private MicroserviceItem _servic;

        public LogWindow()
        {
            InitializeComponent();
            if (Servic != null)
                SaveLogMenuItem.Header = $"Save to {Servic.Service}_Log.txt";
        }

        void LogWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }

        public string TextLog
        {
            get => (string)GetValue(TextLogProperty);
            set
            {
                var offset = txtLog.VerticalOffset;
                SetValue(TextLogProperty, value);
                OnPropertyChanged(nameof(TextLog));
                if (IsFocused || Visibility != Visibility.Visible) return;

                txtLog.ScrollToVerticalOffset(offset);
            }
        }

        public MicroserviceItem Servic
        {
            get { return _servic; }
            set
            {
                _servic = value;
                SaveLogMenuItem.Header = $"Save to {Servic.Service}_Log.txt";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void LogMessage(string message, string description = null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new MainWindow.MyDelegate(() =>
            {
                TextLog += $"-{DateTime.Now.ToLongTimeString()}: \t{message}{Environment.NewLine}";
                if (description.HasValue())
                    TextLog += "decription : \t" + description?.Replace("\n", "\n\t\t") + Environment.NewLine;

            }));

        }

        public void SetTheLogWindowBy(MainWindow mainWindow)
        {
            const int border = 7;
            Top = mainWindow.Top;
            Height = mainWindow.Height + border;
            Left = mainWindow.Left + mainWindow.Width - border;

        }

        void ClearLogMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            TextLog = null;
        }


        void SaveLogMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            File.WriteAllText($"{Servic.Service}_Log_{LocalTime.Now.ToShortDateString().Replace('\\', '-').Replace('/', '-')}_{LocalTime.Now.ToShortTimeString().Replace(':', '-')}.txt", TextLog);

        }
    }

    public class MyContext
    {
        public string TextLog { get; set; }
    }
}
