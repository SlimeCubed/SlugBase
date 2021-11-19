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
using System.Linq;
using System.Reflection;

namespace Tiny
{
	internal static class TypeExtensions
	{
		public static bool IsInstanceOfGenericType(this Type type, Type genericType)
		{
			while (type != null)
			{
				if (type.IsGenericType && type.GetGenericTypeDefinition() == genericType) return true;
				type = type.BaseType;
			}
			return false;
		}

		public static bool HasGenericInterface(this Type type, Type genericInterface)
		{
			if (genericInterface == null) throw new ArgumentNullException();
			var interfaceTest = new Predicate<Type>(i => i.IsGenericType && i.GetGenericTypeDefinition().IsAssignableFrom(genericInterface));
			return interfaceTest(type) || type.GetInterfaces().Any(i => interfaceTest(i));
		}

		static string UnwrapFieldName(string name)
		{
			if (name.StartsWith("<", StringComparison.Ordinal) && name.Contains(">"))
			{
				return name.Substring(name.IndexOf("<", StringComparison.Ordinal) + 1, name.IndexOf(">", StringComparison.Ordinal) - 1);
			}
			return name;
		}

		public static string UnwrappedFieldName(this FieldInfo field, Type type)
		{
			string name = UnwrapFieldName(field.Name);

			if (field.GetCustomAttributes(typeof(JsonPropertyAttribute), true).Length == 1)
			{
				var jsonProperty = field.GetCustomAttributes(typeof(JsonPropertyAttribute), true)[0] as JsonPropertyAttribute;
				name = jsonProperty.Name;
			}
			else
			{
				foreach (var property in type.GetProperties())
				{
					if (UnwrapFieldName(property.Name).Equals(name, StringComparison.OrdinalIgnoreCase))
					{
						name = property.UnwrappedPropertyName();
						break;
					}
				}
			}

			return name;
		}

		public static string UnwrappedPropertyName(this PropertyInfo property)
		{
			string name = UnwrapFieldName(property.Name);

			if (property.GetCustomAttributes(typeof(JsonPropertyAttribute), true).Length == 1)
			{
				var jsonProperty = property.GetCustomAttributes(typeof(JsonPropertyAttribute), true)[0] as JsonPropertyAttribute;
				name = jsonProperty.Name;
			}

			return name;
		}
	}

	internal static class JsonExtensions
	{
		public static bool IsNullable(this Type type)
		{
			return Nullable.GetUnderlyingType(type) != null || !type.IsPrimitive;
		}

		public static bool IsNumeric(this Type type)
		{
			if (type.IsEnum) return false;
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Byte:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
				case TypeCode.UInt64:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.Int64:
				case TypeCode.Decimal:
				case TypeCode.Double:
				case TypeCode.Single:
					return true;
				case TypeCode.Object:
					Type underlyingType = Nullable.GetUnderlyingType(type);
					return underlyingType != null && underlyingType.IsNumeric();
				default:
					return false;
			}
		}

		public static bool IsFloatingPoint(this Type type)
		{
			if (type.IsEnum) return false;
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Decimal:
				case TypeCode.Double:
				case TypeCode.Single:
					return true;
				case TypeCode.Object:
					Type underlyingType = Nullable.GetUnderlyingType(type);
					return underlyingType != null && underlyingType.IsFloatingPoint();
				default:
					return false;
			}
		}
	}

	internal static class StringBuilderExtensions
	{
		public static void Clear(this System.Text.StringBuilder sb)
		{
			sb.Length = 0;
		}
	}
}
