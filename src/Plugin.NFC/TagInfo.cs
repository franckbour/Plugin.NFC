using Plugin.NFC.Utils;

namespace Plugin.NFC;

/// <summary>
/// Default implementation of <see cref="ITagInfo"/>
/// </summary>
internal sealed class TagInfo : ITagInfo
{
    public byte[] Identifier { get; } = [];

    /// <summary>
    /// Tag Serial Number
    /// </summary>
    public string SerialNumber { get; } = string.Empty;

    /// <summary>
    /// Writable tag
    /// </summary>
    public bool IsWritable { get; set; }

    /// <summary>
    /// Capacity of tag in bytes
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Array of <see cref="NFCNdefRecord"/> of tag
    /// </summary>
    public NFCNdefRecord[] Records { get; set; } = [];

    /// <summary>
    /// Empty tag
    /// </summary>
    public bool IsEmpty => Records == null || Records.Length == 0 || Records[0] == null || Records[0].TypeFormat == NFCNdefTypeFormat.Empty;

    /// <summary>
    /// Supported tag 
    /// </summary>
    public bool IsSupported { get; private set; }

    /// <summary>
    /// Formatable tag
    /// </summary>
    public bool IsFormatable { get; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public TagInfo()
    {
        IsSupported = true;
    }

    /// <summary>
    /// Custom contructor
    /// </summary>
    /// <param name="identifier">Tag Identifier</param>
    /// <param name="isNdefCompatible">Is Ndef compatible tag</param>
    /// <param name="isNdefFormatable">Is Ndef formatable tag</param>
    public TagInfo(byte[] identifier, bool isNdefCompatible = true, bool isNdefFormatable = false)
    {
        Identifier = identifier;
        SerialNumber = NFCUtils.ByteArrayToHexString(identifier);
        IsSupported = isNdefCompatible;
        IsFormatable = isNdefFormatable;
    }

    public override string ToString() => $"TagInfo: identifier: {Identifier}, SerialNumber:{SerialNumber}, Capacity:{Capacity} bytes, IsSupported:{IsSupported}, IsEmpty:{IsEmpty}, IsWritable:{IsWritable}, IsFormatable:{IsFormatable}";
}
