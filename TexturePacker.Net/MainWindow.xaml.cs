using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
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
            LoggerText.Content = string.Empty;
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
                var start = DateTime.UtcNow;
                ItemGroup itemGroup = new ItemGroup(directory, directory, Path.GetFileName(directory));
                var duration = DateTime.UtcNow - start;
                Dispatcher.Invoke(() =>
                {
                    LoggerText.Content = $"Find {duration.TotalSeconds:0.###}s";
                    trvImages.ItemsSource = new List<ItemGroup>() { itemGroup };
                });

                Task.Run(async () =>
                {
                    await Task.Delay(60);
                    DateTime start = DateTime.UtcNow;
                    List<Item> allItems = itemGroup.GetAllItems(true).ToList();
                    foreach (Item item in allItems)
                    {
                        item.LoadImage();
                    }
                    Dispatcher.Invoke(() =>
                    {
                        foreach (Item item in allItems)
                        {
                            item.NotifyPropertyChanged(nameof(Item.Thumbnail));
                        }
                        LoggerText.Content += $" | Load {(DateTime.UtcNow - start).TotalSeconds:0.###}s";
                    });
                    List<Packager.Rect> rects = allItems.Select(item => item.SpriteRect).ToList();
                    List<Packager.Rect> result = Packager.Packager.Pack(rects, out int width, out int height);
                    if (result != null)
                    {
                        WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
                        foreach (var rect in result)
                        {
                            BitmapImage image = (rect.Item as Item).RawImage;
                            if (rect.Rotated)
                            {
                                writeableBitmap.Blit(
                                    new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                                    new WriteableBitmap(image).Rotate(90),
                                    new Rect(0, 0, rect.Width, rect.Height));
                            }
                            else
                            {
                                writeableBitmap.Blit(
                                    new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                                    new WriteableBitmap(image),
                                    new Rect(0, 0, rect.Width, rect.Height));
                            }
                        }
                        BitmapImage bmImage = new BitmapImage();
                        using (MemoryStream stream = new MemoryStream())
                        {
                            PngBitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                            encoder.Save(stream);
                            bmImage.BeginInit();
                            bmImage.CacheOption = BitmapCacheOption.OnLoad;
                            bmImage.StreamSource = stream;
                            bmImage.EndInit();
                            bmImage.Freeze();
                        }
                        imgMainCanvas.Dispatcher.Invoke(() =>
                        {
                            imgMainCanvas.Source = bmImage;
                            //imgMainCanvas.Width = bmImage.Width;
                            //imgMainCanvas.Height = bmImage.Height;
                            LoggerResult.Content = bmImage.PixelWidth + "x" + bmImage.PixelHeight;
                        });
                    }
                });
            }
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
