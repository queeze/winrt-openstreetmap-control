using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using OsmMapControlLibrary.TileProviders;

namespace OsmMapControlLibrary
{
    public class OsmMapControl : Canvas
    {
        /// <summary>
        ///     Base zoom to be used to make the map bigger
        /// </summary>
        public const double BaseZoom = 1;

        private readonly ITileProvider _tileProvider = new OsmTileProvider();

        /// <summary>
        ///     Stores a list of all tiles, that shall be loading or have been
        ///     Loading
        /// </summary>
        private readonly List<TileInfo> _addedTiles = new List<TileInfo>();

        /// <summary>
        ///     Stores a list of all tiles, that have not been loaded yet
        /// </summary>
        private readonly List<TileInfo> _notLoadedTiles = new List<TileInfo>();

        /// Defines the energymanager, storing the power available for scrolling
        private readonly ScrollEnergyManager _scrollEnergyManager = new ScrollEnergyManager();

        protected int TilesRetrieved = 0;

        /// <summary>
        ///     Gets or sets the current zoom level
        /// </summary>
        private int _currentZoomLevel;

        /// <summary>
        ///     Gets or sets the current zoom by GUI
        /// </summary>
        private double _targetZoom;

        /// <summary>
        ///     Number of images, that are currently loading
        /// </summary>
        private int _currentlyLoading;

        /// <summary>
        ///     Stores whether the mouse is currently down
        /// </summary>
        private bool _isMouseDown;

        /// <summary>
        ///     Stores the value when the datetime have been clicked last
        /// </summary>
        private DateTime _lastClick = DateTime.MinValue;

        private Point? _lastPosition;

        /// <summary>
        ///     Initializes a new instance of the MainPage class.
        /// </summary>
        public OsmMapControl()
        {
            CurrentPosition = new Point(0.476317347467935, 0.669774812152535);
            TargetPosition = new Point(0.476317347467935, 0.669774812152535);
            CurrentSpeed = new Point(0.00, 0.00);
            MoveToPosition = null;
            CurrentZoomLevel = 9;
            CurrentZoom = 250000;
            TargetZoom = 300000;

            // do not show any rendering in design mode
            if (!DesignMode.DesignModeEnabled)
            {
                CompositionTarget.Rendering += CompositionTarget_Rendering;

                PointerPressed += Map_PointerPressed;
                PointerReleased += Map_PointerReleased;
                PointerMoved += Map_PointerMoved;
                PointerWheelChanged += Map_PointerWheelChanged;
                PointerExited += Map_PointerExited;

                // this.KeyDown += Map_KeyDown;
                // this.AddHandler(KeyDownEvent, new KeyEventHandler(Map_KeyDown), true);

                Loaded += OnLoaded;
                SizeChanged += OnSizeChanged;
            }
        }

        /// <summary>
        ///     Gets or sets the current position
        /// </summary>
        protected Point CurrentPosition { get; set; }

        /// <summary>
        ///     Gets or sets the current position
        /// </summary>
        protected Point CurrentSpeed { get; set; }

        /// <summary>
        ///     Gets or sets the current zoom by GUI
        /// </summary>
        protected double CurrentZoom { get; set; }

