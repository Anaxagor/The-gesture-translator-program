using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace WatchDog
{
    public unsafe class FastBitmap
    {
        Bitmap bitmap;

        // three elements used for MakeGreyUnsafe
        int _width;
        BitmapData _bitmapData;
        Byte* _pBase = null;
        
        public FastBitmap(Image bitmap)
        {
            this.bitmap = new Bitmap(bitmap);
        }

        public FastBitmap(int width, int height)
        {
            this.bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        }

        public void Dispose()
        {
            bitmap.Dispose();
        }

        public Bitmap Bitmap
        {
            get
            {
                return (bitmap);
            }
        }

        private Point PixelSize
        {
            get
            {
                GraphicsUnit unit = GraphicsUnit.Pixel;
                RectangleF bounds = bitmap.GetBounds(ref unit);

                return new Point((int)bounds.Width, (int)bounds.Height);
            }
        }

        public void LockBitmap()
        {
            GraphicsUnit unit = GraphicsUnit.Pixel;
            RectangleF boundsF = bitmap.GetBounds(ref unit);
            Rectangle bounds = new Rectangle((int)boundsF.X,
               (int)boundsF.Y,
               (int)boundsF.Width,
               (int)boundsF.Height);

            // Figure out the number of bytes in a row
            // This is rounded up to be a multiple of 4
            // bytes, since a scan line in an image must always be a multiple of 4 bytes
            // in length. 
            _width = (int)boundsF.Width * sizeof(PixelData);
            if (_width % 4 != 0)
            {
                _width = 4 * (_width / 4 + 1);
            }
            _bitmapData =
                bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

            _pBase = (Byte*)_bitmapData.Scan0.ToPointer();
        }

        public PixelData GetPixel(int x, int y)
        {
            PixelData returnValue = *PixelAt(x, y);
            return returnValue;
        }

        public void SetPixel(int x, int y, PixelData colour)
        {
            PixelData* pixel = PixelAt(x, y);
            *pixel = colour;
        }

        public void UnlockBitmap()
        {
            bitmap.UnlockBits(_bitmapData);
            _bitmapData = null;
            _pBase = null;
        }
        public PixelData* PixelAt(int x, int y)
        {
            return (PixelData*)(_pBase + y * _width + x * sizeof(PixelData));
        }
    }
    public struct PixelData
    {
        public byte B;
        public byte G;
        public byte R;
    }
}


