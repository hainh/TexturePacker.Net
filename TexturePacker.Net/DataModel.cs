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
    public class DataModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private double progress;
        public double Progress
        {
            get => progress;
            set
            {
                progress = value;
                NotifyPropertyChanged(nameof(Progress));
            }
        }
    }
}
