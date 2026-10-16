# ADR 0017: Validate intact structures before historical damage

Status: Accepted

Every structure is generated and validated in an intact state. Historical damage then modifies topology and geometry under explicit rules. The damaged result is validated again and repaired locally when required. Random deletion is not a ruin generator.
