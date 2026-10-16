#!/usr/bin/env python3
from pathlib import Path
import heapq,json,sys
ROOT=Path(__file__).resolve().parents[1]
data=json.loads((ROOT/'eng/phase14-golden-vectors.json').read_text())
r=data['route']; graph={n:[] for n in r['nodes']}
for a,b,c in r['edges']:
 graph[a].append((b,c)); graph[b].append((a,c))
for n in graph: graph[n].sort()
q=[(0,(r['start'],),r['start'])]; best={r['start']:0}; result=None
while q:
 cost,path,node=heapq.heappop(q)
 if cost!=best.get(node): continue
 if node==r['goal']: result=(list(path),cost); break
 for nxt,edge in graph[node]:
  proposed=cost+edge
  if proposed<best.get(nxt,10**18): best[nxt]=proposed;heapq.heappush(q,(proposed,path+(nxt,),nxt))
errors=[]
if result!=(r['expectedPath'],r['expectedCost']): errors.append(f'route mismatch: {result}')
u=data['utility']
attack_score=100+(900+700//3-100//2)
if not (u['attack']['awareness']>=3 and u['attack']['ready'] and u['attack']['ammo'] and attack_score>0): errors.append('attack vector failed')
s=u['surrender']; surrender=100+(2500 if s['outnumbered'] and s['morale']<220 and not s['escape'] else -6000)
if surrender<=0: errors.append('surrender vector failed')
def tier(d): return 'Full' if d<=90000 else 'Reduced' if d<=400000 else 'Strategic'
actual=[tier(d) for d in data['tierDistancesMillimeters']]
if actual!=data['expectedTiers']: errors.append(f'tier mismatch: {actual}')
if errors:
 print('Phase 14 golden-vector verification FAILED:')
 for e in errors: print('  - '+e)
 sys.exit(1)
print('Phase 14 golden-vector verification PASSED.')
