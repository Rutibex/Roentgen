# CampaignLab

`Roentgen.CampaignLab` is the Phase 17 generation and validation tool.

```text
./eng/campaignlab.sh generate [seed]
./eng/campaignlab.sh inspect-history [seed]
./eng/campaignlab.sh inspect-premise [seed]
./eng/campaignlab.sh print-graph [seed]
./eng/campaignlab.sh validate [seed]
./eng/campaignlab.sh solve [seed] [build-id]
./eng/campaignlab.sh solve-all-builds [seed]
./eng/campaignlab.sh kill-critical-role [seed]
./eng/campaignlab.sh analyze-endings [seed]
./eng/campaignlab.sh export-epilogue [seed]
```

Seeds accept decimal values or a hexadecimal value prefixed by `0x`.

## Diagnostic expectations

- `generate` prints campaign identity, fingerprint, premise, and content counts.
- `inspect-history` prints every regional event in causal order.
- `inspect-premise` prints scoring reasons and the bound hidden cause.
- `print-graph` prints milestone order, successors, and bound quest kernels.
- `validate` runs the campaign-plan validator.
- `solve-all-builds` requires all six established build families to reach a real ending.
- `kill-critical-role` verifies the replacement ladder.
- `analyze-endings` lists priorities, tones, and requirement counts.

CampaignLab is a development tool; no CampaignLab command is required to finish a released campaign.
