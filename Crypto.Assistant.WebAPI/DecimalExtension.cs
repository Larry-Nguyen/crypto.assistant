using System;

namespace Crypto.Assistant.WebAPI
{
    public static class DecimalExtension
    {
        public static string ToStringPrice(this decimal value, IFormatProvider provider)
        {
            return value.ToString("C4", provider).TrimEnd('0').TrimEnd('.');
        }
    }
}
