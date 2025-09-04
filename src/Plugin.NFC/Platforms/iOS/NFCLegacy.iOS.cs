using CoreFoundation;
using CoreNFC;
using Foundation;
using Plugin.NFC.Configuration;
using Plugin.NFC.Utils;
using UIKit;

namespace Plugin.NFC;

/// <summary>
/// Old iOS implementation of <see cref="INFC"/> (iOS < 13)
/// </summary>
internal sealed class NFCImplementation_Before_iOS13 : NFCNdefReaderSessionDelegate, INFC
{
    public const string SessionTimeoutMessage = "session timeout";

    /// <summary>
    /// Default constructor
    /// </summary>
    public NFCImplementation_Before_iOS13()
    {
    }

    private bool _isWriting;
    private bool _isFormatting;
    private bool _customInvalidation = false;
    private INFCNdefTag? _tag;

    public event EventHandler? OnTagConnected;
    public event EventHandler? OnTagDisconnected;
    public event NdefMessageReceivedEventHandler? OnMessageReceived;
    public event NdefMessagePublishedEventHandler? OnMessagePublished;
    public event TagDiscoveredEventHandler? OnTagDiscovered;
    public event EventHandler? OniOSReadingSessionCancelled;
    public event OnNfcStatusChangedEventHandler? OnNfcStatusChanged;
    public event TagListeningStatusChangedEventHandler? OnTagListeningStatusChanged;

    private NFCNdefReaderSession? NfcSession { get; set; }

    /// <summary>
    /// Checks if NFC Feature is available
    /// </summary>
    public bool IsAvailable => NFCNdefReaderSession.ReadingAvailable;

    /// <summary>
    /// Checks if NFC Feature is enabled
    /// </summary>
    public bool IsEnabled => IsAvailable;

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
        _customInvalidation = false;
        _isWriting = false;
        _isFormatting = false;

        NfcSession = new NFCNdefReaderSession(this, NSOperationQueue.CurrentQueue.UnderlyingQueue, true)
        {
            AlertMessage = Configuration.Messages.NFCDialogAlertMessage
        };

