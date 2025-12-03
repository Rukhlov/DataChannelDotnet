using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Events;
using DataChannelDotnet.Internal;

namespace DataChannelDotnet.Impl
{

	public unsafe sealed class RtcDataChannel : IRtcDataChannel
	{
		public bool IsOpen => RtcChannelGetters.GetIsOpen(_channelId, _lock, ref _disposed);
		public bool IsClosed => RtcChannelGetters.GetIsClosed(_channelId, _lock, ref _disposed);
		public string Label => GetLabel();

		public event Action<IRtcDataChannel, RtcTextReceivedEvent>? OnTextReceived;
		public event Action<IRtcDataChannel, RtcTextReceivedEventSafe>? OnTextReceivedSafe;
		public event Action<IRtcDataChannel, RtcBinaryReceivedEventSafe>? OnBinaryReceivedSafe;
		public event Action<IRtcDataChannel>? OnOpen;
		public event Action<IRtcDataChannel>? OnClose;
		public event Action<IRtcDataChannel, string?>? OnError;


		private readonly int _channelId;
		private readonly Lock _lock = new();

		private bool _disposed;
		private string? _label;
		private GCHandle _thisHandle;
		private bool _closeEventRaised;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcOpenCallbackFunc(int id, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcMessageCallbackFunc(int id, sbyte* buffer, int size, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcClosedCallbackFunc(int id, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcErrorCallbackFunc(int id, sbyte* buffer, void* user);

		internal RtcDataChannel(int channelId)
		{
			_channelId = channelId;

			try
			{
				_thisHandle = GCHandle.Alloc(this);
				Rtc.rtcSetUserPointer(_channelId, (void*)GCHandle.ToIntPtr(_thisHandle));

				unsafe
				{
					RtcOpenCallbackFunc openCallback = StaticOnOpenedCallback;
					RtcMessageCallbackFunc messageCallback = StaticOnMessageCallback;
					RtcClosedCallbackFunc closedCallback = StaticOnClosedCallback;
					RtcErrorCallbackFunc errorCallback = StaticOnErrorCallback;

					Rtc.rtcSetOpenCallback(_channelId, (delegate* unmanaged[Cdecl]<int, void*, void>)Marshal.GetFunctionPointerForDelegate(openCallback)).GetValueOrThrow();
					Rtc.rtcSetMessageCallback(_channelId, (delegate* unmanaged[Cdecl]<int, sbyte*, int, void*, void>)Marshal.GetFunctionPointerForDelegate(messageCallback)).GetValueOrThrow();
					Rtc.rtcSetClosedCallback(_channelId, (delegate* unmanaged[Cdecl]<int, void*, void>)Marshal.GetFunctionPointerForDelegate(closedCallback)).GetValueOrThrow();
					Rtc.rtcSetErrorCallback(_channelId, (delegate* unmanaged[Cdecl]<int, sbyte*, void*, void>)Marshal.GetFunctionPointerForDelegate(errorCallback)).GetValueOrThrow();
				}
			}
			catch (Exception)
			{
				Dispose();
				throw;
			}
		}

		public unsafe void Send(ReadOnlySpan<byte> buffer)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcDataChannel));

				fixed (byte* bufferPtr = buffer)
				{
					Rtc.rtcSendMessage(_channelId, (sbyte*)bufferPtr, buffer.Length).GetValueOrThrow();
				}
			}
		}

		public unsafe void Send(string text)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcDataChannel));

				using NativeUtf8String nativeUtf8String = new NativeUtf8String(text + '\0');

				Rtc.rtcSendMessage(_channelId, nativeUtf8String.Ptr, -1).GetValueOrThrow();
			}
		}

		private unsafe string GetLabel()
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcDataChannel));

				if (_label is null)
				{
					Span<byte> buffer = stackalloc byte[128];

					fixed (byte* buffePtr = buffer)
					{
						Rtc.rtcGetDataChannelLabel(_channelId, (sbyte*)buffePtr, 128).GetValueOrThrow();
						_label = Utf8StringMarshaller.ConvertToManaged(buffePtr);
					}
				}

				return _label ?? string.Empty;
			}
		}

		#region Callbacks

		private static unsafe void StaticOnErrorCallback(int id, sbyte* buffer, void* user)
		{
			try
			{
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcDataChannel? instance))
					return;

				var cb = instance.OnError;

				if (cb is not null)
				{
					string errorStr = Utf8StringMarshaller.ConvertToManaged((byte*)buffer) ?? string.Empty;
					cb?.Invoke(instance, errorStr);
				}
			}
			catch (Exception ex)
			{
				RtcTools.RaiseUnhandledException(ex);
			}
		}

		private static unsafe void StaticOnClosedCallback(int id, void* user)
		{
			try
			{
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcDataChannel? instance))
					return;

				var cb = instance.OnClose;
				instance._closeEventRaised = true;
				cb?.Invoke(instance);
			}
			catch (Exception ex)
			{
				RtcTools.RaiseUnhandledException(ex);
			}
		}

		private static unsafe void StaticOnOpenedCallback(int id, void* user)
		{
			try
			{
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcDataChannel? instance))
					return;

				var cb = instance.OnOpen;
				cb?.Invoke(instance);
			}
			catch (Exception ex)
			{
				RtcTools.RaiseUnhandledException(ex);
			}
		}

		private static unsafe void StaticOnMessageCallback(int id, sbyte* buffer, int size, void* user)
		{
			try
			{
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcDataChannel? instance))
					return;

				if (size > 0)
					StaticHandleBinaryMessage(instance, buffer, size);
				else if (size < 0)
					StaticHandleTextMessage(instance, buffer, size);
			}
			catch (Exception ex)
			{
				RtcTools.RaiseUnhandledException(ex);
			}
		}

		private static unsafe void StaticHandleBinaryMessage(RtcDataChannel channel, sbyte* buffer, int size)
		{
			Action<IRtcDataChannel, RtcBinaryReceivedEventSafe>? callback = channel.OnBinaryReceivedSafe;
			if (callback != null)
			{
				byte[] data = new byte[size];
				for (int i = 0; i < size; i++)
				{
					data[i] = (byte)buffer[i];
				}
				callback.Invoke(channel, new RtcBinaryReceivedEventSafe(data));
			}
		}

		private static unsafe void StaticHandleTextMessage(RtcDataChannel channel, sbyte* buffer, int size)
		{
			Action<IRtcDataChannel, RtcTextReceivedEventSafe>? safeCallback = channel.OnTextReceivedSafe;

			if (safeCallback is not null)
			{
				string? text = Utf8StringMarshaller.ConvertToManaged((byte*)buffer);

				if (text is not null)
					safeCallback(channel, new RtcTextReceivedEventSafe(text));
			}

			Action<IRtcDataChannel, RtcTextReceivedEvent>? unsafeCallback = channel.OnTextReceived;
			unsafeCallback?.Invoke(channel, new RtcTextReceivedEvent(buffer, -size));
		}

		#endregion

		private void Cleanup()
		{
			Rtc.rtcClose(_channelId);
			WaitForCallbacks();
			Rtc.rtcDeleteDataChannel(_channelId);
			_thisHandle.Free();
		}

		private void WaitForCallbacks()
		{
			for (int i = 0; i < 100; i++)
			{
				if (_closeEventRaised)
					return;

				Thread.Sleep(25);
			}

			Console.WriteLine("!! RtcDataChannel did not close properly");
		}

		public void Dispose()
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					return;

				_disposed = true;

				if (RtcThread.IsRtcThread)
					Task.Run(Cleanup);
				else
					Cleanup();
			}
		}
	}
}