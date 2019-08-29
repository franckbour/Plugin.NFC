using CoreFoundation;
using CoreNFC;
using Foundation;
using System;
using System.IO;
using UIKit;

namespace Plugin.NFC
{
	/// <summary>
	/// iOS implementation of <see cref="INFC"/>
	/// </summary>
	public class NFCImplementation : NSObject, INFC, INFCNdefReaderSessionDelegate
	{
		public event EventHandler OnTagConnected;
		public event EventHandler OnTagDisconnected;
		public event NdefMessageReceivedEventHandler OnMessageReceived;
		public event NdefMessagePublishedEventHandler OnMessagePublished;
		public event TagDiscoveredEventHandler OnTagDiscovered;
		public event EventHandler OniOSReadingSessionCancelled;

		readonly string _writingNotSupportedMessage = "Writing NFC Tag is not supported on iOS";

		NFCNdefReaderSession NfcSession { get; set; }

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
		public bool IsWritingTagSupported => false;

		/// <summary>
		/// Default constructor
		/// </summary>
		public NFCImplementation() { }

		/// <summary>
		/// Starts tags detection
		/// </summary>
		public void StartListening()
		{
			NfcSession = new NFCNdefReaderSession(this, DispatchQueue.CurrentQueue, true);
			NfcSession?.BeginSession();
		}

		/// <summary>
		/// Stops tags detection
		/// </summary>
		public void StopListening() => NfcSession?.InvalidateSession();

		/// <summary>
		/// Starts tag publishing (writing or formatting)
		/// </summary>
		/// <param name="clearMessage">Format tag</param>
		public void StartPublishing(bool clearMessage = false) => throw new NotSupportedException(_writingNotSupportedMessage);

		/// <summary>
		/// Stops tag publishing
		/// </summary>
		public void StopPublishing() => throw new NotSupportedException(_writingNotSupportedMessage);

		/// <summary>
		/// Publish or write a message on a tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		public void PublishMessage(ITagInfo tagInfo) => throw new NotSupportedException(_writingNotSupportedMessage);

		/// <summary>
		/// Format tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		public void ClearMessage(ITagInfo tagInfo) => throw new NotSupportedException(_writingNotSupportedMessage);

		/// <summary>
		/// Event raised when tag is detected
		/// </summary>
		/// <param name="session">iOS <see cref="NFCNdefReaderSession"/></param>
		/// <param name="messages">Array of iOS <see cref="NFCNdefMessage"/></param>
		public void DidDetect(NFCNdefReaderSession session, NFCNdefMessage[] messages)
		{
			OnTagConnected?.Invoke(null, EventArgs.Empty);

			if (messages != null && messages.Length > 0)
			{
				var first = messages[0];
				var tagInfo = new TagInfo
				{
					IsWritable = false,
					Records = GetRecords(first.Records)
				};
				OnMessageReceived?.Invoke(tagInfo);
			}

			OnTagDisconnected?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>
		/// Event raised when an error happened during detection
		/// </summary>
		/// <param name="session">iOS <see cref="NFCNdefReaderSession"/></param>
		/// <param name="error">iOS <see cref="NSError"/></param>
		public void DidInvalidate(NFCNdefReaderSession session, NSError error)
		{
			var readerError = (NFCReaderError)(long)error.Code;
			if (readerError != NFCReaderError.ReaderSessionInvalidationErrorFirstNDEFTagRead && readerError != NFCReaderError.ReaderSessionInvalidationErrorUserCanceled)
			{
				var alertController = UIAlertController.Create("Session Invalidated", error.LocalizedDescription, UIAlertControllerStyle.Alert);
				alertController.AddAction(UIAlertAction.Create("Ok", UIAlertActionStyle.Default, null));
				DispatchQueue.MainQueue.DispatchAsync(() =>
				{
					GetCurrentController().PresentViewController(alertController, true, null);
				});
			}
			else if (readerError == NFCReaderError.ReaderSessionInvalidationErrorUserCanceled)
				OniOSReadingSessionCancelled.Invoke(null, EventArgs.Empty);
		}

		#region Private

		/// <summary>
		/// Returns the current iOS controller
		/// </summary>
		/// <returns>Object <see cref="UIViewController"/></returns>
		UIViewController GetCurrentController()
		{
			var window = UIApplication.SharedApplication.KeyWindow;
			var vc = window.RootViewController;
			while (vc.PresentedViewController != null)
				vc = vc.PresentedViewController;
			return vc;
		}

		/// <summary>
		/// Transforms an array of <see cref="NFCNdefPayload"/> into an array of <see cref="NFCNdefRecord"/>
		/// </summary>
		/// <param name="records">Array of <see cref="NFCNdefPayload"/></param>
		/// <returns>Array of <see cref="NFCNdefRecord"/></returns>
		NFCNdefRecord[] GetRecords(NFCNdefPayload[] records)
		{
			var results = new NFCNdefRecord[records.Length];
			for (var i = 0; i < records.Length; i++)
			{
				var record = records[i];
				var ndefRecord = new NFCNdefRecord
				{
					TypeFormat = (NFCNdefTypeFormat)record.TypeNameFormat,
					Payload = record.Payload.ToByteArray()
				};
				results.SetValue(ndefRecord, i);
			}
			return results;
		}

		#endregion
	}

