## Summary

What changed and why? Link any related issue(s) and note the affected subsystem.

## Compatibility and determinism

- [ ] Seed stability / gameplay-authority boundaries considered.
- [ ] Save/schema compatibility and content ID changes considered.
- [ ] Third-party asset licenses reviewed, if applicable.

## Verification

- [ ] `python eng/validate_repository.py`
- [ ] `python eng/verify_source_manifest.py` (after regenerating intentional changes)
- [ ] `dotnet build Roentgen.sln -c Release`
- [ ] `dotnet test Roentgen.sln -c Release`
- [ ] Manual gameplay or visual test notes included when relevant.
