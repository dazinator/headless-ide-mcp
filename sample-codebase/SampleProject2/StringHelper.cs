namespace SampleProject2;

public class StringHelper
{
    public string ReverseString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        char[] charArray = input.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }
}
