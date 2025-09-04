using Android.Content;

namespace Plugin.NFC;

public static partial class CrossNFC
{
    /// <summary>
    /// Overrides Activity.OnNewIntent()
    /// </summary>
    /// <param name="intent">Android <see cref="Intent"/></param>
    public static void OnNewIntent(Intent? intent) => ((NFCImplementation_Android)Current).HandleNewIntent(intent);

    /// <summary>
    /// Overrides Activity.OnResume()
    /// </summary>
    public static void OnResume() => ((NFCImplementation_Android)Current).HandleOnResume();
}
