using Plugin.NFC.Utils;
using System.Text;

namespace Plugin.NFC.Maui.Sample;

public partial class MainPage : ContentPage
{
    private readonly INFC _nfc;

    public MainPage(INFC nfc)
    {
        InitializeComponent();
        _nfc = nfc;
    }

    public const string ALERT_TITLE = "NFC";
    public const string MIME_TYPE = "application/com.companyname.nfcsample";

    private NFCNdefTypeFormat _type;
    private bool _makeReadOnly = false;
    private bool _eventsAlreadySubscribed = false;

    private bool _isDeviceiOS;
    private bool _deviceIsListening;
    private bool _nfcIsEnabled;

    /// <summary>
    /// Gets or sets if device is running on iOS
    /// </summary>
    public bool IsDeviceiOS
    {
        get => _isDeviceiOS;
        set
        {
            _isDeviceiOS = value;
            OnPropertyChanged(nameof(IsDeviceiOS));
        }
    }

    /// <summary>
    /// Gets or sets if device is listening for NFC tags
    /// </summary>
    public bool DeviceIsListening
    {
        get => _deviceIsListening;
        set
        {
            _deviceIsListening = value;
            OnPropertyChanged(nameof(DeviceIsListening));
        }
    }

    /// <summary>
    /// Gets or set if NFC feature is enabled on device
    /// </summary>
    public bool NfcIsEnabled
    {
        get => _nfcIsEnabled;
        set
        {
            _nfcIsEnabled = value;
            OnPropertyChanged(nameof(NfcIsEnabled));
            OnPropertyChanged(nameof(NfcIsDisabled));
        }
    }

