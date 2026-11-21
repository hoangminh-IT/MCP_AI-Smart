using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

var folder = FindExperimentFolder();
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
var compact = new JsonSerializerOptions(json) { WriteIndented = false };
if (args is ["--predict", var text])
{
    var latest = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(folder, "results/training/latest.json")));
    var saved = JsonSerializer.Deserialize<SavedModel>(File.ReadAllText(Path.Combine(folder, "results/training", latest.GetProperty("runId").GetString()!, "selected_model.json")), json)!;
    var features = TextFeatures.Load(saved.Features).Transform(text);
    var probabilities = new SoftmaxClassifier(saved.Model).Predict(features);
    var label = SoftmaxClassifier.ArgMax(probabilities);
    Console.WriteLine(JsonSerializer.Serialize(new { tool = saved.Labels[label], softmaxScore = probabilities[label], selectedConfig = saved.SelectedConfig }, json));
    return;
}
if (args is ["--help"] or ["-h"])
{
    Console.WriteLine("dotnet run --project Training");
    Console.WriteLine("Train/evaluate a local TF-IDF + softmax classifier for eight read-only MCP intents. No LLM, API or DB access.");
    Console.WriteLine("Use --predict \"Vietnamese question\" with the latest saved model.");
    return;
}
if (args.Length != 0) throw new ArgumentException("Unknown arguments. Use --help.");

var datasetPath = Path.Combine(folder, "data/mcp_tool_intents.csv");
var configurationsPath = Path.Combine(folder, "data/training_configurations.json");
var examples = ExperimentFiles.ReadExamples(datasetPath);
var training = examples.Where(example => example.Split == "train").ToArray();
var validation = examples.Where(example => example.Split == "validation").ToArray();
var test = examples.Where(example => example.Split == "test").ToArray();
var labels = training.Select(example => example.Intent).Distinct().Order(StringComparer.Ordinal).ToArray();
var labelIndex = labels.Select((label, index) => (label, index)).ToDictionary(item => item.label, item => item.index);
if (examples.Count != 160 || training.Length != 96 || validation.Length != 32 || test.Length != 32 || labels.Length != 8 ||
    examples.Select(example => example.Id).Distinct().Count() != examples.Count ||
    examples.Select(example => TextFeatures.Normalize(example.Text)).Distinct().Count() != examples.Count ||
    examples.Any(example => !labelIndex.ContainsKey(example.Intent)) ||
    labels.Any(label => training.Count(example => example.Intent == label) != 12 || validation.Count(example => example.Intent == label) != 4 || test.Count(example => example.Intent == label) != 4))
    throw new InvalidOperationException("Dataset split, class balance or duplicate checks failed.");
var configs = JsonSerializer.Deserialize<List<ExperimentConfig>>(File.ReadAllText(configurationsPath), json)!;
if (configs.Count != 10 || configs.Select(config => config.Id).Distinct().Count() != 10 ||
    configs.Any(config => config.Epochs < 1 || config.LearningRate <= 0 || config.L2 < 0 || config.Ngrams is not (1 or 2)))
    throw new InvalidOperationException("Invalid training configurations.");
var datasetHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(datasetPath)));
var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
var directory = Path.Combine(folder, "results/training", runId);
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory, "dataset_manifest.json"), JsonSerializer.Serialize(new
{
    datasetSha256 = datasetHash, configurationsSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(configurationsPath))),
    provenance = "Synthetic labeled Vietnamese utterances composed for this experiment; not production user conversations.",
    splitPolicy = "Fixed train/validation/test; normalized utterances and IDs are disjoint; vectorizer is fitted on train only.",
    trainingIds = training.Select(example => example.Id), validationIds = validation.Select(example => example.Id), testIds = test.Select(example => example.Id), labels
}, json));
using var trainingCsv = new StreamWriter(Path.Combine(directory, "training_log.csv"));
using var trainingLog = new StreamWriter(Path.Combine(directory, "training_log.jsonl"));
ExperimentFiles.CsvRow(trainingCsv, "config_id", "epoch", "train_loss", "train_accuracy", "validation_loss", "validation_accuracy", "validation_macro_f1", "elapsed_ms");
var models = new List<TrainedRun>();

