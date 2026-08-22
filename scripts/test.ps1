$ErrorActionPreference = 'Stop'
uv run python (Join-Path $PSScriptRoot 'test.py') @args
exit $LASTEXITCODE
