internal sealed record ModelParameters(double[][] Weights, double[] Bias);
internal sealed record Scores(double Loss, double Accuracy, double MacroF1, int[][] Confusion);

internal sealed class SoftmaxClassifier
{
    public ModelParameters Parameters { get; }

    public SoftmaxClassifier(int classes, int features, int seed)
    {
        var random = new Random(seed);
        Parameters = new(Enumerable.Range(0, classes).Select(_ => Enumerable.Range(0, features).Select(_ => (random.NextDouble() - .5) * .02).ToArray()).ToArray(), new double[classes]);
    }

    public SoftmaxClassifier(ModelParameters parameters) => Parameters = parameters;

    public double[] Predict((int Index, double Value)[] features)
    {
        var logits = Parameters.Bias.Select((bias, label) => bias + features.Sum(feature => Parameters.Weights[label][feature.Index] * feature.Value)).ToArray();
        var maximum = logits.Max();
        var values = logits.Select(value => Math.Exp(value - maximum)).ToArray();
        var total = values.Sum();
        return values.Select(value => value / total).ToArray();
    }

    public void TrainEpoch(IReadOnlyList<EncodedExample> examples, double learningRate, double l2)
    {
        var weights = Parameters.Weights;
        var gradients = weights.Select(row => new double[row.Length]).ToArray();
        var biasGradient = new double[Parameters.Bias.Length];
        foreach (var example in examples)
        {
            var probabilities = Predict(example.Features);
            for (var label = 0; label < probabilities.Length; label++)
            {
                var error = probabilities[label] - (example.Label == label ? 1 : 0);
                biasGradient[label] += error;
                foreach (var feature in example.Features) gradients[label][feature.Index] += error * feature.Value;
            }
        }
        for (var label = 0; label < weights.Length; label++)
        {
            Parameters.Bias[label] -= learningRate * biasGradient[label] / examples.Count;
            for (var column = 0; column < weights[label].Length; column++)
                weights[label][column] -= learningRate * (gradients[label][column] / examples.Count + l2 * weights[label][column]);
        }
    }

    public Scores Evaluate(IReadOnlyList<EncodedExample> examples)
    {
        var confusion = Parameters.Bias.Select(_ => new int[Parameters.Bias.Length]).ToArray();
        var loss = 0.0; var correct = 0;
        foreach (var example in examples)
        {
            var probabilities = Predict(example.Features);
            if (probabilities.Any(value => !double.IsFinite(value)) || Math.Abs(probabilities.Sum() - 1) > 1e-9)
                throw new InvalidOperationException("Invalid probability distribution.");
            var prediction = ArgMax(probabilities);
            confusion[example.Label][prediction]++;
            if (prediction == example.Label) correct++;
            loss -= Math.Log(Math.Max(probabilities[example.Label], 1e-15));
        }
        var f1 = Enumerable.Range(0, confusion.Length).Select(label =>
        {
            var truePositive = confusion[label][label];
            var falsePositive = confusion.Sum(row => row[label]) - truePositive;
            var falseNegative = confusion[label].Sum() - truePositive;
            var denominator = 2 * truePositive + falsePositive + falseNegative;
            return denominator == 0 ? 0 : 2.0 * truePositive / denominator;
        }).Average();
        return new(loss / examples.Count, (double)correct / examples.Count, f1, confusion);
    }

    public static int ArgMax(double[] values)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++) if (values[i] > values[best]) best = i;
        return best;
    }
}
