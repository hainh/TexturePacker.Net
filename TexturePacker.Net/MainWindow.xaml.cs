using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using static System.Windows.SystemParameters;

namespace TexturePacker.Net
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; }
        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            Dispatcher.InvokeAsync(async() =>
            {
                await Task.Delay(1);
                LoadDeviceScale();
            });
        }

        public double ScaleX { get; private set; }
        
        public double ScaleY { get; private set; }

        private void LoadDeviceScale()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            System.Windows.Media.Matrix matrix = source.CompositionTarget.TransformToDevice;
            ScaleX = matrix.M11;
            ScaleY = matrix.M22;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            LoadDeviceScale();

            var open = new OpenFileDialog
            {
                FileName = "This directory",
                Filter = "Add|This Directory",
                Title = "Add Directory",
                ValidateNames = false,
                CheckFileExists = false, 
            };
            if (open.ShowDialog()?.Equals(true)??false)
            {
                var directory = Path.GetDirectoryName(open.FileName);
                Task.Run(() =>
                {
                    var start = DateTime.UtcNow;
                    var duration = DateTime.UtcNow - start;
                    ItemGroup itemGroup = new ItemGroup(directory, directory, Path.GetFileName(directory));
                    Dispatcher.Invoke(() =>
                    {
                        LoggerText.Content = $"Find {duration.TotalSeconds:0.###}s";
                        trvImages.ItemsSource = new List<ItemGroup>() { itemGroup };
                    });

                    Task.Run(() =>
                    {
                        var start = DateTime.UtcNow;
                        foreach (Item item in itemGroup.GetAllItems(true))
                        {
                            item.LoadImage();
                        }
                        Dispatcher.Invoke(() =>
                        {
                            var binding = trvImages.GetBindingExpression(ItemsControl.ItemsSourceProperty);
                            binding.UpdateSource();
                            LoggerText.Content += $" | Load {(DateTime.UtcNow - start).TotalSeconds:0.###}s";
                        });
                    });
                });
            }
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