        NfcSession?.BeginSession();
        OnTagListeningStatusChanged?.Invoke(true);
    }

    /// <summary>
    /// Stops tags detection
    /// </summary>
    public void StopListening()
    {
        NfcSession?.InvalidateSession();
    }

    /// <summary>
    /// Starts tag publishing (writing or formatting)
    /// </summary>
    /// <param name="clearMessage">Format tag</param>
    public void StartPublishing(bool clearMessage = false)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(Configuration.Messages.NFCWritingNotSupported);
        }

        _customInvalidation = false;
        _isWriting = true;
        _isFormatting = clearMessage;

        NfcSession = new NFCNdefReaderSession(this, NSOperationQueue.CurrentQueue.UnderlyingQueue, true)
        {
            AlertMessage = Configuration.Messages.NFCDialogAlertMessage
        };

        NfcSession?.BeginSession();
        OnTagListeningStatusChanged?.Invoke(true);
    }

    /// <summary>
    /// Stops tag publishing
    /// </summary>
    public void StopPublishing()
    {
        _isWriting = _isFormatting = _customInvalidation = false;
        _tag = null;
        NfcSession?.InvalidateSession();
    }

    /// <summary>
    /// Publish or write a message on a tag
    /// </summary>
    /// <param name="tagInfo">see <see cref="ITagInfo"/></param>
    /// <param name="makeReadOnly">Make a tag read-only</param>
    public void PublishMessage(ITagInfo tagInfo, bool makeReadOnly = false) => WriteOrClearMessage(_tag, tagInfo, false, makeReadOnly);

    /// <summary>
    /// Format tag
    /// </summary>
    /// <param name="tagInfo">see <see cref="ITagInfo"/></param>
    public void ClearMessage(ITagInfo tagInfo) => WriteOrClearMessage(_tag, tagInfo, true);

    /// <summary>
    /// Event raised when NDEF messages are detected
    /// </summary>
    /// <param name="session">iOS <see cref="NFCNdefReaderSession"/></param>
    /// <param name="messages">Array of iOS <see cref="NFCNdefMessage"/></param>
    public override void DidDetect(NFCNdefReaderSession session, NFCNdefMessage[] messages)
    {
        OnTagConnected?.Invoke(null, EventArgs.Empty);

        if (messages is not null && messages.Length > 0)
        {
            var first = messages[0];
            var tagInfo = new TagInfo
            {
                IsWritable = false,
                Records = NFCNdefPayloadExtensions.GetRecords(first.Records)
            };
            OnMessageReceived?.Invoke(tagInfo);
        }

        OnTagDisconnected?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Event raised when NFC tag detected
    /// </summary>
    /// <param name="session">iOS <see cref="NFCNdefReaderSession"/></param>
    /// <param name="tags">Array of iOS <see cref="INFCNdefTag"/></param>
    public override void DidDetectTags(NFCNdefReaderSession session, INFCNdefTag[] tags)
    {
        _customInvalidation = false;
        _tag = tags.First();

        session.ConnectToTag(_tag, error =>
        {
            if (error is not null)
            {
                Invalidate(session, error.LocalizedDescription);
                return;
            }

            if (_tag is null)
            {
                Invalidate(session, Configuration.Messages.NFCErrorNotCompliantTag);
                return;
            }

            _tag.QueryNdefStatus((status, capacity, ndefError) =>
            {
                if (ndefError is not null)
                {
                    Invalidate(session, ndefError.LocalizedDescription);
                    return;
                }

                var isNdefSupported = status != NFCNdefStatus.NotSupported;

                var identifier = NfcNdefTagExtensions.GetTagIdentifier(_tag);

                identifier ??= [];

                if (identifier is null)
                {
                    Invalidate(session, Configuration.Messages.NFCErrorNotCompliantTag);
                    return;
                }

                var nTag = new TagInfo(identifier, isNdefSupported)
                {
                    IsWritable = status == NFCNdefStatus.ReadWrite,
                    Capacity = Convert.ToInt32(capacity)
                };

                if (!isNdefSupported)
                {
                    session.AlertMessage = Configuration.Messages.NFCErrorNotSupportedTag;

                    OnMessageReceived?.Invoke(nTag);
                    Invalidate(session);
                    return;
                }

                if (_isWriting)
                {
                    // Write mode
                    OnTagDiscovered?.Invoke(nTag, _isFormatting);
                }
                else
                {
                    // Read mode
                    _tag.ReadNdef((message, readError) =>
                    {
                        if (readError is not null)
                        {
                            Invalidate(session, readError.Code == (int)NFCReaderError.NdefReaderSessionErrorZeroLengthMessage
                                ? Configuration.Messages.NFCErrorEmptyTag
                                : Configuration.Messages.NFCErrorRead);
                            return;
                        }

                        session.AlertMessage = Configuration.Messages.NFCSuccessRead;

                        nTag.Records = NFCNdefPayloadExtensions.GetRecords(message?.Records);
                        OnMessageReceived?.Invoke(nTag);
                        Invalidate(session);
                    });
                }
            });
        });
    }

    /// <summary>
    /// Event raised when an error happened during detection
    /// </summary>
    /// <param name="session">iOS <see cref="NFCTagReaderSession"/></param>
    /// <param name="error">iOS <see cref="NSError"/></param>
    public override void DidInvalidate(NFCNdefReaderSession session, NSError error)
    {
        OnTagListeningStatusChanged?.Invoke(false);

        var readerError = (NFCReaderError)(long)error.Code;
        if (readerError != NFCReaderError.ReaderSessionInvalidationErrorFirstNDEFTagRead && readerError != NFCReaderError.ReaderSessionInvalidationErrorUserCanceled)
        {
            var alertController = UIAlertController.Create(Configuration.Messages.NFCSessionInvalidated, error.LocalizedDescription.ToLower().Equals(SessionTimeoutMessage) ? Configuration.Messages.NFCSessionTimeout : error.LocalizedDescription, UIAlertControllerStyle.Alert);
            alertController.AddAction(UIAlertAction.Create(Configuration.Messages.NFCSessionInvalidatedButton, UIAlertActionStyle.Default, null));
            OniOSReadingSessionCancelled?.Invoke(null, EventArgs.Empty);
            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                GetCurrentController().PresentViewController(alertController, true, null);
            });
        }
        else if (readerError == NFCReaderError.ReaderSessionInvalidationErrorUserCanceled && !_customInvalidation)
        {
            OniOSReadingSessionCancelled?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Write or Clear a NDEF message
    /// </summary>
    /// <param name="tag"><see cref="INFCTag"/></param>
    /// <param name="tagInfo"><see cref="ITagInfo"/></param>
    /// <param name="clearMessage">Clear Message</param>
    /// <param name="makeReadOnly">Make a tag read-only</param>
    internal void WriteOrClearMessage(INFCNdefTag? tag, ITagInfo? tagInfo, bool clearMessage = false, bool makeReadOnly = false)
    {
        if (NfcSession is null)
        {
            return;
        }

        if (tag is null)
        {
            Invalidate(NfcSession, Configuration.Messages.NFCErrorMissingTag);
            return;
        }

        if (tagInfo is null || (!clearMessage && tagInfo.Records.Any(record => record.Payload is null)))
        {
            Invalidate(NfcSession, Configuration.Messages.NFCErrorMissingTagInfo);
            return;
        }

        if (_tag is null)
        {
            Invalidate(NfcSession, Configuration.Messages.NFCErrorNotCompliantTag);
            return;
        }

        try
        {
            if (!_tag.Available)
            {
                NfcSession.ConnectToTag(_tag, (error) =>
                {
                    if (error is not null)
                    {
                        Invalidate(NfcSession, error.LocalizedDescription);
                        return;
                    }

                    ExecuteWriteOrClear(NfcSession, _tag, tagInfo, clearMessage);
                });
            }
            else
            {
                ExecuteWriteOrClear(NfcSession, _tag, tagInfo, clearMessage);
            }

            if (!clearMessage && makeReadOnly)
            {
                MakeTagReadOnly(NfcSession, tag);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            OnTagDisconnected?.Invoke(null, EventArgs.Empty);
        }
    }

    #region Private

    /// <summary>
    /// Returns the current iOS controller
    /// </summary>
    /// <returns>Object <see cref="UIViewController"/></returns>
    private static UIViewController GetCurrentController()
    {
#if IOS15_0_OR_GREATER
        var window = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>().SelectMany(s => s.Windows).First(w => w.IsKeyWindow);
#else
        var window = UIApplication.SharedApplication.Windows.FirstOrDefault(w => w.IsKeyWindow);
#endif
        var vc = window.RootViewController!;

        while (vc.PresentedViewController is not null)
            vc = vc.PresentedViewController;

        return vc;
    }

    /// <summary>
    /// Writes or clears a TAG
    /// </summary>
    /// <param name="session"><see cref="NFCTagReaderSession"/></param>
    /// <param name="tag"><see cref="INFCNdefTag"/></param>
    /// <param name="tagInfo"><see cref="ITagInfo"/></param>
    /// <param name="clearMessage">Clear message</param>
    private void ExecuteWriteOrClear(NFCNdefReaderSession session, INFCNdefTag tag, ITagInfo tagInfo, bool clearMessage = false) 
        => tag.QueryNdefStatus((status, capacity, error) =>
        {
            if (error is not null)
            {
                Invalidate(session, error.LocalizedDescription);
                return;
            }

            if (status == NFCNdefStatus.ReadOnly)
            {
                Invalidate(session, Configuration.Messages.NFCErrorReadOnlyTag);
                return;
            }

            if (Convert.ToInt32(capacity) < NFCUtils.GetSize(tagInfo.Records))
            {
                Invalidate(session, Configuration.Messages.NFCErrorCapacityTag);
                return;
            }

            NFCNdefMessage? message = null;
            if (!clearMessage)
            {
                session.AlertMessage = Configuration.Messages.NFCSuccessWrite;

                var records = new List<NFCNdefPayload>();

                foreach (var record in tagInfo.Records)
                {
                    if (NFCNdefPayloadExtensions.GetiOSPayload(record, Configuration) is NFCNdefPayload ndefPayload)
                        records.Add(ndefPayload);
                }

                if (records.Any())
                    message = new NFCNdefMessage(records.ToArray());
            }
            else
            {
                session.AlertMessage = Configuration.Messages.NFCSuccessClear;
                message = NFCNdefMessageExtensions.EmptyNdefMessage;
            }

            if (message is not null)
            {
                tag.WriteNdef(message, (error) =>
                {
                    if (error is not null)
                    {
                        Invalidate(session, error.LocalizedDescription);
                        return;
                    }

                    tagInfo.Records = NFCNdefPayloadExtensions.GetRecords(message.Records);
                    OnMessagePublished?.Invoke(tagInfo);
                    Invalidate(NfcSession!);
                });
            }
            else
                Invalidate(session, Configuration.Messages.NFCErrorWrite);
        });

    /// <summary>
    /// Make a tag read-only
    /// WARNING: This operation is permanent
    /// </summary>
    /// <param name="session"><see cref="NFCTagReaderSession"/></param>
    /// <param name="tag"><see cref="ITagInfo"/></param>
    /// <param name="ndefTag"><see cref="INFCNdefTag"/></param>
    private static void MakeTagReadOnly(NFCNdefReaderSession session, INFCNdefTag tag) 
        => session.ConnectToTag(tag, (error) =>
        {
            if (error is not null)
            {
                Console.WriteLine(error.LocalizedDescription);
                return;
            }

            tag.WriteLock((error) =>
            {
                if (error is not null)
                    Console.WriteLine("Error when locking a tag on iOS: " + error.LocalizedDescription);
                else
                    Console.WriteLine("Locking Successful!");
            });
        });

    /// <summary>
    /// Invalidate the session
    /// </summary>
    /// <param name="session"><see cref="NFCTagReaderSession"/></param>
    /// <param name="message">Message to show</param>
    private void Invalidate(NFCNdefReaderSession session, string? message = null)
    {
        _customInvalidation = true;
        if (string.IsNullOrWhiteSpace(message))
            session.InvalidateSession();
        else
            session.InvalidateSession(message);
    }

    #endregion
}