        protected double TargetZoom
        {
            get { return _targetZoom; }
            set
            {
                if (value < 100)
                {
                    _targetZoom = 100;
                }
                else if (value > 100000000)
                {
                    _targetZoom = 100000000;
                }
                else
                {
                    _targetZoom = value;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the target position DURING the button
        ///     down of the left mouse button. If mouse button is
        ///     released the position won't be regarded
        /// </summary>
        protected Point TargetPosition { get; set; }

        /// <summary>
        ///     Gets or sets the position to move to without
        ///     regarding any other speed or position argument
        /// </summary>
        protected Point? MoveToPosition { get; set; }

        protected int CurrentZoomLevel
        {
            get { return _currentZoomLevel; }
            set
            {
                if (value > TileInfo.MaxZoom)
                {
                    _currentZoomLevel = TileInfo.MaxZoom;
                }
                else if (value < 0)
                {
                    _currentZoomLevel = 0;
                }
                else
                {
                    _currentZoomLevel = value;
                }
            }
        }

        protected bool SuspendRendering { get; set; }

        private void Map_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Add)
            {
                TargetZoom *= 2;
                UpdateAllTiles();
            }
            else if (e.Key == VirtualKey.Subtract)
            {
                TargetZoom /= 2;
                UpdateAllTiles();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            ClipToBounds();
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            ClipToBounds();
        }

        private void ClipToBounds()
        {
            Clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, ActualWidth, ActualHeight)
            };
        }

        private void CompositionTarget_Rendering(object sender, object o)
        {
            if (SuspendRendering) return;

            // Debug.WriteLine("Rendering " + DateTime.Now.ToString());

            _scrollEnergyManager.Recharge();

            bool change = false;
            double ratio = (CurrentZoom/TargetZoom);

            if (ratio < 0.98 || ratio > 1.02)
            {
                //var diff = this.CurrentZoom - this.TargetZoom;
                CurrentZoom /= Math.Pow(ratio, 1/10.0);
                CurrentZoomLevel = ConvertZoomToZoomLevel(CurrentZoom);

                change = true;
            }

            if (MoveToPosition.HasValue)
            {
                // Cinematic movement
                double posDiffX = CurrentPosition.X - MoveToPosition.Value.X;
                double posDiffY = CurrentPosition.Y - MoveToPosition.Value.Y;

                CurrentPosition = new Point(
                    CurrentPosition.X - posDiffX*0.15,
                    CurrentPosition.Y - posDiffY*0.15);

                if (Math.Abs(posDiffX) + Math.Abs(posDiffY)
                    < 1/TargetZoom)
                {
                    MoveToPosition = null;
                }

                change = true;
            }
            else
            {
                // Cinematic movement
                double posDiffX = 0.0;
                double posDiffY = 0.0;

                if (_isMouseDown)
                {
                    posDiffX = CurrentPosition.X - TargetPosition.X;
                    posDiffY = CurrentPosition.Y - TargetPosition.Y;
                }

                double springFactor = 0.7;
                double friction = 0.999;
                CurrentSpeed = new Point(
                    (CurrentSpeed.X*friction - posDiffX)*springFactor,
                    (CurrentSpeed.Y*friction - posDiffY)*springFactor);

                if ((Math.Abs(CurrentSpeed.X) + Math.Abs(CurrentSpeed.Y))
                    > 1/TargetZoom)
                {
                    double timeStep = 0.1;
                    CurrentPosition = new Point(
                        (CurrentPosition.X + CurrentSpeed.X*timeStep),
                        (CurrentPosition.Y + CurrentSpeed.Y*timeStep));

                    change = true;
                }
            }

            if (change)
            {
                UpdateAllTiles();
            }

            // Check, if we can load a tile
            if (_currentlyLoading <= 3 && _notLoadedTiles.Count > 0)
            {
                TileInfo notLoadedTile = _notLoadedTiles.Last();

                notLoadedTile.LoadImage(_tileProvider);
                UpdateTile(notLoadedTile);
                Children.Add(notLoadedTile.TileImage);

                _notLoadedTiles.Remove(notLoadedTile);
                _currentlyLoading++;
                notLoadedTile.LoadingFinished += (x, y) => { _currentlyLoading--; };
            }
        }

        protected void UpdateAllTiles()
        {
            // Convert upper left image of window to tile coordinates
            // this.CurrentZoom   
            LoadAllTilesForZoomLevel(CurrentZoomLevel, true);

            foreach (TileInfo tileInfo in _addedTiles.Where(x => x.TileImage != null).ToList())
            {
                UpdateTile(tileInfo);
            }
        }

