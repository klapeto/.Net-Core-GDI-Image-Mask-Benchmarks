using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace ImageMaskBenchmarks
{
	public static class MaskApplier
	{
		private static (int width, int height) CheckParametersAndGetDimensions(Bitmap bmp, byte[] maskBytes)
		{
			if (bmp == null)
			{
				throw new ArgumentNullException(nameof(bmp));
			}

			if (maskBytes == null)
			{
				throw new ArgumentNullException(nameof(maskBytes));
			}

			var height = bmp.Height;
			var width = bmp.Width;
			if (height * width != maskBytes.Length)
			{
				throw new ArgumentException("Mask bytes array size does not match the original bitmap size");
			}

			return (width, height);
		}

		public static Bitmap ApplyMask_SetPixel(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);

			for (var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
			{
				targetBitmap.SetPixel(x, y, Color.FromArgb(maskBytes[(y * width) + x], bmp.GetPixel(x, y)));
			}

			return targetBitmap;
		}

		public static Bitmap ApplyMask_Marshal_Copy(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var bytesPerPixel = Image.GetPixelFormatSize(targetPixelFormat) / 8;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);
			
			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);


				var originalBytes = new byte[originalLockData.Height * originalLockData.Stride];

				Marshal.Copy(originalLockData.Scan0, originalBytes, 0,
					originalLockData.Height * originalLockData.Stride);

				var maskI = 0;
				for (var i = bytesPerPixel - 1; i < originalBytes.Length; i += bytesPerPixel)
				{
					originalBytes[i] = maskBytes[maskI++];
				}

				Marshal.Copy(originalBytes, 0, targetLockData.Scan0, originalLockData.Height * originalLockData.Stride);
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static Bitmap ApplyMask_Marshal_Copy_Span_MultiThread(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var threadNum = Environment.ProcessorCount;

			var threads = new List<Thread>(threadNum);

			var bytesPerPixel = Image.GetPixelFormatSize(targetPixelFormat) / 8;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);
			
			BitmapData originalLockData = null;
			BitmapData targetLockData = null;

			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);
				
				var originalBytes = new byte[originalLockData.Height * originalLockData.Stride];

				Marshal.Copy(originalLockData.Scan0, originalBytes, 0,
					originalLockData.Height * originalLockData.Stride);

				var chunkSize = originalBytes.Length / threadNum;

				while (chunkSize % bytesPerPixel != 0)
				{
					chunkSize = originalBytes.Length / --threadNum;
				}

				for (var i = 0; i < threadNum; i++)
				{
					var i1 = i;
					var th = new Thread(() =>
					{
						var chunk = new Span<byte>(originalBytes, i1 * chunkSize, chunkSize);
						var maskChunk = new Span<byte>(maskBytes, i1 * (chunkSize / bytesPerPixel), chunkSize / bytesPerPixel);
						var maskI = 0;
						for (var j = bytesPerPixel - 1; j < chunkSize; j += bytesPerPixel)
						{
							chunk[j] = maskChunk[maskI++];
						}
					});
					threads.Add(th);
					th.Start();
				}

				foreach (var thread in threads)
				{
					thread.Join();
				}

				Marshal.Copy(originalBytes, 0, targetLockData.Scan0, originalLockData.Height * originalLockData.Stride);
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant_Span(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);


			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				var totalLength = originalLockData.Height * originalLockData.Stride;

				var originalData = new Span<byte>(originalLockData.Scan0.ToPointer(), totalLength);
				var newData = new Span<byte>(targetLockData.Scan0.ToPointer(), totalLength);

				var maskI = 0;
				for (var i = 0; i < totalLength; ++i)
				{
					if ((i + 1) % 4 == 0)
					{
						newData[i] = maskBytes[maskI++];
						continue;
					}

					newData[i] = originalData[i];
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Span(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var bytesPerPixel = Image.GetPixelFormatSize(targetPixelFormat) / 8;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);


			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				var totalLength = originalLockData.Height * originalLockData.Stride;

				var originalData = new Span<byte>(originalLockData.Scan0.ToPointer(), totalLength);
				var newData = new Span<byte>(targetLockData.Scan0.ToPointer(), totalLength);

				var maskI = 0;
				for (var i = 0; i < totalLength; ++i)
				{
					if ((i + 1) % bytesPerPixel == 0)
					{
						newData[i] = maskBytes[maskI++];
						continue;
					}

					newData[i] = originalData[i];
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant_Unrolled_Span(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);

			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				var totalLength = originalLockData.Height * originalLockData.Stride;

				var originalData = new Span<byte>(originalLockData.Scan0.ToPointer(), totalLength);
				var newData = new Span<byte>(targetLockData.Scan0.ToPointer(), totalLength);

				var maskI = 0;
				for (var i = 0; i < totalLength; i += 4)
				{
					newData[i] = originalData[i];
					newData[i + 1] = originalData[i + 1];
					newData[i + 2] = originalData[i + 2];
					newData[i + 3] = maskBytes[maskI++];
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var bytesPerPixel = Image.GetPixelFormatSize(targetPixelFormat) / 8;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);


			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				var totalLength = originalLockData.Height * originalLockData.Stride;

				var originalData = (byte*) originalLockData.Scan0.ToPointer();
				var newData = (byte*) targetLockData.Scan0.ToPointer();

				var maskI = 0;
				for (var i = 0; i < totalLength; ++i)
				{
					if ((i + 1) % bytesPerPixel == 0)
					{
						newData[i] = maskBytes[maskI++];
						continue;
					}

					newData[i] = originalData[i];
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);


			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				var totalLength = originalLockData.Height * originalLockData.Stride;

				var originalData = (byte*) originalLockData.Scan0.ToPointer();
				var newData = (byte*) targetLockData.Scan0.ToPointer();

				var maskI = 0;
				for (var i = 0; i < totalLength; ++i)
				{
					if ((i + 1) % 4 == 0)
					{
						newData[i] = maskBytes[maskI++];
						continue;
					}

					newData[i] = originalData[i];
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant_Unrolled(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);

			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				var totalLength = originalLockData.Height * originalLockData.Stride;

				var originalData = (byte*) originalLockData.Scan0.ToPointer();
				var newData = (byte*) targetLockData.Scan0.ToPointer();

				var maskI = 0;
				for (var i = 0; i < totalLength; i += 4)
				{
					newData[i] = originalData[i];
					newData[i + 1] = originalData[i + 1];
					newData[i + 2] = originalData[i + 2];
					newData[i + 3] = maskBytes[maskI++];
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked(Bitmap bmp, byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);

			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				unchecked
				{
					var totalLength = originalLockData.Height * originalLockData.Stride;

					var originalData = (byte*) originalLockData.Scan0.ToPointer();
					var newData = (byte*) targetLockData.Scan0.ToPointer();

					var maskI = 0;
					for (var i = 0; i < totalLength; i += 4)
					{
						newData[i] = originalData[i];
						newData[i + 1] = originalData[i + 1];
						newData[i + 2] = originalData[i + 2];
						newData[i + 3] = maskBytes[maskI++];
					}
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked_Fixed(Bitmap bmp,
			byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);

			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);

				fixed (byte* mskPtr = maskBytes)
				{
					unchecked
					{
						var totalLength = originalLockData.Height * originalLockData.Stride;

						var originalData = (byte*) originalLockData.Scan0.ToPointer();
						var newData = (byte*) targetLockData.Scan0.ToPointer();

						var maskI = 0;
						for (var i = 0; i < totalLength; i += 4)
						{
							newData[i] = originalData[i];
							newData[i + 1] = originalData[i + 1];
							newData[i + 2] = originalData[i + 2];
							newData[i + 3] = mskPtr[maskI++];
						}
					}
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}

		public static unsafe Bitmap ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked_Fixed_MultiThread(Bitmap bmp,
			byte[] maskBytes)
		{
			var (width, height) = CheckParametersAndGetDimensions(bmp, maskBytes);

			var threadNum = Environment.ProcessorCount;

			var threads = new List<Thread>(threadNum);

			const PixelFormat targetPixelFormat = PixelFormat.Format32bppArgb;

			var targetBitmap = new Bitmap(width, height, targetPixelFormat);

			BitmapData originalLockData = null;
			BitmapData targetLockData = null;
			try
			{
				originalLockData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly,
					targetPixelFormat);
				targetLockData = targetBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly,
					targetPixelFormat);


				fixed (byte* mskPtr = maskBytes)
				{
					unchecked
					{
						var totalLength = targetLockData.Height * targetLockData.Stride;

						var chuckSize = totalLength / threadNum;

						while (chuckSize % 4 != 0)
						{
							chuckSize = totalLength / --threadNum;
						}

						var targetPtr = (byte*) targetLockData.Scan0.ToPointer();
						var originalPtr = (byte*) originalLockData.Scan0.ToPointer();

						for (var i = 0; i < threadNum; i++)
						{
							var localTargetPtr = targetPtr + (i * chuckSize);
							var localOriginalPtr = originalPtr + (i * chuckSize);
							var localMskPtr = mskPtr + (i * chuckSize / 4);
							var th = new Thread(() =>
							{
								var mskI = 0;
								for (var j = 0; j < chuckSize; j += 4)
								{
									localTargetPtr[j] = localOriginalPtr[j];
									localTargetPtr[j + 1] = localOriginalPtr[j + 1];
									localTargetPtr[j + 2] = localOriginalPtr[j + 2];
									localTargetPtr[j + 3] = localMskPtr[mskI++];
								}
							});

							threads.Add(th);
							th.Start();
						}

						foreach (var thread in threads)
						{
							thread.Join();
						}
					}
				}
			}
			finally
			{
				if (originalLockData != null)
				{
					bmp.UnlockBits(originalLockData);
				}

				if (targetLockData != null)
				{
					targetBitmap.UnlockBits(targetLockData);
				}
			}

			return targetBitmap;
		}
	}
}