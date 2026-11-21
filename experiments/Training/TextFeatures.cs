using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal sealed record EncodedExample(string Id, string Text, int Label, (int Index, double Value)[] Features);
internal sealed record FeatureParameters(int Ngrams, string[] Vocabulary, double[] Idf);

internal sealed class TextFeatures
{
    public FeatureParameters Parameters { get; }
    private readonly Dictionary<string, int> indices;

    private TextFeatures(FeatureParameters parameters)
    {
        Parameters = parameters;
        indices = parameters.Vocabulary.Select((word, index) => (word, index)).ToDictionary(item => item.word, item => item.index, StringComparer.Ordinal);
    }

    public static TextFeatures Fit(IReadOnlyList<Example> training, int ngrams)
    {
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var example in training)
            foreach (var token in Tokens(example.Text, ngrams).Distinct(StringComparer.Ordinal))
                documentFrequency[token] = documentFrequency.GetValueOrDefault(token) + 1;
        var vocabulary = documentFrequency.Keys.Order(StringComparer.Ordinal).ToArray();
        var idf = vocabulary.Select(word => Math.Log((training.Count + 1.0) / (documentFrequency[word] + 1.0)) + 1).ToArray();
        return new(new(ngrams, vocabulary, idf));
    }

    public static TextFeatures Load(FeatureParameters parameters) => new(parameters);

    public (int Index, double Value)[] Transform(string text)
    {
        var counts = new Dictionary<int, int>();
        foreach (var token in Tokens(text, Parameters.Ngrams))
            if (indices.TryGetValue(token, out var index)) counts[index] = counts.GetValueOrDefault(index) + 1;
        var values = counts.Select(item => (Index: item.Key, Value: (1 + Math.Log(item.Value)) * Parameters.Idf[item.Key])).ToArray();
        var norm = Math.Sqrt(values.Sum(item => item.Value * item.Value));
        return norm == 0 ? [] : values.Select(item => (item.Index, item.Value / norm)).OrderBy(item => item.Index).ToArray();
    }

    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return string.Join(' ', Regex.Matches(new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray()), "[a-z0-9]+")
            .Select(match => match.Value));
    }

    private static IEnumerable<string> Tokens(string text, int ngrams)
    {
        var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words) yield return word;
        if (ngrams == 2)
            for (var i = 0; i < words.Length - 1; i++) yield return words[i] + " " + words[i + 1];
    }
}
