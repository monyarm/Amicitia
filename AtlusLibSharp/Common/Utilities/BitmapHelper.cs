﻿namespace AtlusLibSharp.Common.Utilities
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Threading.Tasks;
    using nQuant;

    /// <summary>
    /// Contains helper methods for creating and converting bitmaps to and from indexed bitmap data.
    /// </summary>
    public static class BitmapHelper
    {
        /// <summary>
        /// Create a new <see cref="Bitmap"/> instance using a color palette, per-pixel palette color indices and the image width and height.
        /// </summary>
        /// <param name="palette">The color palette used by the image.</param>
        /// <param name="indices">The per-pixel palette color indices used by the image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <returns>A new <see cref="Bitmap"/> instance created using the data provided.</returns>
        public static Bitmap Create(Color[] palette, byte[] indices, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bitmapData = bitmap.LockBits
            (
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat
            );                   

            unsafe
            {
                byte* p = (byte*)bitmapData.Scan0;
                Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = (x * 4) + y * bitmapData.Stride;
                        Color color = palette[indices[x + y * width]];
                        p[offset] = color.B;
                        p[offset + 1] = color.G;
                        p[offset + 2] = color.R;
                        p[offset + 3] = color.A;
                    }
                });
            }    

            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        /// <summary>
        /// Create a new <see cref="Bitmap"/> instance using an array of <see cref="Color"/> pixels and the image width and height.
        /// </summary>
        /// <param name="colors"><see cref="Color"/> array containing the color of each pixel in the image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <returns>A new <see cref="Bitmap"/> instance created using the data provided.</returns>
        public static Bitmap Create(Color[] colors, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bitmapData = bitmap.LockBits
            (
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat
            );

            unsafe
            {
                byte* p = (byte*)bitmapData.Scan0;
                Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = (x * 4) + y * bitmapData.Stride;
                        Color color = colors[x + y * width];
                        p[offset] = color.B;
                        p[offset + 1] = color.G;
                        p[offset + 2] = color.R;
                        p[offset + 3] = color.A;
                    }
                });
            }

            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        /// <summary>
        /// Read the per-pixel palette color indices of an indexed bitmap and return them.
        /// </summary>
        /// <param name="bitmap">The indexed bitmap to read the per-pixel palette color indices of.</param>
        /// <returns>Array of <see cref="System.Byte"/> containing the per-pixel palette color indices.</returns>
        public static byte[] GetIndices(Bitmap bitmap)
        {
            BitmapData rawData = bitmap.LockBits
            (
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, 
                bitmap.PixelFormat
            );

            byte[] indices = new byte[rawData.Height * rawData.Width];

            unsafe
            {
                byte* p = (byte*)rawData.Scan0;
                Parallel.For(0, rawData.Height, y =>
                {
                    for (int x = 0; x < rawData.Width; x++)
                    {
                        int offset = y * rawData.Stride + x;
                        indices[x + y * rawData.Width] = (p[offset]);
                    }
                });
            }

            // Unlock the bitmap so it won't stay locked in memory
            bitmap.UnlockBits(rawData);

            return indices;
        }

        /// <summary>
        /// Read the pixel colors of a bitmap and return them.
        /// </summary>
        /// <param name="bitmap">The bitmap to read the pixel colors of.</param>
        /// <returns>Array of <see cref="Color"/> containing the color of each pixel in the <see cref="Bitmap"/>.</returns>
        public static Color[] GetColors(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            BitmapData bitmapData = bitmap.LockBits
            (
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat
            );

            Color[] colors = new Color[height * width];

            unsafe
            {
                byte* p = (byte*)bitmapData.Scan0;
                Parallel.For(0, height, y =>
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = (x * 4) + y * bitmapData.Stride;
                        colors[x + y * width] = Color.FromArgb
                        (
                            p[offset + 3], p[offset + 2], p[offset + 1], p[offset]
                        );
                    }
                });
            }
            bitmap.UnlockBits(bitmapData);

            return colors;
        }

        /// <summary>
        /// Retrieve the color palette of an indexed bitmap and returns a specified max limit of colors to return. 
        /// The limit does not guarantee the amount of colors returned if the palette contains less colors than the specified limit.
        /// </summary>
        /// <param name="bitmap">The bitmap to read the palette palette of.</param>
        /// <param name="paletteColorCount">The max limit of palette colors to return.</param>
        /// <returns>Array of <see cref="Color"/> containing the palette colors of the <see cref="Bitmap"/>.</returns>
        public static Color[] GetPalette(Bitmap bitmap, int paletteColorCount)
        {
            Color[] palette = new Color[paletteColorCount];

            for (int i = 0; i < bitmap.Palette.Entries.Length; i++)
            {
                if (i == paletteColorCount)
                {
                    break;
                }

                palette[i] = bitmap.Palette.Entries[i];
            }

            return palette;
        }

        /// <summary>
        /// Encodes a bitmap into an indexed bitmap with a per-pixel palette color index using a specified number of colors in the palette.
        /// </summary>
        /// <param name="bitmap">The bitmap to encode.</param>
        /// <param name="paletteColorCount">The number of colors to be present in the palette.</param>
        /// <param name="indices">The per-pixel palette color indices.</param>
        /// <param name="palette">The <see cref="Color"/> array containing the palette colors of the indexed bitmap.</param>
        public static void QuantizeBitmap(Bitmap bitmap, int paletteColorCount, out byte[] indices, out Color[] palette)
        {
            WuQuantizer quantizer = new WuQuantizer();
            Bitmap quantBitmap = (Bitmap)quantizer.QuantizeImage(bitmap, paletteColorCount, 0, 1);
            palette = GetPalette(quantBitmap, paletteColorCount);
            indices = GetIndices(quantBitmap);
        }
    }
}