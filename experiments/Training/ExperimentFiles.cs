using System.Text;

internal sealed record Example(string Id, string Split, string Intent, string Text);
internal sealed record ExperimentConfig(string Id, int Epochs, double LearningRate, double L2, int Ngrams, int Seed);
internal sealed record SavedModel(string SelectedConfig, string DatasetSha256, string[] Labels, FeatureParameters Features, ModelParameters Model);

internal static class ExperimentFiles
{
    public static List<Example> ReadExamples(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines[0].TrimStart('\uFEFF') != "id,split,intent,text") throw new InvalidOperationException("Unexpected dataset header.");
        return lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            var columns = ParseCsv(line);
            if (columns.Length != 4) throw new InvalidOperationException("Dataset must have four CSV columns.");
            return new Example(columns[0], columns[1], columns[2], columns[3]);
        }).ToList();
    }

    private static string[] ParseCsv(string line)
    {
        var result = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { result.Add(value.ToString()); value.Clear(); }
            else value.Append(character);
        }
        if (quoted) throw new InvalidOperationException("Unclosed CSV quotation.");
        result.Add(value.ToString()); return result.ToArray();
    }

    public static void CsvRow(TextWriter writer, params object[] values) => writer.WriteLine(string.Join(',', values.Select(value =>
        "\"" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!.Replace("\"", "\"\"") + "\"")));
}
