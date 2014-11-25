using System;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using OsmMapControlLibrary.TileProviders;

namespace OsmMapControlLibrary
{
    /// <summary>
    ///     This class stores information about a tile
    /// </summary>
    public class TileInfo
    {
        /// <summary>
        ///     Stores the maximum allowed zoomvalue
        /// </summary>
        public const int MaxZoom = 16;

        /// <summary>
        ///     Gets or sets the x-coordinate of the tile
        /// </summary>
        public int TileX { get; set; }

        /// <summary>
        ///     Gets or sets the y-coordinate of the tile
        /// </summary>
        public int TileY { get; set; }

        /// <summary>
        ///     Gets or sets the zoom of the tile
        /// </summary>
        public int Zoom { get; set; }

        /// <summary>
        ///     Gets or sets the image of the tile
        /// </summary>
        public Image TileImage { get; set; }

        /// <summary>
        ///     This event is called when loading has been finished
        /// </summary>
        public event EventHandler LoadingFinished;

        /// <summary>
        ///     Loads the image by tileposition
        /// </summary>
        public void LoadImage(ITileProvider tileProvider)
        {
            int tileX = TileX;
            int tileY = TileY;
            var localZoom = (int) Math.Pow(2, Zoom);
            int currentZoom = Zoom;

            while (currentZoom > MaxZoom)
            {
                currentZoom--;
                tileX /= 2;
                tileY /= 2;
            }

            while (tileX < 0)
            {
                tileX += localZoom;
            }

            while (tileY < 0)
            {
                tileY += localZoom;
            }

            while (tileX >= localZoom)
            {
                tileX -= localZoom;
            }

            while (tileY >= localZoom)
            {
                tileY -= localZoom;
            }

            var image = new Image();
            image.Opacity = 0.0;
            Canvas.SetZIndex(image, Zoom);

            TileImage = image;

            Uri uri = tileProvider.GetTileUri(Zoom, tileX, tileY);
            var source = new BitmapImage(uri);

            source.ImageOpened += SourceOnImageOpened;
            image.Source = source;
        }

        private void SourceOnImageOpened(object sender, RoutedEventArgs routedEventArgs)
        {
            // Detach event handler immediately
            var bmi = (BitmapImage) sender;
            bmi.ImageOpened -= SourceOnImageOpened;

            var animation = new DoubleAnimation();
            animation.From = 0.0;
            animation.To = 1.0;
            animation.Duration = TimeSpan.FromSeconds(0.1);

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(storyboard, TileImage);

            // http://msdn.microsoft.com/en-us/library/windows/apps/windows.ui.xaml.media.animation.storyboard.settargetproperty
            // http://msdn.microsoft.com/en-us/library/windows/apps/windows.ui.xaml.propertypath.propertypath
            Storyboard.SetTargetProperty(storyboard, "Image.Opacity");
            storyboard.Begin();

            if (LoadingFinished != null)
            {
                LoadingFinished(this, EventArgs.Empty);
            }
        }

        /// <summary>
        ///     Gets the coordinate of the image
        /// </summary>
        /// <returns>Position of the image</returns>
        public Point GetCoordinates(double zoom)
        {
            double divisor = zoom; //  Math.Pow(2, this.Zoom);
            return new Point(
                (TileX/divisor),
                (TileY/divisor));
        }

        public override bool Equals(object obj)
        {
            var item = obj as TileInfo;
            if (item == null)
            {
                return false;
            }

            return TileX == item.TileX
                   && TileY == item.TileY
                   && Zoom == item.Zoom;
        }

        public override int GetHashCode()
        {
            return TileX.GetHashCode() ^ TileY.GetHashCode() ^ Zoom.GetHashCode();
        }
    }
}