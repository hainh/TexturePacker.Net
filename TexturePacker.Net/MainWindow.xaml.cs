using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Windows.SystemParameters;

namespace TexturePacker.Net
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
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
                //GenerateTestImgs();
            });

            MainCanvas.SizeChanged += MainCanvas_SizeChanged;
        }

        private void MainCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            mainScrollViewer.Width = MainCanvas.ActualWidth;
            mainScrollViewer.Height = MainCanvas.ActualHeight;
        }

        private List<Image> Images;

        public double ScaleX { get; private set; }
        
        public double ScaleY { get; private set; }

        public double Progress { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChange(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
            RenderOptions.SetBitmapScalingMode(imagesCanvas, BitmapScalingMode.NearestNeighbor);

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

                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Value = 0;
                    double left = (MainCanvas.ActualWidth - progressBar.ActualWidth) / 2;
                    Canvas.SetLeft(progressBar, left);
                    double top = (MainCanvas.ActualHeight - progressBar.ActualHeight) / 2;
                    Canvas.SetTop(progressBar, top);

                    Task.Run(() => LoadImagesAndTreeView(itemGroup));
                }, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private async void LoadImagesAndTreeView(ItemGroup itemGroup)
        {
            await Task.Delay(60);
            DateTime start = DateTime.UtcNow;
            List<Item> allItems = itemGroup.GetAllItems(true).ToList();
            foreach (Item item in allItems)
            {
                item.LoadImage();
            }
            double loadSecs = (DateTime.UtcNow - start).TotalSeconds;
            Dispatcher.Invoke(() =>
            {
                foreach (Item item in allItems)
                {
                    item.NotifyPropertyChanged(nameof(Item.Thumbnail));
                }
                LoggerText.Content += $" | Load {loadSecs:0.###}s";

                Task.Run(() => Pack(allItems));
            }, System.Windows.Threading.DispatcherPriority.Send);
        }

        private /*async*/ void Pack(List<Item> allItems)
        {
            //await Task.Delay(100);
            DateTime packTime = DateTime.UtcNow;
            List<Packager.Rect> rects = allItems.Select(item => item.SpriteRect).ToList();
            Packager.Option option = new Packager.Option
            {
                AllowRotation = true,
                MaxSide = 2048
            };
            DataModel dataModel = new DataModel();
            Packager.ProgressRef progress = new Packager.ProgressRef(dataModel);
            Dispatcher.Invoke(() => progressBar.DataContext = dataModel);
            List<Packager.Rect> result = Packager.Packager.Pack(rects, option, out int width, out int height, progress);
            double packSecs = (DateTime.UtcNow - packTime).TotalSeconds;
            Dispatcher.Invoke(() =>
            {
                foreach (Item item in allItems)
                {
                    item.UpdateThumbnail();
                    item.NotifyPropertyChanged(nameof(Item.Thumbnail));
                }
                LoggerText.Content += $" | Pack {packSecs:0.###}s";
            });
            if (result != null)
            {
                //WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
                //foreach (var rect in result)
                //{
                //    BitmapImage image = (rect.Item as Item).RawImage;
                //    if (rect.Rotated)
                //    {
                //        writeableBitmap.Blit(
                //            new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                //            new WriteableBitmap(image).Rotate(90),
                //            new Rect(0, 0, rect.Width, rect.Height));
                //    }
                //    else
                //    {
                //        writeableBitmap.Blit(
                //            new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                //            new WriteableBitmap(image),
                //            new Rect(0, 0, rect.Width, rect.Height));
                //    }
                //}
                //writeableBitmap.Freeze();
                Dispatcher.Invoke(() =>
                {
                    DateTime renderTime = DateTime.UtcNow;
                    Images = new List<Image>();
                    imagesCanvas.Width = width;
                    imagesCanvas.Height = height;
                    foreach (var rect in result)
                    {
                        BitmapImage bmImage = (rect.Item as Item).RawImage;
                        Image image = new Image();
                        image.Source = bmImage;
                        if (rect.Rotated)
                        {
                            image.LayoutTransform = new RotateTransform(90, 0.5, 0.5);
                        }
                        imagesCanvas.Children.Add(image);
                        Canvas.SetLeft(image, rect.X);
                        Canvas.SetTop(image, rect.Y);
                    }
                    double scale = 1.5;
                    imagesCanvas.LayoutTransform = new ScaleTransform(scale, scale);
                    Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        Dispatcher.Invoke(() =>
                        {
                            if (imagesCanvas.ActualHeight * scale > mainScrollViewer.ActualHeight)
                            {
                                mainScrollViewer.ScrollToVerticalOffset(imagesCanvas.Margin.Top);
                            }
                            if (imagesCanvas.ActualWidth * scale > mainScrollViewer.ActualWidth)
                            {
                                mainScrollViewer.ScrollToHorizontalOffset(imagesCanvas.Margin.Left);
                            }
                        });
                    });
                    double renderSecs = (DateTime.UtcNow - renderTime).TotalSeconds;
                    LoggerText.Content += $" | Render {renderSecs:0.###}s";
                    LoggerResult.Content = $"{width}x{height} {result.Sum(r => r.Width * r.Height) / (float)width / height:P}";
                    progressBar.Visibility = Visibility.Hidden;
                }, System.Windows.Threading.DispatcherPriority.Send);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Visibility = Visibility.Hidden;
                    LoggerResult.Content = $"Not fit in {option.MaxSide}x{option.MaxSide}";
                }, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private void GenerateTestImgs()
        {
            const int length = 100;
            var random = new Random((int)(new DateTime(2020, 1, 1) - DateTime.UtcNow).TotalHours);
            for (int i = 0; i < length; i++)
            {
                int width = random.Next(10, 200);
                int height = random.Next(10, 200);
                WriteableBitmap wb = BitmapFactory.New(width, height);
                wb.FillRectangle(0, 0, width, height, AllColors[i % AllColors.Length]);
                wb.DrawRectangle(0, 0, width, height, AllColors[(i + 2) % AllColors.Length]);
                wb.DrawLine(0, 0, width, height, AllColors[(i + 3) % AllColors.Length]);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wb));
                using var stream = new FileStream($"img_{i + 1:00}.png", FileMode.Create);
                encoder.Save(stream);
            }
        }

        static MainWindow()
        {
            Type colorsType = typeof(Colors);
            PropertyInfo[] colorsTypePropertyInfos = colorsType.GetProperties(BindingFlags.Public | BindingFlags.Static);
            AllColors =  colorsTypePropertyInfos.Select(propInfo => (Color)propInfo.GetValue(null)).ToArray();
        }
        public static Color[] AllColors { get; }
    }
}
