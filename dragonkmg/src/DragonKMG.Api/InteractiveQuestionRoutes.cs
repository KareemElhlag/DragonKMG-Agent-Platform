using System.Text.Json;
using DragonKMG.Application;
using DragonKMG.Domain;

namespace DragonKMG.Api;

public static class InteractiveQuestionRoutes
{
    public static void MapInteractiveQuestionRoutes(this WebApplication app)
    {
        app.MapGet("/api/sessions/{id:guid}/questions", async (Guid id, IPlatformStore store, CancellationToken ct) => Results.Ok(await store.ListInteractiveQuestionsAsync(id, ct)));
        app.MapPost("/api/sessions/{id:guid}/questions/{questionId:guid}/answer", async (Guid id, Guid questionId, QuestionAnswerRequest request, IPlatformStore store, CancellationToken ct) =>
        {
            var question = await store.FindInteractiveQuestionAsync(questionId, ct);
            if (question is null || question.SessionId != id) return Results.NotFound(new { code = "QuestionUnavailable" });
            if (question.Status != InteractiveQuestionStatus.Pending) return Results.Conflict(new { code = "QuestionClosed" });
            if (question.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                var expired = question with { Status = InteractiveQuestionStatus.Expired };
                await store.SaveInteractiveQuestionAsync(expired, ct);
                return Results.Conflict(new { code = "QuestionExpired", question = expired });
            }
            if (string.Equals(request.Decision, "skip", StringComparison.OrdinalIgnoreCase))
            {
                var skipped = question with { Status = InteractiveQuestionStatus.Cancelled, Answer = "__skipped__", AnsweredAt = DateTimeOffset.UtcNow };
                await store.SaveInteractiveQuestionAsync(skipped, ct);
                return Results.Ok(skipped);
            }
            if (string.Equals(request.Decision, "manual", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.Answer) || request.Answer.Length > 2000) return Results.BadRequest(new { code = "ManualAnswerInvalid" });
                var manual = question with { Status = InteractiveQuestionStatus.Answered, Answer = request.Answer.Trim(), AnsweredAt = DateTimeOffset.UtcNow };
                await store.SaveInteractiveQuestionAsync(manual, ct);
                return Results.Ok(manual);
            }
            if (!string.Equals(request.Decision, "answer", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { code = "DecisionRequired" });
            using var options = JsonDocument.Parse(question.OptionsJson);
            if (!options.RootElement.EnumerateArray().Any(item => string.Equals(item.GetString(), request.Answer, StringComparison.Ordinal))) return Results.BadRequest(new { code = "AnswerNotAllowed" });
            var answered = question with { Status = InteractiveQuestionStatus.Answered, Answer = request.Answer, AnsweredAt = DateTimeOffset.UtcNow };
            await store.SaveInteractiveQuestionAsync(answered, ct);
            return Results.Ok(answered);
        });
    }
}
public sealed record QuestionAnswerRequest(string Answer, string Decision = "answer");
