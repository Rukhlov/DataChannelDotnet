namespace DataChannelDotnet.Events
{
	public readonly struct RtcLocalDescriptionEvent
	{
		public unsafe sbyte* Sdp { get; }
		public unsafe sbyte* Type { get; }

		internal unsafe RtcLocalDescriptionEvent(sbyte* sdp, sbyte* type)
		{
			Sdp = sdp;
			Type = type;
		}
	}
}