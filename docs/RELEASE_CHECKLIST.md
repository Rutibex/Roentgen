# GitHub release checklist

This ZIP is a prepared **source** release, not a certified binary build.

- [ ] Set GitHub repository name and short description from `docs/GITHUB_PAGE.md`; add topics and screenshots **only after capturing real in-game imagery**.
- [ ] Configure the repository default branch as `main`; review CI and CodeQL permissions.
- [ ] Install/verify .NET 10 SDK and restore packages on both Windows and Linux.
- [ ] Run full `dotnet build Roentgen.sln -c Release` and `dotnet test Roentgen.sln -c Release`; resolve failures.
- [ ] Run `python eng/validate_repository.py` and all relevant contract/vector checks.
- [ ] Launch on real GPU and audio hardware; test a new campaign, save/load, Focus, menus, visuals and accessibility.
- [ ] Run representative generated-world, AI/collision and quest/campaign walkthroughs.
- [ ] Inspect `THIRD_PARTY_NOTICES.md`, actual NuGet dependency notices and GeneralUser GS sample provenance concerns before distributing binaries.
- [ ] Ensure no private tokens, personal save files, generated diagnostics or oversized outputs are checked in.
- [ ] Tag `v0.21.0-preview` **only after the above checks**. The Publish workflow will attach Windows/Linux binary archives if its jobs pass.

No screenshots or performance numbers have been fabricated for the repository page.
