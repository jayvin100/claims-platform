namespace Claims.Api.Claims;

/// <summary>
/// An Officer tried to act on a Claim that another Officer holds. The API answers 403 (ADR-0006, spec #2):
/// Officers may see every Claim, so this is "not allowed", not "not found".
/// </summary>
public class NotAssignedOfficerException() : Exception("Only the Assigned Officer can act on this Claim.");
