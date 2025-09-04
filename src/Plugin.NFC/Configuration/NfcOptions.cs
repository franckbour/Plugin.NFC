namespace Plugin.NFC.Configuration;

public sealed class NfcOptions
{
    /// <summary>
    /// Gets or sets legacy mode for iOS
    /// </summary>
    public bool LegacyMode { get; set; } = false;

    /// <summary>
    /// Gets or sets nfc configuration
    /// </summary>
    public NfcConfiguration Configuration { get; set; } = NfcConfiguration.GetDefaultConfiguration();
}
