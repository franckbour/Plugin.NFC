using CoreFoundation;
using CoreNFC;
using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UIKit;

namespace Plugin.NFC
{
    /// <summary>
    /// iOS 13+ implementation of <see cref="INFC"/>
    /// </summary>
    public class NFCImplementation : NFCTagReaderSessionDelegate, INFC
	{
		public const string SessionTimeoutMessage = "session timeout";

		public event EventHandler OnTagConnected;
		public event EventHandler OnTagDisconnected;
		public event NdefMessageReceivedEventHandler OnMessageReceived;
		public event NdefMessagePublishedEventHandler OnMessagePublished;
		public event TagDiscoveredEventHandler OnTagDiscovered;
		public event EventHandler OniOSReadingSessionCancelled;
		public event OnNfcStatusChangedEventHandler OnNfcStatusChanged;
		public event TagListeningStatusChangedEventHandler OnTagListeningStatusChanged;

		bool _isWriting;
		bool _isFormatting;
		bool _customInvalidation = false;

		INFCTag _tag;

		NFCTagReaderSession NfcSession { get; set; }

        /// <summary>
        /// Checks if NFC Feature is available
        /// </summary>
        public bool IsAvailable => NFCReaderSession.ReadingAvailable;

		/// <summary>
		/// Checks if NFC Feature is enabled
		/// </summary>
		public bool IsEnabled => IsAvailable;

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
			_customInvalidation = false;
			_isWriting = false;
			_isFormatting = false;

			NfcSession = new NFCTagReaderSession(NFCPollingOption.Iso14443 | NFCPollingOption.Iso15693, this, DispatchQueue.CurrentQueue)
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
				throw new InvalidOperationException(Configuration.Messages.NFCWritingNotSupported);

			_customInvalidation = false;
			_isWriting = true;
			_isFormatting = clearMessage;

			NfcSession = new NFCTagReaderSession(NFCPollingOption.Iso14443 | NFCPollingOption.Iso15693, this, DispatchQueue.CurrentQueue)
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
		/// <param name="makeReadOnly">make tag read-only</param>
		public void PublishMessage(ITagInfo tagInfo, bool makeReadOnly = false) => WriteOrClearMessage(_tag, tagInfo, false, makeReadOnly);

		/// <summary>
		/// Format tag
		/// </summary>
		/// <param name="tagInfo">see <see cref="ITagInfo"/></param>
		public void ClearMessage(ITagInfo tagInfo) => WriteOrClearMessage(_tag, tagInfo, true);

