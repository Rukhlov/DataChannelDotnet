using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Internal;

namespace DataChannelDotnet
{

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate void RtcLogCallback(rtcLogLevel level, string message);

	public static unsafe class RtcLog
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcLogCallbackNative(rtcLogLevel level, sbyte* message);

		private static RtcLogCallback? _currentCallback;
		private static RtcLogCallbackNative? _nativeCallback;

		private static Lock _lock = new();
		private static bool _initializeCalled;

		public static void Initialize(rtcLogLevel level, RtcLogCallback callback)
		{
			using (_lock.EnterScope())
			{
				if (_initializeCalled)
					return;

				unsafe
				{
					_currentCallback = callback;
					_nativeCallback = StaticLogCallback;
					IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(_nativeCallback);
					Rtc.rtcInitLogger(level, (delegate* unmanaged[Cdecl]<rtcLogLevel, sbyte*, void>)callbackPtr);
				}

				_initializeCalled = true;
			}
		}

		public static void ChangeCallback(RtcLogCallback? callback)
		{
			_currentCallback = callback;
		}

		private static unsafe void StaticLogCallback(rtcLogLevel level, sbyte* message)
		{
			RtcLogCallback? callback = _currentCallback;
			callback?.Invoke(level, Utf8StringMarshaller.ConvertToManaged((byte*)message) ?? string.Empty);
		}
	}
}