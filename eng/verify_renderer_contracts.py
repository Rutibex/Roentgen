#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
ERRORS = []


def require(condition: bool, message: str) -> None:
    if not condition:
        ERRORS.append(message)


mgcb = (ROOT / 'src/Roentgen.App/Content/Content.mgcb').read_text(encoding='utf-8')
for asset in ('Effects/ForwardLit.fx', 'Effects/ShadowDepth.fx', 'Effects/PostProcess.fx'):
    require(asset in mgcb, f'Missing MGCB asset {asset}')

contracts = {
    'ForwardLit.fx': {
        'World', 'WorldInverseTranspose', 'View', 'Projection', 'LightViewProjection',
        'CameraPosition', 'LightDirection', 'LightColor', 'AlbedoColor', 'FogColor',
        'AmbientIntensity', 'EmissiveStrength', 'FogStart', 'FogEnd', 'ShadowStrength',
        'AlbedoTexture', 'ShadowMap'
    },
    'ShadowDepth.fx': {'World', 'LightViewProjection'},
    'PostProcess.fx': {'SceneTexture', 'Exposure', 'Gamma', 'VignetteStrength', 'InverseResolution'},
}
for filename, required in contracts.items():
    source = (ROOT / 'src/Roentgen.App/Content/Effects' / filename).read_text(encoding='utf-8')
    for name in required:
        require(re.search(rf'\b{re.escape(name)}\b', source) is not None, f'{filename} lacks parameter {name}')
    require('technique ' in source, f'{filename} has no technique')

renderer = (ROOT / 'src/Roentgen.Rendering/RendererFoundation.cs').read_text(encoding='utf-8')
for name in contracts['ForwardLit.fx'] - {'WorldInverseTranspose'}:
    if name in {'View', 'Projection', 'World', 'LightViewProjection', 'CameraPosition', 'LightDirection', 'LightColor', 'AlbedoColor', 'FogColor', 'AmbientIntensity', 'EmissiveStrength', 'FogStart', 'FogEnd', 'ShadowStrength', 'AlbedoTexture', 'ShadowMap'}:
        require(f'"{name}"' in renderer, f'Renderer does not bind ForwardLit parameter {name}')
require('"WorldInverseTranspose"' in renderer, 'Renderer does not bind WorldInverseTranspose')
require('RenderThreadGuard' in renderer, 'Renderer lacks render-thread ownership')
require('RenderResourceUploadQueue' in renderer, 'Renderer lacks upload queue')
require('RenderQueueBuilder' in renderer, 'Renderer lacks queue construction')

vertex = (ROOT / 'src/Roentgen.Rendering/RoentgenVertex.cs').read_text(encoding='utf-8')
require('StructLayout(LayoutKind.Sequential' in vertex, 'RoentgenVertex is not explicitly sequential')
require('new VertexElement(32, VertexElementFormat.Color' in vertex, 'RoentgenVertex color offset changed')

if ERRORS:
    print('Renderer contract verification FAILED:')
    for item in ERRORS:
        print('  -', item)
    sys.exit(1)

print('Renderer contracts verified.')
print('  Effects: 3')
print('  Forward parameters:', len(contracts['ForwardLit.fx']))
print('  Render-thread and upload boundaries: present')
