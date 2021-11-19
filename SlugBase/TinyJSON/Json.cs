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

namespace Tiny
{
	internal static class Json
	{
		public static T Decode<T>(this string json)
		{
			if (string.IsNullOrEmpty(json)) return default;
			object jsonObj = JsonParser.ParseValue(json);
			if (jsonObj == null) return default;
			return JsonMapper.DecodeJsonObject<T>(jsonObj);
		}

		public static object Decode(this string json, Type type)
		{
			if (string.IsNullOrEmpty(json)) return null;
			object jsonObj = JsonParser.ParseValue(json);
			if (jsonObj == null) return null;
			return JsonMapper.DecodeJsonObject(jsonObj, type);
		}

		public static string Encode(this object value, bool pretty = false)
		{
			JsonBuilder builder = new JsonBuilder(pretty);
			JsonMapper.EncodeValue(value, builder);
			return builder.ToString();
		}
	}
}
