using Plugin.NFC;
using System;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace NFCSample
{
	public partial class MainPage : ContentPage
	{
		public const string ALERT_TITLE = "NFC";
		public const string MIME_TYPE = "application/com.companyname.nfcsample";

		NFCNdefTypeFormat _type;

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

				if (!CrossNFC.Current.IsEnabled)
					await ShowAlert("NFC is disabled");

				SubscribeEvents();

				if (Device.RuntimePlatform != Device.iOS)
				{
					// Start NFC tag listening manually
					CrossNFC.Current.StartListening();
				}
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

			if (Device.RuntimePlatform == Device.iOS)
				CrossNFC.Current.OniOSReadingSessionCancelled += Current_OniOSReadingSessionCancelled;
		}

		void UnsubscribeEvents()
		{
			CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
			CrossNFC.Current.OnMessagePublished -= Current_OnMessagePublished;
			CrossNFC.Current.OnTagDiscovered -= Current_OnTagDiscovered;

			if (Device.RuntimePlatform == Device.iOS)
				CrossNFC.Current.OniOSReadingSessionCancelled -= Current_OniOSReadingSessionCancelled;
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
			var title = $"Tag [{serialNumber}]";

			if (!tagInfo.IsSupported)
			{
				await ShowAlert("Unsupported tag", title);
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

		async void Current_OniOSReadingSessionCancelled(object sender, EventArgs e) => await ShowAlert("User has cancelled NFC reading session");

		async void Current_OnMessagePublished(ITagInfo tagInfo)
		{
			try
			{
				CrossNFC.Current.StopPublishing();
				if (tagInfo.IsEmpty)
					await ShowAlert("Formatting tag successfully");
				else
					await ShowAlert("Writing tag successfully");
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
							Payload = NFCUtils.EncodeToByteArray("This is a text message!")
						};
						break;
					case NFCNdefTypeFormat.Uri:
						record = new NFCNdefRecord
						{
							TypeFormat = NFCNdefTypeFormat.Uri,
							Payload = NFCUtils.EncodeToByteArray("https://google.fr")
						};
						break;
					case NFCNdefTypeFormat.Mime:
						record = new NFCNdefRecord
						{
							TypeFormat = NFCNdefTypeFormat.Mime,
							MimeType = MIME_TYPE,
							Payload = NFCUtils.EncodeToByteArray("This is a custom record!")
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
					CrossNFC.Current.PublishMessage(tagInfo);
				}
			}
			catch (System.Exception ex)
			{
				await ShowAlert(ex.Message);
			}
		}

		async void Button_Clicked_StartListening(object sender, System.EventArgs e)
		{
			try
			{
				CrossNFC.Current.StartListening();
			}
			catch (Exception ex)
			{
				await ShowAlert(ex.Message);
			}
		}

		void Button_Clicked_StartWriting(object sender, System.EventArgs e) => Publish(NFCNdefTypeFormat.WellKnown);

		void Button_Clicked_StartWriting_Uri(object sender, System.EventArgs e) => Publish(NFCNdefTypeFormat.Uri);

		void Button_Clicked_StartWriting_Custom(object sender, System.EventArgs e) => Publish(NFCNdefTypeFormat.Mime);

		void Button_Clicked_FormatTag(object sender, System.EventArgs e) => Publish();

		async void Publish(NFCNdefTypeFormat? type = null)
		{
			try
			{
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
	}
}
