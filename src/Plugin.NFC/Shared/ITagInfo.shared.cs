namespace Plugin.NFC
{
    public interface ITagInfo
    {
        bool IsWritable { get; set; }
        bool IsEmpty { get; }
        NFCNdefRecord[] Records { get; set; }
    }

    public class NFCNdefRecord
    {
        public NFCNdefTypeFormat TypeFormat { get; set; }
        public string MimeType { get; set; } = "text/plain";
        public string ExternalDomain { get; set; }
        public string ExternalType { get; set; }
        public byte[] Payload { get; set; }
        public string Message => NFCUtils.GetMessage(TypeFormat, Payload);
    }

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