        private void UpdateTile(TileInfo tileInfo)
        {
            if ((tileInfo.Zoom > (CurrentZoomLevel + 1))
                || (tileInfo.Zoom < (CurrentZoomLevel - 2)))
            {
                // Hide unused images
                RemoveImage(tileInfo);
            }

            double localZoom = Math.Pow(2, tileInfo.Zoom);

            Point position = tileInfo.GetCoordinates(localZoom);
            SetLeft(
                tileInfo.TileImage,
                (position.X + CurrentPosition.X)
                *CurrentZoom*BaseZoom
                + ActualWidth/2);
            SetTop(
                tileInfo.TileImage,
                (position.Y + CurrentPosition.Y)
                *CurrentZoom*BaseZoom
                + ActualHeight/2);
            tileInfo.TileImage.Width = (CurrentZoom/localZoom) + 0.5;
            tileInfo.TileImage.Height = (CurrentZoom/localZoom) + 0.5;
        }

        /// <summary>
        ///     Removes an image from tile list
        /// </summary>
        /// <param name="tileInfo">Image to be removed</param>
        private void RemoveImage(TileInfo tileInfo)
        {
            var animation = new DoubleAnimation();
            animation.From = 1.0;
            animation.To = 0.0;
            animation.Duration = TimeSpan.FromSeconds(0.1);

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(storyboard, tileInfo.TileImage);
            Storyboard.SetTargetProperty(storyboard, "Image.Opacity");
            storyboard.Begin();

            storyboard.Completed += (x, y) =>
            {
                Children.Remove(tileInfo.TileImage);
                _notLoadedTiles.Remove(tileInfo);
                _addedTiles.Remove(tileInfo);
            };
        }

        /// <summary>
        ///     Loads all tiles for a specific zoom level
        /// </summary>
        /// <param name="requiredZoom">Required zoomlevel</param>
        /// <param name="visible">Flag whether the tiles are visible</param>
        private void LoadAllTilesForZoomLevel(int requiredZoom, bool visible)
        {
            if (requiredZoom < 0)
            {
                // Nothing to load
                return;
            }

            var toLoadTiles = new List<TileInfo>();
            for (int zoom = requiredZoom - 1; zoom <= requiredZoom; zoom++)
            {
                double localZoom = Math.Pow(2, zoom);
                double left =
                    ((-ActualWidth/1.8/CurrentZoom) -
                     CurrentPosition.X)/BaseZoom;
                double right =
                    ((ActualWidth/1.8/CurrentZoom) -
                     CurrentPosition.X)/BaseZoom;
                double top =
                    ((-ActualHeight/1.8/CurrentZoom) -
                     CurrentPosition.Y)/BaseZoom;
                double bottom =
                    ((ActualHeight/1.8/CurrentZoom) -
                     CurrentPosition.Y)/BaseZoom;

                // Loads all images
                for (var x = (int) Math.Floor(left*localZoom);
                    x <= (int) Math.Ceiling(right*localZoom);
                    x++)
                {
                    for (var y = (int) Math.Floor(top*localZoom);
                        y <= (int) Math.Ceiling(bottom*localZoom);
                        y++)
                    {
                        toLoadTiles.Add(ShowTile(x, y, requiredZoom));
                    }
                }
            }

            // Check for all tiles, that are in notLoadedList and not in loadedTiles
            foreach (TileInfo tile in _notLoadedTiles.ToList())
            {
                if (!toLoadTiles.Contains(tile))
                {
                    _notLoadedTiles.Remove(tile);
                    _addedTiles.Remove(tile);
                }
            }
        }

