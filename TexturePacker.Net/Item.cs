using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace TexturePacker.Net
{
    /// <summary>
    /// An item's data in TreeView
    /// </summary>
    public class Item
    {
        private static readonly Dictionary<string, BitmapImage> CachedFolderThumbnail
            = new Dictionary<string, BitmapImage>(5);

        public virtual string Name => Path.GetFileName(FileName);
        public string FileName { get; private set; }

        public virtual object Thumbnail { get; protected set; }

        public bool IsDirectory { get; }

        public Item(string fileName, bool isDirectory = false)
        {
            if (isDirectory)
            {
                if (!CachedFolderThumbnail.TryGetValue(fileName, out BitmapImage thumbnail))
                {
                    thumbnail = new BitmapImage();
                    thumbnail.BeginInit();
                    thumbnail.UriSource = new System.Uri(fileName);
                    thumbnail.DecodePixelWidth = (int)(20 * MainWindow.Instance.ScaleX);
                    thumbnail.DecodePixelHeight = (int)(15 * MainWindow.Instance.ScaleY);
                    thumbnail.EndInit();
                    CachedFolderThumbnail.Add(fileName, thumbnail);
                }
                Thumbnail = thumbnail;
            }
            else
            {
                Thumbnail = fileName;
            }
            FileName = fileName;
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

        public void LoadImage()
        {
            if (IsDirectory)
            {
                return;
            }
            FileInfo = new FileInfo(FileName);
            if (FileInfo.Length > 2048 || true)
            {
                BitmapImage thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.UriSource = new System.Uri(FileName);
                thumbnail.DecodePixelWidth = (int)(20 * MainWindow.Instance.ScaleX);
                thumbnail.DecodePixelHeight = (int)(15 * MainWindow.Instance.ScaleY);
                thumbnail.EndInit();
                Thumbnail = thumbnail;
            }
            else
            {
                Thumbnail = FileName;
            }
        }
    }
}