    public bool NfcIsDisabled => !NfcIsEnabled;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (CrossNFC.IsSupported)
        {
            if (!_nfc.IsAvailable)
                await ShowAlert("NFC is not available");

            NfcIsEnabled = _nfc.IsEnabled;

            if (!NfcIsEnabled)
                await ShowAlert("NFC is disabled");

            if (DeviceInfo.Platform == DevicePlatform.iOS)
                IsDeviceiOS = true;

            await AutoStartAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns></returns>
    protected override bool OnBackButtonPressed()
    {
        Task.Run(() => StopListening());
        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Auto Start Listening
    /// </summary>
    /// <returns></returns>
    private async Task AutoStartAsync()
    {
        // Some delay to prevent Java.Lang.IllegalStateException "Foreground dispatch can only be enabled when your activity is resumed" on Android
        await Task.Delay(500);
        await StartListeningIfNotiOS();
    }

    /// <summary>
    /// Subscribe to the NFC events
    /// </summary>
    private void SubscribeEvents()
    {
        if (_eventsAlreadySubscribed)
            UnsubscribeEvents();

        _eventsAlreadySubscribed = true;

        _nfc.OnMessageReceived += Current_OnMessageReceived;
        _nfc.OnMessagePublished += Current_OnMessagePublished;
        _nfc.OnTagDiscovered += Current_OnTagDiscovered;
        _nfc.OnNfcStatusChanged += Current_OnNfcStatusChanged;
        _nfc.OnTagListeningStatusChanged += Current_OnTagListeningStatusChanged;

        if (IsDeviceiOS)
            _nfc.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
    }

    /// <summary>
    /// Unsubscribe from the NFC events
    /// </summary>
    private void UnsubscribeEvents()
    {
        _nfc.OnMessageReceived -= Current_OnMessageReceived;
        _nfc.OnMessagePublished -= Current_OnMessagePublished;
        _nfc.OnTagDiscovered -= Current_OnTagDiscovered;
        _nfc.OnNfcStatusChanged -= Current_OnNfcStatusChanged;
        _nfc.OnTagListeningStatusChanged -= Current_OnTagListeningStatusChanged;

        if (IsDeviceiOS)
            _nfc.OniOSReadingSessionCancelled -= Current_OniOSReadingSessionCancelled;

        _eventsAlreadySubscribed = false;
    }

    /// <summary>
    /// Event raised when Listener Status has changed
    /// </summary>
    /// <param name="isListening"></param>
    private void Current_OnTagListeningStatusChanged(bool isListening) => DeviceIsListening = isListening;

    /// <summary>
    /// Event raised when NFC Status has changed
    /// </summary>
    /// <param name="isEnabled">NFC status</param>
    private async void Current_OnNfcStatusChanged(bool isEnabled)
    {
        NfcIsEnabled = isEnabled;
        await ShowAlert($"NFC has been {(isEnabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Event raised when a NDEF message is received
    /// </summary>
    /// <param name="tagInfo">Received <see cref="ITagInfo"/></param>
    private async void Current_OnMessageReceived(ITagInfo tagInfo)
    {
        if (tagInfo == null)
        {
            await ShowAlert("No tag found");
            return;
        }

        // Customized serial number
        var identifier = tagInfo.Identifier;
        var serialNumber = NFCUtils.ByteArrayToHexString(identifier, ":");
        var title = !string.IsNullOrWhiteSpace(serialNumber) ? $"Tag [{serialNumber}]" : "Tag Info";

        if (tagInfo.IsFormatable)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Unformatted Tag:");
            sb.Append("Use \"Clear Tag\" to format it.");
            await ShowAlert(sb.ToString(), title);
        }
        else if (!tagInfo.IsSupported)
        {
            await ShowAlert("Unsupported tag (app)", title);
        }
        else if (tagInfo.IsEmpty)
        {
            await ShowAlert("Empty tag", title);
        }
        else
        {
            var first = tagInfo.Records[0];
            await ShowAlert(GetMessage(first), title);
        }
    }

    /// <summary>
    /// Event raised when user cancelled NFC session on iOS 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Current_OniOSReadingSessionCancelled(object? sender, EventArgs e) => Debug("iOS NFC Session has been cancelled");

    /// <summary>
    /// Event raised when data has been published on the tag
    /// </summary>
    /// <param name="tagInfo">Published <see cref="ITagInfo"/></param>
    private async void Current_OnMessagePublished(ITagInfo tagInfo)
    {
        try
        {
            ChkReadOnly.IsChecked = false;
            _nfc.StopPublishing();
            if (tagInfo.IsEmpty)
                await ShowAlert("Formatting tag operation successful");
            else
                await ShowAlert("Writing tag operation successful");
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    /// <summary>
    /// Event raised when a NFC Tag is discovered
    /// </summary>
    /// <param name="tagInfo"><see cref="ITagInfo"/> to be published</param>
    /// <param name="format">Format the tag</param>
    private async void Current_OnTagDiscovered(ITagInfo tagInfo, bool format)
    {
        if (!_nfc.IsWritingTagSupported)
        {
            await ShowAlert("Writing tag is not supported on this device");
            return;
        }

        try
        {
            NFCNdefRecord? record = null;
            switch (_type)
            {
                case NFCNdefTypeFormat.WellKnown:
                    record = new NFCNdefRecord
                    {
                        TypeFormat = NFCNdefTypeFormat.WellKnown,
                        MimeType = MIME_TYPE,
                        Payload = NFCUtils.EncodeToByteArray("Plugin.NFC is awesome!"),
                        LanguageCode = "en"
                    };
                    break;
                case NFCNdefTypeFormat.Uri:
                    record = new NFCNdefRecord
                    {
                        TypeFormat = NFCNdefTypeFormat.Uri,
                        Payload = NFCUtils.EncodeToByteArray("https://github.com/franckbour/Plugin.NFC")
                    };
                    break;
                case NFCNdefTypeFormat.Mime:
                    record = new NFCNdefRecord
                    {
                        TypeFormat = NFCNdefTypeFormat.Mime,
                        MimeType = MIME_TYPE,
                        Payload = NFCUtils.EncodeToByteArray("Plugin.NFC is awesome!")
                    };
                    break;
                default:
                    break;
            }

            if (!format && record is null)
                throw new Exception("Record can't be null.");

            tagInfo.Records = [record!];

            if (format)
                _nfc.ClearMessage(tagInfo);
            else
            {
                _nfc.PublishMessage(tagInfo, _makeReadOnly);
            }
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    /// <summary>
    /// Start listening for NFC Tags when "READ TAG" button is clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Button_Clicked_StartListening(object sender, System.EventArgs e) => await BeginListening();

    /// <summary>
    /// Stop listening for NFC tags
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Button_Clicked_StopListening(object sender, System.EventArgs e) => await StopListening();

    /// <summary>
    /// Start publish operation to write the tag (TEXT) when <see cref="Current_OnTagDiscovered(ITagInfo, bool)"/> event will be raised
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Button_Clicked_StartWriting(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.WellKnown);

    /// <summary>
    /// Start publish operation to write the tag (URI) when <see cref="Current_OnTagDiscovered(ITagInfo, bool)"/> event will be raised
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Button_Clicked_StartWriting_Uri(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.Uri);

    /// <summary>
    /// Start publish operation to write the tag (CUSTOM) when <see cref="Current_OnTagDiscovered(ITagInfo, bool)"/> event will be raised
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Button_Clicked_StartWriting_Custom(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.Mime);

    /// <summary>
    /// Start publish operation to format the tag when <see cref="Current_OnTagDiscovered(ITagInfo, bool)"/> event will be raised
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Button_Clicked_FormatTag(object sender, System.EventArgs e) => await Publish();

    /// <summary>
    /// Task to publish data to the tag
    /// </summary>
    /// <param name="type"><see cref="NFCNdefTypeFormat"/></param>
    /// <returns>The task to be performed</returns>
    private async Task Publish(NFCNdefTypeFormat? type = null)
    {
        await StartListeningIfNotiOS();
        try
        {
            _type = NFCNdefTypeFormat.Empty;
            if (ChkReadOnly.IsChecked)
            {
                if (!await DisplayAlert("Warning", "Make a Tag read-only operation is permanent and can't be undone. Are you sure you wish to continue?", "Yes", "No"))
                {
                    ChkReadOnly.IsChecked = false;
                    return;
                }
                _makeReadOnly = true;
            }
            else
                _makeReadOnly = false;

            if (type.HasValue) _type = type.Value;
            _nfc.StartPublishing(!type.HasValue);
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    /// <summary>
    /// Display an alert
    /// </summary>
    /// <param name="message">Message to be displayed</param>
    /// <param name="title">Alert title</param>
    /// <returns>The task to be performed</returns>
    private Task ShowAlert(string message, string? title = null) => DisplayAlert(string.IsNullOrWhiteSpace(title) ? ALERT_TITLE : title, message, "OK");

    /// <summary>
    /// Task to start listening for NFC tags if the user's device platform is not iOS
    /// </summary>
    /// <returns>The task to be performed</returns>
    private async Task StartListeningIfNotiOS()
    {
        if (IsDeviceiOS)
        {
            SubscribeEvents();
            return;
        }
        await BeginListening();
    }

    /// <summary>
    /// Task to safely start listening for NFC Tags
    /// </summary>
    /// <returns>The task to be performed</returns>
    private async Task BeginListening()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SubscribeEvents();
                _nfc.StartListening();
            });
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    /// <summary>
    /// Task to safely stop listening for NFC tags
    /// </summary>
    /// <returns>The task to be performed</returns>
    private async Task StopListening()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _nfc.StopListening();
                UnsubscribeEvents();
            });
        }
        catch (Exception ex)
        {
            await ShowAlert(ex.Message);
        }
    }

    /// <summary>
    /// Returns the tag information from NDEF record
    /// </summary>
    /// <param name="record"><see cref="NFCNdefRecord"/></param>
    /// <returns>The tag information</returns>
    private static string GetMessage(NFCNdefRecord record)
    {
        var message = $"Message: {record.Message}";
        message += Environment.NewLine;
        message += $"RawMessage: {(record.Payload is null ? "N/A" : Encoding.UTF8.GetString(record.Payload))}";
        message += Environment.NewLine;
        message += $"Type: {record.TypeFormat}";

        if (!string.IsNullOrWhiteSpace(record.MimeType))
        {
            message += Environment.NewLine;
            message += $"MimeType: {record.MimeType}";
        }

        return message;
    }

    /// <summary>
    /// Write a debug message in the debug console
    /// </summary>
    /// <param name="message">The message to be displayed</param>
    private static void Debug(string message) => System.Diagnostics.Debug.WriteLine(message);


}