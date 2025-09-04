using Plugin.NFC.Utils;

namespace Plugin.NFC;

/// <summary>
/// Class describing the information containing within a NFC tag
/// </summary>
public sealed class NFCNdefRecord
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
    public string? ExternalDomain { get; set; }

    /// <summary>
    /// External type used for <see cref="NFCNdefTypeFormat.External"/> type
    /// </summary>
    public string? ExternalType { get; set; }

    /// <summary>
    /// Payload
    /// </summary>
    public byte[]? Payload { get; set; } 

    /// <summary>
    /// Uri
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// String formatted payload
    /// </summary>
    public string Message => NFCUtils.GetMessage(TypeFormat, Payload, Uri);

    /// <summary>
    /// Two letters ISO 639-1 Language Code (ex: en, fr, de...)
    /// </summary>
    public string? LanguageCode { get; set; }
}
