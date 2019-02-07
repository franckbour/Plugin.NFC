using Plugin.NFC;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace NFCSample
{
	public partial class MainPage : ContentPage
	{
		const string alert_title = "NFC";
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

				SuscribeEvents();

				// Start NFC tag listening
				CrossNFC.Current.StartListening();
				//SuscribeEvents();
			}
		}

		void SuscribeEvents()
		{
			CrossNFC.Current.OnMessageReceived += Current_OnMessageReceived;
			CrossNFC.Current.OnMessagePublished += Current_OnMessagePublished;
			CrossNFC.Current.OnTagDiscovered += Current_OnTagDiscovered;
		}

		void UnsuscribeEvents()
		{
			CrossNFC.Current.OnMessageReceived -= Current_OnMessageReceived;
			CrossNFC.Current.OnMessagePublished -= Current_OnMessagePublished;
			CrossNFC.Current.OnTagDiscovered -= Current_OnTagDiscovered;
		}

		protected override bool OnBackButtonPressed()
		{
			UnsuscribeEvents();
			CrossNFC.Current.StopListening();
			return base.OnBackButtonPressed();
		}

		async void Current_OnMessageReceived(ITagInfo tagInfo)
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
		}

		async void Current_OnMessagePublished(ITagInfo tagInfo)
		{
			CrossNFC.Current.StopPublishing();
			if (tagInfo.IsEmpty)
				await ShowAlert("Formatting tag successfully");
			else
				await ShowAlert("Writing tag successfully");
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
				var record = new NFCNdefRecord
				{
					TypeFormat = NFCNdefTypeFormat.Mime,
					MimeType = MIME_TYPE,
					Payload = NFCUtils.EncodeToByteArray("Hi Buddy!"),
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

		void Button_Clicked_StartWriting(object sender, System.EventArgs e) => CrossNFC.Current.StartPublishing();

		void Button_Clicked_FormatTag(object sender, System.EventArgs e) => CrossNFC.Current.StartPublishing(clearMessage: true);

		Task ShowAlert(string message) => DisplayAlert(alert_title, message, "Cancel");

		//protected override void OnDisappearing()
		//{
		//	UnsuscribeEvents();
		//	base.OnDisappearing();
		//}

	}
}
