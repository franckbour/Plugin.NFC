namespace Plugin.NFC
{
	/// <summary>
	/// Interface for DebugInfo
	/// </summary>
	public interface IDebugInfo
	{
		/// <summary>
		/// A common message for the error.
		/// </summary>
		string Message { get; }

		/// <summary>
		/// The NFCTagType.
		/// </summary>
		string TagType { get; }

		/// <summary>
		/// The number of tags in range.
		/// </summary>
		int TagsDiscovered { get; }

		/// <summary>
		/// The connected tag.
		/// </summary>
		ITagInfo TagInfo { get; }

		/// <summary>
		/// The description of the platform nfc error.
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// The Nfc error.
		/// </summary>
		public string NfcError { get; set; }

		/// <summary>
		/// The nfc error code.
		/// </summary>
		public string ErrorCode { get; set; }
	}
}