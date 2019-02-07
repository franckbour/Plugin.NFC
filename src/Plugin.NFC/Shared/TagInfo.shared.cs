namespace Plugin.NFC
{
	public class TagInfo : ITagInfo
	{
		public bool IsWritable { get; set; }
		public NFCNdefRecord[] Records { get; set; }
		public bool IsEmpty => Records == null || Records.Length == 0 || Records[0].TypeFormat == NFCNdefTypeFormat.Empty;
	}
}
