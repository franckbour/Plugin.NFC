using System.Text;

namespace Plugin.NFC
{
	public static class NFCUtils
	{
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

		public static byte[] EncodeToByteArray(string text) => Encoding.UTF8.GetBytes(text);

		public static string GetMessage(NFCNdefRecord record)
		{
			if (record == null)
				return string.Empty;
			return GetMessage(record.TypeFormat, record.Payload);
		}
	}
}
