# FactionLab

`Roentgen.FactionLab` is the Phase 15 deterministic inspection and soak tool.

```text
dotnet run --project tools/Roentgen.FactionLab -- smoke
dotnet run --project tools/Roentgen.FactionLab -- generate --seed 123
dotnet run --project tools/Roentgen.FactionLab -- inspect-faction Prospect
dotnet run --project tools/Roentgen.FactionLab -- inspect-settlement Blackwater
dotnet run --project tools/Roentgen.FactionLab -- simulate-days 365
dotnet run --project tools/Roentgen.FactionLab -- simulate-years 10
dotnet run --project tools/Roentgen.FactionLab -- kill-leader Prospect
dotnet run --project tools/Roentgen.FactionLab -- interrupt-route
dotnet run --project tools/Roentgen.FactionLab -- add-shortage Prospect Medicine
dotnet run --project tools/Roentgen.FactionLab -- start-conflict
dotnet run --project tools/Roentgen.FactionLab -- record-crime
dotnet run --project tools/Roentgen.FactionLab -- apply-disguise
dotnet run --project tools/Roentgen.FactionLab -- inspect-reputation
dotnet run --project tools/Roentgen.FactionLab -- validate
dotnet run --project tools/Roentgen.FactionLab -- repair
dotnet run --project tools/Roentgen.FactionLab -- export phase15-society.bin
dotnet run --project tools/Roentgen.FactionLab -- replay 3650
```

All generation and simulation commands can be replayed from the campaign seed. The tool prints stable IDs and canonical checksums so a failure can be reproduced without the MonoGame host.
