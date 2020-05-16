using System;

namespace Plugin.NFC
{
	#region Event delegates
	public delegate void NdefMessageReceivedEventHandler(ITagInfo tagInfo);
	public delegate void NdefMessagePublishedEventHandler(ITagInfo tagInfo);
	public delegate void TagDiscoveredEventHandler(ITagInfo tagInfo, bool format);
	public delegate void OnNfcStatusChangedEventHandler(bool isEnabled);
	#endregion

	/// <summary>
	/// Main interface for NFC
	/// </summary>
	public interface INFC
	{
		/// <summary>
		/// Checks if NFC Feature is available
		/// </summary>
		bool IsAvailable { get; }

		/// <summary>
		/// Checks if NFC Feature is enabled
		/// </summary>
		bool IsEnabled { get; }

		/// <summary>
		/// Checks if writing mode is supported
		/// </summary>
		bool IsWritingTagSupported { get; }

		/// <summary>
		/// Starts tags detection
		/// </summary>
		void StartListening();

		/// <summary>
		/// Stops tags detection
		/// </summary>
		void StopListening();

		/// <summary>
		/// Starts tag publishing (writing or formatting)
		/// </summary>
		/// <param name="clearMessage">Format tag</param>
		void StartPublishing(bool clearMessage = false);

		/// <summary>
		/// Stops tag publishing
		/// </summary>
		void StopPublishing();

		/// <summary>
		/// Publish or write a message on a tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		/// <param name="makeReadOnly">make tag read-only</param>
		void PublishMessage(ITagInfo tagInfo, bool makeReadOnly = false);

		/// <summary>
		/// Format tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		void ClearMessage(ITagInfo tagInfo);

		/// <summary>
		/// Event raised when tag is connected
		/// </summary>
		event EventHandler OnTagConnected;

		/// <summary>
		/// Event raised when tag is disconnected
		/// </summary>
		event EventHandler OnTagDisconnected;

		/// <summary>
		/// Event raised when ndef message is received
		/// </summary>
		event NdefMessageReceivedEventHandler OnMessageReceived;

		/// <summary>
		/// Event raised when a tag is discovered (Editing)
		/// </summary>
		event TagDiscoveredEventHandler OnTagDiscovered;

		/// <summary>
		/// Event raised when ndef message has been published
		/// </summary>
		event NdefMessagePublishedEventHandler OnMessagePublished;

		/// <summary>
		/// Event raised when iOS NFC reading session is cancelled
		/// </summary>
		event EventHandler OniOSReadingSessionCancelled;

		/// <summary>
		/// 
		/// </summary>
		event OnNfcStatusChangedEventHandler OnNfcStatusChanged;
	}

	/// <summary>
	/// UI Messages
	/// </summary>
	internal static class UIMessages
	{
		public const string NFCWritingNotSupported = "Writing NFC Tag opration is not supported on this device";
		public const string NFCDialogAlertMessage = "Please hold your phone near an NFC tag";
		public const string NFCErrorRead = "Read error. Please try again";
		public const string NFCErrorEmptyTag = "Tag is empty";
		public const string NFCErrorReadOnlyTag = "Tag is not writable";
		public const string NFCErrorCapacityTag = "Tag's capacity is too low";
		public const string NFCErrorMissingTag = "Tag is missing";
		public const string NFCErrorMissingTagInfo = "No Tag Information. Nothing to write";
		public const string NFCErrorNotSupportedTag = "Tag is not supported";
		public const string NFCErrorNotCompliantTag = "Tag is not NDEF compliant";
		public const string NFCErrorWrite = "Nothing to write";

		public const string NFCSuccessRead = "Tag Read Operation Successful";
		public const string NFCSuccessWrite = "Tag Write Operation Successful";
		public const string NFCSuccessClear = "Tag Clear Operation Successful";
	}
}
