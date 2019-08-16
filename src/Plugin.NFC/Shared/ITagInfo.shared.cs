namespace Plugin.NFC
{
	/// <summary>
	/// Interface for ITagInfo
	/// </summary>
	public interface ITagInfo
	{
		/// <summary>
		/// Tag Raw Identifier
		/// </summary>
		byte[] Identifier { get; }

		/// <summary>
		/// Tag Serial Number
		/// </summary>
		string SerialNumber { get; }
		
		/// <summary>
		/// Writable tag
		/// </summary>
		bool IsWritable { get; set; }

		/// <summary>
		/// Empty tag
		/// </summary>
		bool IsEmpty { get; }

		/// <summary>
		/// Array of <see cref="NFCNdefRecord"/> of tag
		/// </summary>
		NFCNdefRecord[] Records { get; set; }
	}

	/// <summary>
	/// Class describing the information containing within a NFC tag
	/// </summary>
	public class NFCNdefRecord
	{
		/// <summary>
		/// NDEF Type
		/// </summary>
		public NFCNdefTypeFormat TypeFormat { get; set; }

		/// <summary>
		/// MimeType used for <see cref="NFCNdefTypeFormat.Mime"/> type
		/// </summary>
		public string MimeType { get; set; } = "text/plain";

		/// <summary>
		/// External domain used for <see cref="NFCNdefTypeFormat.External"/> type
		/// </summary>
		public string ExternalDomain { get; set; }

		/// <summary>
		/// External type used for <see cref="NFCNdefTypeFormat.External"/> type
		/// </summary>
		public string ExternalType { get; set; }

		/// <summary>
		/// Payload
		/// </summary>
		public byte[] Payload { get; set; }

		/// <summary>
		/// Uri
		/// </summary>
		public string Uri { get; set; }

		/// <summary>
		/// String formatted payload
		/// </summary>
		public string Message => NFCUtils.GetMessage(TypeFormat, Payload, Uri);
	}

	/// <summary>
	/// Enumeration of NDEF type
	/// </summary>
	public enum NFCNdefTypeFormat
	{
		Empty = 0x00,
		WellKnown = 0x01,
		Mime = 0x02,
		Uri = 0x03,
		External = 0x04,
		Unknown = 0x05,
		Unchanged = 0x06,
		Reserved = 0x07
	}
}
