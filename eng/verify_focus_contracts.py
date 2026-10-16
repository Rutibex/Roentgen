from pathlib import Path
root=Path(__file__).resolve().parents[1]
required=['src/Roentgen.Focus/FocusContracts.cs','src/Roentgen.Focus/FocusTargetGraph.cs','src/Roentgen.Focus/FocusForecast.cs','src/Roentgen.Focus/FocusPlan.cs','src/Roentgen.Focus/FocusExecution.cs','src/Roentgen.App/Content/Effects/FocusDiagnosticMask.fx']
missing=[x for x in required if not (root/x).is_file()]
if missing: raise SystemExit('Missing Phase 11 files: '+', '.join(missing))
text='\n'.join((root/x).read_text(errors='ignore') for x in required if x.endswith('.cs'))
for token in ['FocusTemporalMode','CombatResolutionSeed','FocusTargetGraph','FocusActionForecast','FocusResourceState','DraftFocusPlan','FocusExecutionController']:
    if token not in text: raise SystemExit('Missing contract: '+token)
print('Phase 11 Focus contracts verified.')
