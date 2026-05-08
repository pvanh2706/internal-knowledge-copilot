namespace InternalKnowledgeCopilot.Api.Modules.Teams;

public sealed record TeamResponse(Guid Id, string Name, string? Description);

public sealed record CreateTeamRequest(string Name, string? Description);
