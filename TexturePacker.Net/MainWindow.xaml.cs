﻿using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
                //GenerateTestImgs();
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
                    DateTime packTime = DateTime.UtcNow;
                    List<Packager.Rect> rects = allItems.Select(item => item.SpriteRect).ToList();
                    List<Packager.Rect> result = Packager.Packager.Pack(rects, out int width, out int height);
                    Dispatcher.Invoke(() =>
                    {
                        foreach (Item item in allItems)
                        {
                            item.NotifyPropertyChanged(nameof(Item.Thumbnail));
                        }
                        LoggerText.Content += $" | Pack {(DateTime.UtcNow - packTime).TotalSeconds:0.###}s";
                    });
                    if (result != null)
                    {
                        DateTime renderTime = DateTime.UtcNow;
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
                        writeableBitmap.Freeze();
                        imgMainCanvas.Dispatcher.Invoke(() =>
                        {
                            LoggerText.Content += $" | Render {(DateTime.UtcNow - renderTime).TotalSeconds:0.###}s";
                            imgMainCanvas.Source = writeableBitmap;
                            imgMainCanvas.Width = writeableBitmap.Width;
                            imgMainCanvas.Height = writeableBitmap.Height;
                            LoggerResult.Content = writeableBitmap.PixelWidth + "x" + writeableBitmap.PixelHeight;
                        });
                    }
                });
            }
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private void GenerateTestImgs()
        {
            const int length = 30;
            var random = new Random((int)(new DateTime(2020, 1, 1) - DateTime.UtcNow).TotalHours);
            for (int i = 0; i < length; i++)
            {
                int width = random.Next(15, 200);
                int height = random.Next(15, 200);
                WriteableBitmap wb = BitmapFactory.New(width, height);
                wb.FillRectangle( 0, 0, width, height, AllColors[i % length]);
                RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(15, 15, 96, 96, PixelFormats.Pbgra32);
                var textView = new TextBlock
                {
                    FontSize = 10,
                    FontWeight = FontWeights.Normal,
                    Text = "" + (i + 1)
                };
                renderTargetBitmap.Render(textView);
                WriteableBitmap textBm = new WriteableBitmap(renderTargetBitmap);
                var rect = new Rect(0, 0, textView.Width, textView.Height);
                wb.Blit(rect, textBm, rect);

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
