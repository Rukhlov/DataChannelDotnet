using System;

namespace DataChannelDotnet.Internal
{
	internal unsafe struct NativeUtf8String : IDisposable
	{
		private sbyte* _ptr;

		public sbyte* Ptr => _ptr;

		public NativeUtf8String(string str)
		{
			_ptr = null;
			if (str == null)
				return;

			_ptr = (sbyte*)Utf8StringMarshaller.ConvertToUnmanaged(str);
		}

		public void Dispose()
		{
			if (_ptr is null)
				return;

			Utf8StringMarshaller.Free((byte*)_ptr);
			_ptr = null;
		}
	}
}