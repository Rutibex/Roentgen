$root = Split-Path -Parent $PSScriptRoot
& "$root/eng/musiclab.ps1" validate
& "$root/eng/musiclab.ps1" compare-seeds
& "$root/eng/musiclab.ps1" test-transition
