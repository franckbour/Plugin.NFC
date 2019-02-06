using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Networking.Proximity;

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

        readonly ProximityDevice _defaultDevice;
        long ndefSubscriptionId = -1;

        public bool IsAvailable => _defaultDevice != null;

        public bool IsEnabled
        {
            get
            {
                if (IsAvailable && _defaultDevice != null)
                {
                    try
                    {
                        return _defaultDevice.MaxMessageBytes > 0;
                    }
                    catch (Exception ex)
                    {
                        throw new NotSupportedException("NFC Feature is not supported", ex);
                    } 
                }
                return false;
            }
        }

        public bool IsWritingTagSupported => true;

        public NFCImplementation()
        {
            _defaultDevice = ProximityDevice.GetDefault();
        }

        public void SetSpecificMimeTypes(params string[] types)
        {

        }

        public void StartListening()
        {
            _defaultDevice.DeviceArrived += OnDeviceArrived;
            _defaultDevice.DeviceDeparted += OnDeviceDeparted;
            ndefSubscriptionId = _defaultDevice.SubscribeForMessage("NDEF", OnNdefMessageReceived);
        }

        public void StopListening()
        {
            _defaultDevice.DeviceArrived -= OnDeviceArrived;
            _defaultDevice.DeviceDeparted -= OnDeviceDeparted;

            if (ndefSubscriptionId != -1)
            {
                _defaultDevice.StopSubscribingForMessage(ndefSubscriptionId);
                ndefSubscriptionId = -1;
            }
        }

        public void StartPublishing(bool clearMessage = false)
        {
            if (!IsWritingTagSupported)
                return;

            throw new NotImplementedException();
        }

        public void StopPublishing()
        {
            if (!IsWritingTagSupported)
                return;

            throw new NotImplementedException();
        }

        public void PublishMessage(ITagInfo tagInfo)
        {
            if (!IsWritingTagSupported)
                return;

            throw new NotImplementedException();
        }

        public void ClearMessage(ITagInfo tagInfo)
        {
            if (!IsWritingTagSupported)
                return;

            throw new NotImplementedException();
        }

        #region Private

        void OnDeviceDeparted(ProximityDevice sender)
        {
            OnTagConnected?.Invoke(null, EventArgs.Empty);
        }
        void OnDeviceArrived(ProximityDevice sender)
        {
            OnTagDisconnected?.Invoke(null, EventArgs.Empty);
        }
        void OnNdefMessageReceived(ProximityDevice sender, ProximityMessage message)
        {
            var rawMsg = message.Data.ToArray();

            // Use ndef-nfc for windows : https://andijakl.github.io/ndef-nfc/ and https://github.com/andijakl/ndef-nfc


            // Todo : create TagInfo
            TagInfo tagInfo = new TagInfo
            {
                IsWritable = false,
                Records = new NFCNdefRecord[] { new NFCNdefRecord { TypeFormat = NFCNdefTypeFormat.WellKnown, Payload = rawMsg } }
            };
        
            OnMessageReceived?.Invoke(tagInfo);
        }

        #endregion
    }
}
