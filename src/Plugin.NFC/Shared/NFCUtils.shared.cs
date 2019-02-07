using System.Text;

namespace Plugin.NFC
{
	/// <summary>
	/// NFC tools
	/// </summary>
	public static class NFCUtils
	{
		/// <summary>
		/// Returns the content size of an array of <see cref="NFCNdefRecord"/>
		/// </summary>
		/// <param name="records">array of <see cref="NFCNdefRecord"/></param>
		/// <returns>Content size</returns>
		internal static int GetSize(NFCNdefRecord[] records)
		{
			var size = 0;
			if (records != null && records.Length > 0)
			{
				for (var i = 0; i < records.Length; i++)
					size += records[i].Payload.Length;
			}
			return size;
		}

		/// <summary>
		/// Returns the string formatted payload
		/// </summary>
		/// <param name="type">type of <see cref="NFCNdefTypeFormat"/></param>
		/// <param name="payload">record payload</param>
		/// <returns>String formatted payload</returns>
		internal static string GetMessage(NFCNdefTypeFormat type, byte[] payload)
		{
			var message = string.Empty;
			if (type != NFCNdefTypeFormat.WellKnown)
			{
				message = Encoding.UTF8.GetString(payload, 0, payload.Length);
			}
			else
			{
				var languageCodeLength = payload[0] & 0063;
				return Encoding.UTF8.GetString(payload, languageCodeLength + 1, payload.Length - languageCodeLength - 1);
			}
			return message;
		}

		/// <summary>
		/// Transforms a string message into an array of bytes
		/// </summary>
		/// <param name="text">text message</param>
		/// <returns>Array of bytes</returns>
		public static byte[] EncodeToByteArray(string text) => Encoding.UTF8.GetBytes(text);

		/// <summary>
		/// Returns the string formatted payload
		/// </summary>
		/// <param name="record">Object <see cref="NFCNdefRecord"/></param>
		/// <returns>String formatted payload</returns>
		public static string GetMessage(NFCNdefRecord record)
		{
			if (record == null)
				return string.Empty;
			return GetMessage(record.TypeFormat, record.Payload);
		}
	}
}
