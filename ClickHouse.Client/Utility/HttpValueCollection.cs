using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text;

namespace ClickHouse.Client.Utility
{
    /// <summary>
    /// Represents an ordered collection of name/value pairs obtained through parsing an HTTP query string.
    /// </summary>
    internal sealed class HttpValueCollection : NameValueCollection
    {
        private const int MaxKeyCount = 1000;

        private string cache;

        public HttpValueCollection()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        /// <summary>
        ///     Parses a query string into a <see cref="HttpValueCollection"/>.
        /// </summary>
        ///
        /// <param name="str">The query string to parse.</param>
        ///
        /// <returns>A <see cref="HttpValueCollection"/> of query parameters and values.</returns>
        public static HttpValueCollection Parse(string str)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            var collection = new HttpValueCollection();
            var length = str.Length;
            if (length == 0)
            {
                return collection;
            }

            var index = 0;
            if (str[index] == '?')
            {
                ++index;
            }

            while (index < length)
            {
                if (collection.Count >= MaxKeyCount)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "The number of keys in the collection has reached the maximum of {0}.", MaxKeyCount));
                }

                var startIndex = index;
                var delimiterIndex = -1;

                while (index < length)
                {
                    var ch = str[index];

                    if (ch == '=')
                    {
                        if (delimiterIndex < 0)
                        {
                            delimiterIndex = index;
                        }
                    }
                    else if (ch == '&')
                    {
                        break;
                    }

                    index++;
                }

                string name = null;
                string value;

                if (delimiterIndex > -1)
                {
                    name = str.Substring(startIndex, delimiterIndex - startIndex);
                    value = str.Substring(delimiterIndex + 1, index - delimiterIndex - 1);
                }
                else
                {
                    value = str.Substring(startIndex, index - startIndex);
                }

                collection.Add(UrlDecode(name), UrlDecode(value));

                if (index == length - 1 && str[index] == '&')
                {
                    collection.Add(null, string.Empty);
                }

                index++;
            }

            return collection;
        }

        public override void Add(string name, string value)
        {
            cache = null;
            base.Add(name, value);
        }

        public override void Remove(string name)
        {
            cache = null;
            base.Remove(name);
        }

        public override void Set(string name, string value)
        {
            cache = null;
            base.Set(name, value);
        }

        public override string ToString()
        {
            if (Count == 0)
            {
                return string.Empty;
            }

            return cache ??= BuildString();
        }

        private static string UrlDecode(string str) => str == null ? null : WebUtility.UrlDecode(str);

        private static string UrlEncode(string str) => str == null ? null : WebUtility.UrlEncode(str);

        private string BuildString()
        {
            var builder = new StringBuilder(4096);
            string key, keyPrefix;

            for (var i = 0; i < Count; ++i)
            {
                key = UrlEncode(GetKey(i));
                keyPrefix = key is null ? string.Empty : key + "=";

                var values = GetValues(i);
                if (builder.Length != 0)
                {
                    builder.Append('&');
                }

                if (values is null || values.Length == 0)
                {
                    builder.Append(keyPrefix);
                }
                else if (values.Length == 1)
                {
                    builder.Append(keyPrefix);
                    builder.Append(UrlEncode(values[0]));
                }
                else
                {
                    for (var j = 0; j < values.Length; ++j)
                    {
                        if (j != 0)
                        {
                            builder.Append('&');
                        }

                        builder.Append(keyPrefix);
                        builder.Append(UrlEncode(values[j]));
                    }
                }
            }

            return builder.ToString();
        }
    }
}
