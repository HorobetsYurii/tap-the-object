<#
.SYNOPSIS
    Publishes the demo site, meaning the Addressables content and the WebGL build when there is one, to the
    gh-pages branch that GitHub Pages serves over HTTPS.

.DESCRIPTION
    Build the content first, either from the editor (Tools > Tap the Object > Build Addressables Content)
    or head-less:

        Unity -batchmode -quit -projectPath . `
              -executeMethod TapTheObject.Editor.AddressablesBuildCommand.Build

    That writes ServerData/<BuildTarget>/, which this script copies into the gh-pages branch through a
    temporary worktree, so the working tree is never switched. The WebGL build, if present, is copied to
    the branch root, which puts the player and the content it downloads on the same origin.

.PARAMETER Push
    Push the branch after committing. Without it the commit is left local for review.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path "$PSScriptRoot\.."),
    [string]$Branch = 'gh-pages',
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    # git reports progress on stderr, which a Stop preference would turn into a terminating error.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & git @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "git $($Arguments -join ' ') exited with $LASTEXITCODE."
        }
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

function Test-Branch {
    param([string]$Name)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & git -C $RepositoryRoot show-ref --verify --quiet "refs/heads/$Name"
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

$serverData = Join-Path $RepositoryRoot 'ServerData'
if (-not (Test-Path $serverData)) {
    throw "No built content at '$serverData'. Build the Addressables content first."
}

$webGlBuild = Join-Path $RepositoryRoot 'Builds\WebGL'

$worktree = Join-Path ([System.IO.Path]::GetTempPath()) "tap-the-object-$Branch"
if (Test-Path $worktree) {
    Invoke-Git -C $RepositoryRoot worktree remove --force $worktree
}

if (Test-Branch $Branch) {
    Invoke-Git -C $RepositoryRoot worktree add $worktree $Branch
} else {
    Invoke-Git -C $RepositoryRoot worktree add --detach $worktree
    Invoke-Git -C $worktree checkout --orphan $Branch
    Invoke-Git -C $worktree rm -rf --quiet .
}

Get-ChildItem $worktree -Force |
    Where-Object { $_.Name -ne '.git' } |
    Remove-Item -Recurse -Force

Copy-Item $serverData (Join-Path $worktree 'ServerData') -Recurse

if (Test-Path $webGlBuild) {
    Copy-Item "$webGlBuild\*" $worktree -Recurse
    Write-Host 'Included the WebGL build.'
} else {
    Write-Host 'No WebGL build found, publishing content only.'
}

# GitHub Pages runs Jekyll by default, which would skip files and folders starting with an underscore.
Set-Content -Path (Join-Path $worktree '.nojekyll') -Value '' -Encoding utf8

$readme = @'
# gh-pages

Generated output, published by `Tools/publish-content.ps1` from the `main` branch. Nothing here is edited
by hand, and this branch deliberately shares no files with the source.

`index.html` plus `Build/`, `TemplateData/` and `StreamingAssets/` are the WebGL build.
`ServerData/<BuildTarget>/` holds the Addressables catalog and the round image bundles that the game
downloads at runtime.

Source code is on the `main` branch.
'@
Set-Content -Path (Join-Path $worktree 'README.md') -Value $readme -Encoding utf8

# --force, because the repository ignore rules are written for the Unity project, not for build output.
Invoke-Git -C $worktree add --all --force

if (& git -C $worktree status --porcelain) {
    Invoke-Git -C $worktree commit -q -m 'Publish demo site'
    Write-Host "Committed to '$Branch'."
} else {
    Write-Host 'Nothing changed.'
}

if ($Push) {
    Invoke-Git -C $worktree push origin $Branch
    Write-Host "Pushed '$Branch'."
}

Invoke-Git -C $RepositoryRoot worktree remove --force $worktree