		/// <summary>
		/// Event raised when NFC tags are detected
		/// </summary>
		/// <param name="session">iOS <see cref="NFCTagReaderSession"/></param>
		/// <param name="tags">Array of iOS <see cref="INFCTag"/></param>
		public override void DidDetectTags(NFCTagReaderSession session, INFCTag[] tags)
		{
			_customInvalidation = false;
			_tag = tags.First();

			var connectionError = string.Empty;
			session.ConnectTo(_tag, (error) =>
			{
				if (error != null)
				{
					connectionError = error.LocalizedDescription;
					Invalidate(session, connectionError);
					return;
				}

				var ndefTag = NfcNdefTagExtensions.GetNdefTag(_tag);

				if (ndefTag == null)
				{
					Invalidate(session, Configuration.Messages.NFCErrorNotCompliantTag);
					return;
				}

				ndefTag.QueryNdefStatus((status, capacity, error) =>
				{
					if (error != null)
					{
						Invalidate(session, error.LocalizedDescription);
						return;
					}

					var isNdefSupported = status != NFCNdefStatus.NotSupported;

					var identifier = NfcNdefTagExtensions.GetTagIdentifier(ndefTag);
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
						ndefTag.ReadNdef((message, error) =>
						{
							// iOS Error: NFCReaderError.NdefReaderSessionErrorZeroLengthMessage (NDEF tag does not contain any NDEF message)
							// NFCReaderError.NdefReaderSessionErrorZeroLengthMessage constant should be equals to 403 instead of 304
							// see https://developer.apple.com/documentation/corenfc/nfcreadererror/code/ndefreadersessionerrorzerolengthmessage
							if (error != null && error.Code != 403)
							{
								Invalidate(session, Configuration.Messages.NFCErrorRead);
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
		public override void DidInvalidate(NFCTagReaderSession session, NSError error)
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
				OniOSReadingSessionCancelled?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>
		/// Write or Clear a NDEF message
		/// </summary>
		/// <param name="tag"><see cref="INFCTag"/></param>
		/// <param name="tagInfo"><see cref="ITagInfo"/></param>
		/// <param name="clearMessage">Clear Message</param>
		/// <param name="makeReadOnly">Make a tag read-only</param>
		internal void WriteOrClearMessage(INFCTag tag, ITagInfo tagInfo, bool clearMessage = false, bool makeReadOnly = false)
		{
			if (NfcSession == null)
				return;

			if (tag == null)
			{
				Invalidate(NfcSession, Configuration.Messages.NFCErrorMissingTag);
				return;
			}

			if (tagInfo == null || (!clearMessage && tagInfo.Records.Any(record => record.Payload == null)))
			{
				Invalidate(NfcSession, Configuration.Messages.NFCErrorMissingTagInfo);
				return;
			}

			var ndefTag = NfcNdefTagExtensions.GetNdefTag(tag);
			if (ndefTag == null)
			{
				Invalidate(NfcSession, Configuration.Messages.NFCErrorNotCompliantTag);
				return;
			}

			try
			{
				if (!ndefTag.Available)
				{
					NfcSession.ConnectTo(tag, (error) =>
					{
						if (error != null)
						{
							Invalidate(NfcSession, error.LocalizedDescription);
							return;
						}

						ExecuteWriteOrClear(NfcSession, ndefTag, tagInfo, clearMessage);
					});
				}
				else
				{
					ExecuteWriteOrClear(NfcSession, ndefTag, tagInfo, clearMessage);
				}

				if (!clearMessage && makeReadOnly)
				{
					MakeTagReadOnly(NfcSession, tag, ndefTag);
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
		UIViewController GetCurrentController()
		{
			var window = UIApplication.SharedApplication.KeyWindow;
			var vc = window.RootViewController;
			while (vc.PresentedViewController != null)
				vc = vc.PresentedViewController;
			return vc;
		}

		/// <summary>
		/// Invalidate the session
		/// </summary>
		/// <param name="session"><see cref="NFCTagReaderSession"/></param>
		/// <param name="message">Message to show</param>
		void Invalidate(NFCTagReaderSession session, string message = null)
		{
			_customInvalidation = true;
			if (string.IsNullOrWhiteSpace(message))
				session.InvalidateSession();
			else
				session.InvalidateSession(message);
		}

		/// <summary>
		/// Writes or clears a TAG
		/// </summary>
		/// <param name="session"><see cref="NFCTagReaderSession"/></param>
		/// <param name="tag"><see cref="INFCNdefTag"/></param>
		/// <param name="tagInfo"><see cref="ITagInfo"/></param>
		/// <param name="clearMessage">Clear message</param>
		void ExecuteWriteOrClear(NFCTagReaderSession session, INFCNdefTag tag, ITagInfo tagInfo, bool clearMessage = false)
		{
			tag.QueryNdefStatus((status, capacity, error) =>
			{
				if (error != null)
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

				NFCNdefMessage message = null;
				if (!clearMessage)
				{
					session.AlertMessage = Configuration.Messages.NFCSuccessWrite;

					var records = new List<NFCNdefPayload>();
					for (var i = 0; i < tagInfo.Records.Length; i++)
					{
						var record = tagInfo.Records[i];
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

				if (message != null)
				{
					tag.WriteNdef(message, (error) =>
					{
						if (error != null)
						{
							Invalidate(session, error.LocalizedDescription);
							return;
						}

						tagInfo.Records = NFCNdefPayloadExtensions.GetRecords(message.Records);
						OnMessagePublished?.Invoke(tagInfo);
						Invalidate(NfcSession);
					});
				}
				else
					Invalidate(session, Configuration.Messages.NFCErrorWrite);
			});
		}

		/// <summary>
		/// Make a tag read-only
		/// WARNING: This operation is permanent
		/// </summary>
		/// <param name="session"><see cref="NFCTagReaderSession"/></param>
		/// <param name="tag"><see cref="ITagInfo"/></param>
		/// <param name="ndefTag"><see cref="INFCNdefTag"/></param>
		void MakeTagReadOnly(NFCTagReaderSession session, INFCTag tag, INFCNdefTag ndefTag)
		{
			session.ConnectTo(tag, (error) =>
			{
				if (error != null)
				{
					Console.WriteLine(error.LocalizedDescription);
					return;
				}

				ndefTag.WriteLock((error) =>
				{
					if (error != null)
						Console.WriteLine("Error when locking a tag on iOS: " + error.LocalizedDescription);
					else
						Console.WriteLine("Locking Successful!");
				});
			});
		}

		#endregion
	}

	/// <summary>
	/// Old iOS implementation of <see cref="INFC"/> (iOS < 13)
	/// </summary>
	public class NFCImplementation_Before_iOS13 : NFCNdefReaderSessionDelegate, INFC
	{
		public const string SessionTimeoutMessage = "session timeout";

		private bool _isWriting;
		private bool _isFormatting;
		private bool _customInvalidation = false;
		private INFCNdefTag _tag;

		public event EventHandler OnTagConnected;
		public event EventHandler OnTagDisconnected;
		public event NdefMessageReceivedEventHandler OnMessageReceived;
		public event NdefMessagePublishedEventHandler OnMessagePublished;
		public event TagDiscoveredEventHandler OnTagDiscovered;
		public event EventHandler OniOSReadingSessionCancelled;
		public event OnNfcStatusChangedEventHandler OnNfcStatusChanged;
		public event TagListeningStatusChangedEventHandler OnTagListeningStatusChanged;

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
		public bool IsWritingTagSupported => NFCUtils.IsWritingSupported();

		/// <summary>
		/// NFC configuration
		/// </summary>
		public NfcConfiguration Configuration { get; private set; }

		/// <summary>
		/// Default constructor
		/// </summary>
		public NFCImplementation_Before_iOS13()
		{
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
			_customInvalidation = false;
			_isWriting = false;
			_isFormatting = false;

			NfcSession = new NFCNdefReaderSession(this, DispatchQueue.CurrentQueue, true)
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

			NfcSession = new NFCNdefReaderSession(this, DispatchQueue.CurrentQueue, true)
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

			if (messages != null && messages.Length > 0)
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
				if (error != null)
				{
					Invalidate(session, error.LocalizedDescription);
					return;
				}

				if (_tag == null)
				{
					Invalidate(session, Configuration.Messages.NFCErrorNotCompliantTag);
					return;
				}

				_tag.QueryNdefStatus((status, capacity, ndefError) =>
				{
					if (ndefError != null)
					{
						Invalidate(session, ndefError.LocalizedDescription);
						return;
					}

					var isNdefSupported = status != NFCNdefStatus.NotSupported;

					var identifier = NfcNdefTagExtensions.GetTagIdentifier(_tag);
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
							if (readError != null)
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
		internal void WriteOrClearMessage(INFCNdefTag tag, ITagInfo tagInfo, bool clearMessage = false, bool makeReadOnly = false)
		{
			if (NfcSession == null)
			{
				return;
			}

			if (tag == null)
			{
				Invalidate(NfcSession, Configuration.Messages.NFCErrorMissingTag);
				return;
			}

			if (tagInfo == null || (!clearMessage && tagInfo.Records.Any(record => record.Payload == null)))
			{
				Invalidate(NfcSession, Configuration.Messages.NFCErrorMissingTagInfo);
				return;
			}

			if (_tag == null)
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
						if (error != null)
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
        UIViewController GetCurrentController()
		{
			var window = UIApplication.SharedApplication.KeyWindow;
			var vc = window.RootViewController;
			while (vc.PresentedViewController != null)
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
		{
			tag.QueryNdefStatus((status, capacity, error) =>
			{
				if (error != null)
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

				NFCNdefMessage message = null;
				if (!clearMessage)
				{
					session.AlertMessage = Configuration.Messages.NFCSuccessWrite;

					var records = new List<NFCNdefPayload>();
					for (var i = 0; i < tagInfo.Records.Length; i++)
					{
						var record = tagInfo.Records[i];
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

				if (message != null)
				{
					tag.WriteNdef(message, (error) =>
					{
						if (error != null)
						{
							Invalidate(session, error.LocalizedDescription);
							return;
						}

						tagInfo.Records = NFCNdefPayloadExtensions.GetRecords(message.Records);
						OnMessagePublished?.Invoke(tagInfo);
						Invalidate(NfcSession);
					});
				}
				else
					Invalidate(session, Configuration.Messages.NFCErrorWrite);
			});
		}

		/// <summary>
		/// Make a tag read-only
		/// WARNING: This operation is permanent
		/// </summary>
		/// <param name="session"><see cref="NFCTagReaderSession"/></param>
		/// <param name="tag"><see cref="ITagInfo"/></param>
		/// <param name="ndefTag"><see cref="INFCNdefTag"/></param>
		private void MakeTagReadOnly(NFCNdefReaderSession session, INFCNdefTag tag)
		{
			session.ConnectToTag(tag, (error) =>
			{
				if (error != null)
				{
					Console.WriteLine(error.LocalizedDescription);
					return;
				}

				tag.WriteLock((error) =>
				{
					if (error != null)
						Console.WriteLine("Error when locking a tag on iOS: " + error.LocalizedDescription);
					else
						Console.WriteLine("Locking Successful!");
				});
			});
		}

		/// <summary>
		/// Invalidate the session
		/// </summary>
		/// <param name="session"><see cref="NFCTagReaderSession"/></param>
		/// <param name="message">Message to show</param>
		void Invalidate(NFCNdefReaderSession session, string message = null)
		{
			_customInvalidation = true;
			if (string.IsNullOrWhiteSpace(message))
				session.InvalidateSession();
			else
				session.InvalidateSession(message);
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
			if (data.Length > 0) System.Runtime.InteropServices.Marshal.Copy(data.Bytes, bytes, 0, Convert.ToInt32(data.Length));
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

		/// <summary>
		/// Returns an empty iOS <see cref="NFCNdefMessage"/>
		/// </summary>
		/// <returns>iOS <see cref="NFCNdefMessage"/></returns>
		internal static NFCNdefMessage EmptyNdefMessage
		{
			get
			{
				var records = new NFCNdefPayload[1];
				records[0] = NFCNdefPayloadExtensions.EmptyPayload;
				return new NFCNdefMessage(records);
			}
		}
	}

	/// <summary>
	/// NFC Ndef Payload Extensions Class
	/// </summary>
	internal static class NFCNdefPayloadExtensions
	{
		/// <summary>
		/// Returns ndef payload into MimeType
		/// </summary>
		/// <param name="payload"><see cref="NFCNdefPayload"/></param>
		/// <returns>string</returns>
		public static string ToMimeType(this NFCNdefPayload payload)
		{
			switch (payload.TypeNameFormat)
			{
				case NFCTypeNameFormat.NFCWellKnown:
					if (payload.Type.ToString() == "T")
						return "text/plain";
					break;
				case NFCTypeNameFormat.Media:
					return payload.Type.ToString();
			}
			return null;
		}

		/// <summary>
		/// Returns Ndef payload into URI
		/// </summary>
		/// <param name="payload"><see cref="NFCNdefPayload"/></param>
		/// <returns><see cref="Uri"/></returns>
		public static Uri ToUri(this NFCNdefPayload payload)
		{
			switch (payload.TypeNameFormat)
			{
				case NFCTypeNameFormat.NFCWellKnown:
					if (payload.Type.ToString() == "U")
					{
						var uri = payload.Payload?.ParseWktUri();
						return uri;
					}
					break;
				case NFCTypeNameFormat.AbsoluteUri:
				case NFCTypeNameFormat.Media:
					var content = Encoding.UTF8.GetString(payload.Payload?.ToByteArray());
					if (Uri.TryCreate(content, UriKind.RelativeOrAbsolute, out var result))
						return result;

					break;
			}
			return null;
		}

		/// <summary>
		/// Returns complete URI of TNF_WELL_KNOWN, RTD_URI records.
		/// </summary>
		/// <returns><see cref="Uri"/></returns>
		private static Uri ParseWktUri(this NSData data)
		{
			var payload = data.ToByteArray();

			if (payload.Length < 2)
				return null;

			var prefixIndex = payload[0] & 0xFF;
			if (prefixIndex < 0 || prefixIndex >= _uri_Prefixes_Map.Length)
				return null;

			var prefix = _uri_Prefixes_Map[prefixIndex];
			var suffix = Encoding.UTF8.GetString(CopyOfRange(payload, 1, payload.Length));

			if (Uri.TryCreate(prefix + suffix, UriKind.Absolute, out var result))
				return result;

			return null;
		}

		/// <summary>
		/// Copy a range of an array into another array
		/// </summary>
		/// <param name="src">Array of <see cref="byte"/></param>
		/// <param name="start">Start</param>
		/// <param name="end">End</param>
		/// <returns>Array of <see cref="byte"/></returns>
		private static byte[] CopyOfRange(byte[] src, int start, int end)
		{
			var length = end - start;
			var dest = new byte[length];
			for (var i = 0; i < length; i++)
				dest[i] = src[start + i];
			return dest;
		}

		/// <summary>
		/// NFC Forum "URI Record Type Definition"
		/// This is a mapping of "URI Identifier Codes" to URI string prefixes,
		/// per section 3.2.2 of the NFC Forum URI Record Type Definition document.
		/// </summary>
		private static readonly string[] _uri_Prefixes_Map = new string[] {
			"", // 0x00
            "http://www.", // 0x01
            "https://www.", // 0x02
            "http://", // 0x03
            "https://", // 0x04
            "tel:", // 0x05
            "mailto:", // 0x06
            "ftp://anonymous:anonymous@", // 0x07
            "ftp://ftp.", // 0x08
            "ftps://", // 0x09
            "sftp://", // 0x0A
            "smb://", // 0x0B
            "nfs://", // 0x0C
            "ftp://", // 0x0D
            "dav://", // 0x0E
            "news:", // 0x0F
            "telnet://", // 0x10
            "imap:", // 0x11
            "rtsp://", // 0x12
            "urn:", // 0x13
            "pop:", // 0x14
            "sip:", // 0x15
            "sips:", // 0x16
            "tftp:", // 0x17
            "btspp://", // 0x18
            "btl2cap://", // 0x19
            "btgoep://", // 0x1A
            "tcpobex://", // 0x1B
            "irdaobex://", // 0x1C
            "file://", // 0x1D
            "urn:epc:id:", // 0x1E
            "urn:epc:tag:", // 0x1F
            "urn:epc:pat:", // 0x20
            "urn:epc:raw:", // 0x21
            "urn:epc:", // 0x22
            "urn:nfc:", // 0x23
		};

		/// <summary>
		/// Returns an empty iOS <see cref="NFCNdefPayload"/>
		/// </summary>
		/// <returns>iOS <see cref="NFCNdefPayload"/></returns>
		internal static NFCNdefPayload EmptyPayload => new(NFCTypeNameFormat.Empty, new NSData(), new NSData(), new NSData());

		/// <summary>
		/// Transforms an array of <see cref="NFCNdefPayload"/> into an array of <see cref="NFCNdefRecord"/>
		/// </summary>
		/// <param name="records">Array of <see cref="NFCNdefPayload"/></param>
		/// <returns>Array of <see cref="NFCNdefRecord"/></returns>
		internal static NFCNdefRecord[] GetRecords(NFCNdefPayload[] records)
		{
            if (records == null)
                return new NFCNdefRecord[0];

			var results = new NFCNdefRecord[records.Length];
			for (var i = 0; i < records.Length; i++)
			{
				var record = records[i];
				var ndefRecord = new NFCNdefRecord
				{
					TypeFormat = (NFCNdefTypeFormat)record.TypeNameFormat,
					Uri = records[i].ToUri()?.ToString(),
					MimeType = records[i].ToMimeType(),
					Payload = record.Payload?.ToByteArray()
				};
				results.SetValue(ndefRecord, i);
			}
			return results;
		}

		/// <summary>
		/// Returns NDEF payload
		/// </summary>
		/// <param name="record"><see cref="NFCNdefRecord"/></param>
		/// <returns><see cref="NFCNdefPayload"/></returns>
		internal static NFCNdefPayload GetiOSPayload(NFCNdefRecord record, NfcConfiguration configuration)
		{
			if (record == null)
				return null;

			NFCNdefPayload payload = null;
			switch (record.TypeFormat)
			{
				case NFCNdefTypeFormat.WellKnown:
					var lang = record.LanguageCode;
					if (string.IsNullOrWhiteSpace(lang)) lang = configuration.DefaultLanguageCode;
					var langData = Encoding.ASCII.GetBytes(lang.Substring(0, 2));
					var payloadData = new byte[] { 0x02 }.Concat(langData).Concat(record.Payload).ToArray();
					payload = new NFCNdefPayload(NFCTypeNameFormat.NFCWellKnown, NSData.FromString("T"), new NSData(), NSData.FromString(Encoding.UTF8.GetString(payloadData), NSStringEncoding.UTF8));
					break;
				case NFCNdefTypeFormat.Mime:
					payload = new NFCNdefPayload(NFCTypeNameFormat.Media, record.MimeType, new NSData(), NSData.FromArray(record.Payload));
					break;
				case NFCNdefTypeFormat.Uri:
					payload = NFCNdefPayload.CreateWellKnownTypePayload(NSUrl.FromString(Encoding.UTF8.GetString(record.Payload)));
					break;
				case NFCNdefTypeFormat.External:
					payload = new NFCNdefPayload(NFCTypeNameFormat.NFCExternal, record.ExternalType, new NSData(), NSData.FromString(Encoding.UTF8.GetString(record.Payload), NSStringEncoding.UTF8));
					break;
				case NFCNdefTypeFormat.Empty:
					payload = EmptyPayload;
					break;
				case NFCNdefTypeFormat.Unknown:
				case NFCNdefTypeFormat.Unchanged:
				case NFCNdefTypeFormat.Reserved:
				default:
					break;
			}
			return payload;
		}
	}

	/// <summary>
	/// NFC Tag Extensions Class
	/// </summary>
	internal static class NfcNdefTagExtensions
	{
		/// <summary>
		/// Get Ndef tag
		/// </summary>
		/// <param name="tag"><see cref="INFCTag"/></param>
		/// <returns><see cref="INFCNdefTag"/></returns>
		internal static INFCNdefTag GetNdefTag(INFCTag tag)
		{
			if (tag == null || !tag.Available)
				return null;

			INFCNdefTag ndef;

#if NET6_0_OR_GREATER
			if (tag.Type == CoreNFC.NFCTagType.MiFare)
				ndef = tag.AsNFCMiFareTag;
			else if (tag.Type == CoreNFC.NFCTagType.Iso7816Compatible)
				ndef = tag.AsNFCIso7816Tag;
			else if (tag.Type == CoreNFC.NFCTagType.Iso15693)
				ndef = tag.AsNFCIso15693Tag;
			else if (tag.Type == CoreNFC.NFCTagType.FeliCa)
				ndef = tag.AsNFCFeliCaTag;
			else
				ndef = null;
#else
			if (tag.GetNFCMiFareTag() != null)
				ndef = tag.GetNFCMiFareTag();
			else if (tag.GetNFCIso7816Tag() != null)
				ndef = tag.GetNFCIso7816Tag();
			else if (tag.GetNFCIso15693Tag() != null)
				ndef = tag.GetNFCIso15693Tag();
			else if (tag.GetNFCFeliCaTag() != null)
				ndef = tag.GetNFCFeliCaTag();
			else
				ndef = null;
#endif

			return ndef;
		}

		/// <summary>
		/// Returns NFC Tag identifier
		/// </summary>
		/// <param name="tag"><see cref="INFCNdefTag"/></param>
		/// <returns>Tag identifier</returns>
		internal static byte[] GetTagIdentifier(INFCNdefTag tag)
		{
			byte[] identifier = null;
			if (tag is INFCMiFareTag mifareTag)
			{
				identifier = mifareTag.Identifier.ToByteArray();
			}
			else if (tag is INFCFeliCaTag felicaTag)
			{
				identifier = felicaTag.CurrentIdm.ToByteArray();
			}
			else if (tag is INFCIso15693Tag iso15693Tag)
			{
				identifier = iso15693Tag.Identifier.ToByteArray().Reverse().ToArray();
			}
			else if (tag is INFCIso7816Tag iso7816Tag)
			{
				identifier = iso7816Tag.Identifier.ToByteArray();
			}
			return identifier;
		}
	}
}
