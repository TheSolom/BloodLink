namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Base record for Domain-Driven Design Value Objects.
/// In modern C#, records provide value-based equality, immutability,
/// non-destructive mutation ('with' expressions), and compiler-generated equality semantics out of the box.
/// </summary>
public abstract record ValueObject;
