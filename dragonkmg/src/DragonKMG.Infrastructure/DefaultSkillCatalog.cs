using DragonKMG.Domain;

namespace DragonKMG.Infrastructure;

internal static class DefaultSkillCatalog
{
    internal static readonly IReadOnlyList<SkillDefinition> Skills =
    [
        Create("3d325842-1a75-4937-a97a-91b491a7fdf1", "kmg-engineering-standard", "KMG Engineering Standard", "Lifecycle, evidence, and fail-closed engineering discipline.", "Classify work, preserve clean architecture boundaries, and record only verified claims.", "$kmg-agent-skill"),
        Create("6d750091-148a-43c4-8429-b7bd09169e60", "cyber-kmg-security", "Cyber KMG Security", "Defensive security review and hardening guidance.", "Use passive security checks first. Never execute tools, transmit data, or escalate privileges without an independent grant and approval.", "$cyber-kmg-security"),
        Create("ba5bf566-b149-4d25-b1fc-8d137e30b2bd", "nexusecore-ui-ux", "NexusCore UI/UX", "Accessible, RTL-safe operational dashboard guidance.", "Design data-backed workflows with loading, empty, error, retry, and permission-denied states. Do not claim a cosmetic control is functional.", "$nexusecore-ui-ux"),
        Create("8a0c1b9e-29a0-4541-9878-f3f16b9c6bd8", "flutter-architect-review", "Flutter Architect Review", "Mobile architecture and ERP workflow review guidance.", "Keep Flutter widgets declarative, preserve typed state boundaries, and treat mobile data security and offline behavior as explicit concerns.", "$flutter-architect-review")
    ];

    private static SkillDefinition Create(string id, string key, string name, string description, string instructions, string sourceRef) => new(Guid.Parse(id), key, name, description, 1, true, SkillType.Instruction, instructions, "[]", "local-default", "local-codex-skill", sourceRef);
}