foreach (var config in configs)
{
    var features = TextFeatures.Fit(training, config.Ngrams);
    var encodedTrain = Encode(training, features);
    var encodedValidation = Encode(validation, features);
    var classifier = new SoftmaxClassifier(labels.Length, features.Parameters.Vocabulary.Length, config.Seed);
    var watch = Stopwatch.StartNew();
    var initialLoss = classifier.Evaluate(encodedTrain).Loss;
    Scores trainScores = null!, validationScores = null!;
    for (var epoch = 0; epoch <= config.Epochs; epoch++)
    {
        if (epoch > 0) classifier.TrainEpoch(encodedTrain, config.LearningRate, config.L2);
        trainScores = classifier.Evaluate(encodedTrain);
        validationScores = classifier.Evaluate(encodedValidation);
        ExperimentFiles.CsvRow(trainingCsv, config.Id, epoch, trainScores.Loss, trainScores.Accuracy, validationScores.Loss, validationScores.Accuracy, validationScores.MacroF1, watch.Elapsed.TotalMilliseconds);
        trainingLog.WriteLine(JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow, phase = "training", configId = config.Id, epoch,
            trainLoss = trainScores.Loss, trainAccuracy = trainScores.Accuracy,
            validationLoss = validationScores.Loss, validationAccuracy = validationScores.Accuracy, validationMacroF1 = validationScores.MacroF1
        }, compact));
    }
    if (trainScores.Loss >= initialLoss) throw new InvalidOperationException("Training loss did not decrease: " + config.Id);
    models.Add(new(config, features, classifier, trainScores, validationScores, initialLoss, watch.Elapsed.TotalMilliseconds));
    Console.WriteLine($"TRAIN {config.Id}: epochs={config.Epochs}, train accuracy={trainScores.Accuracy:P1}, validation F1={validationScores.MacroF1:F4}");
}
trainingCsv.Flush(); trainingLog.Flush();

// Select and persist using validation before evaluating any test examples.
var selected = models.OrderByDescending(model => model.Validation.MacroF1).ThenBy(model => model.Validation.Loss).ThenBy(model => model.Config.Id, StringComparer.Ordinal).First();
var savedModel = new SavedModel(selected.Config.Id, datasetHash, labels, selected.Features.Parameters, selected.Model.Parameters);
File.WriteAllText(Path.Combine(directory, "selection.json"), JsonSerializer.Serialize(new
{
    selectedConfig = selected.Config.Id, selectedAt = DateTimeOffset.UtcNow,
    criterion = "Validation macro-F1 descending; validation cross-entropy ascending; config ID as final tie-break.",
    testUsedForSelection = false
}, json));
File.WriteAllText(Path.Combine(directory, "selected_model.json"), JsonSerializer.Serialize(savedModel, json));
var reloaded = JsonSerializer.Deserialize<SavedModel>(File.ReadAllText(Path.Combine(directory, "selected_model.json")), json)!;
var reloadedModel = new SoftmaxClassifier(reloaded.Model);
var reloadedFeatures = TextFeatures.Load(reloaded.Features);
foreach (var example in validation)
{
    var original = selected.Model.Predict(selected.Features.Transform(example.Text));
    var restored = reloadedModel.Predict(reloadedFeatures.Transform(example.Text));
    if (original.Zip(restored).Any(pair => Math.Abs(pair.First - pair.Second) > 1e-12)) throw new InvalidOperationException("Saved model prediction mismatch.");
}

