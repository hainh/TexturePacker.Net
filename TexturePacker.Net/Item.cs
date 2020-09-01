using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TexturePacker.Net.Packager;

namespace TexturePacker.Net
{
    /// <summary>
    /// An item's data in TreeView
    /// </summary>
    public class Item : SpriteRect
    {
        private static readonly Dictionary<string, BitmapImage> CachedFolderThumbnail
            = new Dictionary<string, BitmapImage>(5);

        public virtual string Name => Path.GetFileName(FileName);

        /// <summary>
        /// Absolute Filename
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Relative path in Atlas
        /// </summary>
        public string RelativePath { get; protected set; }

        public virtual object Thumbnail { get; protected set; }

        public BitmapImage RawImage { get; private set; }

        public bool IsDirectory { get; }

        public Item(string fileName, string relativePath, bool isDirectory = false)
        {
            if (isDirectory)
            {
                Thumbnail = fileName;
            }
            FileName = fileName;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
        }

        private bool rotated = false;

        public FileInfo FileInfo { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public bool Rotated
        {
            get => rotated;
            set
            {
                if (rotated != value)
                {
                    rotated = value;
                    int temp = Width;
                    Width = Height;
                    Height = temp;
                }
            }
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Sheet { get; set; }

        public void LoadImage()
        {
            if (IsDirectory)
            {
                return;
            }
            FileInfo = new FileInfo(FileName);
            var uri = new System.Uri(FileName);
            RawImage = new BitmapImage(uri);

            bool scaleByWidth = RawImage.PixelWidth / (float)RawImage.PixelHeight > 20 / 15.0f;
            double scale = scaleByWidth
                ? 20 * MainWindow.Instance.ScaleX / RawImage.PixelWidth
                : 15 * MainWindow.Instance.ScaleY / RawImage.PixelHeight;
            Thumbnail = new TransformedBitmap(RawImage, new ScaleTransform(scale, scale));
        }
    }
}
