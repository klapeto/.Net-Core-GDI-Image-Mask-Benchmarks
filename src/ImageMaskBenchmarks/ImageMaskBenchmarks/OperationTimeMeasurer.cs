using System;
using System.Diagnostics;

namespace ImageMaskBenchmarks
{
	public class OperationTimeMeasurer
	{
		private readonly Stopwatch _stopwatch = new Stopwatch();

		public TimeSpan Measure(Action action)
		{
			if (action == null) throw new ArgumentNullException(nameof(action));
			_stopwatch.Reset();
			_stopwatch.Start();
			action();
			_stopwatch.Stop();
			return _stopwatch.Elapsed;
		}
	}
}