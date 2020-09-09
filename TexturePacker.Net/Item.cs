using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TexturePacker.Net.Packager;

namespace TexturePacker.Net
{
    /// <summary>
    /// An item's data in TreeView
    /// </summary>
    public class Item : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, BitmapImage> CachedFolderThumbnail
            = new Dictionary<string, BitmapImage>(3);

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
                if (!CachedFolderThumbnail.TryGetValue(fileName, out BitmapImage thumbnail))
                {
                    thumbnail = new BitmapImage(new System.Uri(fileName));
                    CachedFolderThumbnail.Add(fileName, thumbnail);
                    thumbnail.Freeze();
                }
                Thumbnail = thumbnail;
            }
            FileName = fileName;
            RelativePath = relativePath;
            IsDirectory = isDirectory;
        }

        public FileInfo FileInfo { get; private set; }

        public Rect SpriteRect { get; private set; }

        public void LoadImage()
        {
            if (IsDirectory)
            {
                return;
            }
            FileInfo = new FileInfo(FileName);
            var uri = new System.Uri(FileName);
            RawImage = new BitmapImage(uri);
            RawImage.Freeze();
            SpriteRect = new Rect(0, 0, RawImage.PixelWidth, RawImage.PixelHeight) { Item = this };
        }

        public void UpdateThumbnail()
        {
            Thumbnail = RawImage;
        }
    }
}
