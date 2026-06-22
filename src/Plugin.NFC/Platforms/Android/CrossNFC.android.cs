namespace Plugin.NFC;

public static partial class CrossNFC
{
    /// <summary>
    /// Overrides Activity.OnResume()
    /// </summary>
    public static void OnResume() => ((NFCImplementation_Android)Current).HandleOnResume();
}
