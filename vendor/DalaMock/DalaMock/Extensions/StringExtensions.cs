namespace DalaMock.Core.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Parse the input string by placing a space between character case changes in the string.
    /// </summary>
    /// <param name="strInput">The string to parse.</param>
    /// <returns>The altered string.</returns>
    public static string AddSpaces(this string strInput)
    {
        string strOutput = string.Empty;

        int intCurrentCharPos = 0;

        int intLastCharPos = strInput.Length - 1;

        for (intCurrentCharPos = 0; intCurrentCharPos <= intLastCharPos; intCurrentCharPos++)
        {
            char chrCurrentInputChar = strInput[intCurrentCharPos];

            char chrPreviousInputChar = chrCurrentInputChar;

            if (intCurrentCharPos > 0)
            {
                chrPreviousInputChar = strInput[intCurrentCharPos - 1];
            }

            if (char.IsUpper(chrCurrentInputChar) == true && char.IsLower(chrPreviousInputChar) == true)
            {
                strOutput += " ";
            }

            strOutput += chrCurrentInputChar;
        }

        return strOutput;
    }
}
