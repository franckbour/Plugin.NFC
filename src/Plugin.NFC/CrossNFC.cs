namespace Plugin.NFC;

/// <summary>
/// Cross NFC
/// </summary>
public static partial class CrossNFC
{
    private static Lazy<INFC?> _implementation = new(() => CreateNFC(), LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Gets if the plugin is supported on the current platform.
    /// </summary>
    public static bool IsSupported => _implementation.Value != null;

    /// <summary>
    /// Legacy Mode (Supporting Mifare Classic on iOS)
    /// </summary>
    private static bool _legacy = false;

    internal static bool Legacy
    {
        get
        {
            return _legacy;
        }

        set
        {
            _legacy = value;

            _implementation = new Lazy<INFC?>(() => CreateNFC(), LazyThreadSafetyMode.PublicationOnly);
        }
    }

    public static bool IsLegacy => _legacy;

    /// <summary>
    /// Current plugin implementation to use
    /// </summary>
    public static INFC Current
    {
        get
        {
            var ret = _implementation.Value;
            return ret ?? throw NotImplementedInReferenceAssembly();
        }
    }

    private static INFC? CreateNFC()
    {

#if ANDROID
        return new NFCImplementation_Android();
#elif IOS
        ObjCRuntime.Class.ThrowOnInitFailure = false;
        if (Utils.NFCUtils.IsWritingSupported() && !Legacy)
            return new NFCImplementation_iOS();
        return new NFCImplementation_Before_iOS13();
#else
        return null;
#endif
    }

    internal static Exception NotImplementedInReferenceAssembly() =>
        new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");

}
