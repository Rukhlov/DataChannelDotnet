using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DataChannelDotnet.Internal
{
	internal static unsafe class Utf8StringMarshaller
	{
		public static string? ConvertToManaged(byte* unmanaged)
		{
			if (unmanaged == null)
				return null;

			int len = 0;
			while (unmanaged[len] != 0)
				len++;

			if (len == 0)
				return string.Empty;

			return Encoding.UTF8.GetString(unmanaged, len);
		}

		public static byte* ConvertToUnmanaged(string? managed)
		{
			if (managed == null)
				return null;

			byte[] bytes = Encoding.UTF8.GetBytes(managed);
			int size = bytes.Length + 1; // +1 for null terminator
			byte* ptr = (byte*)Marshal.AllocHGlobal(size);

			for (int i = 0; i < bytes.Length; i++)
			{
				ptr[i] = bytes[i];
			}
			ptr[bytes.Length] = 0; // null terminator

			return ptr;
		}

		public static void Free(byte* unmanaged)
		{
			if (unmanaged != null)
			{
				Marshal.FreeHGlobal((IntPtr)unmanaged);
			}
		}
	}
}

