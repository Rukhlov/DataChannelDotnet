using System;
using System.Threading;

namespace DataChannelDotnet.Internal
{
	internal sealed class Lock
	{
		private readonly object _lockObject = new object();

		public Scope EnterScope()
		{
			return new Scope(_lockObject);
		}

		public struct Scope : IDisposable
		{
			private readonly object _lockObject;
			private bool _disposed;

			internal Scope(object lockObject)
			{
				_lockObject = lockObject;
				Monitor.Enter(_lockObject);
				_disposed = false;
			}

			public void Dispose()
			{
				if (!_disposed)
				{
					Monitor.Exit(_lockObject);
					_disposed = true;
				}
			}
		}
	}
}

