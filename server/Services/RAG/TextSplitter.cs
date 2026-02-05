namespace server.Services.RAG
{
    public class TextSplitter
    {
        private readonly int _maxChunkSize;
        private readonly int _chunkOverlap;
        private readonly string[] _separators = { "\n\n", "\n", ". ", " ", "" };

        public TextSplitter(int maxSize = 1000, int overlap = 200)
        {
            _maxChunkSize = maxSize;
            _chunkOverlap = overlap;
        }

        public List<string> SplitText(string text)
        {
            return RecursiveSplit(text, _separators.ToList());
        }

        private List<string> RecursiveSplit(string text, List<string> separators)
        {
            var finalChunks = new List<string>();

            // if the text is already smaller than the limit, we return it 
            if (text.Length <= _maxChunkSize) return new List<string> { text };

            // choosing a separator
            string separator = separators.FirstOrDefault() ?? "";
            var remainingSeparators = separators.Skip(1).ToList();

            var splits = string.IsNullOrEmpty(separator)
                ? text.Select(c => c.ToString()).ToList()
                : text.Split(separator).ToList();

            var currentDoc = new List<string>();
            int currentLen = 0;

            foreach (var s in splits)
            {
                if (currentLen + s.Length + separator.Length > _maxChunkSize)
                {
                    if (currentDoc.Any())
                    {
                        var chunk = string.Join(separator, currentDoc);
                        finalChunks.Add(chunk);

                        // realization Overlap 
                        KeepOverlap(currentDoc, ref currentLen);
                    }

                    // if the element itself is too large, we go deeper using separators
                    if (s.Length > _maxChunkSize)
                    {
                        finalChunks.AddRange(RecursiveSplit(s, remainingSeparators));
                    }
                    else
                    {
                        currentDoc.Add(s);
                        currentLen += s.Length + separator.Length;
                    }
                }
                else
                {
                    currentDoc.Add(s);
                    currentLen += s.Length + separator.Length;
                }
            }

            if (currentDoc.Any()) finalChunks.Add(string.Join(separator, currentDoc));

            return finalChunks;
        }

        private void KeepOverlap(List<string> currentDoc, ref int currentLen)
        {
            // remove old elements until there is no space left for overlapping
            while (currentLen > _chunkOverlap && currentDoc.Count > 1)
            {
                var removed = currentDoc[0];
                currentDoc.RemoveAt(0);
                currentLen -= removed.Length;
            }
        }
    }
}
