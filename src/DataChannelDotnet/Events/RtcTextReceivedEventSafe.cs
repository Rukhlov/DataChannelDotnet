namespace DataChannelDotnet.Events
{
	public readonly struct RtcTextReceivedEventSafe
	{
		public string Text { get; }

		public RtcTextReceivedEventSafe(string text)
		{
			Text = text;
		}
	}
}