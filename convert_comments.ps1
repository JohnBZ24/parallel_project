param(
    [string]$Root = 'parallel_project'
)

$ErrorActionPreference = 'Stop'

function Clean-DocText([string]$s) {
    if ($null -eq $s) { return '' }

    $s = $s -replace '<see\s+cref="([^"]+)"\s*/>', '$1'
    $s = $s -replace '<c>(.*?)</c>', '$1'
    $s = $s.Replace('&gt;', '>').Replace('&lt;', '<')
    return $s.Trim()
}

function Convert-XmlDocBlock([string[]]$lines, [ref]$i, [string]$indent) {
    $summary = New-Object System.Collections.Generic.List[string]
    $params = New-Object System.Collections.Generic.List[object]
    $returns = $null
    $notes = New-Object System.Collections.Generic.List[string]

    # We are on the <summary> line already.
    $i.Value++

    while ($i.Value -lt $lines.Count -and $lines[$i.Value] -match '^\s*///') {
        $l = $lines[$i.Value] -replace '^\s*///\s?', ''
        if ($l -match '^</summary>') { $i.Value++; break }
        $c = Clean-DocText $l
        if ($c) { $summary.Add($c) }
        $i.Value++
    }

    while ($i.Value -lt $lines.Count -and $lines[$i.Value] -match '^\s*///') {
        $l = $lines[$i.Value] -replace '^\s*///\s?', ''

        if ($l -match '^<param name="([^"]+)">(.*)</param>\s*$') {
            $params.Add([pscustomobject]@{ name = $matches[1]; desc = (Clean-DocText $matches[2]) })
        }
        elseif ($l -match '^<returns>(.*)</returns>\s*$') {
            $returns = (Clean-DocText $matches[1])
        }
        elseif ($l -match '^<remarks>\s*$') {
            $i.Value++
            while ($i.Value -lt $lines.Count -and $lines[$i.Value] -match '^\s*///') {
                $r = $lines[$i.Value] -replace '^\s*///\s?', ''
                if ($r -match '^</remarks>') { break }
                $cr = Clean-DocText $r
                if ($cr) { $notes.Add($cr) }
                $i.Value++
            }
        }

        $i.Value++
    }

    $summaryText = ($summary -join ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($summaryText)) { $summaryText = '...' }

    $out = New-Object System.Collections.Generic.List[string]
    $out.Add($indent + '// ' + $summaryText)

    if ($params.Count -gt 0 -or $returns -or $notes.Count -gt 0) {
        $out.Add($indent + '//')
    }

    foreach ($p in $params) {
        $out.Add($indent + ('// @param ' + $p.name + ': ' + $p.desc))
    }

    if ($returns) {
        $out.Add($indent + ('// @returns: ' + $returns))
    }

    if ($notes.Count -gt 0) {
        $out.Add($indent + ('// @notes: ' + ($notes -join ' ')))
    }

    return ,$out.ToArray()
}

function Convert-BlockCommentsToSlashes([string[]]$lines) {
    $out = New-Object System.Collections.Generic.List[string]
    $inBlock = $false

    foreach ($line in $lines) {
        if (-not $inBlock) {
            if ($line -match '^(\s*)/\*\s*$') {
                $indent = $matches[1]
                $inBlock = $true
                $out.Add($indent + '//')
                continue
            }
            elseif ($line -match '^(\s*)/\*\s*(.*)$') {
                $indent = $matches[1]
                $rest = $matches[2]
                $inBlock = $true

                $rest = $rest -replace '\s*\*/\s*$', ''
                if ([string]::IsNullOrWhiteSpace($rest)) {
                    $out.Add($indent + '//')
                }
                else {
                    $out.Add($indent + '// ' + $rest.TrimEnd())
                }

                if ($line -match '\*/\s*$') { $inBlock = $false }
                continue
            }

            $out.Add($line)
            continue
        }

        if ($line -match '^(\s*)\*/\s*$') {
            $indent = $matches[1]
            $out.Add($indent + '//')
            $inBlock = $false
            continue
        }

        if ($line -match '^(\s*)\*\s?(.*)$') {
            $indent2 = $matches[1]
            $rest = $matches[2]
            if ([string]::IsNullOrWhiteSpace($rest)) {
                $out.Add($indent2 + '//')
            }
            else {
                $out.Add($indent2 + '// ' + $rest.TrimEnd())
            }
        }
        else {
            $out.Add($line)
        }
    }

    return ,$out.ToArray()
}

$files = Get-ChildItem -Path $Root -Filter '*.cs' -File -Recurse | Where-Object {
    $_.FullName -notlike '*\obj\*' -and $_.FullName -notlike '*\bin\*'
}

foreach ($f in $files) {
    $orig = Get-Content -LiteralPath $f.FullName

    # 1) Convert XML docs into // blocks.
    $tmp = New-Object System.Collections.Generic.List[string]
    $i = 0
    while ($i -lt $orig.Count) {
        $line = $orig[$i]
        if ($line -match '^(\s*)///\s*<summary>\s*$') {
            $indent = $matches[1]
            $converted = Convert-XmlDocBlock -lines $orig -i ([ref]$i) -indent $indent
            foreach ($cl in $converted) { $tmp.Add($cl) }
            continue
        }

        $tmp.Add($line)
        $i++
    }

    # 2) Convert any remaining /* */ blocks into // blocks.
    $new = Convert-BlockCommentsToSlashes -lines $tmp.ToArray()

    if ((Compare-Object -ReferenceObject $orig -DifferenceObject $new -SyncWindow 0).Count -gt 0) {
        Set-Content -LiteralPath $f.FullName -Value $new -Encoding UTF8
    }
}

Write-Host "Converted comments under '$Root'."
