using System;
using System.Collections.Generic;
using System.Linq;

public class CommandParser
{
    public List<int> Parse(string input)
    {
        if (input.Contains("-")) return ParseRange(input);
        if (input.Contains(";")) return ParseSelection(input);
        
        if (int.TryParse(input, out int singleIndex))
            return new List<int> { singleIndex - 1 }; // -1 for 0-based index

        return new List<int>();
    }

    private List<int> ParseRange(string input)
    {
        var parts = input.Split('-');
        int start = int.Parse(parts[0]) - 1;
        int end = int.Parse(parts[1]) - 1;
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private List<int> ParseSelection(string input)
    {
        return input.Split(';')
                    .Select(s => int.Parse(s) - 1)
                    .ToList();
    }
}