using var testLog = new StreamWriter(Path.Combine(directory, "test_log.jsonl"));
using var results = new StreamWriter(Path.Combine(directory, "experiment_results.csv"));
using var predictions = new StreamWriter(Path.Combine(directory, "test_predictions.csv"));
ExperimentFiles.CsvRow(results, "config_id", "epochs", "learning_rate", "l2", "ngrams", "seed", "vocabulary_size", "initial_train_loss", "train_loss", "train_accuracy", "validation_loss", "validation_accuracy", "validation_macro_f1", "test_loss", "test_accuracy", "test_macro_f1", "training_ms", "selected");
ExperimentFiles.CsvRow(predictions, "config_id", "id", "text", "expected_tool", "predicted_tool", "softmax_score", "correct", "selected");
var finalScores = new Dictionary<string, Scores>();
foreach (var model in models)
{
    var encodedTest = Encode(test, model.Features);
    var scores = model.Model.Evaluate(encodedTest);
    finalScores[model.Config.Id] = scores;
    testLog.WriteLine(JsonSerializer.Serialize(new
    {
        timestamp = DateTimeOffset.UtcNow, phase = "test", configId = model.Config.Id, selected = model == selected,
        sampleCount = test.Length, loss = scores.Loss, accuracy = scores.Accuracy, macroF1 = scores.MacroF1, labels, confusionMatrix = scores.Confusion
    }, compact));
    var config = model.Config;
    ExperimentFiles.CsvRow(results, config.Id, config.Epochs, config.LearningRate, config.L2, config.Ngrams, config.Seed,
        model.Features.Parameters.Vocabulary.Length, model.InitialLoss, model.Train.Loss, model.Train.Accuracy,
        model.Validation.Loss, model.Validation.Accuracy, model.Validation.MacroF1, scores.Loss, scores.Accuracy, scores.MacroF1, model.Milliseconds, model == selected);
    foreach (var example in encodedTest)
    {
        var probabilities = model.Model.Predict(example.Features); var prediction = SoftmaxClassifier.ArgMax(probabilities);
        ExperimentFiles.CsvRow(predictions, config.Id, example.Id, example.Text, labels[example.Label], labels[prediction], probabilities[prediction], prediction == example.Label, model == selected);
    }
    Console.WriteLine($"TEST {config.Id}: accuracy={scores.Accuracy:P1}, macro F1={scores.MacroF1:F4}");
}
results.Flush(); predictions.Flush(); testLog.Flush();
var baselineAccuracy = (double)test.Count(example => example.Intent == labels[0]) / test.Length;
File.WriteAllText(Path.Combine(directory, "summary.json"), JsonSerializer.Serialize(new
{
    passed = true, source = "REAL_CSHARP_TRAINING_SYNTHETIC_UTTERANCES", algorithm = "TF-IDF + multiclass softmax regression, full-batch gradient descent and L2 regularization",
    runId, datasetSha256 = datasetHash, trainCount = training.Length, validationCount = validation.Length, testCount = test.Length,
    configCount = configs.Count, epochRows = configs.Sum(config => config.Epochs + 1), selectedConfig = selected.Config.Id,
    baseline = "Always predict the first majority class (classes balanced)", baselineTestAccuracy = baselineAccuracy,
    selectedTestAccuracy = finalScores[selected.Config.Id].Accuracy, selectedTestMacroF1 = finalScores[selected.Config.Id].MacroF1,
    testUsedForSelection = false, savedModelRoundTripPassed = true
}, json));
File.WriteAllText(Path.Combine(folder, "results/training/latest.json"), JsonSerializer.Serialize(new { runId, selectedConfig = selected.Config.Id, datasetSha256 = datasetHash }, json));
Console.WriteLine("TRAINING_RUN=" + directory);

EncodedExample[] Encode(IEnumerable<Example> data, TextFeatures features) => data.Select(example => new EncodedExample(example.Id, example.Text, labelIndex[example.Intent], features.Transform(example.Text))).ToArray();

static string FindExperimentFolder()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "data/mcp_tool_intents.csv")) &&
            File.Exists(Path.Combine(directory.FullName, "Training/Training.csproj"))) return directory.FullName;
    throw new InvalidOperationException("Keep Training and data inside the same experiments folder.");
}

internal sealed record TrainedRun(ExperimentConfig Config, TextFeatures Features, SoftmaxClassifier Model, Scores Train, Scores Validation, double InitialLoss, double Milliseconds);
