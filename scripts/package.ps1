$ErrorActionPreference = 'Stop'
uv run python (Join-Path $PSScriptRoot 'package.py') @args
exit $LASTEXITCODE
