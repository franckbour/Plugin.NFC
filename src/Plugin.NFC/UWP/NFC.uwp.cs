using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Networking.Proximity;

namespace Plugin.NFC
{
	/// <summary>
	/// Windows implementation of <see cref="INFC"/>
	/// </summary>
	public class NFCImplementation : INFC
	{
		public event EventHandler OnTagConnected;
		public event EventHandler OnTagDisconnected;
		public event NdefMessageReceivedEventHandler OnMessageReceived;
		public event NdefMessagePublishedEventHandler OnMessagePublished;
		public event TagDiscoveredEventHandler OnTagDiscovered;
		public event EventHandler OniOSReadingSessionCancelled;
		public event OnNfcStatusChangedEventHandler OnNfcStatusChanged;
		public event TagListeningStatusChangedEventHandler OnTagListeningStatusChanged;

		readonly ProximityDevice _defaultDevice;
		long _ndefSubscriptionId = -1;

		/// <summary>
		/// Checks if NFC Feature is available
		/// </summary>
		public bool IsAvailable => _defaultDevice != null;

		/// <summary>
		/// Checks if NFC Feature is enabled
		/// </summary>
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

		/// <summary>
		/// Checks if writing mode is supported
		/// </summary>
		public bool IsWritingTagSupported => true;

		/// <summary>
		/// NFC configuration
		/// </summary>
		public NfcConfiguration Configuration { get; private set; }

		/// <summary>
		/// Default constructor
		/// </summary>
		public NFCImplementation()
		{
			_defaultDevice = ProximityDevice.GetDefault();
			Configuration = NfcConfiguration.GetDefaultConfiguration();
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
			_defaultDevice.DeviceArrived += OnDeviceArrived;
			_defaultDevice.DeviceDeparted += OnDeviceDeparted;
			_ndefSubscriptionId = _defaultDevice.SubscribeForMessage("NDEF", OnNdefMessageReceived);
			OnTagListeningStatusChanged?.Invoke(true);
		}

		/// <summary>
		/// Stops tags detection
		/// </summary>
		public void StopListening()
		{
			_defaultDevice.DeviceArrived -= OnDeviceArrived;
			_defaultDevice.DeviceDeparted -= OnDeviceDeparted;

			if (_ndefSubscriptionId != -1)
			{
				_defaultDevice.StopSubscribingForMessage(_ndefSubscriptionId);
				_ndefSubscriptionId = -1;
			}

			OnTagListeningStatusChanged?.Invoke(false);
		}

		/// <summary>
		/// Starts tag publishing (writing or formatting)
		/// </summary>
		/// <param name="clearMessage">Format tag</param>
		public void StartPublishing(bool clearMessage = false)
		{
			if (!IsWritingTagSupported)
				return;

			throw new NotImplementedException();
		}

		/// <summary>
		/// Stops tag publishing
		/// </summary>
		public void StopPublishing()
		{
			if (!IsWritingTagSupported)
				return;

			throw new NotImplementedException();
		}

		/// <summary>
		/// Publish or write a message on a tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		/// <param name="makeReadOnly">make tag read-only</param>
		public void PublishMessage(ITagInfo tagInfo, bool makeReadOnly = false)
		{
			if (!IsWritingTagSupported)
				return;

			throw new NotImplementedException();
		}

		/// <summary>
		/// Format tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		public void ClearMessage(ITagInfo tagInfo)
		{
			if (!IsWritingTagSupported)
				return;

			throw new NotImplementedException();
		}

		#region Private

		/// <summary>
		/// Event raised when a tag is connected
		/// </summary>
		/// <param name="sender">Object <see cref="ProximityDevice"/></param>
		void OnDeviceArrived(ProximityDevice sender) => OnTagConnected?.Invoke(null, EventArgs.Empty);

		/// <summary>
		/// Event raised when a tag is disconnected
		/// </summary>
		/// <param name="sender">Object <see cref="ProximityDevice"/></param>
		void OnDeviceDeparted(ProximityDevice sender) => OnTagDisconnected?.Invoke(null, EventArgs.Empty);

		/// <summary>
		/// Event raised when a NDEF message is received
		/// </summary>
		/// <param name="sender">Object <see cref="ProximityDevice"/></param>
		/// <param name="message">Object <see cref="ProximityMessage"/></param>
		void OnNdefMessageReceived(ProximityDevice sender, ProximityMessage message)
		{
			var rawMsg = message.Data.ToArray();

			// Todo : create TagInfo
			// May be use ndef-nfc for windows : https://andijakl.github.io/ndef-nfc/ and https://github.com/andijakl/ndef-nfc
			var tagInfo = new TagInfo
			{
				IsWritable = false,
				Records = new NFCNdefRecord[] { new NFCNdefRecord { TypeFormat = NFCNdefTypeFormat.WellKnown, Payload = rawMsg } }
			};
		
			OnMessageReceived?.Invoke(tagInfo);
		}

		#endregion
	}
}
