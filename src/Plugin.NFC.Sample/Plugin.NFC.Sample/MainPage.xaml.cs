using Plugin.NFC;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace NFCSample
{
	public partial class MainPage : ContentPage
	{
		const string alert_title = "Test NFC";
		public const string MIME_TYPE = "application/com.companyname.nfcsample";

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

				//CrossNFC.Current.SetSpecificMimeTypes(MIME_TYPE);

				// Register NFC events
				CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
				CrossNFC.Current.OnMessagePublished += Current_OnMessagePublished;
				CrossNFC.Current.OnTagDiscovered += Current_OnTagDiscovered;

				// Start NFC tag listening
				CrossNFC.Current.StartListening();
			}
		}

		protected override bool OnBackButtonPressed()
		{
			CrossNFC.Current.StopListening();
			return base.OnBackButtonPressed();
		}

		private void Current_OnMessageReceived(ITagInfo tagInfo)
		{
			Device.BeginInvokeOnMainThread(async () =>
			{
				if (tagInfo == null)
				{
					await ShowAlert("No tag found");
					return;
				}

				if (tagInfo.IsEmpty)
				{
					await ShowAlert("Empty tag");
				}
				else
				{
					var first = tagInfo.Records[0];
					var type = first.TypeFormat.ToString();
					var raw = Encoding.UTF8.GetString(first.Payload);
					await ShowAlert($"{type} => {first.Message} [{raw}] ({first.MimeType})");
				}
			});
		}

		private async void Current_OnMessagePublished(ITagInfo tagInfo)
		{
			if (tagInfo.IsEmpty)
				await ShowAlert("Formatting tag successfully");
			else
				await ShowAlert("Writing tag successfully");

			CrossNFC.Current.StopPublishing();
		}

		private async void Current_OnTagDiscovered(ITagInfo tagInfo, bool format)
		{
			if (!CrossNFC.Current.IsWritingTagSupported)
			{
				await ShowAlert("Writing tag is not supported on this device");
				return;
			}

			try
			{
				NFCNdefRecord record = new NFCNdefRecord
				{
					TypeFormat = NFCNdefTypeFormat.Uri,
					//MimeType = MIME_TYPE,
					Payload = NFCUtils.EncodeToByteArray("https://www.google.fr"),
				};

				tagInfo.Records = new[] { record };

				if (format)
					CrossNFC.Current.ClearMessage(tagInfo);
				else
					CrossNFC.Current.PublishMessage(tagInfo);
			}
			catch (System.Exception ex)
			{
				await ShowAlert(ex.Message);
			}
		}

		private void Button_Clicked_StartWriting(object sender, System.EventArgs e)
		{
			CrossNFC.Current.StartPublishing();
		}

		private void Button_Clicked_FormatTag(object sender, System.EventArgs e)
		{
			CrossNFC.Current.StartPublishing(clearMessage: true);
		}

		private Task ShowAlert(string message)
		{
			return DisplayAlert(alert_title, message, "Annuler");
		}
	}
}
