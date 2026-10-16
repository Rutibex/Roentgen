# Third-party assets and dependencies

Roentgen's own source code is distributed under the MIT license in [`LICENSE`](LICENSE). This does **not** replace third-party copyright or license terms.

## Bundled assets

| Material | Location | Terms |
|---|---|---|
| **GeneralUser GS SoundFont** by S. Christian Collins | `content/music/soundfonts/default.sf2` (about 31 MB) | See [`GeneralUser-GS-LICENSE.txt`](content/music/soundfonts/GeneralUser-GS-LICENSE.txt). The author allows use in software projects, including modification and commercial music production, but explicitly discloses uncertainty about some historical sample provenance. Review those terms before commercial redistribution. |
| **DejaVu/Bitstream Vera-derived UI font artwork** | `content/ui/body-font.png` | See [`licenses/DEJAVU_FONT_LICENSE.txt`](licenses/DEJAVU_FONT_LICENSE.txt) for the bundled upstream copyright and terms. |
| **Playroma Sound / Playroma Music code** | `src/Playroma.Sound.*`, `src/PlayromaMusic.*` | Integrated source with MIT notices in `licenses/PLAYROMA_SOUND_MIT.txt` and `licenses/PLAYROMA_MUSIC_MIT.txt`. |

The SoundFont is a runtime content asset, **not** a generated build file; it has been kept intact. Its SHA-256 is recorded in `content/music/soundfonts/default.sf2.sha256`.

## Package dependencies

NuGet dependencies are resolved from `Directory.Packages.props`, individual project files, and their `packages.lock.json` files. See the included [`Playroma Sound`](licenses/PLAYROMA_SOUND_THIRD_PARTY_NOTICES.md), [`Playroma Music`](licenses/PLAYROMA_MUSIC_THIRD_PARTY_NOTICES.md), and [`content/music` notices](content/music/PLAYROMA_MUSIC_THIRD_PARTY_NOTICES.md). MonoGame, Gum, NAudio, DryWetMIDI, MeltySynth, test tooling, and any native libraries retain their original licenses. No NuGet binaries are committed here.

Before distributing compiled packages, audit the **actual** restored dependency list and attach any additional required license notices. Optional FluidSynth native libraries, if redistributed separately, bring their own LGPL requirements.

The Playroma Music notices originated with a standalone project and contain a statement that no SoundFont is included. That statement does not apply to this combined Roentgen repository: `default.sf2` **is included** here.
