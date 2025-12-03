using System;
using System.Runtime.InteropServices;

namespace DataChannelDotnet.Internal
{

	internal unsafe struct NativeUtf8StringArray : IDisposable
	{
		private sbyte** _ptr;
		private int _stringCount;

		public sbyte** Ptr => _ptr;
		public int StringCount => _stringCount;

		public NativeUtf8StringArray(string[] array)
		{
			_ptr = null;
			_stringCount = 0;
			if (array == null)
				return;

			_stringCount = array.Length;
			int size = _stringCount * IntPtr.Size;
			_ptr = (sbyte**)Marshal.AllocHGlobal(size);
			for (int i = 0; i < _stringCount; i++)
			{
				((IntPtr*)_ptr)[i] = IntPtr.Zero;
			}

			try
			{
				for (int i = 0; i < _stringCount; i++)
				{
					_ptr[i] = (sbyte*)Utf8StringMarshaller.ConvertToUnmanaged(array[i]);
				}
			}
			catch (Exception)
			{
				Dispose();
				throw;
			}
		}

		public void Dispose()
		{
			if (_ptr is null)
				return;

			for (var i = 0; i < _stringCount; i++)
			{
				Utf8StringMarshaller.Free((byte*)_ptr[i]);
			}

			Marshal.FreeHGlobal((IntPtr)_ptr);
			_ptr = null;
		}
	}
}