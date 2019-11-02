using System.Drawing;
using System.Drawing.Imaging;

namespace ImageMaskBenchmarks
{
	public static class ImageGenerator
	{
		public static (Bitmap, byte[]) CreateWithMask(int width, int height)
		{
			var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
			var mask = new byte[width * height];

			using (var g = Graphics.FromImage(bmp))
			{
				g.Clear(Color.Red);
			}

			for (var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
			{
				mask[y * width + x] = (byte) (((x / (float) width) * 128) + ((y / (float) height) * 128));
			}

			return (bmp, mask);
		}

		public static (Bitmap, byte[]) LoadWithMask(string path)
		{
			var bmp = new Bitmap(path);
			var width = bmp.Width;
			var height = bmp.Height;
			var mask = new byte[width * height];

			for (var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
			{
				mask[y * width + x] = (byte) (((x / (float) width) * 128) + ((y / (float) height) * 128));
			}

			return (bmp, mask);
		}
	}
}