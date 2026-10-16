# BalanceLab and the Six-Build Gate

`Roentgen.BalanceLab` reports attribute formulas, starting skills, level curves, perk graph validity, six-build route viability, deterministic NPC progression soaks, and JSON acceptance reports.

The permanent Prospect Clinic progression fixture requires six build-centered completions. A passing run uses six distinct solution routes. This proves that noncombat progression is mechanically meaningful before full quests and dialogue arrive.

Run:

```text
./eng/balancelab.sh scenario
./eng/balancelab.sh progression-soak 10000
./eng/balancelab.sh export-report
```
