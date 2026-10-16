# AgentLab

`Roentgen.AgentLab` is the headless Phase 14 diagnostic and soak tool.

```text
dotnet run --project tools/Roentgen.AgentLab -- smoke
dotnet run --project tools/Roentgen.AgentLab -- route 10000
dotnet run --project tools/Roentgen.AgentLab -- utility 10000
dotnet run --project tools/Roentgen.AgentLab -- schedule 14400
dotnet run --project tools/Roentgen.AgentLab -- door
dotnet run --project tools/Roentgen.AgentLab -- strategic 14400
```

`route` executes deterministic routes across a generated grid and reports aggregate checksums and failures. `utility` exercises varied awareness, health, morale, fear, needs, cover, and ammunition contexts. `schedule` advances reduced simulation and reports final location, activity, and canonical checksum. `door` compares the same interior route with a closed and dynamically opened authoritative door. `strategic` advances distant schedule, abstract combat, fear, health, and recovery state and reports a canonical checksum.
