using System;
using System.Runtime.InteropServices;
using DataChannelDotnet.Bindings;
using DataChannelDotnet.Data;
using DataChannelDotnet.Impl;

namespace DataChannelDotnet.Internal
{

	internal static unsafe class RtcHelpers
	{
		public delegate int StringFetchFunc(int id, sbyte* buffer, int len);
		public static string GetString(int id, StringFetchFunc func, Lock @lock, ref bool disposed, string callerName)
		{
			using (@lock.EnterScope())
			{
				if (disposed)
					throw new ObjectDisposedException(callerName);

				return GetString(id, func);
			}
		}

		/// <summary>
		/// Tries to retrieve a string from an unmanaged function without allocating anything (Except the string).
		/// if it's too large, it rents a buffer instead.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="func"></param>
		/// <returns></returns>
		public static string GetString(int id, StringFetchFunc func)
		{
			const int maxStackallocSize = 4096;
			int len = func(id, null, 0).GetValueOrThrow();

			if (len == 0)
				return string.Empty;

			if (len > maxStackallocSize)
			{
				//Too large, just rent a buffer
				return GetStringRented(id, func);
			}
			else
			{
				//Small enough to stackalloc
				Span<byte> buffer = stackalloc byte[len];

				fixed (byte* bufferPtr = buffer)
				{
					int hr = func(id, (sbyte*)bufferPtr, buffer.Length);

					if (hr == -4) //-4 is the error code meaning 'buffer too small'. This could happen if the local description has changed since the previous call to 
								  //acquire the size
						return GetStringRented(id, func);

					if (hr == 0)
						return string.Empty;

					//If it's a different error code, then just throw
					hr.GetValueOrThrow();
					return Utf8StringMarshaller.ConvertToManaged(bufferPtr) ?? string.Empty;
				}
			}
		}

		private static unsafe string GetStringRented(int id, StringFetchFunc func)
		{
			int bufferSize = 1024 * 256;
			IntPtr bufferPtr = Marshal.AllocHGlobal(bufferSize);
			try
			{
				int len = func(id, (sbyte*)bufferPtr, bufferSize).GetValueOrThrow();

				if (len == 0)
					return string.Empty;

				return Utf8StringMarshaller.ConvertToManaged((byte*)bufferPtr) ?? string.Empty;
			}
			finally
			{
				Marshal.FreeHGlobal(bufferPtr);
			}
		}

		public static unsafe bool TryGetSelectedCandidatePair(int peerId, Lock @lock, ref bool disposed, out RtcCandidatePair pair)
		{
			pair = default(RtcCandidatePair);

			using var _ = @lock.EnterScope();
			if (disposed)
				throw new ObjectDisposedException(nameof(RtcPeerConnection));

			int bufferSize = 1024 * 128;
			IntPtr localCandidatePtr = Marshal.AllocHGlobal(bufferSize);
			IntPtr remoteCandidatePtr = Marshal.AllocHGlobal(bufferSize);
			try
			{
				int maxLen = Rtc.rtcGetSelectedCandidatePair(peerId, (sbyte*)localCandidatePtr, bufferSize,
									(sbyte*)remoteCandidatePtr, bufferSize);
				if (maxLen <= 0)
					return false;

				string localCandidate = Utf8StringMarshaller.ConvertToManaged((byte*)localCandidatePtr) ?? string.Empty;
				string remoteCandidate = Utf8StringMarshaller.ConvertToManaged((byte*)remoteCandidatePtr) ?? string.Empty;
				pair = new RtcCandidatePair
				{
					LocalCandidate = localCandidate,
					RemoteCandidate = remoteCandidate
				};

				return true;
			}
			finally
			{
				Marshal.FreeHGlobal(localCandidatePtr);
				Marshal.FreeHGlobal(remoteCandidatePtr);
			}
		}

		public static RtcDescriptionType ParseSdpType(sbyte* ptr)
		{
			Span<byte> typeBuffer = stackalloc byte[16];
			int len = Utf8Helper.GetUtf8Chars(ptr, typeBuffer);
			typeBuffer = typeBuffer.Slice(0, len);

			byte[] answerBytes = System.Text.Encoding.UTF8.GetBytes("answer");
			byte[] offerBytes = System.Text.Encoding.UTF8.GetBytes("offer");
			byte[] pranswerBytes = System.Text.Encoding.UTF8.GetBytes("pranswer");
			byte[] rollbackBytes = System.Text.Encoding.UTF8.GetBytes("rollback");

			if (typeBuffer.SequenceEqual(new ReadOnlySpan<byte>(answerBytes)))
				return RtcDescriptionType.Answer;
			else if (typeBuffer.SequenceEqual(new ReadOnlySpan<byte>(offerBytes)))
				return RtcDescriptionType.Offer;
			else if (typeBuffer.SequenceEqual(new ReadOnlySpan<byte>(pranswerBytes)))
				return RtcDescriptionType.PrAnswer;
			else if (typeBuffer.SequenceEqual(new ReadOnlySpan<byte>(rollbackBytes)))
				return RtcDescriptionType.Rollback;

			throw new InvalidOperationException("Unknown description type");
		}
	}
}