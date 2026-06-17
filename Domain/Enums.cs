namespace SupportMemoryService.Domain;

public enum EntityType { Account, Contact, Ticket, Policy }

public enum SourceSystem { Crm, Contract, Chat, Email, AgentNote, System }

public enum Reliability { Low, Medium, High }

public enum FactStatus { Active, Superseded, Stale, Contradicted, Ambiguous }

public enum InsightKind { IdempotencyConflict, PossibleDuplicate, AmbiguousIdentity, UnverifiedClaim }
