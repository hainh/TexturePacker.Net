using System;
using System.ComponentModel;

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

        const double H = 110;

        public double GetScaleFromSiler()
        {
            double scale = zoomSliderValue;
            if (scale < H)
            {
                return scale * 0.9 / H + 0.1;
            }
            else if (scale == H)
            {
                return 1.0;
            }
            else
            {
                return (scale - H) * 9 / H + 1;
            }
        }

        private double TransformTextBoxValueToSliderValue()
        {
            string text = zoomTxbValue;
            int i = 1;
            int scale = -1;
            while (i <= text.Length && int.TryParse(text.Substring(0, i++), out scale))
            {
            }

            int scale1 = Math.Min(Math.Max(scale, 10), 1000);
            if (scale1 < 100)
            {
                return (scale1 - 10) * H / 90;
            }
            else if (scale1 == 100)
            {
                return H;
            }
            else
            {
                return (scale1 - 100) * H / 900 + H;
            }
        }

        private double zoomSliderValue;
        public double ZoomSliderValue
        {
            get => zoomSliderValue;
            set
            {
                zoomSliderValue = value;

                double scale = GetScaleFromSiler();
                scale *= 100;
                zoomTxbValue = scale.ToString("#");

                NotifyPropertyChanged(nameof(ZoomSliderValue));
                NotifyPropertyChanged(nameof(ZoomTxbValue));
            }
        }

        private string zoomTxbValue;
        public string ZoomTxbValue
        {
            get => zoomTxbValue;
            set
            {
                zoomTxbValue = value;

                zoomSliderValue = TransformTextBoxValueToSliderValue();

                NotifyPropertyChanged(nameof(ZoomSliderValue));
                NotifyPropertyChanged(nameof(ZoomTxbValue));
            }
        }
    }
}
