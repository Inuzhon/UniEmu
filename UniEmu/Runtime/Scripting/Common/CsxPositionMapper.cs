namespace UniEmu.Runtime.Scripting.Common;

public static class CsxPositionMapper
{
    public static int ToOffset(string sourceCode, CsxEditorPosition? position)
    {
        if (position is null)
        {
            return sourceCode.Length;
        }

        var targetLine = Math.Max(0, position.Line - 1);
        var targetColumn = Math.Max(0, position.Column - 1);
        var currentLine = 0;
        var offset = 0;
        while (currentLine < targetLine && offset < sourceCode.Length)
        {
            var next = sourceCode.IndexOf('\n', offset);
            if (next < 0)
            {
                return sourceCode.Length;
            }

            offset = next + 1;
            currentLine++;
        }

        return Math.Clamp(offset + targetColumn, 0, sourceCode.Length);
    }
}
