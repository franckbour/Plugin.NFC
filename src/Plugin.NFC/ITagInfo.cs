namespace Plugin.NFC;

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
    /// Supported tag
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Formatable tag
    /// </summary>
    bool IsFormatable { get; }

    /// <summary>
    /// Capacity of tag in bytes
    /// </summary>
    int Capacity { get; set; }

    /// <summary>
    /// Array of <see cref="NFCNdefRecord"/> of tag
    /// </summary>
    NFCNdefRecord[] Records { get; set; }
}
