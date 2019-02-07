namespace Plugin.NFC
{
	/// <summary>
	/// Default implementation of <see cref="ITagInfo"/>
	/// </summary>
	public class TagInfo : ITagInfo
	{
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
	}
}
