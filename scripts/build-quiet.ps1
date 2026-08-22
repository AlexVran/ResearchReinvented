$ErrorActionPreference = 'Stop'
uv run python (Join-Path $PSScriptRoot 'build.py') @args
exit $LASTEXITCODE