        /// <summary>
        ///     Shows a specific tile
        /// </summary>
        /// <param name="tileX">X-Coordinate of tile</param>
        /// <param name="tileY">Y-Coordinate of tile</param>
        /// <param name="zoom">Zoomlevel of tile</param>
        protected TileInfo ShowTile(int tileX, int tileY, int zoom)
        {
            // Shows if shown tile is in loadedtiles
            TileInfo found = _addedTiles.FirstOrDefault(
                x => x.TileX == tileX && x.TileY == tileY && x.Zoom == zoom);

            if (found != null)
            {
                if (found.TileImage != null)
                {
                    if (zoom <= CurrentZoom &&
                        found.TileImage.Visibility == Visibility.Collapsed)
                    {
                        // Switch visibility flag
                        found.TileImage.Visibility = Visibility.Visible;
                    }
                }

                // Already loaded
                return found;
            }

            // Creates images if necessary
            var tileInfo = new TileInfo();
            tileInfo.TileX = tileX;
            tileInfo.TileY = tileY;
            tileInfo.Zoom = zoom;

            _addedTiles.Add(tileInfo);
            _notLoadedTiles.Add(tileInfo);
            TilesRetrieved++;

            return tileInfo;
        }

        private void Map_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

            double factor = Math.Exp(_scrollEnergyManager.RequestEnergy(((double) delta)/120));

            if (Double.IsNaN(factor))
            {
                Debug.WriteLine("factor = Double.NaN");
            }
            else
            {
                TargetZoom *= factor;
            }
        }


        private void Map_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint currentPoint = e.GetCurrentPoint(this);
            bool isLeftButton = currentPoint.Properties.IsLeftButtonPressed;
            if (!isLeftButton) return;

            DateTime now = DateTime.Now;
            if ((now - _lastClick) < TimeSpan.FromSeconds(0.2))
            {
                // Double click
                TargetZoom *= 2;

                // Inverse position
                double x = (currentPoint.Position.X - ActualWidth/2)
                           /CurrentZoom/BaseZoom
                           - CurrentPosition.X;
                double y = (currentPoint.Position.Y - ActualHeight/2)
                           /CurrentZoom/BaseZoom
                           - CurrentPosition.Y;

                MoveToPosition = new Point(-x, -y);
            }

            _lastClick = now;

            // Gets position of mouse
            TargetPosition = CurrentPosition;
            _lastPosition = e.GetCurrentPoint(this).Position;

            _isMouseDown = true;
        }

        private void Map_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _lastPosition = e.GetCurrentPoint(this).Position;
            _isMouseDown = false;
        }

        private void Map_PointerExited(object sender, PointerRoutedEventArgs pointerRoutedEventArgs)
        {
            _isMouseDown = false;
        }

        private void Map_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isMouseDown)
            {
                Point position = e.GetCurrentPoint(this).Position;

                if (_lastPosition.HasValue)
                {
                    double deltaX = position.X - _lastPosition.Value.X;
                    double deltaY = position.Y - _lastPosition.Value.Y;

                    deltaX /= CurrentZoom;
                    deltaY /= CurrentZoom;

                    TargetPosition =
                        new Point(
                            TargetPosition.X + deltaX,
                            TargetPosition.Y + deltaY);

                    UpdateAllTiles();
                }

                _lastPosition = position;
            }
        }

        protected int ConvertZoomToZoomLevel(double zoom)
        {
            return (int) Math.Round(Math.Log(zoom, 2.0) - 7.9);
        }

        protected double ConvertZoomLevelToZoom(double zoomlevel)
        {
            return Math.Pow(2, zoomlevel + 7.9);
        }

        // Public Interface of the Control

        public void SetView(double latitude, double longitude, int zoomlevel)
        {
            if (zoomlevel < 0 || zoomlevel > TileInfo.MaxZoom)
                throw new ArgumentOutOfRangeException("zoomlevel", @"Zoom Level is out of range");

            // Temporarily suspend rendering until all variables are set
            SuspendRendering = true;

            TargetZoom = ConvertZoomLevelToZoom(zoomlevel);
            MoveToPosition = OsmHelper.ConvertToTilePosition(-longitude, -latitude, 0);

            SuspendRendering = false;
        }
    }
}