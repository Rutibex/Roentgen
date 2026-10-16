#!/usr/bin/env python3
from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1]
data=json.loads((ROOT/'eng/phase15-golden-vectors.json').read_text(encoding='utf-8'))
errors=[]
t=data['bilateralTrade']
exporter_after=t['exporterBefore']-t['loaded']
lost=t['loaded']*t['lossPermille']//1000
delivered=t['loaded']-lost
if exporter_after!=t['expectedExporterAfter']: errors.append(f'exporter conservation mismatch: {exporter_after}')
if lost!=t['expectedLost'] or delivered!=t['expectedDelivered']: errors.append(f'shipment resolution mismatch: delivered={delivered}, lost={lost}')
r=data['reputation']; values=dict(r['initial'])
for evidence in r['evidence']:
 effective=evidence['delta']*evidence['reliabilityPermille']//1000*evidence['publicityPermille']//1000
 values[evidence['dimension']]=max(-1000,min(1000,values[evidence['dimension']]+effective))
standing='feared' if values['fear']>=700 and values['trust']<0 else 'neutral'
if values!={k:v for k,v in r['expected'].items() if k!='standing'} or standing!=r['expected']['standing']: errors.append(f'reputation vector mismatch: {values}, {standing}')
d=data['disguise']
def evaluate(inspection,familiarity,exposure):
 challenge=inspection+familiarity//2+exposure
 margin=d['qualityPermille']-challenge
 return 'Accepted' if margin>=250 else 'Suspicious' if margin>=-100 else 'Exposed'
if evaluate(d['casualInspection'],d['casualFamiliarity'],0)!=d['expectedCasual']: errors.append('casual disguise vector failed')
if evaluate(d['guardInspection'],d['guardFamiliarity'],d['guardExposure'])!=d['expectedGuard']: errors.append('guard disguise vector failed')
c=data['conflict']
if (c['playerRelevancePermille']>=c['reservationThreshold'])!=c['expectedReserved']: errors.append('conflict reservation vector failed')
if errors:
 print('Phase 15 golden-vector verification FAILED:')
 for error in errors: print('  - '+error)
 sys.exit(1)
print('Phase 15 golden-vector verification PASSED.')
