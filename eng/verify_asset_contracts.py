#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from pathlib import Path

import jsonschema

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def require(condition: bool, message: str) -> None:
    if not condition:
        ERRORS.append(message)


def main() -> int:
    mesh_schema = json.loads((ROOT / 'schemas/mesh-recipe.schema.json').read_text(encoding='utf-8'))
    material_schema = json.loads((ROOT / 'schemas/material-recipe.schema.json').read_text(encoding='utf-8'))
    mesh_sample = json.loads((ROOT / 'samples/assets/scrap-rifle.mesh.json').read_text(encoding='utf-8'))
    material_sample = json.loads((ROOT / 'samples/assets/rusted-steel.material.json').read_text(encoding='utf-8'))
    jsonschema.Draft202012Validator(mesh_schema).validate(mesh_sample)
    jsonschema.Draft202012Validator(material_schema).validate(material_sample)

    projects = json.loads((ROOT / 'eng/projects.json').read_text(encoding='utf-8'))
    names = {item['name'] for group in ('runtimeProjects', 'testProjects', 'toolProjects') for item in projects.get(group, [])}
    for name in ('Roentgen.Assets', 'Roentgen.Assets.Tests', 'Roentgen.AssetLab'):
        require(name in names, f'{name} is not declared in eng/projects.json.')
    require(projects.get('schemaVersion') >= 6, 'eng/projects.json must use schemaVersion 6 or later.')

    assets_project = (ROOT / 'src/Roentgen.Assets/Roentgen.Assets.csproj').read_text(encoding='utf-8')
    require('MonoGame' not in assets_project, 'Roentgen.Assets must remain MonoGame-free.')
    for path in (ROOT / 'src/Roentgen.Assets').glob('*.cs'):
        require('Microsoft.Xna.Framework' not in path.read_text(encoding='utf-8'), f'{path.name} leaks MonoGame into Roentgen.Assets.')

    enum_source = (ROOT / 'src/Roentgen.Assets/AssetEnums.cs').read_text(encoding='utf-8')
    for primitive in ('Box', 'Cylinder', 'Cone', 'UvSphere', 'Wedge', 'Lathe', 'Extrusion', 'Tube'):
        require(primitive in enum_source, f'Missing primitive {primitive}.')
    for modifier in ('Taper', 'TwistDegrees', 'BendDegrees', 'NoiseDisplace', 'ClipBelow', 'ClipAbove'):
        require(modifier in enum_source, f'Missing modifier {modifier}.')

    compiler = (ROOT / 'src/Roentgen.Assets/MeshRecipeCompiler.cs').read_text(encoding='utf-8')
    require('CompileLod' in compiler and 'RecalculateNormals' in compiler and 'ApplyUvProjection' in compiler, 'Mesh compiler stages are incomplete.')
    material = (ROOT / 'src/Roentgen.Assets/MaterialRecipeCompiler.cs').read_text(encoding='utf-8')
    for layer in ('RustSpots', 'Scratches', 'WarningStripes', 'EdgeWear'):
        require(layer in material, f'Material compiler does not implement {layer}.')

    rendering_project = (ROOT / 'src/Roentgen.Rendering/Roentgen.Rendering.csproj').read_text(encoding='utf-8')
    require('Roentgen.Assets' in rendering_project, 'Renderer does not reference procedural asset contracts.')
    renderer = (ROOT / 'src/Roentgen.Rendering/RendererFoundation.cs').read_text(encoding='utf-8')
    require('CompiledMeshAsset' in renderer and 'CompiledMaterialAsset' in renderer, 'Renderer lacks compiled asset upload overloads.')
    app = (ROOT / 'src/Roentgen.App/RoentgenGame.cs').read_text(encoding='utf-8')
    require('Phase9WorldScene' in app or 'Phase8WorldScene' in app or 'Phase7WorldScene' in app or 'ProceduralAssetShowcaseScene' in app, 'Application does not consume the procedural asset pipeline.')
    golden_renderer = (ROOT / 'src/Roentgen.Assets/AssetGoldenImageRenderer.cs').read_text(encoding='utf-8')
    require('RenderWireframe' in golden_renderer and 'ComputeSha256Hex' in golden_renderer, 'Headless asset golden-image renderer is incomplete.')
    catalog = (ROOT / 'src/Roentgen.Assets/ProceduralAssetCatalog.cs').read_text(encoding='utf-8')
    require('inspection-cart' in catalog and 'vehicle' in catalog, 'Phase 5 closure vehicle recipe is missing.')

    for required in (
        'src/Roentgen.Assets/AssetCaches.cs',
        'src/Roentgen.Assets/AssetGoldenImageRenderer.cs',
        'src/Roentgen.Assets/MaterialAtlasBuilder.cs',
        'src/Roentgen.Assets/AssetRecipeJson.cs',
        'tools/Roentgen.AssetLab/Program.cs',
        'tests/Roentgen.Assets.Tests/ProceduralAssetTests.cs',
    ):
        require((ROOT / required).is_file(), f'Missing {required}.')

    if ERRORS:
        print('Procedural asset contract verification FAILED:')
        for error in ERRORS:
            print(f'  - {error}')
        return 1
    print('Procedural asset contracts verified.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
