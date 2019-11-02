using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ImageMaskBenchmarks
{
	internal static class Program
	{
		private static void Main(string[] args)
		{
			const string originalFilename = @"original.png";

			int width;
			int height;

			try
			{
				width = int.Parse(args[0]);
				height = int.Parse(args[1]);
			}
			catch
			{
				width = 10000;
				height = 10000;
				Console.WriteLine($"Usage: ./ImageMaskBenchmarks width height");
				Console.WriteLine($"Invalid arguments. Arguments will get default values (w: {width} ,h: {height})");
			}

			var timeMeasurer = new OperationTimeMeasurer();

			(Bitmap bmp, byte[] mask) = (null, null);

			var timeElapsed = timeMeasurer.Measure(() => (bmp, mask) = ImageGenerator.CreateWithMask(width, height));

			Console.WriteLine(
				$"Image/Mask Generation (w: {width} ,h: {height}) took: {timeElapsed.TotalMilliseconds} ms");

			bmp.Save(originalFilename, ImageFormat.Png);
			
			
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_SetPixel), () => MaskApplier.ApplyMask_SetPixel(bmp, mask));
			
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Marshal_Copy), () => MaskApplier.ApplyMask_Marshal_Copy(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Marshal_Copy_Span_MultiThread), () => MaskApplier.ApplyMask_Marshal_Copy_Span_MultiThread(bmp, mask));

			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Span), () => MaskApplier.ApplyMask_Unsafe_Copy_Span(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant_Span), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant_Span(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Span), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Span(bmp, mask));

			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy), () => MaskApplier.ApplyMask_Unsafe_Copy(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked_Fixed), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked_Fixed(bmp, mask));
			MeasureAction(timeMeasurer, nameof(MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked_Fixed_MultiThread), () => MaskApplier.ApplyMask_Unsafe_Copy_Constant_Unrolled_Unchecked_Fixed_MultiThread(bmp, mask));
		}

		private static void MeasureAction(OperationTimeMeasurer timeMeasurer, string name, Func<Bitmap> action)
		{
			Bitmap newBmp = null;
			var timeElapsed = timeMeasurer.Measure(() => newBmp = action());
			Console.WriteLine($"{name} took: {timeElapsed.TotalMilliseconds} ms");
			newBmp.Save($@"{name}.png", ImageFormat.Png);
			newBmp.Dispose();
		}
	}
}