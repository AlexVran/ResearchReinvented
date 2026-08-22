$ErrorActionPreference = 'Stop'
uv run python (Join-Path $PSScriptRoot 'check.py') @args
exit $LASTEXITCODE
