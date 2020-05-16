using Plugin.NFC;
using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace NFCSample
{
	public partial class MainPage : ContentPage, INotifyPropertyChanged
	{
		public const string ALERT_TITLE = "NFC";
		public const string MIME_TYPE = "application/com.companyname.nfcsample";

		NFCNdefTypeFormat _type;
		bool _makeReadOnly = false;
		bool _isDeviceiOS = false;

		/// <summary>
		/// Property that tracks whether the Android device is still listening,
		/// so it can indicate that to the user.
		/// </summary>
		public bool DeviceIsListening
		{
			get => _deviceIsListening && NfcIsEnabled;
			set
			{
				_deviceIsListening = value;
				OnPropertyChanged(nameof(DeviceIsListening));
			}
		}
		private bool _deviceIsListening;

		private bool _nfcIsEnabled;
		public bool NfcIsEnabled {
			get => _nfcIsEnabled; 
			set
			{
				_nfcIsEnabled = value;
				OnPropertyChanged(nameof(NfcIsEnabled));
				OnPropertyChanged(nameof(NfcIsDisabled));
			}
		}

		public bool NfcIsDisabled => !NfcIsEnabled;

		public MainPage()
		{
			InitializeComponent();
		}

		protected async override void OnAppearing()
		{
			base.OnAppearing();

			if (CrossNFC.IsSupported)
			{
				if (!CrossNFC.Current.IsAvailable)
					await ShowAlert("NFC is not available");

				NfcIsEnabled = CrossNFC.Current.IsEnabled;
				if (!NfcIsEnabled)
					await ShowAlert("NFC is disabled");

				if (Device.RuntimePlatform == Device.iOS)
					_isDeviceiOS = true;

				SubscribeEvents();

				await StartListeningIfNotiOS();
			}
		}

		protected override bool OnBackButtonPressed()
		{
			UnsubscribeEvents();
			CrossNFC.Current.StopListening();
			return base.OnBackButtonPressed();
		}

		void SubscribeEvents()
		{
			CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
			CrossNFC.Current.OnMessagePublished += Current_OnMessagePublished;
			CrossNFC.Current.OnTagDiscovered += Current_OnTagDiscovered;
			CrossNFC.Current.OnNfcStatusChanged += Current_OnNfcStatusChanged;

			if (_isDeviceiOS)
				CrossNFC.Current.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
		}

		void UnsubscribeEvents()
		{
			CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
			CrossNFC.Current.OnMessagePublished -= Current_OnMessagePublished;
			CrossNFC.Current.OnTagDiscovered -= Current_OnTagDiscovered;
			CrossNFC.Current.OnNfcStatusChanged -= Current_OnNfcStatusChanged;

			if (_isDeviceiOS)
				CrossNFC.Current.OniOSReadingSessionCancelled -= Current_OniOSReadingSessionCancelled;
		}

		async void Current_OnNfcStatusChanged(bool isEnabled)
		{
			NfcIsEnabled = isEnabled;
			await ShowAlert($"NFC has been {(isEnabled ? "enabled" : "disabled")}");
		}

		async void Current_OnMessageReceived(ITagInfo tagInfo)
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

			if (!tagInfo.IsSupported)
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
			DeviceIsListening = false;
		}

		void Current_OniOSReadingSessionCancelled(object sender, EventArgs e) => Debug("User has cancelled NFC Session");

		async void Current_OnMessagePublished(ITagInfo tagInfo)
		{
			try
			{
				ChkReadOnly.IsChecked = false;
				CrossNFC.Current.StopPublishing();
				if (tagInfo.IsEmpty)
					await ShowAlert("Formatting tag operation successful");
				else
					await ShowAlert("Writing tag operation successful");
			}
			catch (System.Exception ex)
			{
				await ShowAlert(ex.Message);
			}
		}

		async void Current_OnTagDiscovered(ITagInfo tagInfo, bool format)
		{
			if (!CrossNFC.Current.IsWritingTagSupported)
			{
				await ShowAlert("Writing tag is not supported on this device");
				return;
			}

			try
			{
				NFCNdefRecord record = null;
				switch(_type)
				{
					case NFCNdefTypeFormat.WellKnown:
						record = new NFCNdefRecord
						{
							TypeFormat = NFCNdefTypeFormat.WellKnown,
							MimeType = MIME_TYPE,
							Payload = NFCUtils.EncodeToByteArray("Plugin.NFC is awesome!")
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

				if (!format && record == null)
					throw new Exception("Record can't be null.");

				tagInfo.Records = new[] { record };

				if (format)
					CrossNFC.Current.ClearMessage(tagInfo);
				else
				{
					CrossNFC.Current.PublishMessage(tagInfo, _makeReadOnly);
				}
			}
			catch (System.Exception ex)
			{
				await ShowAlert(ex.Message);
			}
			DeviceIsListening = false;
		}

		async void Button_Clicked_StartListening(object sender, System.EventArgs e) => await BeginListening();

		async void Button_Clicked_StartWriting(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.WellKnown);

		async void Button_Clicked_StartWriting_Uri(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.Uri);

		async void Button_Clicked_StartWriting_Custom(object sender, System.EventArgs e) => await Publish(NFCNdefTypeFormat.Mime);

		async void Button_Clicked_FormatTag(object sender, System.EventArgs e) => await Publish();

		async Task Publish(NFCNdefTypeFormat? type = null)
		{
			await StartListeningIfNotiOS();
			try
			{
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
				CrossNFC.Current.StartPublishing(!type.HasValue);
			}
			catch (System.Exception ex)
			{
				await ShowAlert(ex.Message);
			}
		}

		string GetMessage(NFCNdefRecord record)
		{
			var message = $"Message: {record.Message}";
			message += Environment.NewLine;
			message += $"RawMessage: {Encoding.UTF8.GetString(record.Payload)}";
			message += Environment.NewLine;
			message += $"Type: {record.TypeFormat.ToString()}";

			if (!string.IsNullOrWhiteSpace(record.MimeType))
			{
				message += Environment.NewLine;
				message += $"MimeType: {record.MimeType}";
			}

			return message;
		}

		void Debug(string message) => System.Diagnostics.Debug.WriteLine(message);

		Task ShowAlert(string message, string title = null) => DisplayAlert(string.IsNullOrWhiteSpace(title) ? ALERT_TITLE : title, message, "Cancel");

		/// <summary>
		/// Task to start listening for NFC tags if the user's device platform is not iOS
		/// </summary>
		/// <returns>Task to be performed</returns>
		async Task StartListeningIfNotiOS()
		{
			if (_isDeviceiOS)
				return;
			await BeginListening();
		}

		/// <summary>
		/// Task to safely start listening for NFC Tags
		/// </summary>
		/// <returns>The task to be performed</returns>
		async Task BeginListening()
		{
			DeviceIsListening = true;
			try
			{
				CrossNFC.Current.StartListening();
			}
			catch (Exception ex)
			{
				await ShowAlert(ex.Message);
			}
		}
	}
}
