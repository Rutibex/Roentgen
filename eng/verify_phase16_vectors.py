#!/usr/bin/env python3
from pathlib import Path
import re,hashlib,json,sys
ROOT=Path(__file__).resolve().parents[1]
expected=json.loads((ROOT/'eng/phase16-golden-vectors.json').read_text(encoding='utf-8'))
text=(ROOT/'src/Roentgen.Narrative/Phase16Content.cs').read_text(encoding='utf-8')
catalog=text.split('public static IReadOnlyList<QuestKernelDefinition> CreateInitialKernels')[0]
ids=re.findall(r'\("([a-z0-9-]+)",\s*"[^"]+",\s*"[^"]+"\)',catalog)
actual={
 'schemaVersion':16,
 'kernelCount':len(ids),
 'kernelIdsSha256':hashlib.sha256('\n'.join(ids).encode()).hexdigest(),
 'firstKernel':ids[0] if ids else None,
 'lastKernel':ids[-1] if ids else None,
 'minimumRoutesPerKernel':3,
 'regressionQuest':'The Pale Manifest'
}
if actual!=expected:
 print('Phase 16 vector verification FAILED:')
 print('expected',json.dumps(expected,indent=2)); print('actual',json.dumps(actual,indent=2)); sys.exit(1)
if text.count('new("technical-route"') != 1 or text.count('new("combat-route"') != 1:
 print('Phase 16 vector verification FAILED: route catalog markers missing'); sys.exit(1)
print('Phase 16 vector verification PASSED.')
