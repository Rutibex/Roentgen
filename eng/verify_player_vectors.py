#!/usr/bin/env python3
import hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def tag(n):return bytes([n])
def i32(v):return tag(2)+struct.pack('>i',v)
def i64(v):return tag(4)+struct.pack('>q',v)
def u64(v):return tag(5)+struct.pack('>Q',v)
def boolean(v):return tag(1)+bytes([1 if v else 0])
def string(v):b=v.encode();return tag(8)+struct.pack('>i',len(b))+b
def sid(v):return tag(10)+bytes.fromhex(v)
def stable(namespace,key):
 h=hashlib.sha256(string('roentgen.stable-id')+i32(1)+string(namespace)+string(key)).digest()
 return h[:16].hex()
def checksum(v):
 stance={'Standing':0,'Crouched':1}[v['stance']];loco={'Idle':0,'Walking':1,'Running':2,'Sprinting':3,'Crouching':4,'Airborne':5,'Sliding':6,'Mantling':7,'Ladder':8}[v['locomotion']];mode={'Exploration':0,'Container':1,'Paused':2,'Traversal':3}.get(v.get('controlMode','Exploration'),0)
 b=sid(v['playerId'])
 for x in v['feetMillimeters']:b+=i64(x)
 for x in v['velocityMillimetersPerSecond']:b+=i64(x)
 b+=i32(v['bodyYawUnits'])+i32(0)+i32(stance)+i32(loco)+boolean(v['grounded'])+sid('0'*32)+sid('0'*32)+sid(v.get('openContainerId','0'*32))+i32(mode)+i32(0)+i32(0)
 for _ in range(6):b+=i64(0)
 b+=u64(v['revision'])
 return hashlib.sha256(b).hexdigest()
data=json.loads((ROOT/'eng/player-golden-vectors.json').read_text())
for v in data['vectors']:
 if v['playerId']!=stable('player','phase8-regression'):raise SystemExit(f"{v['name']} player ID mismatch")
 actual=checksum(v)
 if actual!=v['checksum']:raise SystemExit(f"{v['name']} checksum mismatch: {actual}")
print('Phase 8 player golden vectors verified.')
