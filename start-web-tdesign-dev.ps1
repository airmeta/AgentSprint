$ErrorActionPreference = 'Stop'
$node = 'C:\Users\23129\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'
$pnpm = 'D:\Dev\nvm\v16.19.1\node_global\node_modules\pnpm\bin\pnpm.cjs'
$env:PATH = 'C:\Users\23129\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin;' + $env:PATH
Set-Location 'F:\AI\AgentSprint\src\admin\apps\web-tdesign'
Write-Host 'Starting AgentSprint web-tdesign dev server...'
Write-Host 'Node:'
& $node -v
Write-Host 'pnpm:'
& $node $pnpm -v
Write-Host 'URL: http://127.0.0.1:5173/'
& $node $pnpm run dev --host 127.0.0.1 --port 5173
if ($LASTEXITCODE -ne 0) {
  Write-Host "Dev server exited with code $LASTEXITCODE"
  pause
}
