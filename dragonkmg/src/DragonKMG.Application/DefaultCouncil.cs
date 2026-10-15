using DragonKMG.Domain;

namespace DragonKMG.Application;

public static class DefaultCouncil
{
    public static readonly IReadOnlyList<AgentRole> Roles =
    [
        new(Guid.Parse("83cf1198-54b5-4b52-aabb-d787f2ce4ca1"), "Execution Engineer", "Builds the requested change with verifiable evidence.", "Implement the smallest complete change. Keep boundaries explicit, preserve invariants, and report every limitation.", true, 1),
        new(Guid.Parse("16ec0a19-30bd-4df8-bc34-ed8f0365ea33"), "Quality Reviewer", "Independently reviews correctness, architecture, and operational UX.", "Review the implementation independently. Identify missing tests, unsafe assumptions, broken user flows, and unverified claims. Do not approve without evidence.", true, 1),
        new(Guid.Parse("6c3297f2-b244-4823-86e2-0c07461d77ea"), "Security Test Reviewer", "Reviews defensive security, authorization, and test evidence.", "Perform an authorized defensive review. Verify fail-closed behavior, secret handling, boundary validation, and relevant tests. Do not execute risky actions without explicit authorization.", true, 1)
    ];

    public static readonly IReadOnlyList<CouncilAgentTemplate> Agents =
    [
        new("Execution Agent", "Executes approved work and records implementation evidence.", Roles[0].Id, "Implement the requested change and prepare evidence for independent review."),
        new("Quality Review Agent", "Reviews the execution output for correctness and UX quality.", Roles[1].Id, "Review the execution output. Return findings ordered by impact, with clear evidence and test gaps."),
        new("Security Test Agent", "Reviews security posture and verifies the relevant test evidence.", Roles[2].Id, "Review the proposed change defensively. Validate security and tests; report only verified findings.")
    ];
}

public sealed record CouncilAgentTemplate(string Name, string Description, Guid RoleId, string Instructions);
public sealed record DefaultCouncilProvisionRequest(Guid ConnectionId, string ModelId);
public sealed record CouncilRun(Guid ExecutionRunId, Guid QualityReviewRunId, Guid SecurityReviewRunId);
