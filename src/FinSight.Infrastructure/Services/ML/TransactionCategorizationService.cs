using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FinSight.Infrastructure.Services.ML;

/// <summary>
/// ML.NET multi-class classification for transaction categorization.
///
/// On first run (no model file) it trains a new SDCA Maximum Entropy classifier
/// using the seed training data embedded below. Subsequent calls load the saved model.
///
/// In production you'd retrain periodically with user-confirmed categories.
/// </summary>
public sealed class TransactionCategorizationService(ILogger<TransactionCategorizationService> logger)
    : ITransactionCategorizationService
{
    private const string ModelPath = "/tmp/finsight-categorization-model.zip";
    private static ITransformer? _cachedModel;
    private static readonly object SyncLock = new();

    public TransactionCategory Categorize(string merchantName, string description)
    {
        // Fast path: rule-based for common merchants (zero ML overhead)
        var ruleResult = RuleBasedCategorize(merchantName, description);
        if (ruleResult.HasValue) return ruleResult.Value;

        // ML path: loaded/trained model
        try
        {
            var mlContext = new MLContext(seed: 42);
            var model = GetOrTrainModel(mlContext);

            var engine = mlContext.Model.CreatePredictionEngine<TransactionInput, CategoryPrediction>(model);
            var prediction = engine.Predict(new TransactionInput
            {
                MerchantName = merchantName ?? string.Empty,
                Description = description ?? string.Empty
            });

            return MapPredictedCategory(prediction.PredictedCategory);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ML categorization failed for '{Merchant}' — falling back to Uncategorized.", merchantName);
            return TransactionCategory.Uncategorized;
        }
    }

    // ─── Rule-based fast path ────────────────────────────────────────────────

    private static TransactionCategory? RuleBasedCategorize(string? merchant, string? description)
    {
        var text = $"{merchant} {description}".ToLowerInvariant();

        return text switch
        {
            var t when ContainsAny(t, "tim hortons", "mcdonald", "subway", "loblaws", "metro", "sobeys",
                "walmart grocery", "instacart", "doordash", "ubereats", "skip the dishes",
                "starbucks", "coffee", "pizza", "restaurant", "cafe", "bakery", "sushi")
                => TransactionCategory.GroceriesAndFood,

            var t when ContainsAny(t, "presto", "ttc", "oc transpo", "shell", "esso", "petro-canada",
                "sunoco", "uber", "lyft", "via rail", "air canada", "westjet", "parking", "gas station")
                => TransactionCategory.Transportation,

            var t when ContainsAny(t, "netflix", "spotify", "apple music", "crave", "prime video",
                "disney+", "xbox", "playstation", "steam", "cineplex", "amazon prime")
                => TransactionCategory.Entertainment,

            var t when ContainsAny(t, "cineplex", "theatre", "concert", "sport", "raptors",
                "leafs", "blue jays", "rogers centre", "scotiabank arena")
                => TransactionCategory.Entertainment,

            var t when ContainsAny(t, "shoppers drug", "rexall", "pharmacy", "clinic", "hospital",
                "dentist", "optometrist", "ohip", "physio", "massage therapy")
                => TransactionCategory.HealthAndMedical,

            var t when ContainsAny(t, "toronto hydro", "enbridge", "bell canada", "rogers", "telus",
                "shaw", "internet", "hydro one", "alectra", "utilities")
                => TransactionCategory.Utilities,

            var t when ContainsAny(t, "amazon", "best buy", "ikea", "home depot", "canadian tire",
                "h&m", "zara", "winners", "marshalls", "indigo", "chapters", "sport chek")
                => TransactionCategory.ShoppingAndClothing,

            var t when ContainsAny(t, "salary", "payroll", "direct deposit", "e-transfer received",
                "dividend", "refund", "cashback", "interest income")
                => TransactionCategory.Salary,

            var t when ContainsAny(t, "tuition", "university", "college", "coursera", "udemy",
                "books", "school supplies", "student loan")
                => TransactionCategory.EducationAndWork,

            var t when ContainsAny(t, "rent", "mortgage", "strata", "condo fee", "property tax")
                => TransactionCategory.HousingAndRent,

            _ => null  // fall through to ML model
        };
    }

    private static bool ContainsAny(string text, params string[] terms)
        => terms.Any(text.Contains);

    // ─── ML.NET model training + loading ────────────────────────────────────

    private static ITransformer GetOrTrainModel(MLContext mlContext)
    {
        lock (SyncLock)
        {
            if (_cachedModel is not null) return _cachedModel;

            if (File.Exists(ModelPath))
            {
                _cachedModel = mlContext.Model.Load(ModelPath, out _);
                return _cachedModel;
            }

            _cachedModel = TrainModel(mlContext);
            mlContext.Model.Save(_cachedModel, null, ModelPath);
            return _cachedModel;
        }
    }

    private static ITransformer TrainModel(MLContext mlContext)
    {
        // Seed training data — in production this would be user-labeled history
        var trainingData = new List<TransactionInput>
        {
            // Food
            new("Tim Hortons", "Coffee and bagel", "Food"),
            new("McDonald's", "Drive through purchase", "Food"),
            new("Loblaws", "Grocery shopping", "Food"),
            new("Metro", "Grocery store", "Food"),
            new("Instacart", "Grocery delivery", "Food"),
            new("DoorDash", "Food delivery", "Food"),
            new("Starbucks", "Coffee shop", "Food"),
            new("Pizza Pizza", "Pizza order", "Food"),
            new("Chipotle", "Restaurant", "Food"),
            new("Uber Eats", "Food delivery order", "Food"),

            // Transportation
            new("Presto Card", "Transit top-up", "Transportation"),
            new("Shell", "Gas station", "Transportation"),
            new("Esso", "Fuel purchase", "Transportation"),
            new("Uber", "Rideshare trip", "Transportation"),
            new("Via Rail", "Train ticket", "Transportation"),
            new("Air Canada", "Flight booking", "Transportation"),
            new("Parking Authority", "Parking fee", "Transportation"),

            // Shopping
            new("Amazon", "Online purchase", "Shopping"),
            new("Best Buy", "Electronics store", "Shopping"),
            new("IKEA", "Furniture purchase", "Shopping"),
            new("H&M", "Clothing store", "Shopping"),
            new("Canadian Tire", "Hardware store", "Shopping"),
            new("Indigo", "Bookstore purchase", "Shopping"),
            new("Winners", "Clothing and home", "Shopping"),

            // Utilities
            new("Toronto Hydro", "Electricity bill", "Utilities"),
            new("Enbridge Gas", "Natural gas bill", "Utilities"),
            new("Bell Canada", "Internet and phone", "Utilities"),
            new("Rogers", "Mobile phone plan", "Utilities"),
            new("Telus", "Telecom bill", "Utilities"),

            // Entertainment
            new("Cineplex", "Movie tickets", "Entertainment"),
            new("Scotiabank Arena", "Event tickets", "Entertainment"),
            new("Steam", "Video games", "Entertainment"),
            new("Nintendo", "Game purchase", "Entertainment"),

            // Subscription
            new("Netflix", "Monthly subscription", "Subscription"),
            new("Spotify", "Music streaming", "Subscription"),
            new("Adobe Creative Cloud", "Software subscription", "Subscription"),
            new("Microsoft 365", "Software subscription", "Subscription"),
            new("1Password", "Password manager", "Subscription"),
            new("GitHub", "Developer tools", "Subscription"),

            // Healthcare
            new("Shoppers Drug Mart", "Pharmacy purchase", "Healthcare"),
            new("Rexall", "Pharmacy", "Healthcare"),
            new("Dental Clinic", "Dental appointment", "Healthcare"),
            new("Optometrist", "Eye exam", "Healthcare"),
            new("Physiotherapy", "Treatment session", "Healthcare"),

            // Income
            new("Employer Corp", "Payroll direct deposit", "Income"),
            new("CRA", "Tax refund", "Income"),
            new("E-Transfer", "Received payment", "Income"),

            // Housing
            new("Property Management", "Monthly rent", "Housing"),
            new("First National", "Mortgage payment", "Housing"),
            new("City of Toronto", "Property tax", "Housing"),
        };

        var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = mlContext.Transforms.Text
            .FeaturizeText("MerchantFeatures", nameof(TransactionInput.MerchantName))
            .Append(mlContext.Transforms.Text
                .FeaturizeText("DescriptionFeatures", nameof(TransactionInput.Description)))
            .Append(mlContext.Transforms.Concatenate("Features", "MerchantFeatures", "DescriptionFeatures"))
            .Append(mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TransactionInput.Category)))
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedCategory", "PredictedLabel"));

        return pipeline.Fit(dataView);
    }

    private static TransactionCategory MapPredictedCategory(string predictedCategory)
        => predictedCategory switch
        {
            "Food" => TransactionCategory.GroceriesAndFood,
            "Transportation" => TransactionCategory.Transportation,
            "Shopping" => TransactionCategory.ShoppingAndClothing,
            "Utilities" => TransactionCategory.Utilities,
            "Entertainment" => TransactionCategory.Entertainment,
            "Subscription" => TransactionCategory.Entertainment,
            "Healthcare" => TransactionCategory.HealthAndMedical,
            "Income" => TransactionCategory.Salary,
            "Housing" => TransactionCategory.HousingAndRent,
            "Education" => TransactionCategory.EducationAndWork,
            _ when Enum.TryParse<TransactionCategory>(predictedCategory, out var category) => category,
            _ => TransactionCategory.Uncategorized
        };

    // ─── ML.NET data classes ─────────────────────────────────────────────────

    private sealed class TransactionInput
    {
        public string MerchantName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public TransactionInput() { }
        public TransactionInput(string merchant, string description, string category)
        {
            MerchantName = merchant;
            Description = description;
            Category = category;
        }
    }

    private sealed class CategoryPrediction
    {
        [ColumnName("PredictedCategory")]
        public string PredictedCategory { get; set; } = string.Empty;
    }
}
