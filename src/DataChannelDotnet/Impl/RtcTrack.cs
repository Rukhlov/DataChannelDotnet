using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DataChannelDotnet.Events;
using DataChannelDotnet.Internal;
using DataChannelDotnet.Data;
using DataChannelDotnet.Bindings;

namespace DataChannelDotnet.Impl
{

	public unsafe sealed class RtcTrack : IRtcTrack
	{
		public bool IsOpen => RtcChannelGetters.GetIsOpen(_trackId, _lock, ref _disposed);
		public bool IsClosed => RtcChannelGetters.GetIsClosed(_trackId, _lock, ref _disposed);

		public uint Timestamp
		{
			get => GetTimestamp();
			set => SetTimestamp(value);
		}

		public string Label =>
			RtcHelpers.GetString(_trackId, Rtc.rtcGetDataChannelLabel, _lock, ref _disposed, nameof(RtcTrack));

		public rtcDirection Direction => GetDirection();
		public string Description => RtcHelpers.GetString(_trackId, Rtc.rtcGetTrackDescription, _lock, ref _disposed, nameof(RtcTrack));
		public string Mid => RtcHelpers.GetString(_trackId, Rtc.rtcGetTrackMid, _lock, ref _disposed, nameof(RtcTrack));

		public event Action<IRtcTrack>? OnOpen;
		public event Action<IRtcTrack>? OnClose;
		public event Action<IRtcTrack, string?>? OnError;
		public event Action<IRtcTrack, RtcBinaryReceivedEventSafe>? OnBinaryReceivedSafe;

		private Action? _pliHandlerCallback;
		private GCHandle _pliHandlerHandle;

		private readonly Lock _lock = new();
		private readonly GCHandle _thisHandle;
		private readonly int _trackId;
		private bool _disposed;

