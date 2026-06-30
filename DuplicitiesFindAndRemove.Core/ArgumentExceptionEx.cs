namespace DuplicitiesFindAndRemove.Core;

public class ArgumentExceptionEx
{
    public static void ThrowIfNullOrWhiteSpace(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(s));
        }
    }
}
