namespace Plugin.NFC.Configuration;

/// <summary>
/// NFC Configuration class
/// </summary>
public sealed class NfcConfiguration
{
    /// <summary>
    /// Sets ISO 639-1 Language Code for all ndef records (default is "en")
    /// </summary>
    public required string DefaultLanguageCode { get; set; }

    /// <summary>
    /// List of user defined messages
    /// </summary>
    public required UserDefinedMessages Messages { get; set; }

    /// <summary>
    /// Update Nfc Configuration with a new configuration object
    /// </summary>
    /// <param name="newCfg"><see cref="NfcConfiguration"/></param>
    public void Update(NfcConfiguration? newCfg)
    {
        if (newCfg is null || newCfg.Messages is null)
            return;

        Messages = newCfg.Messages;
        DefaultLanguageCode = newCfg.DefaultLanguageCode;
    }

    /// <summary>
    /// Get the default Nfc configuration
    /// </summary>
    /// <returns>Default <see cref="NfcConfiguration"/></returns>
    public static NfcConfiguration GetDefaultConfiguration()
        => new () { Messages = new UserDefinedMessages(), DefaultLanguageCode = "en" };
}
