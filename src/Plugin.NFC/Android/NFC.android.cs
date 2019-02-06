using Android;
using Android.App;
using Android.Content;
using Android.Nfc;
using Android.Nfc.Tech;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Plugin.NFC
{
    /// <summary>
    /// Interface for NFC
    /// </summary>
    public class NFCImplementation : INFC
    {
        public event EventHandler OnTagConnected;
        public event EventHandler OnTagDisconnected;
        public event NdefMessageReceivedEventHandler OnMessageReceived;
        public event NdefMessagePublishedEventHandler OnMessagePublished;
        public event TagDiscoveredEventHandler OnTagDiscovered;

        readonly List<string> _mimeTypes = new List<string>();
        readonly NfcAdapter _nfcAdapter;

        bool _isWriting;
        bool _isFormatting;
        Context _currentContext;
        Activity _currentActivity;
        Tag _currentTag;

        public Context CurrentContext => _currentContext ??  (_currentContext = CrossNFC.AppContext);

        public Activity CurrentActivity => _currentActivity ?? (_currentActivity = CrossNFC.GetCurrentActivity(true));

        public bool IsAvailable
        {
            get
            {
                if (CurrentContext.CheckCallingOrSelfPermission(Manifest.Permission.Nfc) != Android.Content.PM.Permission.Granted)
                    return false;
                return _nfcAdapter != null;
            }
        }

        public bool IsEnabled => IsAvailable && _nfcAdapter.IsEnabled;

        public bool IsWritingTagSupported => true;

        public NFCImplementation()
        {
            _nfcAdapter = NfcAdapter.GetDefaultAdapter(CurrentContext);
        }

        public void SetSpecificMimeTypes(params string[] types)
        {
            foreach (var type in types)
                _mimeTypes.Add(type);
        }

        public void StartListening()
        {
            var intent = new Intent(CurrentActivity, CurrentActivity.GetType()).AddFlags(ActivityFlags.SingleTop);
            var pendingIntent = PendingIntent.GetActivity(CurrentActivity, 0, intent, 0);

            var ndefFilter = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
            if (_mimeTypes.Count > 0)
                _mimeTypes.ForEach(x => ndefFilter.AddDataType(x));
            ndefFilter.AddDataType("*/*");

            var tagFilter = new IntentFilter(NfcAdapter.ActionTagDiscovered);
            tagFilter.AddCategory(Intent.CategoryDefault);

            var filters = new IntentFilter[] { ndefFilter, tagFilter };

            _nfcAdapter.EnableForegroundDispatch(CurrentActivity, pendingIntent, filters, null);
        }

        public void StopListening()
        {
            DisablePublishing();
            if (_nfcAdapter != null)
                _nfcAdapter.DisableForegroundDispatch(CurrentActivity);
        }

        public void StartPublishing(bool clearMessage = false)
        {
            if (!IsWritingTagSupported)
                return;

            _isWriting = true;
            _isFormatting = clearMessage;
        }

        public void StopPublishing()
        {
            DisablePublishing();
        }

        public void PublishMessage(ITagInfo tagInfo)
        {
            WriteOrClearMessage(tagInfo);
        }

        public void ClearMessage(ITagInfo tagInfo)
        {
            WriteOrClearMessage(tagInfo, true);
        }

        internal void WriteOrClearMessage(ITagInfo tagInfo, bool clearMessage = false)
        {
            if (_currentTag == null)
                throw new Exception("Tag error: No tag to write");

            if (tagInfo == null)
                throw new Exception("TagInfo error: No tag to write");

            Ndef ndef = Ndef.Get(_currentTag);
            if (ndef != null)
            {
                try
                {
                    if (!ndef.IsWritable)
                        throw new Exception("Tag is not writable");

                    if (ndef.MaxSize < NFCUtils.GetSize(tagInfo.Records))
                        throw new Exception("Tag is too small");

                    ndef.Connect();
                    OnTagConnected?.Invoke(null, EventArgs.Empty);

                    NdefMessage message = null;
                    if (clearMessage)
                    {
                        message = GetEmptyNdefMessage();
                    }
                    else
                    {
                        var records = new List<NdefRecord>();
                        for (int i = 0; i < tagInfo.Records.Length; i++)
                        {
                            var record = tagInfo.Records[i];
                            if (GetAndroidNdefRecord(record) is NdefRecord ndefRecord)
                                records.Add(ndefRecord);
                        }

                        if (records.Any())
                            message = new NdefMessage(records.ToArray());
                    }

                    if (message != null)
                    {
                        ndef.WriteNdefMessage(message);
                        var nTag = GetTagInfo(_currentTag, ndef.NdefMessage);
                        OnMessagePublished?.Invoke(nTag);
                    }
                    else
                        throw new Exception("nothing to write on tag");
                }
                catch (Android.Nfc.TagLostException tlex)
                {
                    throw new Exception("Tag lost error: " + tlex.Message);
                }
                catch (Java.IO.IOException ioex)
                {
                    throw new Exception("Tag IO error: " + ioex.Message);
                }
                catch (Android.Nfc.FormatException fe)
                {
                    throw new Exception("Tag format error: " + fe.Message);
                }
                catch (Exception ex)
                {
                    throw new Exception("Tag other error:" + ex.Message);
                }
                finally
                {
                    if (ndef.IsConnected)
                        ndef.Close();

                    _currentTag = null;
                    OnTagDisconnected?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        internal void HandleNewIntent(Intent intent)
        {
            if (intent == null)
                return;

            if (intent.Action == NfcAdapter.ActionTagDiscovered || intent.Action == NfcAdapter.ActionNdefDiscovered)
            {
                _currentTag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
                if (_currentTag != null)
                {
                    var nTag = GetTagInfo(_currentTag);
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

        #region Private

        void DisablePublishing()
        {
            _isWriting = false;
            _isFormatting = false;
        }
        NFCNdefRecord[] GetRecords(NdefRecord[] records)
        {
            var results = new NFCNdefRecord[records.Length];
            for (int i = 0; i < records.Length; i++)
            {
                var ndefRecord = new NFCNdefRecord
                {
                    TypeFormat = (NFCNdefTypeFormat)records[i].Tnf,
                    
                    MimeType = records[i].ToMimeType(),
                    Payload = records[i].GetPayload()
                };
                results.SetValue(ndefRecord, i);
            }
            return results;
        }
        ITagInfo GetTagInfo(Tag tag, NdefMessage ndefMessage = null)
        {
            if (tag == null)
                return null;

            Ndef ndef = Ndef.Get(tag);
            if (ndef == null)
                return null;

            if (ndefMessage == null)
                ndefMessage = ndef.CachedNdefMessage;

            TagInfo nTag = new TagInfo()
            {
                IsWritable = ndef.IsWritable
            };

            if (ndefMessage != null)
            {
                var records = ndefMessage.GetRecords();
                nTag.Records = GetRecords(records);
            }

            return nTag;
        }
        NdefRecord GetAndroidNdefRecord(NFCNdefRecord record)
        {
            if (record == null)
                return null;

            NdefRecord ndefRecord = null;
            switch (record.TypeFormat)
            {
                case NFCNdefTypeFormat.WellKnown:
                    ndefRecord = NdefRecord.CreateTextRecord(Locale.Default.ToLanguageTag(), Encoding.UTF8.GetString(record.Payload));
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
        NdefRecord GetEmptyNdefRecord()
        {
            byte[] empty = Array.Empty<byte>();
            return new NdefRecord(NdefRecord.TnfEmpty, empty, empty, empty);
        }
        NdefMessage GetEmptyNdefMessage()
        {
            NdefRecord[] records = new NdefRecord[1];
            records[0] = GetEmptyNdefRecord();
            return new NdefMessage(records);
        }

        #endregion
    }
}
