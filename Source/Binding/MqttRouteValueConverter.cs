using System;
using System.Globalization;

#nullable enable

namespace MQTTnet.AspNetCore.Routing
{
    internal static class MqttRouteValueConverter
    {
        public static bool TryConvert(
            object? value,
            Type targetType,
            out object? convertedValue,
            out string? errorMessage)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (value == null)
            {
                if (CanAcceptNull(targetType))
                {
                    convertedValue = null;
                    errorMessage = null;
                    return true;
                }

                convertedValue = null;
                errorMessage = "Route value is required.";
                return false;
            }

            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
            {
                convertedValue = value;
                errorMessage = null;
                return true;
            }

            var nonNullableTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (nonNullableTargetType.IsAssignableFrom(valueType))
            {
                convertedValue = value;
                errorMessage = null;
                return true;
            }

            if (nonNullableTargetType == typeof(string))
            {
                convertedValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                errorMessage = null;
                return true;
            }

            if (value is string text)
            {
                if (text.Length == 0 && Nullable.GetUnderlyingType(targetType) != null)
                {
                    convertedValue = null;
                    errorMessage = null;
                    return true;
                }

                return TryConvertString(text, nonNullableTargetType, out convertedValue, out errorMessage);
            }

            if (nonNullableTargetType.IsEnum)
            {
                return TryConvertEnumValue(value, nonNullableTargetType, out convertedValue, out errorMessage);
            }

            try
            {
                convertedValue = Convert.ChangeType(value, nonNullableTargetType, CultureInfo.InvariantCulture);
                errorMessage = null;
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                convertedValue = null;
                errorMessage = $"Route value cannot be converted to '{GetFriendlyTypeName(targetType)}'.";
                return false;
            }
        }

        private static bool CanAcceptNull(Type targetType)
        {
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        }

        private static bool TryConvertString(
            string text,
            Type targetType,
            out object? convertedValue,
            out string? errorMessage)
        {
            if (targetType == typeof(Guid))
            {
                if (Guid.TryParse(text, out var guid))
                {
                    convertedValue = guid;
                    errorMessage = null;
                    return true;
                }

                convertedValue = null;
                errorMessage = "Route value must be a valid GUID.";
                return false;
            }

            if (targetType.IsEnum)
            {
                if (Enum.TryParse(targetType, text, ignoreCase: true, out var enumValue))
                {
                    convertedValue = enumValue;
                    errorMessage = null;
                    return true;
                }

                convertedValue = null;
                errorMessage = $"Route value must be a valid '{targetType.Name}' value.";
                return false;
            }

            if (targetType == typeof(int))
            {
                return TryParse<int>(text, int.TryParse, out convertedValue, out errorMessage, "integer");
            }

            if (targetType == typeof(long))
            {
                return TryParse<long>(text, long.TryParse, out convertedValue, out errorMessage, "long integer");
            }

            if (targetType == typeof(double))
            {
                if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
                {
                    convertedValue = result;
                    errorMessage = null;
                    return true;
                }

                convertedValue = null;
                errorMessage = "Route value must be a valid double.";
                return false;
            }

            if (targetType == typeof(decimal))
            {
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                {
                    convertedValue = result;
                    errorMessage = null;
                    return true;
                }

                convertedValue = null;
                errorMessage = "Route value must be a valid decimal.";
                return false;
            }

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(text, out var result))
                {
                    convertedValue = result;
                    errorMessage = null;
                    return true;
                }

                convertedValue = null;
                errorMessage = "Route value must be a valid boolean.";
                return false;
            }

            try
            {
                convertedValue = Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
                errorMessage = null;
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                convertedValue = null;
                errorMessage = $"Route value cannot be converted to '{GetFriendlyTypeName(targetType)}'.";
                return false;
            }
        }

        private static bool TryConvertEnumValue(
            object value,
            Type targetType,
            out object? convertedValue,
            out string? errorMessage)
        {
            try
            {
                var underlyingValue = Convert.ChangeType(
                    value,
                    Enum.GetUnderlyingType(targetType),
                    CultureInfo.InvariantCulture);

                convertedValue = Enum.ToObject(targetType, underlyingValue!);
                errorMessage = null;
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                convertedValue = null;
                errorMessage = $"Route value must be a valid '{targetType.Name}' value.";
                return false;
            }
        }

        private static bool TryParse<T>(
            string text,
            TryParseInteger<T> parser,
            out object? convertedValue,
            out string? errorMessage,
            string typeName)
        {
            if (parser(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                convertedValue = result;
                errorMessage = null;
                return true;
            }

            convertedValue = null;
            errorMessage = $"Route value must be a valid {typeName}.";
            return false;
        }

        private static string GetFriendlyTypeName(Type type)
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            return underlyingType == null ? type.Name : $"{underlyingType.Name}?";
        }

        private delegate bool TryParseInteger<T>(
            string text,
            NumberStyles numberStyles,
            IFormatProvider formatProvider,
            out T result);
    }
}
