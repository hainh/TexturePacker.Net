using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

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
        }

        private void MainCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            mainScrollViewer.Width = mainCanvas.ActualWidth;
            mainScrollViewer.Height = mainCanvas.ActualHeight;
        }

        private DataModel Data;

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
            Matrix matrix = source.CompositionTarget.TransformToDevice;
            ScaleX = matrix.M11;
            ScaleY = matrix.M22;
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double scale = Data.GetScaleFromSiler();
            imagesCanvas.LayoutTransform = new ScaleTransform(scale, scale);
        }

        private void ZoomTxb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int d = e.Key switch
            {
                Key.Up => 1,
                Key.Down => -1,
                Key.PageUp => 10,
                Key.PageDown => -10,
                _ => 0
            };
            if (d != 0)
            {
                Data.ZoomSliderValue = Math.Max(zoomSlider.Minimum, Math.Min(zoomSlider.Maximum, Data.ZoomSliderValue + d));
                e.Handled = true;
            }
        }

        private void ImagesCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            Point position = Mouse.GetPosition(imagesCanvas);
            double scale0 = Data.GetScaleFromSiler();

            int value = e.Delta > 0 ? Math.Max(1, e.Delta / 120) : Math.Min(-1, e.Delta / 120);
            Data.ZoomSliderValue = Math.Max(zoomSlider.Minimum, Math.Min(zoomSlider.Maximum, Data.ZoomSliderValue + value));
            double scale1 = Data.GetScaleFromSiler();

            double sw = mainScrollViewer.ScrollableWidth;
            if (sw > 0)
            {
                mainScrollViewer.ScrollToHorizontalOffset(mainScrollViewer.HorizontalOffset + position.X * (scale1 - scale0));
            }

            double sh = mainScrollViewer.ScrollableHeight;
            if (sh > 0)
            {
                mainScrollViewer.ScrollToVerticalOffset(mainScrollViewer.VerticalOffset + position.Y * (scale1 - scale0));
            }
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

                    progressBar.Visibility = Visibility.Visible;
                    progressBar.Value = 0;
                    double left = (mainCanvas.ActualWidth - progressBar.ActualWidth) / 2;
                    Canvas.SetLeft(progressBar, left);
                    double top = (mainCanvas.ActualHeight - progressBar.ActualHeight) / 2;
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

        private void Pack(List<Item> allItems)
        {
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
            bool updatedThumbnail = false;
            void updateThumbnail()
            {
                if (!updatedThumbnail)
                {
                    foreach (Item item in allItems)
                    {
                        item.UpdateThumbnail();
                        item.NotifyPropertyChanged(nameof(Item.Thumbnail));
                    }
                    updatedThumbnail = true;
                }
            }
            Dispatcher.Invoke(() =>
            {
                updateThumbnail();
                LoggerText.Content += $" | Pack {packSecs:0.###}s";
            });
            Dispatcher.InvokeAsync(async() =>
            {
                await Task.Delay(500);
                updateThumbnail();
            });
            if (result != null)
            {
                Dispatcher.Invoke(() =>
                {
                    DateTime renderTime = DateTime.UtcNow;
                    Images = new List<Image>();
                    imagesCanvas.Width = width;
                    imagesCanvas.Height = height;
                    foreach (var rect in result)
                    {
                        BitmapImage bmImage = (rect.Item as Item).RawImage;
                        Image image = new Image
                        {
                            Source = bmImage
                        };
                        if (rect.Rotated)
                        {
                            image.LayoutTransform = new RotateTransform(90, 0.5, 0.5);
                        }
                        imagesCanvas.Children.Add(image);
                        Canvas.SetLeft(image, rect.X);
                        Canvas.SetTop(image, rect.Y);
                    }
                    Task.Run(async () =>
                    {
                        await Task.Delay(10);
                        Dispatcher.Invoke(() =>
                        {
                            double scale = Data.GetScaleFromSiler();
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RenderOptions.SetBitmapScalingMode(imagesCanvas, BitmapScalingMode.NearestNeighbor);
            LoggerText.Content = string.Empty;
            LoadDeviceScale();
            //GenerateTestImgs();
            Data = new DataModel()
            {
                ZoomSliderValue = 110,
                ZoomTxbValue = "100"
            };
            zoomSlider.DataContext = Data;
            zoomTxb.DataContext = Data;
        }
    }
}
