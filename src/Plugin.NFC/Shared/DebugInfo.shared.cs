namespace Plugin.NFC
{
	public class DebugInfo : IDebugInfo
	{
		/// <inheritdoc/>
		public string Message { get; set; }

		/// <inheritdoc/>
		public string TagType { get; set; }

		/// <inheritdoc/>
		public string Description { get; set; }

		/// <inheritdoc/>
		public int TagsDiscovered { get; set; }

		/// <inheritdoc/>
		public ITagInfo TagInfo { get; set; }

		/// <inheritdoc/>
		public string NfcError { get; set; }

		/// <inheritdoc/>
		public string ErrorCode { get; set; }

		/// <summary>
		/// Custom contructor
		/// </summary>
		/// <param name="message">The custom debug message.</param>
		/// <param name="description">The description.</param>
		/// <param name="tagType">The rfid tag type.</param>
		/// <param name="tagsDiscovered">The number of rfid tags in scan range.</param>
		/// <param name="errorCode">The platform nfc error code.</param>
		/// <param name="nfcError">The named platform nfc error.</param>
		/// <param name="tagInfo">The tag info.</param>
		public DebugInfo(string message, string description = "", string tagType = "", int tagsDiscovered = 0, string errorCode = "", string nfcError = "", ITagInfo tagInfo = null)
		{
			Message = message;
			Description = description;
			TagType = tagType;
			TagsDiscovered = tagsDiscovered;
			ErrorCode = errorCode;
			NfcError = nfcError;
			TagInfo = tagInfo;
		}

		/// <summary>
		/// Custom contructor
		/// </summary>
		/// <param name="message">The custom debug message.</param>
		/// <param name="tagInfo">The tag info.</param>
		public DebugInfo(string message, ITagInfo tagInfo)
		{
			Message = message;
			TagInfo = tagInfo;
		}
	}
}