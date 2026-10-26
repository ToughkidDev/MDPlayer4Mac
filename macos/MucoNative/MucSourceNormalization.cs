namespace MDPlayer.MucoNative;

// Host-independent source framing. Kept here so the native compiler does not
// depend on MDPlayerCore, its audio devices, or any MSX build products.
public static class MucSourceNormalization
{
        public static byte[] NormalizeTrackDeclarationIndentation(byte[] source)
        {
            const string trackCharacters = "ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmnopqrstuvWXYZwxyz";
            using MemoryStream result = new(source.Length);

            for (int position = 0; position < source.Length;)
            {
                int lineEnd = position;
                while (lineEnd < source.Length && source[lineEnd] != '\r' && source[lineEnd] != '\n') lineEnd++;

                int contentStart = position;
                while (contentStart < lineEnd && (source[contentStart] == (byte)' ' || source[contentStart] == (byte)'\t')) contentStart++;
                bool isTrackDeclaration = contentStart < lineEnd
                    && source[contentStart] < 0x80
                    && trackCharacters.IndexOf((char)source[contentStart]) >= 0;

                int copyStart = isTrackDeclaration ? contentStart : position;
                byte[] line = source.AsSpan(copyStart, lineEnd - copyStart).ToArray();
                MovePortamentoLengths(line);
                result.Write(line, 0, line.Length);

                if (lineEnd >= source.Length) break;

                // MUCOM's parser deliberately splits source on CRLF.  MUC files copied
                // from a Git repository or authored on macOS often use LF, which would
                // otherwise be parsed as one giant line (and terminate almost at once).
                if (source[lineEnd] == '\r')
                {
                    result.WriteByte((byte)'\r');
                    lineEnd++;
                    if (lineEnd < source.Length && source[lineEnd] == '\n') lineEnd++;
                }
                else
                {
                    lineEnd++;
                    result.WriteByte((byte)'\r');
                }
                result.WriteByte((byte)'\n');
                position = lineEnd;
            }

            return result.ToArray();
        }

        // MUCOM88's portamento takes its length right after the first note: {c4>c}.
        // The MUCEdit manual also writes it after the closing brace ({c>c}4), which the
        // compiler rejects with E0100 because the trailing number is left over. Rewrite
        // that form in memory only (the user's MUC file is never changed) by moving the
        // length next to the first note. The line keeps its byte length, so compiler error
        // columns stay correct. Comment (;) and tag (#) lines and Shift-JIS double-byte
        // characters are left untouched; a portamento that already has a length on its
        // first note is left as written.
        public static void MovePortamentoLengths(byte[] line)
        {
            int first = 0;
            while (first < line.Length && (line[first] == (byte)' ' || line[first] == (byte)'\t')) first++;
            if (first >= line.Length || line[first] == (byte)';' || line[first] == (byte)'#') return;

            for (int i = first; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)';') return;
                if ((b >= 0x81 && b <= 0x9f) || (b >= 0xe0 && b <= 0xfc)) { i++; continue; }
                if (b != (byte)'{') continue;

                int noteEnd = SkipNote(line, i + 1);
                if (noteEnd < 0 || SkipLength(line, noteEnd) != noteEnd) continue;
                int k = noteEnd;
                while (k < line.Length && (line[k] == (byte)' ' || line[k] == (byte)'\t' || line[k] == (byte)'<' || line[k] == (byte)'>')) k++;
                k = SkipNote(line, k);
                if (k < 0) continue;
                while (k < line.Length && (line[k] == (byte)' ' || line[k] == (byte)'\t')) k++;
                if (k >= line.Length || line[k] != (byte)'}') continue;
                int close = k;
                int lengthStart = close + 1;
                while (lengthStart < line.Length && (line[lengthStart] == (byte)' ' || line[lengthStart] == (byte)'\t')) lengthStart++;
                int lengthEnd = SkipLength(line, lengthStart);
                if (lengthEnd == lengthStart) continue;

                // {note  rest}  ws  length  ->  {note length rest}  ws
                byte[] length = line.AsSpan(lengthStart, lengthEnd - lengthStart).ToArray();
                byte[] rest = line.AsSpan(noteEnd, close + 1 - noteEnd).ToArray();
                byte[] gap = line.AsSpan(close + 1, lengthStart - close - 1).ToArray();
                int w = noteEnd;
                length.CopyTo(line, w); w += length.Length;
                rest.CopyTo(line, w); w += rest.Length;
                gap.CopyTo(line, w);
                i = lengthEnd - 1;
            }
        }

        // A MUCOM note: c d e f g a b, optionally followed by one + or -. Returns the index
        // after it, or -1 when there is no note at index.
        private static int SkipNote(byte[] line, int index)
        {
            if (index >= line.Length || line[index] < (byte)'a' || line[index] > (byte)'g') return -1;
            index++;
            if (index < line.Length && (line[index] == (byte)'+' || line[index] == (byte)'-')) index++;
            return index;
        }

        // A MUCOM length: digits or %digits (clock count), followed by optional dots.
        // Returns index unchanged when there is no length.
        private static int SkipLength(byte[] line, int index)
        {
            int p = index;
            if (p < line.Length && line[p] == (byte)'%') p++;
            int digits = p;
            while (p < line.Length && line[p] >= (byte)'0' && line[p] <= (byte)'9') p++;
            if (p == digits) return index;
            while (p < line.Length && line[p] == (byte)'.') p++;
            return p;
        }

}