		private volatile bool _closeEventRaised = false;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcOpenCallbackFunc(int id, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcClosedCallbackFunc(int id, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcErrorCallbackFunc(int id, sbyte* buffer, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcMessageCallbackFunc(int id, sbyte* buffer, int size, void* user);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void RtcPliCallbackFunc(int id, void* user);

		internal RtcTrack(int trackId)
		{
			_trackId = trackId;
			_thisHandle = GCHandle.Alloc(this);

			Rtc.rtcSetUserPointer(trackId, (void*)GCHandle.ToIntPtr(_thisHandle));

			unsafe
			{
				RtcOpenCallbackFunc openCallback = StaticOnOpenedCallback;
				RtcClosedCallbackFunc closedCallback = StaticOnClosedCallback;
				RtcErrorCallbackFunc errorCallback = StaticOnErrorCallback;
				RtcMessageCallbackFunc messageCallback = StaticOnMessageCallback;

				Rtc.rtcSetOpenCallback(trackId, (delegate* unmanaged[Cdecl]<int, void*, void>)Marshal.GetFunctionPointerForDelegate(openCallback)).GetValueOrThrow();
				Rtc.rtcSetClosedCallback(trackId, (delegate* unmanaged[Cdecl]<int, void*, void>)Marshal.GetFunctionPointerForDelegate(closedCallback)).GetValueOrThrow();
				Rtc.rtcSetErrorCallback(trackId, (delegate* unmanaged[Cdecl]<int, sbyte*, void*, void>)Marshal.GetFunctionPointerForDelegate(errorCallback)).GetValueOrThrow();
				Rtc.rtcSetMessageCallback(trackId, (delegate* unmanaged[Cdecl]<int, sbyte*, int, void*, void>)Marshal.GetFunctionPointerForDelegate(messageCallback)).GetValueOrThrow();
			}
		}

		#region Add packetizers
		public unsafe void AddH264Packetizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));
				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetH264Packetizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddH265Packetizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));
				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetH265Packetizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddAv1Packetizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));
				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetAV1Packetizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddOpusPacketizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));
				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetOpusPacketizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddAacPacketizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));
				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetAACPacketizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddPcmuPacketizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));
				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetPCMUPacketizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddPcmaPacketizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetPCMAPacketizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		public unsafe void AddG722Packetizer(RtcPacketizerInitArgs args)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				ConvertPacketizerArgs(args,
					(nativeArgs) => { Rtc.rtcSetG722Packetizer(_trackId, &nativeArgs).GetValueOrThrow(); });
			}
		}

		private unsafe void ConvertPacketizerArgs(RtcPacketizerInitArgs managedArgs, Action<rtcPacketizerInit> action)
		{
			using NativeUtf8String cName = new NativeUtf8String(managedArgs.Cname);

			rtcPacketizerInit nativeArgs = new rtcPacketizerInit()
			{
				timestamp = managedArgs.Timestamp,
				payloadType = managedArgs.PayloadType,
				ssrc = (uint)managedArgs.Ssrc,
				clockRate = managedArgs.Clockrate,
				cname = cName.Ptr,
				maxFragmentSize = managedArgs.MaxFragmentSize,
				nalSeparator = managedArgs.NalUnitSeparator,
				obuPacketization = managedArgs.ObuPacketization,
				playoutDelayId = managedArgs.PlayoutDelayId,
				playoutDelayMax = managedArgs.PlayoutDelayMax,
				playoutDelayMin = managedArgs.PlayoutDelayMin,
				sequenceNumber = managedArgs.SequenceNumber
			};

			action(nativeArgs);
		}
		#endregion

		public unsafe void SetPliHandler(Action callback)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				if (_pliHandlerCallback is not null)
					throw new InvalidOperationException("Only one PLI handler can be added");

				unsafe
				{
					RtcPliCallbackFunc pliCallback = StaticOnPliCallback;
					Rtc.rtcChainPliHandler(_trackId, (delegate* unmanaged[Cdecl]<int, void*, void>)Marshal.GetFunctionPointerForDelegate(pliCallback));
				}
				_pliHandlerCallback = callback;
				_pliHandlerHandle = GCHandle.Alloc(callback);
			}
		}

		public void AddRtcpNackResponder(uint maxPackets)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				Rtc.rtcChainRtcpNackResponder(_trackId, maxPackets).GetValueOrThrow();
			}
		}

		public void AddRtcpSrReporter()
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				Rtc.rtcChainRtcpSrReporter(_trackId).GetValueOrThrow();
			}
		}

		public unsafe void Write(ReadOnlySpan<byte> buffer)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				fixed (byte* bufferPtr = buffer)
				{
					Rtc.rtcSendMessage(_trackId, (sbyte*)bufferPtr, buffer.Length).GetValueOrThrow();
				}
			}
		}

		public unsafe uint ConvertTimestampSecondsToTimestamp(double seconds)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				uint timestamp = 0;
				Rtc.rtcTransformSecondsToTimestamp(_trackId, seconds, &timestamp).GetValueOrThrow();
				return timestamp;
			}
		}

		public unsafe uint GetLastTrackSenderReportTimestamp()
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				uint timestamp = 0;
				Rtc.rtcGetLastTrackSenderReportTimestamp(_trackId, &timestamp).GetValueOrThrow();
				return timestamp;
			}
		}

		private unsafe uint GetTimestamp()
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				uint timestamp = 0;
				Rtc.rtcGetCurrentTrackTimestamp(_trackId, &timestamp).GetValueOrThrow();
				return timestamp;
			}
		}

		private void SetTimestamp(uint value)
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));

				Rtc.rtcSetTrackRtpTimestamp(_trackId, value).GetValueOrThrow();
			}
		}

		private unsafe rtcDirection GetDirection()
		{
			using (_lock.EnterScope())
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(RtcTrack));


				rtcDirection direction = 0;
				Rtc.rtcGetTrackDirection(_trackId, &direction).GetValueOrThrow();
				return direction;
			}
		}

		#region Callbacks

		private static unsafe void StaticOnPliCallback(int id, void* user)
		{
			try
			{
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcTrack? instance))
					return;

				var cb = instance._pliHandlerCallback;
				cb?.Invoke();
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
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcTrack? instance))
					return;

				if (size < 0)
				{
					//This shouldn't happen™
				}
				else
				{
					Action<IRtcTrack, RtcBinaryReceivedEventSafe>? callback = instance.OnBinaryReceivedSafe;
					if (callback != null)
					{
						byte[] data = new byte[size];
						for (int i = 0; i < size; i++)
						{
							data[i] = (byte)buffer[i];
						}
						callback.Invoke(instance, new RtcBinaryReceivedEventSafe(data));
					}
				}
			}
			catch (Exception ex)
			{
				RtcTools.RaiseUnhandledException(ex);
			}
		}

		private static unsafe void StaticOnErrorCallback(int id, sbyte* buffer, void* user)
		{
			try
			{
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcTrack? instance))
					return;

				string errorStr = Utf8StringMarshaller.ConvertToManaged((byte*)buffer) ?? string.Empty;

				var cb = instance.OnError;
				cb?.Invoke(instance, errorStr);
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
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcTrack? instance))
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
				if (!RtcThread.TryGetRtcObjectInstance(user, out RtcTrack? instance))
					return;


				var cb = instance.OnOpen;
				cb?.Invoke(instance);
			}
			catch (Exception ex)
			{
				RtcTools.RaiseUnhandledException(ex);
			}
		}

		#endregion

		private void Cleanup()
		{
			Rtc.rtcClose(_trackId);
			WaitForCallbacks();
			Rtc.rtcDeleteTrack(_trackId);

			if (_pliHandlerHandle.IsAllocated)
				_pliHandlerHandle.Free();

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

			Console.WriteLine("!! RtcTrack did not close properly");
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