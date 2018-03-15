using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Vision.Motion;
using Image=System.Drawing.Image;

namespace WatchDog
{
    /// <summary>
    /// Motion detector that folows the object based on its color
    /// </summary>
   
    public class ColorDetection : IMotionDetector
    {
        // Private class for calculations
        public class ObjectPosition
        {
            public int Up;
            public int Down;
            public int Left;
            public int Right;

            public Point Center
            {
                get
                {
                    return new Point((Right + Left) / 2, (Up + Down) / 2);
                }
            }

            public Rectangle BoundsBox
            {
                get { return new Rectangle(Left, Up, Right - Left, Down - Up); }
            }
        }

        // Private class for calculations
        private class ColorOccurance : IComparable<ColorOccurance>
        {
            public PixelData Color;

            public int Occurance;

            public int CompareTo(ColorOccurance other)
            {
                return -Occurance.CompareTo(other.Occurance);
            }
        }

        // motion frame holder
        private UnmanagedImage _motionFrame;

        
        // frame size
        private int _width;
        private int _height;

        // threshold values

        // Color difference
        private int _differenceThreshold = 25;

        // Threshold of size. If object becomes smaller - we don't change its position
        private const int SIZE_THRESHOLD = 20;

        // Color of the tracked object
        private PixelData _targetPDColor;        

        // Previous object position
        private readonly ObjectPosition _previousPosition;

        //Verifyies, whether tracking should calculate new color gradient
        private bool _dynamicallyCalculateNewColor;
        
        // Is object initialized
        private bool _initialized;

        // .ctor
        public ColorDetection()
        {
            _previousPosition = new ObjectPosition();
        }


        /// <summary>
        /// Difference threshold value, [1, 255]. 
        /// Identifies threshold for color variance while detecting new position of the object
        /// </summary>
        public int DifferenceThreshold
        {
            get { return _differenceThreshold; }
            set
            {
                lock (this)
                {
                    _differenceThreshold = Math.Max(1, Math.Min(255, value));
                }
            }
        }

        /// <summary>
        /// Whether to calculate new item color gradient
        /// </summary>
        public bool DynamicColorTracking
        {
            get { return _dynamicallyCalculateNewColor; }
            set
            {
                lock (this)
                {
                    _dynamicallyCalculateNewColor = value;
                }
            }
        }


