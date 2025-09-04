using Android;
using Android.App;
using Android.Content;
using Android.Nfc;
using Android.Nfc.Tech;
using Plugin.NFC.Configuration;
using Plugin.NFC.Utils;
using System.Text;

namespace Plugin.NFC;

/// <summary>
/// Android implementation of <see cref="INFC"/>
/// </summary>
internal sealed class NFCImplementation_Android : INFC
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public NFCImplementation_Android()
    {
        _nfcAdapter = NfcAdapter.GetDefaultAdapter(CurrentContext);
    }

    public event EventHandler? OnTagConnected;
    public event EventHandler? OnTagDisconnected;
    public event NdefMessageReceivedEventHandler? OnMessageReceived;
    public event NdefMessagePublishedEventHandler? OnMessagePublished;
    public event TagDiscoveredEventHandler? OnTagDiscovered;
    public event EventHandler? OniOSReadingSessionCancelled;
    public event TagListeningStatusChangedEventHandler? OnTagListeningStatusChanged;

    private event OnNfcStatusChangedEventHandler? _onNfcStatusChangedInternal;
    public event OnNfcStatusChangedEventHandler OnNfcStatusChanged
    {
        add
        {
            var wasRunning = _onNfcStatusChangedInternal is not null;
            _onNfcStatusChangedInternal += value;
            if (!wasRunning && _onNfcStatusChangedInternal is not null)
            {
                RegisterListener();
            }
        }
        remove
        {
            var wasRunning = _onNfcStatusChangedInternal is not null;
            _onNfcStatusChangedInternal -= value;
            if (wasRunning && _onNfcStatusChangedInternal is null)
            {
                UnRegisterListener();
            }
        }
    }

    private bool _isListening;
    private bool _isWriting;
    private bool _isFormatting;

    /// <summary>
    /// Broadcast receiver to check NFC availability
    /// </summary>
    private NfcBroadcastReceiver? _nfcBroadcastReceiver;

    /// <summary>
    /// Local NFC adapter
    /// </summary>
    private readonly NfcAdapter? _nfcAdapter;

    /// <summary>
    /// Current disccovered NFC Tag
    /// </summary>
    private Tag? _currentTag;

    /// <summary>
    /// Current Android <see cref="Context"/>
    /// </summary>
    private static Context CurrentContext => Platform.AppContext;

    /// <summary>
    /// Current Android <see cref="Activity"/>
    /// </summary>
    private static Activity CurrentActivity => Platform.CurrentActivity!;

    /// <summary>
    /// Checks if NFC Feature is available
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            if (_nfcAdapter is null)
                return false;

            if (CurrentContext.CheckCallingOrSelfPermission(Manifest.Permission.Nfc) != Android.Content.PM.Permission.Granted)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Checks if NFC Feature is enabled
    /// </summary>
    public bool IsEnabled => IsAvailable && _nfcAdapter!.IsEnabled;

    /// <summary>
    /// Checks if writing mode is supported
    /// </summary>
    public bool IsWritingTagSupported => NFCUtils.IsWritingSupported();

    private NfcOptions _options = new();

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public NfcOptions Options
    {
        get => _options;
        set
        {
            _options = value;
            EnableLegacy(_options.LegacyMode);
            SetConfiguration(_options.Configuration);
        }
    }

    /// <summary>
    /// NFC configuration
    /// </summary>
    public NfcConfiguration Configuration { get; } = NfcConfiguration.GetDefaultConfiguration();

    /// <summary>
    /// Set legacy mode
    /// </summary>
    /// <param name="value"></param>
    public void EnableLegacy(bool value)
    {
        if (CrossNFC.IsLegacy == value)
            return;

        CrossNFC.Legacy = value;
    }

    /// <summary>
    /// Update NFC configuration
    /// </summary>
    /// <param name="configuration"><see cref="NfcConfiguration"/></param>
    public void SetConfiguration(NfcConfiguration configuration) => Configuration.Update(configuration);

    /// <summary>
    /// Starts tags detection
    /// </summary>
    public void StartListening()
    {
        if (_nfcAdapter is null)
            return;

        var intent = new Intent(CurrentActivity, CurrentActivity.GetType()).AddFlags(ActivityFlags.SingleTop);

        // We don't use MonoAndroid12.0 as targetframework for easier backward compatibility:
        // MonoAndroid12.0 needs JDK 11.
        PendingIntentFlags pendingIntentFlags = 0;

        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
            pendingIntentFlags = PendingIntentFlags.Mutable;

        var pendingIntent = PendingIntent.GetActivity(CurrentActivity, 0, intent, pendingIntentFlags);

        var ndefFilter = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
        ndefFilter.AddDataType("*/*");

        var tagFilter = new IntentFilter(NfcAdapter.ActionTagDiscovered);
        tagFilter.AddCategory(Intent.CategoryDefault);

        //var techFilter = new IntentFilter(NfcAdapter.ActionTechDiscovered);
        //var techList = new string[][]
        //{
        //    new string[] { "android.nfc.tech.NfcA" },
        //    new string[] { "android.nfc.tech.NfcB" },
        //    new string[] { "android.nfc.tech.NfcF" },
        //    new string[] { "android.nfc.tech.NfcV" },
        //    new string[] { "android.nfc.tech.Ndef" },
        //    new string[] { "android.nfc.tech.NdefFormatable" },
        //    new string[] { "android.nfc.tech.MifareClassic" },
        //    new string[] { "android.nfc.tech.MifareUltralight" }
        //};

        var filters = new IntentFilter[] { ndefFilter, tagFilter };

        _nfcAdapter.EnableForegroundDispatch(CurrentActivity, pendingIntent, filters, null);

        _isListening = true;
        OnTagListeningStatusChanged?.Invoke(_isListening);
    }

    /// <summary>
    /// Stops tags detection
    /// </summary>
    public void StopListening()
    {
        DisablePublishing();
        _nfcAdapter?.DisableForegroundDispatch(CurrentActivity);

        _isListening = false;
        OnTagListeningStatusChanged?.Invoke(_isListening);
    }

    /// <summary>
    /// Starts tag publishing (writing or formatting)
    /// </summary>
    /// <param name="clearMessage">Format tag</param>
    public void StartPublishing(bool clearMessage = false)
    {
        if (!IsWritingTagSupported)
            return;

        _isWriting = true;
        _isFormatting = clearMessage;
    }

    /// <summary>
    /// Stops tag publishing
    /// </summary>
    public void StopPublishing() => DisablePublishing();

    /// <summary>
    /// Publish or write a message on a tag
    /// </summary>
    /// <param name="tagInfo">see <see cref="ITagInfo"/></param>
    /// <param name="makeReadOnly">make tag read-only</param>
    public void PublishMessage(ITagInfo tagInfo, bool makeReadOnly = false) => WriteOrClearMessage(tagInfo, false, makeReadOnly);

    /// <summary>
    /// Format tag
    /// </summary>
    /// <param name="tagInfo">see <see cref="ITagInfo"/></param>
    public void ClearMessage(ITagInfo tagInfo) => WriteOrClearMessage(tagInfo, true);

    /// <summary>
    /// Handle Android OnNewIntent
    /// </summary>
    /// <param name="intent">Android <see cref="Intent"/></param>
    internal void HandleNewIntent(Intent? intent)
    {
        if (intent is null)
            return;

        if (intent.Action == NfcAdapter.ActionTagDiscovered || intent.Action == NfcAdapter.ActionNdefDiscovered)
        {
            Java.Lang.Object? parcelable;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                parcelable = intent.GetParcelableExtra(NfcAdapter.ExtraTag, Java.Lang.Class.ForName("android.nfc.Tag"));
            }
            else
            {
                parcelable = intent.GetParcelableExtra(NfcAdapter.ExtraTag);
            }

            if (parcelable is Tag tag)
            {
                _currentTag = tag;

                var nTag = GetTagInfo(_currentTag);
                if (nTag is not null)
                {
                    if (_isWriting)
                    {
                        // Write mode
                        OnTagDiscovered?.Invoke(nTag, _isFormatting);
                    }
                    else
                    {
                        // Read mode
                        OnMessageReceived?.Invoke(nTag);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handle Android OnResume
    /// </summary>
    internal void HandleOnResume()
    {
        // Android 10 fix:
        // If listening mode is already enable, we restart listening when activity is resumed
        if (_isListening)
            StartListening();
    }

    /// <summary>
    /// Write or Clear a NDEF message
    /// </summary>
    /// <param name="tagInfo"><see cref="ITagInfo"/></param>
    /// <param name="clearMessage">Clear Message</param>
    /// <param name="makeReadOnly">Make tag read-only</param>
    private void WriteOrClearMessage(ITagInfo tagInfo, bool clearMessage = false, bool makeReadOnly = false)
    {
        try
        {
            if (_currentTag is null)
                throw new Exception(Configuration.Messages.NFCErrorMissingTag);

            if (tagInfo is null)
                throw new Exception(Configuration.Messages.NFCErrorMissingTagInfo);

            var isTagConnected = false;

            var ndef = Ndef.Get(_currentTag);

            // If tag is not Ndef, then it may be NdefFormatable. If it's the case, we format it into Ndef in order to write into it.
            if (ndef is null)
            {
                if (!tagInfo.IsFormatable)
                    throw new Exception(Configuration.Messages.NFCErrorNotCompliantTag);

                var ndefFormatable = NdefFormatable.Get(_currentTag)
                    ?? throw new Exception(Configuration.Messages.NFCErrorFormatTag);

                try
                {
                    ndefFormatable.Connect();
                    OnTagConnected?.Invoke(null, EventArgs.Empty);
                    isTagConnected = true;

                    ndefFormatable.Format(GetEmptyNdefMessage());
                }
                catch (Android.Nfc.TagLostException tlex)
                {
                    throw new Exception("Tag Lost Error: " + tlex.Message);
                }
                catch (Java.IO.IOException ioex)
                {
                    throw new Exception("Tag IO Error: " + ioex.Message);
                }
                catch (Android.Nfc.FormatException fe)
                {
                    throw new Exception("Tag Format Error: " + fe.Message);
                }
                catch (Exception ex)
                {
                    throw new Exception("Tag Error: " + ex.Message);
                }
                finally
                {
                    if (ndefFormatable.IsConnected)
                        ndefFormatable.Close();
                }

                ndef = Ndef.Get(_currentTag);

                if (ndef is null)
                {
                    _currentTag = null;
                    OnTagDisconnected?.Invoke(null, EventArgs.Empty);

                    throw new Exception("Tag has been successfully formated but an error occured when trying open it again");
                }
            }

            try
            {
                if (!ndef.IsWritable)
                    throw new Exception(Configuration.Messages.NFCErrorReadOnlyTag);

                if (ndef.MaxSize < NFCUtils.GetSize(tagInfo.Records))
                    throw new Exception(Configuration.Messages.NFCErrorCapacityTag);

                ndef.Connect();

                if (!isTagConnected)
                    OnTagConnected?.Invoke(null, EventArgs.Empty);

                NdefMessage? message = null;
                if (clearMessage)
                {
                    message = GetEmptyNdefMessage();
                }
                else
                {
                    var records = new List<NdefRecord>();

                    foreach (var record in tagInfo.Records)
                    {
                        if (GetAndroidNdefRecord(record) is NdefRecord ndefRecord)
                            records.Add(ndefRecord);
                    }

                    if (records.Count > 0)
                        message = new NdefMessage(records.ToArray());
                }

                if (message is not null)
                {
                    ndef.WriteNdefMessage(message);

                    if (!clearMessage && makeReadOnly)
                    {
                        if (!MakeReadOnly(ndef))
                            Console.WriteLine("Cannot lock tag");
                    }

                    var nTag = GetTagInfo(_currentTag, ndef.NdefMessage)
                        ?? throw new Exception("Missing TagInfo");

                    OnMessagePublished?.Invoke(nTag);
                }
                else
                    throw new Exception(Configuration.Messages.NFCErrorWrite);
            }
            catch (Android.Nfc.TagLostException tlex)
            {
                throw new Exception("Tag Lost Error: " + tlex.Message);
            }
            catch (Java.IO.IOException ioex)
            {
                throw new Exception("Tag IO Error: " + ioex.Message);
            }
            catch (Android.Nfc.FormatException fe)
            {
                throw new Exception("Tag Format Error: " + fe.Message);
            }
            catch (Exception ex)
            {
                throw new Exception("Tag Error: " + ex.Message);
            }
            finally
            {
                if (ndef.IsConnected)
                    ndef.Close();

                _currentTag = null;
                OnTagDisconnected?.Invoke(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            StopPublishingAndThrowError(ex.Message);
        }
    }

    /// <summary>
    /// Stops publishing and throws error
    /// </summary>
    /// <param name="message">message</param>
    private void StopPublishingAndThrowError(string message)
    {
        StopPublishing();
        throw new Exception(message);
    }

    /// <summary>
    /// Deactivate publishing
    /// </summary>
    private void DisablePublishing()
    {
        _isWriting = false;
        _isFormatting = false;
    }

    /// <summary>
    /// Transforms an array of <see cref="NdefRecord"/> into an array of <see cref="NFCNdefRecord"/>
    /// </summary>
    /// <param name="records">Array of <see cref="NdefRecord"/></param>
    /// <returns>Array of <see cref="NFCNdefRecord"/></returns>
    private static NFCNdefRecord[] GetRecords(NdefRecord[] records)
    {
        var results = new NFCNdefRecord[records.Length];
        for (var i = 0; i < records.Length; i++)
        {
            var ndefRecord = new NFCNdefRecord
            {
                TypeFormat = (NFCNdefTypeFormat)records[i].Tnf,
                Uri = records[i].ToUri()?.ToString(),
                MimeType = records[i].ToMimeType() ?? string.Empty,
                Payload = records[i].GetPayload()
            };
            results.SetValue(ndefRecord, i);
        }
        return results;
    }

    /// <summary>
    /// Returns informations contains in NFC Tag
    /// </summary>
    /// <param name="tag">Android <see cref="Tag"/></param>
    /// <param name="ndefMessage">Android <see cref="NdefMessage"/></param>
    /// <returns><see cref="ITagInfo"/></returns>
    private static TagInfo? GetTagInfo(Tag tag, NdefMessage? ndefMessage = null)
    {
        if (tag is null || tag.GetId() is null)
            return null;

        var ndef = Ndef.Get(tag);

        var isNdefFormatable = false;
        if (ndef is null)
        {
            var ndefFormatable = NdefFormatable.Get(tag);
            isNdefFormatable = ndefFormatable is not null;
        }

        var isNdefCompatible = ndef is not null || isNdefFormatable;

        var nTag = new TagInfo(tag.GetId()!, isNdefCompatible, isNdefFormatable);

        if (ndef is not null)
        {
            nTag.Capacity = ndef.MaxSize;
            nTag.IsWritable = ndef.IsWritable;

            ndefMessage ??= ndef.CachedNdefMessage;

            if (ndefMessage is not null)
            {
                var records = ndefMessage.GetRecords();
                if (records is not null)
                {
                    nTag.Records = GetRecords(records);
                }
            }
        }

        return nTag;
    }

    /// <summary>
    /// Transforms a <see cref="NFCNdefRecord"/> into an Android <see cref="NdefRecord"/>
    /// </summary>
    /// <param name="record">Object <see cref="NFCNdefRecord"/></param>
    /// <returns>Android <see cref="NdefRecord"/></returns>
    private NdefRecord? GetAndroidNdefRecord(NFCNdefRecord record)
    {
        if (record is null || record.Payload is null)
            return null;

        NdefRecord? ndefRecord = null;
        switch (record.TypeFormat)
        {
            case NFCNdefTypeFormat.WellKnown:
                var languageCode = record.LanguageCode;
                if (string.IsNullOrWhiteSpace(languageCode)) languageCode = Configuration.DefaultLanguageCode;
                ndefRecord = NdefRecord.CreateTextRecord(languageCode.Substring(0, 2), Encoding.UTF8.GetString(record.Payload));
                break;
            case NFCNdefTypeFormat.Mime:
                ndefRecord = NdefRecord.CreateMime(record.MimeType, record.Payload);
                break;
            case NFCNdefTypeFormat.Uri:
                ndefRecord = NdefRecord.CreateUri(Encoding.UTF8.GetString(record.Payload));
                break;
            case NFCNdefTypeFormat.External:
                ndefRecord = NdefRecord.CreateExternal(record.ExternalDomain, record.ExternalType, record.Payload);
                break;
            case NFCNdefTypeFormat.Empty:
                ndefRecord = GetEmptyNdefRecord();
                break;
            case NFCNdefTypeFormat.Unknown:
            case NFCNdefTypeFormat.Unchanged:
            case NFCNdefTypeFormat.Reserved:
            default:
                break;

        }
        return ndefRecord;
    }

    /// <summary>
    /// Returns an empty Android <see cref="NdefRecord"/>
    /// </summary>
    /// <returns>Android <see cref="NdefRecord"/></returns>
    private static NdefRecord GetEmptyNdefRecord()
    {
        var empty = Array.Empty<byte>();
        return new NdefRecord(NdefRecord.TnfEmpty, empty, empty, empty);
    }

    /// <summary>
    /// Returns an empty Android <see cref="NdefMessage"/>
    /// </summary>
    /// <returns>Android <see cref="NdefMessage"/></returns>
    private static NdefMessage GetEmptyNdefMessage()
    {
        var records = new NdefRecord[1];
        records[0] = GetEmptyNdefRecord();
        return new NdefMessage(records);
    }

    /// <summary>
    /// Make a tag read-only
    /// </summary>
    /// <remarks>WARNING: This operation is permanent</remarks>
    /// <param name="ndef"><see cref="Ndef"/></param>
    /// <returns>boolean</returns>
    private static bool MakeReadOnly(Ndef ndef)
    {
        if (ndef is null)
            return false;

        var result = false;
        var newConnection = false;

        if (!ndef.IsConnected)
        {
            newConnection = true;
            ndef.Connect();
        }

        if (ndef.CanMakeReadOnly())
            result = ndef.MakeReadOnly();

        if (newConnection && ndef.IsConnected)
            ndef.Close();

        return result;
    }

    /// <summary>
    /// Register NFC Broadcast Receiver
    /// </summary>
    private void RegisterListener()
    {
        _nfcBroadcastReceiver = new NfcBroadcastReceiver(OnNfcStatusChange);
        CurrentContext?.RegisterReceiver(_nfcBroadcastReceiver, new IntentFilter(NfcAdapter.ActionAdapterStateChanged));
    }

    /// <summary>
    /// Unregister NFC Broadcast Receiver
    /// </summary>
    private void UnRegisterListener()
    {
        if (_nfcBroadcastReceiver is null)
            return;

        try
        {
            CurrentContext?.UnregisterReceiver(_nfcBroadcastReceiver);
        }
        catch (Java.Lang.IllegalArgumentException ex)
        {
            throw new Exception("NFC Broadcast Receiver Error: " + ex.Message);
        }

        _nfcBroadcastReceiver.Dispose();
        _nfcBroadcastReceiver = null;
    }

    /// <summary>
    /// Called when NFC status has changed
    /// </summary>
    private void OnNfcStatusChange() => _onNfcStatusChangedInternal?.Invoke(IsEnabled);

    /// <summary>
    /// Broadcast Receiver to check NFC feature availability
    /// </summary>
    [BroadcastReceiver(Enabled = true, Exported = false, Label = "NFC Status Broadcast Receiver")]
    private class NfcBroadcastReceiver : BroadcastReceiver
    {
        private readonly Action? _onChanged;

        public NfcBroadcastReceiver() { }
        public NfcBroadcastReceiver(Action onChanged)
        {
            _onChanged = onChanged;
        }

        public override async void OnReceive(Context? context, Intent? intent)
        {
            if (intent is not null && intent.Action == NfcAdapter.ActionAdapterStateChanged)
            {
                var state = intent.GetIntExtra(NfcAdapter.ExtraAdapterState, default);
                if (state == NfcAdapter.StateOff || state == NfcAdapter.StateOn)
                {
                    // await 1500ms to ensure that the status updates
                    await Task.Delay(1500);
                    _onChanged?.Invoke();
                }
            }
        }
    }
}
