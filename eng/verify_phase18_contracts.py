#!/usr/bin/env python3
from pathlib import Path
import re,sys
ROOT=Path(__file__).resolve().parents[1]
required=[
'src/Playroma.Sound.Core/Playroma.Sound.Core.csproj','src/Playroma.Sound.Dsp/Playroma.Sound.Dsp.csproj','src/Playroma.Sound.Graph/Playroma.Sound.Graph.csproj','src/Playroma.Sound.Generators/Playroma.Sound.Generators.csproj','src/Playroma.Sound.Streaming/Playroma.Sound.Streaming.csproj',
'src/Roentgen.Audio/AudioContracts.cs','src/Roentgen.Audio.Playroma/PlayromaAudioRuntime.cs','src/Roentgen.Audio.MonoGame/PlayromaMonoGameHost.cs','tools/Roentgen.AudioLab/Roentgen.AudioLab.csproj','licenses/PLAYROMA_SOUND_MIT.txt']
errors=[f'missing {p}' for p in required if not (ROOT/p).is_file()]
props=(ROOT/'Directory.Build.props').read_text()
if 'PlayromaSoundRoot' in props or 'PlayromaSoundAvailable' in props: errors.append('external Playroma Sound properties remain')
bridge=(ROOT/'src/Roentgen.Audio.Playroma/Roentgen.Audio.Playroma.csproj').read_text()
if 'external/Playroma-Sound' in bridge or '$(PlayromaSoundRoot)' in bridge: errors.append('audio bridge references external Playroma Sound')
host=(ROOT/'src/Roentgen.Audio.MonoGame/PlayromaMonoGameHost.cs').read_text()
for token in ['DynamicSoundEffectInstance','BufferNeeded','SubmitBuffer','PcmBlockRingBuffer']:
 if token not in host: errors.append(f'host missing {token}')
contracts=(ROOT/'src/Playroma.Sound.Streaming/Runtime/StreamingContracts.cs').read_text()
for token in ['HostedVoiceHandle','VoiceLifecycleEvent','ZoneId']:
 if token not in contracts: errors.append(f'Playroma upgrade missing {token}')
if errors:
 print('\n'.join('ERROR '+e for e in errors));sys.exit(1)
print('Phase 18 contracts verified: integrated Playroma source, runtime upgrades, semantic boundary, and MonoGame PCM host.')
