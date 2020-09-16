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

            double left = (mainCanvas.ActualWidth - progressBar.ActualWidth) / 2;
            Canvas.SetLeft(progressBar, left);
            double top = (mainCanvas.ActualHeight - progressBar.ActualHeight) / 2;
            Canvas.SetTop(progressBar, top);
        }

        private DataModel Data;

        private List<Packager.Rect> Rects;

        public double ScaleX { get; private set; }
        
        public double ScaleY { get; private set; }

        public double Progress { get; set; }

        private readonly SolidColorBrush outlineStroke = new SolidColorBrush(new Color() { A = 255, R = 0, G = 0, B = 255 });
        private readonly SolidColorBrush outlineFill = new SolidColorBrush(new Color() { A = 48, R = 0, G = 0, B = 255 });
        private readonly SolidColorBrush unselectedOutlineFill = new SolidColorBrush(new Color() { A = 32, R = 0, G = 0, B = 255 });
        private System.Windows.Shapes.Rectangle dashedOutline;

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

            double thickness = GetOutlineThickness(scale);
            foreach (var item in imagesCanvas.Children)
            {
                if (item is System.Windows.Shapes.Rectangle rectangle)
                {
                    Packager.Rect rect = (Packager.Rect)rectangle.DataContext;
                    rectangle.StrokeThickness = thickness;
                    rectangle.Width = rect.Width + thickness;
                    rectangle.Height = rect.Height + thickness;
                    Canvas.SetLeft(rectangle, rect.X - thickness / 2);
                    Canvas.SetTop(rectangle, rect.Y - thickness / 2);
                }
                else if (item is System.Windows.Shapes.Line diagonal)
                {
                    diagonal.StrokeThickness = thickness * 0.6;
                }
            }
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

            int value = e.Delta > 0 ? Math.Max(1, e.Delta / 30) : Math.Min(-1, e.Delta / 30);
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
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(500);
                updateThumbnail();
            });

            Rects = null;
            DataModel dataModel = new DataModel();
            Packager.ProgressRef progress = new Packager.ProgressRef(dataModel);
            Dispatcher.Invoke(() => progressBar.DataContext = dataModel);
            List<Packager.Rect> result = Packager.Packager.Pack(rects, option, out int width, out int height, progress);
            double packSecs = (DateTime.UtcNow - packTime).TotalSeconds;

            Dispatcher.InvokeAsync(async() =>
            {
                updateThumbnail();
                LoggerText.Content += $" | Pack {packSecs:0.###}s";

                if (result != null)
                {
                    DateTime renderTime = DateTime.UtcNow;
                    await Render(result, width, height);

                    double renderSecs = (DateTime.UtcNow - renderTime).TotalSeconds;
                    LoggerText.Content += $" | Render {renderSecs:0.###}s";
                    LoggerResult.Content = $"{width}x{height} {result.Sum(r => r.Width * r.Height) / (float)width / height:P}";
                    progressBar.Visibility = Visibility.Hidden;

                }
                else
                {
                    progressBar.Visibility = Visibility.Hidden;
                    LoggerResult.Content = $"Not fit in {option.MaxSide}x{option.MaxSide}";

                }
            }, System.Windows.Threading.DispatcherPriority.Send);
        }

        private async Task Render(List<Packager.Rect> result, int width, int height)
        {
            Rects = result; ;
            imagesCanvas.Width = width;
            imagesCanvas.Height = height;
            imagesCanvas.Children.RemoveRange(0, imagesCanvas.Children.Count);
            dashedOutline = null;
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
                rect.Item = new ItemsOfRect { Image = image };
            }
            DrawOutline();

            await Task.Delay(30);
            double scale = Data.GetScaleFromSiler();
            if (imagesCanvas.ActualHeight * scale > mainScrollViewer.ActualHeight)
            {
                mainScrollViewer.ScrollToVerticalOffset(imagesCanvas.Margin.Top);
            }
            if (imagesCanvas.ActualWidth * scale > mainScrollViewer.ActualWidth)
            {
                mainScrollViewer.ScrollToHorizontalOffset(imagesCanvas.Margin.Left);
            }
        }

        static double GetOutlineThickness(double scale)
        {
            return 1 / scale;
        }

        private void DrawOutline()
        {
            if (!outlineCb.IsChecked.HasValue || outlineCb.IsChecked.Value == false || Rects == null)
            {
                return;
            }
            double scale = Data.GetScaleFromSiler();
            double thickness = GetOutlineThickness(scale);
            foreach (var rect in Rects)
            {
                System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle
                {
                    Width = rect.Width + thickness,
                    Height = rect.Height + thickness,
                    Stroke = outlineStroke,
                    Fill = outlineFill,
                    StrokeThickness = thickness,
                    DataContext = rect
                };
                System.Windows.Shapes.Line diagonal = new System.Windows.Shapes.Line()
                {
                    X1 = rect.X,
                    Y1 = rect.Rotated ? rect.Y : rect.FarY,
                    X2 = rect.FarX,
                    Y2 = rect.Rotated ? rect.FarY : rect.Y,
                    Stroke = outlineStroke,
                    StrokeThickness = thickness * 0.6,
                    DataContext = rect
                };
                RenderOptions.SetEdgeMode(rectangle, EdgeMode.Aliased);

                imagesCanvas.Children.Add(rectangle);
                Canvas.SetLeft(rectangle, rect.X - thickness / 2);
                Canvas.SetTop(rectangle, rect.Y - thickness / 2);
                imagesCanvas.Children.Add(diagonal);

                ItemsOfRect itemsOfRect = rect.Item as ItemsOfRect;
                itemsOfRect.Rectangle = rectangle;
                itemsOfRect.Diagonal = diagonal;
            }
        }

        private void RemoveOutline()
        {
            if ((outlineCb.IsChecked.HasValue && outlineCb.IsChecked.Value == true) || Rects == null)
            {
                return;
            }
            imagesCanvas.Children.RemoveRange(Rects.Count, imagesCanvas.Children.Count - Rects.Count);
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

        private void ImagesCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //e.Handled = true;
            if (Rects == null || e.MiddleButton == MouseButtonState.Pressed)
            {
                return;
            }
            var mousePos = Mouse.GetPosition(imagesCanvas);
            var mousePoint = new Packager.Rect((int)mousePos.X, (int)mousePos.Y, 0, 0);
            Packager.Rect found = null;
            foreach (var rect in Rects)
            {
                if (rect.Contains(mousePoint))
                {
                    found = rect;
                    break;
                }
            }
            
            bool outline = outlineCb.IsChecked.HasValue && outlineCb.IsChecked.Value == true;
            if (found != null)
            {
                ItemsOfRect selectedItems = found.Item as ItemsOfRect;
                Rects.ForEach(rect =>
                {
                    var items = rect.Item as ItemsOfRect;
                    items.Image.Opacity = 0.5;
                    if (outline)
                    {
                        items.Rectangle.Fill = unselectedOutlineFill;
                    }
                });
                selectedItems.Image.Opacity = 1;
                if (outline)
                {
                    selectedItems.Rectangle.Fill = outlineFill;
                }
                else
                {
                    if (dashedOutline == null)
                    {
                        dashedOutline = new System.Windows.Shapes.Rectangle()
                        {
                            StrokeThickness = 1,
                            StrokeDashArray = new DoubleCollection(new double[] { 5, 4 }),
                            Stroke = Brushes.Black
                        };
                        RenderOptions.SetEdgeMode(dashedOutline, EdgeMode.Aliased);
                        imagesCanvas.Children.Add(dashedOutline);
                    }
                    dashedOutline.DataContext = found;
                    double scale = Data.GetScaleFromSiler();
                    double thickness = GetOutlineThickness(scale);
                    dashedOutline.Width = found.Width + thickness;
                    dashedOutline.Height = found.Height + thickness;
                    Canvas.SetLeft(dashedOutline, found.X - thickness / 2);
                    Canvas.SetTop(dashedOutline, found.Y - thickness / 2);
                }
            }
            else
            {
                Rects.ForEach(rect =>
                {
                    var items = rect.Item as ItemsOfRect;
                    items.Image.Opacity = 1;
                    if (outline)
                    {
                        items.Rectangle.Fill = outlineFill;
                    }
                });
                if (dashedOutline != null)
                {
                    imagesCanvas.Children.Remove(dashedOutline);
                    dashedOutline = null;
                }
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

        private void MinusBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Data.ZoomSliderValue = Math.Max(zoomSlider.Minimum, Math.Min(zoomSlider.Maximum, Data.ZoomSliderValue - 3));
        }

        private void PlusBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Data.ZoomSliderValue = Math.Max(zoomSlider.Minimum, Math.Min(zoomSlider.Maximum, Data.ZoomSliderValue + 3));
        }

        private void OneByOneBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Data.ZoomSliderValue = (zoomSlider.Minimum + zoomSlider.Maximum) / 2;
        }

        private void FitBtn_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            double scaleX = mainCanvas.ActualWidth / imagesCanvas.ActualWidth;
            double scaleY = mainCanvas.ActualHeight / imagesCanvas.ActualHeight;
            double scale = Math.Min(scaleX, scaleY);

            double hScrollBarHeight = 0;
            double vSrollBarWidth = 0;

            if (scale * imagesCanvas.ActualWidth + imagesCanvas.Margin.Left + imagesCanvas.Margin.Right > mainCanvas.ActualWidth
                && scale * imagesCanvas.ActualHeight + imagesCanvas.Margin.Top + imagesCanvas.Margin.Bottom > mainCanvas.ActualHeight)
            {
                vSrollBarWidth = SystemParameters.VerticalScrollBarWidth;
                hScrollBarHeight = SystemParameters.HorizontalScrollBarHeight;
                scaleX = (mainCanvas.ActualWidth - vSrollBarWidth) / imagesCanvas.ActualWidth;
                scaleY = (mainCanvas.ActualHeight - hScrollBarHeight) / imagesCanvas.ActualHeight;
                scale = Math.Min(scaleX, scaleY);
            }

            Data.ZoomTxbValue = Math.Floor(scale * 100).ToString();
            scale = int.Parse(Data.ZoomTxbValue) / 100.0;
            mainScrollViewer.ScrollToVerticalOffset(imagesCanvas.Margin.Top - (mainCanvas.ActualHeight - hScrollBarHeight - imagesCanvas.ActualHeight * scale) / 2);
            mainScrollViewer.ScrollToHorizontalOffset(imagesCanvas.Margin.Left - (mainCanvas.ActualWidth - vSrollBarWidth - imagesCanvas.ActualWidth * scale) / 2);
        }

        Point lastMouseWheelPressedPos = new Point(int.MinValue, 0);
        private void ImagesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                var currentPos = Mouse.GetPosition(mainCanvas);
                if (lastMouseWheelPressedPos.X != int.MinValue)
                {
                    mainScrollViewer.ScrollToHorizontalOffset(mainScrollViewer.HorizontalOffset - (currentPos.X - lastMouseWheelPressedPos.X));
                    mainScrollViewer.ScrollToVerticalOffset(mainScrollViewer.VerticalOffset - (currentPos.Y - lastMouseWheelPressedPos.Y));
                }
                lastMouseWheelPressedPos = currentPos;
                Cursor = Cursors.SizeAll;
            }
            else
            {
                lastMouseWheelPressedPos.X = int.MinValue;
                if (Cursor == Cursors.SizeAll)
                {
                    Cursor = Cursors.Arrow;
                }
            }
        }

        private void ImagesCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Cursor == Cursors.SizeAll)
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void OutlineCb_Checked(object sender, RoutedEventArgs e)
        {
            DrawOutline();
            RemoveOutline();
        }

        class ItemsOfRect
        {
            public Image Image;
            public System.Windows.Shapes.Rectangle Rectangle;
            public System.Windows.Shapes.Line Diagonal;
        }
    }
}
