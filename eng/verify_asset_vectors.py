#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VECTORS = json.loads((ROOT / 'eng/asset-golden-vectors.json').read_text(encoding='utf-8'))


def next_power_of_two(value: int) -> int:
    result = 1
    while result < value:
        result <<= 1
    return result


def cylinder_topology(segments: int) -> tuple[int, int]:
    vertices = segments * 4 + (1 + segments) * 2
    indices = segments * 6 + segments * 3 * 2
    return vertices, indices


def atlas_layout(count: int, tile_width: int, tile_height: int, maximum_width: int, padding: int):
    x = padding
    y = padding
    row_height = 0
    used_width = 0
    placements = []
    for index in range(count):
        required_width = tile_width + padding * 2
        required_height = tile_height + padding * 2
        if x + tile_width + padding > maximum_width:
            x = padding
            y += row_height
            row_height = 0
        placements.append({'id': chr(ord('a') + index), 'x': x, 'y': y})
        x += tile_width + padding * 2
        row_height = max(row_height, required_height)
        used_width = max(used_width, x)
    width = next_power_of_two(min(maximum_width, max(16, used_width)))
    height = next_power_of_two(max(16, y + row_height))
    return width, height, placements


def wireframe_preview(width: int, height: int, margin: int):
    vertices = [(-1.0, -1.0, 0.0), (1.0, -1.0, 0.0), (1.0, 1.0, 0.0), (-1.0, 1.0, 0.0)]
    indices = [0, 1, 2, 0, 2, 3]
    projected = [(x - z, (x + z) * 0.5 - y) for x, y, z in vertices]
    minimum_x = min(point[0] for point in projected)
    maximum_x = max(point[0] for point in projected)
    minimum_y = min(point[1] for point in projected)
    maximum_y = max(point[1] for point in projected)
    usable_width = width - 1 - margin * 2
    usable_height = height - 1 - margin * 2

    def round_away(value: float) -> int:
        import math
        return math.floor(value + 0.5) if value >= 0 else math.ceil(value - 0.5)

    screen = []
    for x, y in projected:
        sx = margin + round_away((x - minimum_x) / (maximum_x - minimum_x) * usable_width)
        sy = margin + round_away((maximum_y - y) / (maximum_y - minimum_y) * usable_height)
        screen.append((max(0, min(width - 1, sx)), max(0, min(height - 1, sy))))

    pixels = bytearray(width * height)

    def draw_line(start, end):
        x, y = start
        end_x, end_y = end
        dx = abs(end_x - x)
        step_x = 1 if x < end_x else -1
        dy = -abs(end_y - y)
        step_y = 1 if y < end_y else -1
        error = dx + dy
        while True:
            if 0 <= x < width and 0 <= y < height:
                pixels[y * width + x] = 255
            if x == end_x and y == end_y:
                break
            doubled = error * 2
            if doubled >= dy:
                error += dy
                x += step_x
            if doubled <= dx:
                error += dx
                y += step_y

    for index in range(0, len(indices), 3):
        a, b, c = (screen[indices[index + offset]] for offset in range(3))
        draw_line(a, b)
        draw_line(b, c)
        draw_line(c, a)
    return bytes(pixels), [list(point) for point in screen]


def main() -> int:
    box = VECTORS['meshTopology']['box']
    assert box == {'vertices': 24, 'indices': 36, 'triangles': 12}
    vertices, indices = cylinder_topology(12)
    cylinder = VECTORS['meshTopology']['cylinder12']
    assert (vertices, indices, indices // 3) == (cylinder['vertices'], cylinder['indices'], cylinder['triangles'])

    material = VECTORS['solidMaterial4x4']
    payload = bytes(material['rgba']) * 16
    assert len(payload) == material['byteLength']
    assert hashlib.sha256(payload).hexdigest() == material['sha256']

    atlas = VECTORS['twoTileAtlas']
    width, height, placements = atlas_layout(2, atlas['tileWidth'], atlas['tileHeight'], atlas['maximumWidth'], atlas['padding'])
    assert width == atlas['atlasWidth']
    assert height == atlas['atlasHeight']
    assert placements == atlas['placements']

    preview = VECTORS['wireframePreview']
    pixels, screen_points = wireframe_preview(preview['width'], preview['height'], preview['margin'])
    assert screen_points == preview['screenPoints']
    assert sum(value != 0 for value in pixels) == preview['litPixels']
    assert hashlib.sha256(pixels).hexdigest() == preview['sha256']
    print('Procedural asset golden vectors verified.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
