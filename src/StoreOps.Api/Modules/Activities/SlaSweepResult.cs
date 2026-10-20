namespace StoreOps.Api.Modules.Activities;

// Outcome of one SLA sweep: how many new breaches were raised and how many escalations (always 0 until escalation exists).
public sealed record SlaSweepResult(int BreachesRaised, int EscalationsRaised);