        /// <summary>
        /// As far as this class is for tracking object - not implemented
        /// </summary>
        public float MotionLevel
        {
            get
            {
                lock (this)
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Motion frame containing detected areas of motion.
        /// </summary>
        /// 
        /// <remarks><para>Motion frame is a grayscale image, which shows areas of detected motion.
        /// All black pixels in the motion frame correspond to areas, where no motion is
        /// detected. But white pixels correspond to areas, where motion is detected.</para>
        public UnmanagedImage MotionFrame
        {
            get
            {
                lock (this)
                {
                    return _motionFrame;
                }
            }
        }

        /// <summary>
        /// Function for initializing detector
        /// </summary>
        /// <param name="image">First frame where object is defined</param>
        /// <param name="rect">The rectangle that identifies object's boundaries on the first frame</param>
        public unsafe void Initialize(Image image, Rectangle rect)
        {
            lock (this)
            {
                FastBitmap fBitmap = new FastBitmap(image);

                _height = image.Height;
                _width = image.Width;

                int left = rect.Left;
                int right = rect.Right;
                int up = rect.Top;
                int down = rect.Bottom;

                _previousPosition.Up = up;
                _previousPosition.Down = down;
                _previousPosition.Left = left;
                _previousPosition.Right = right;

                _targetPDColor = GetDominantColorOfTheRegion(fBitmap, up, down, left, right);

                _motionFrame = UnmanagedImage.Create(image.Width, image.Height, PixelFormat.Format8bppIndexed);
                
                byte* currFrame = (byte*)_motionFrame.ImageData.ToPointer();
                
                for (int j = 0; j < _height; j++)
                {
                    for (int i = 0; i < _width; i++, currFrame++)
                    {
                        *currFrame = 0;
                    }
                }
                _initialized = true;
            }
        }

        /// <summary>
        /// Calculates dominant color of the region
        /// </summary>
        private PixelData GetDominantColorOfTheRegion(FastBitmap fBitmap, int up, int down, int left,int right)
        {
                fBitmap.LockBitmap();
                List<ColorOccurance> listColors = new List<ColorOccurance>();

                for (int j = up; j < down; j++)
                {
                    for (int i = left; i < right; i++)
                    {
                        PixelData data = fBitmap.GetPixel(i, j);

                        bool colorFound = false;
                        for (int z = 0; z < listColors.Count; z++ )
                        {
                            if ((listColors[z].Color.R + _differenceThreshold > data.R) && (listColors[z].Color.R - _differenceThreshold < data.R) &&
                                (listColors[z].Color.G + _differenceThreshold > data.G) && (listColors[z].Color.G - _differenceThreshold < data.G) &&
                                (listColors[z].Color.B + _differenceThreshold > data.B) && (listColors[z].Color.B - _differenceThreshold < data.B))
                            {
                                listColors[z].Occurance ++;
                                colorFound = true;
                            }
                        }
                        if (!colorFound)
                        {
                            ColorOccurance oc = new ColorOccurance();
                            oc.Color = data;
                            listColors.Add(oc);
                        }
                    }
                }
                fBitmap.UnlockBitmap();


                // Here we got the list of all colors
                // let's sort
                listColors.Sort();

                // At first position we should have our object color;
                return listColors[0].Color;

        }

        /// <summary>
        /// Function for processing a separate captured frame
        /// </summary>
        public unsafe void ProcessFrame(Image videoFrame)
        {
            lock (this)
            {
                // check if we started searching
                if (!_initialized)
                {
                    return;
                }

                // check image dimension
                if ((videoFrame.Width != _width) || (videoFrame.Height != _height))
                    return;
                
                // Easier way to extract pixels
                FastBitmap fBitmap = new FastBitmap(videoFrame);


                
                // mask for easier processing
                bool[,] newMask = new bool[_width, _height];

                // For new color detection
                
                long colorR = 0;
                long colorG = 0;
                long colorB = 0;
                int coloredPixelsCount = 0;

                // defining search color regions
                int deltaX = (_previousPosition.Right - _previousPosition.Left) / 2;

                int left = (_previousPosition.Left - deltaX > 0) ? _previousPosition.Left - deltaX : 0;
                int right = (_previousPosition.Right + deltaX < _width - 1) ? _previousPosition.Right + deltaX : _width - 1;

                int deltaY = (_previousPosition.Down - _previousPosition.Up) / 2;

                int up = (_previousPosition.Up - deltaY > 0) ? _previousPosition.Up - deltaY : 0;
                int down = (_previousPosition.Down + deltaY < _height - 1) ? _previousPosition.Down + deltaY : _height - 1;

             
                fBitmap.LockBitmap();
                for (int j = 0; j< _height; j++)
                {
                    for (int i = 0; i< _width; i++)
                    {
                        PixelData data = fBitmap.GetPixel(i, j);

                        if ((data.R + _differenceThreshold > _targetPDColor.R) && (data.R - _differenceThreshold < _targetPDColor.R) &&
                            (data.G + _differenceThreshold > _targetPDColor.G) && (data.G - _differenceThreshold < _targetPDColor.G) &&
                            (data.B + _differenceThreshold > _targetPDColor.B) && (data.B - _differenceThreshold < _targetPDColor.B) &&
                            (i >= left && i <= right && j >= up && j <= down))
                        {
                            newMask[i, j] = true;

                            coloredPixelsCount++;
                            colorR += data.R;
                            colorG += data.G;
                            colorB += data.B;
                        }
                    }
                }
                fBitmap.UnlockBitmap();



                PixelData gradientFound = new PixelData();

                if (coloredPixelsCount>0)
                {
                    gradientFound.R = (byte) (colorR/coloredPixelsCount);
                    gradientFound.G = (byte) (colorG/coloredPixelsCount);
                    gradientFound.B = (byte) (colorB/coloredPixelsCount);
                }
                else
                {
                    gradientFound = _targetPDColor;
                }

                // stamping new info to the frame
                Grayscale.CommonAlgorithms.BT709.Apply(UnmanagedImage.FromManagedImage((Bitmap) videoFrame), 
                    _motionFrame);
                byte* currFrame = (byte*)_motionFrame.ImageData.ToPointer();
                for (int j = 0; j < _height; j++)
                {
                    for (int i = 0; i < _width; i++, currFrame++)
                    {
                        if (j >= up && j <= down &&
                            i >= left && i <= right
                            && newMask[i,j])
                        {
                            *currFrame = 255;
                        }
                        else
                        {
                            *currFrame = 0;
                        }
                    }
                }
                
                // to start
                currFrame = (byte*)_motionFrame.ImageData.ToPointer();

                bool firstTime = true;

                // Here  - we should find new points
                for (int j = 0; j < _height; j++)
                {
                    for (int i = 0; i < _width; i++, currFrame++)
                    {
                        
                        if (*currFrame == 255)
                        {
                            if (firstTime)
                            {
                                up = j;
                                down = j;
                                left = i;
                                right = i;

                                firstTime = false;
                            }
                            else
                            {
                                if (i < left)
                                    left = i;
                                if (i > right)
                                    right = i;
                                if (j < up)
                                    up = j;
                                if (j > down)
                                    down = j;
                            }
                        }
                    }
                }

                // Here - revision. If found object is too small - than we have a problem....
                // let's better leave last links to the object
                if (right-left < SIZE_THRESHOLD || down-up < SIZE_THRESHOLD)
                {
                    if (down - up >= _previousPosition.Down - _previousPosition.Up)
                    {
                        _previousPosition.Up = up;
                        _previousPosition.Down = down;
                    }
                    if (right - left >= _previousPosition.Right - _previousPosition.Left)
                    {
                        _previousPosition.Right = right;
                        _previousPosition.Left = left;
                    }
                }
                else
                {
                    _previousPosition.Up = up;
                    _previousPosition.Down = down;
                    _previousPosition.Right = right;
                    _previousPosition.Left = left;

                    // And one more. If we need to calculate new color of the region - let's do it
                    // but not when image is the WHOLE screen
                    if (_dynamicallyCalculateNewColor)
                    {
                        // this is more reliable, but not really tested
                        _targetPDColor.R = gradientFound.R;
                        _targetPDColor.G = gradientFound.G;
                        _targetPDColor.B = gradientFound.B;
                    }
                }
               
               
            }
        }

        public void ProcessFrame(UnmanagedImage videoFrame)
        {
            ProcessFrame(videoFrame.ToManagedImage());
        }


        /// <summary>
        /// Reset motion detector to initial state.
        /// </summary>
        public void Reset()
        {
            lock (this)
            {
               if (_motionFrame != null)
                {
                    _motionFrame.Dispose();
                    _motionFrame = null;
                }
            }
        }

        /// <summary>
        /// Central position of the object on the frame
        /// </summary>
        public Point Center
        {
            get
            {
                return _previousPosition.Center;
            }
        }

        /// <summary>
        /// Object's bounding box
        /// </summary>
        public Rectangle BoundsBox
        {
            get
            {
                return _previousPosition.BoundsBox;
            }
        }

        /// <summary>
        /// Object's dominant color
        /// </summary>
        public Color ObjectColor
        {
            get
            {
                return Color.FromArgb(_targetPDColor.R, _targetPDColor.G, _targetPDColor.B);                
            }
        }
    }
}