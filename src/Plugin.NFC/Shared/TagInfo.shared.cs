namespace Plugin.NFC
{
	/// <summary>
	/// Default implementation of <see cref="ITagInfo"/>
	/// </summary>
	public class TagInfo : ITagInfo
	{
		public byte[] Identifier { get; }

		/// <summary>
		/// Tag Serial Number
		/// </summary>
		public string SerialNumber { get;  }

		/// <summary>
		/// Writable tag
		/// </summary>
		public bool IsWritable { get; set; }

		/// <summary>
		/// Array of <see cref="NFCNdefRecord"/> of tag
		/// </summary>
		public NFCNdefRecord[] Records { get; set; }

		/// <summary>
		/// Empty tag
		/// </summary>
		public bool IsEmpty => Records == null || Records.Length == 0 || Records[0].TypeFormat == NFCNdefTypeFormat.Empty;

		/// <summary>
		/// Default constructor
		/// </summary>
		public TagInfo()
		{

		}

		/// <summary>
		/// Custom contructor
		/// </summary>
		/// <param name="identifier">Tag Identifier as <see cref="byte[]"/></param>
		public TagInfo(byte[] identifier)
		{
			Identifier = identifier;
			SerialNumber = NFCUtils.ByteArrayToHexString(identifier);
		}
	}
}
