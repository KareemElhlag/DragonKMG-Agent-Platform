using DragonKMG.Domain;

namespace DragonKMG.Application;

public static class ModelIdentifier
{
    public static string Canonical(ConnectionKind kind, string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        modelId = modelId.Trim();
        while (kind == ConnectionKind.Gemini && modelId.StartsWith("models/", StringComparison.Ordinal)) modelId = modelId[7..];
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return modelId;
    }
}
