using System;

namespace DataChannelDotnet.Events
{
	public sealed class RtcBinaryReceivedEventSafe
	{
		public byte[] Data { get; }

		internal RtcBinaryReceivedEventSafe(byte[] data)
		{
			Data = data;
		}
	}
}