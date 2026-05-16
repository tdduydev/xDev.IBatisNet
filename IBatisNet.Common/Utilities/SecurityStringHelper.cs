#region Apache Notice
/*****************************************************************************
 * xDev.IBatisNet
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 ********************************************************************************/
#endregion

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IBatisNet.Common.Utilities
{
	/// <summary>
	/// Security-oriented string helpers used before values reach diagnostics,
	/// debug logs, or in-memory diagnostic structures.
	/// </summary>
	public static class SecurityStringHelper
	{
		private static readonly string[] SensitiveConnectionStringKeys = new string[]
		{
			"password",
			"pwd",
			"user id",
			"uid",
			"user",
			"username",
			"token",
			"access token",
			"secret",
			"client secret",
			"key",
			"api key",
			"account key",
			"shared access key"
		};

		private static readonly string[] SensitiveNameFragments = new string[]
		{
			"password",
			"pwd",
			"secret",
			"token",
			"apikey",
			"api_key",
			"accesskey",
			"access_key",
			"credential",
			"ssn",
			"socialsecurity",
			"cardnumber",
			"creditcard"
		};

		/// <summary>
		/// Masks credentials and tokens in a provider connection string.
		/// </summary>
		public static string MaskConnectionString(string connectionString)
		{
			if (connectionString == null || connectionString.Trim().Length == 0)
			{
				return string.Empty;
			}

			string[] parts = connectionString.Split(new char[] {';'}, StringSplitOptions.RemoveEmptyEntries);
			StringBuilder builder = new StringBuilder();
			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i].Trim();
				int separator = part.IndexOf('=');
				string safePart = part;
				if (separator > 0)
				{
					string key = part.Substring(0, separator).Trim();
					if (IsSensitiveConnectionStringKey(key))
					{
						safePart = key + "=***";
					}
				}

				if (builder.Length > 0)
				{
					builder.Append("; ");
				}
				builder.Append(safePart);
			}

			return builder.ToString();
		}

		/// <summary>
		/// Creates an opaque stable cache key so raw connection strings are not kept as Hashtable keys.
		/// </summary>
		public static string CreateCacheKey(string connectionString, string commandText)
		{
			return StableHash((connectionString ?? string.Empty) + "\u001f" + (commandText ?? string.Empty));
		}

		/// <summary>
		/// Formats parameter values for debug logging without exposing sensitive data, credentials, or request payloads.
		/// </summary>
		public static string FormatLogParameterValue(string parameterName, string propertyName, object value)
		{
			if (value == null || value == DBNull.Value)
			{
				return "null";
			}

			if (IsSensitiveName(parameterName) || IsSensitiveName(propertyName))
			{
				return "***";
			}

			string stringValue = value as string;
			if (stringValue != null)
			{
				return "<string length=" + stringValue.Length.ToString(CultureInfo.InvariantCulture) + ">";
			}

			byte[] bytes = value as byte[];
			if (bytes != null)
			{
				return "<binary length=" + bytes.Length.ToString(CultureInfo.InvariantCulture) + ">";
			}

			return "<" + value.GetType().Name + ">";
		}

		private static bool IsSensitiveConnectionStringKey(string key)
		{
			for (int i = 0; i < SensitiveConnectionStringKeys.Length; i++)
			{
				if (string.Equals(key, SensitiveConnectionStringKeys[i], StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsSensitiveName(string name)
		{
			if (name == null)
			{
				return false;
			}

			string normalized = name.Replace("@", string.Empty)
				.Replace(":", string.Empty)
				.Replace("-", string.Empty)
				.Replace(".", string.Empty)
				.ToLowerInvariant();

			for (int i = 0; i < SensitiveNameFragments.Length; i++)
			{
				if (normalized.IndexOf(SensitiveNameFragments[i], StringComparison.Ordinal) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static string StableHash(string value)
		{
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = Encoding.UTF8.GetBytes(value);
				byte[] hash = sha256.ComputeHash(bytes);
				StringBuilder builder = new StringBuilder(hash.Length * 2);
				for (int i = 0; i < hash.Length; i++)
				{
					builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
				}

				return builder.ToString();
			}
		}
	}
}
