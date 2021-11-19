/*
The MIT License (MIT)

Copyright (c) 2015 Robert Gering

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Collections;
using System.Reflection;

namespace Tiny
{
	using Encoder = Action<object, JsonBuilder>;

	internal static class DefaultEncoder
	{

		public static Encoder GenericEncoder()
		{
			return (obj, builder) =>
			{
				builder.AppendBeginObject();
				Type type = obj.GetType();
				bool first = true;
				while (type != null)
				{
					foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
					{
						if (field.GetCustomAttributes(typeof(NonSerializedAttribute), true).Length == 0)
						{
							if (first) first = false; else builder.AppendSeperator();

							var fieldName = field.UnwrappedFieldName(type);
							JsonMapper.EncodeNameValue(fieldName, field.GetValue(obj), builder);
						}
					}
					type = type.BaseType;
				}
				builder.AppendEndObject();
			};
		}

		public static Encoder DictionaryEncoder()
		{
			return (obj, builder) =>
			{
				builder.AppendBeginObject();
				bool first = true;
				IDictionary dict = (IDictionary)obj;
				foreach (var key in dict.Keys)
				{
					if (first) first = false; else builder.AppendSeperator();
					JsonMapper.EncodeNameValue(key.ToString(), dict[key], builder);
				}
				builder.AppendEndObject();
			};
		}

		public static Encoder EnumerableEncoder()
		{
			return (obj, builder) =>
			{
				builder.AppendBeginArray();
				bool first = true;
				foreach (var item in (IEnumerable)obj)
				{
					if (first) first = false; else builder.AppendSeperator();
					JsonMapper.EncodeValue(item, builder);
				}
				builder.AppendEndArray();
			};
		}

		public static Encoder ZuluDateEncoder()
		{
			return (obj, builder) =>
			{
				DateTime date = (DateTime)obj;
				string zulu = date.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
				builder.AppendString(zulu);
			};
		}
	}
}