	/// <summary>
	/// NFC Ndef Message extensions class
	/// </summary>
	internal static class NFCNdefMessageExtensions
	{
		/// <summary>
		/// Convert an iOS <see cref="NSData"/> into an array of bytes
		/// </summary>
		/// <param name="data">iOS <see cref="NSData"/></param>
		/// <returns>Array of bytes</returns>
		public static byte[] ToByteArray(this NSData data)
		{
			var bytes = new byte[data.Length];
			System.Runtime.InteropServices.Marshal.Copy(data.Bytes, bytes, 0, Convert.ToInt32(data.Length));
			return bytes;
		}

		/// <summary>
		/// Converte an iOS <see cref="NFCNdefMessage"/> into an array of bytes
		/// </summary>
		/// <param name="message">iOS <see cref="NFCNdefMessage"/></param>
		/// <returns>Array of bytes</returns>
		public static byte[] ToByteArray(this NFCNdefMessage message)
		{
			var records = message?.Records;

			// Empty message: single empty record
			if (records == null || records.Length == 0)
			{
				records = new NFCNdefPayload[] { null };
			}

			var m = new MemoryStream();
			for (var i = 0; i < records.Length; i++)
			{
				var record = records[i];
				var typeNameFormat = record?.TypeNameFormat ?? NFCTypeNameFormat.Empty;
				var payload = record?.Payload;
				var id = record?.Identifier;
				var type = record?.Type;

				var flags = (byte)typeNameFormat;

				// Message begin / end flags. If there is only one record in the message, both flags are set.
				if (i == 0)
					flags |= 0x80;      // MB (message begin = first record in the message)
				if (i == records.Length - 1)
					flags |= 0x40;      // ME (message end = last record in the message)

				// cf (chunked records) not supported yet

				// SR (Short Record)?
				if (payload == null || payload.Length < 255)
					flags |= 0x10;

				// ID present?
				if (id != null && id.Length > 0)
					flags |= 0x08;

				m.WriteByte(flags);

				// Type length
				if (type != null)
					m.WriteByte((byte)type.Length);
				else
					m.WriteByte(0);

				// Payload length 1 byte (SR) or 4 bytes
				if (payload == null)
				{
					m.WriteByte(0);
				}
				else
				{
					if ((flags & 0x10) != 0)
					{
						// SR
						m.WriteByte((byte)payload.Length);
					}
					else
					{
						// No SR (Short Record)
						var payloadLength = (uint)payload.Length;
						m.WriteByte((byte)(payloadLength >> 24));
						m.WriteByte((byte)(payloadLength >> 16));
						m.WriteByte((byte)(payloadLength >> 8));
						m.WriteByte((byte)(payloadLength & 0x000000ff));
					}
				}

				// ID length
				if (id != null && (flags & 0x08) != 0)
					m.WriteByte((byte)id.Length);

				// Type length
				if (type != null && type.Length > 0)
					m.Write(type.ToArray(), 0, (int)type.Length);

				// ID data
				if (id != null && id.Length > 0)
					m.Write(id.ToArray(), 0, (int)id.Length);

				// Payload data
				if (payload != null && payload.Length > 0)
					m.Write(payload.ToArray(), 0, (int)payload.Length);
			}

			return m.ToArray();
		}
	}
}
