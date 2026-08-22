$ErrorActionPreference = 'Stop'
uv run python (Join-Path $PSScriptRoot 'deploy.py') @args
exit $LASTEXITCODE
